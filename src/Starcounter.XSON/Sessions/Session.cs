 ﻿// ***********************************************************************
// <copyright file="SessionDictionary.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Diagnostics; 
using Starcounter.Templates;
using Starcounter.Advanced;
using HttpStructs;
using Starcounter.Internal;
using System.Text;
using System.Collections.Generic;

namespace Starcounter {
    /// <summary>
    /// Class Session
    /// </summary>
    public partial class Session : IAppsSession {
        [ThreadStatic]
        static Session _Current;

        [ThreadStatic]
        static Request _Request;

        internal Json _Data;

        bool isInUse;

        /// <summary>
        /// Cached pages dictionary.
        /// </summary>
        Dictionary<String, Json> JsonNodeCacheDict;

        /// <summary>
        /// Tries to get cached JSON node.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        internal Json GetCachedJsonNode(String uri)
        {
            Json obj;

            if ((JsonNodeCacheDict == null) || (!JsonNodeCacheDict.TryGetValue(uri, out obj)))
                return null;

            Debug.Assert(null != obj);

            // Checking if its a root.
            if (_Data == obj)
                return _Data;

            // Checking if node has no parent, indicating that it was removed from tree.
            if (null != obj.Parent)
                return obj;

            return null;
        }

        /// <summary>
        /// Adds JSON node to cache.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="obj"></param>
        internal void AddJsonNodeToCache(String uri, Json obj)
        {
            // Checking if cached state dictionary is already created.
            if (null == JsonNodeCacheDict)
                JsonNodeCacheDict = new Dictionary<String, Json>();

            // Adding current URI to cache.
            JsonNodeCacheDict[uri] = obj;
        }

        /// <summary>
        /// Destroy session delegate.
        /// </summary>
        internal Action<Session> destroy_user_delegate_;

        /// <summary>
        /// Returns the current active session.
        /// </summary>
        /// <value></value>
        public static Session Current { get { return _Current; } }

        /// <summary>
        /// Indicates if user wants to use session cookie.
        /// </summary>
        public Boolean UseSessionCookie {
            get { return InternalSession.use_session_cookie_; }
            set { InternalSession.use_session_cookie_ = value; }
        }

        /// <summary>
        /// Returns the original request for session.
        /// </summary>
        /// <value></value>
        internal static Request InitialRequest {
            get { return _Request; }
            set { _Request = value; }
        }

        /// <summary>
        /// Getting internal session.
        /// </summary>
        public ScSessionClass InternalSession { get; set; }

        /// <summary>
        /// Creates new empty session.
        /// </summary>
        /// <returns></returns>
        internal static Session CreateNewEmptySession()
        {
            Debug.Assert(_Current == null);

            _Current = new Session();

            return _Current;
        }

        /// <summary>
        /// Sets session data.
        /// </summary>
        public static Json Data {
            get {
                Session s = _Current;
                if (s == null) {
                    s = CreateNewEmptySession();
                }
                if (s != null && s._Data != null) {
                    return s._Data;
                }
                return null;
            }
            set {
                if (_Current == null) {

                    // Creating new empty session.
                    _Current = new Session();

                    UInt32 errCode = 0;

                    if (_Request != null) {
#if DEBUG
                        // Checking if we have a predefined session.
                        if (_Request.IsSessionPredefined()) {
                            errCode = _Request.GenerateForcedSession(_Current);
                        }
                        else {
                            errCode = _Request.GenerateNewSession(_Current);
                        }
#else
                    errCode = _Request.GenerateNewSession(_Current);
#endif
                    }

                    if (errCode != 0)
                        throw ErrorCode.ToException(errCode);
                }
                _Current.SetData(value);
            }
        }

        /// <summary>
        /// Setting data object.
        /// </summary>
        /// <param name="data"></param>
        private void SetData(Json data) {

            // If we are replacing the JSON tree, we need to dispose previous one.
            if (_Data != null) {
                DisposeJsonRecursively(_Current._Data);
            }
            _Data = data;

            if (data != null) {
                data._Session = this;
            }
        }

        /// <summary>
        /// Internal session string.
        /// </summary>
        public String SessionIdString
        {
            get { return InternalSession.ToAsciiString(); }
        }

        /// <summary>
        /// Get complete resource locator.
        /// </summary>
        /// <returns></returns>
        internal string GetDataLocation()
        {
            if (_Data == null)
                return null;

            return ScSessionClass.DataLocationUriPrefix + SessionIdString;
        }

        /// <summary>
        /// Pushes data on existing session.
        /// </summary>
        /// <param name="data"></param>
        public void Push(String data, Boolean isText = true, Response.ConnectionFlags connFlags = Response.ConnectionFlags.NoSpecialFlags)
        {
            Push(Encoding.UTF8.GetBytes(data), isText, connFlags);
        }

        /// <summary>
        /// Pushes data on existing session.
        /// </summary>
        /// <param name="data"></param>
        public void Push(Byte[] data, Boolean isText = false, Response.ConnectionFlags connFlags = Response.ConnectionFlags.NoSpecialFlags)
        {
            Request req = bmx.GenerateNewRequest(InternalSession, MixedCodeConstants.NetworkProtocolType.PROTOCOL_WEBSOCKETS, isText);

            req.SendResponse(data, 0, data.Length, connFlags);
        }

        /// <summary>
        /// Start usage of given session.
        /// </summary>
        /// <param name="session"></param>
        internal static void Start(Session session)
        {
            Debug.Assert(_Current == null);

            // Session still can be null, e.g. did not pass the verification.
            if (session == null)
                return;

            Session._Current = session;
        }

        /// <summary>
        /// Finish usage of current session.
        /// </summary>
        internal static void End()
        {
			if (_Current != null) {
				_Current.Clear();
				Session._Current = null;
			}

            // Resetting current transaction if any exists.
            if (StarcounterBase._DB != null && StarcounterBase._DB.GetCurrentTransaction() != null)
                StarcounterBase._DB.SetCurrentTransaction(null);
        }

        /// <summary>
        /// Executes the specified action inside the Session
        /// </summary>
        /// <param name="session"></param>
        /// <param name="action"></param>
        internal static void Execute(Session session, Action action) {
            try {
                Start(session);
                action();
            } finally {
                End();
            }
        }

        /// <summary>
        /// Returns True if session is being used now.
        /// </summary>
        /// <returns></returns>
        public bool IsBeingUsed() {
            return isInUse;
        }

        /// <summary>
        /// 
        /// </summary>
        public void StartUsing() {
            isInUse = true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void StopUsing() {
            isInUse = false;
        }

        /// <summary>
        /// Checks if session is active.
        /// </summary>
        /// <returns></returns>
        public Boolean IsAlive()
        {
            return (InternalSession != null) && (InternalSession.IsAlive());
        }

        /// <summary>
        /// Set user destroy callback.  
        /// </summary>
        /// <param name="destroy_user_delegate"></param>
        public void SetDestroyCallback(Action<Session> destroy_user_delegate)
        {
            destroy_user_delegate_ = destroy_user_delegate;
        }

        /// <summary>
        /// Gets destroy callback if it was supplied before.
        /// </summary>
        /// <returns></returns>
        public Action<Session> GetDestroyCallback()
        {
            return destroy_user_delegate_;
        }

        /// <summary>
        /// Destroys the session.
        /// </summary>
        public void Destroy()
        {
            if (_Data != null) {
                DisposeJsonRecursively(_Data);
            }
            _Data = null;

            // Checking if destroy callback is supplied.
            if (null != destroy_user_delegate_)
            {
                destroy_user_delegate_(this);
                destroy_user_delegate_ = null;
            }

//            _ChangeLog = null;
            Session._Current = null;
        }

        /// <summary>
        /// Destroys Json tree recursively, including session.
        /// </summary>
        /// <param name="json"></param>
        private void DisposeJsonRecursively(Json json) {
            if (json == null)
                return;

            // Disposing transaction if it exists on this node.
            if (json.TransactionOnThisNode != null) {
                json.TransactionOnThisNode.Dispose();
            }

            if (json.Template == null || json.Template.IsPrimitive)
                return;

            foreach (Template child in ((TContainer)json.Template).Children) {
                if (child is TObject) {
                    DisposeJsonRecursively(((TObject)child).Getter(json));
                } else if (child is TObjArr) {
                    Json listing = ((TObjArr)child).Getter(json);
                    foreach (Json listApp in listing) {
                        DisposeJsonRecursively(listApp);
                    }
                }
            }

        }
    }
}
