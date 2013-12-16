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
        /// <summary>
        /// Current static JSON object.
        /// </summary>
        [ThreadStatic]
        internal static Session _Current;

        /// <summary>
        /// Current static Request object.
        /// </summary>
        [ThreadStatic]
        static Request _Request;

        /// <summary>
        /// Attached Json object.
        /// </summary>
        internal Json _Data;

        /// <summary>
        /// Indicates if session is being used.
        /// </summary>
        bool _IsInUse;

        /// <summary>
        /// Cached pages dictionary.
        /// </summary>
        Dictionary<String, Json> _JsonNodeCacheDict;

        /// <summary>
        /// Destroy session delegate.
        /// </summary>
        internal Action<Session> _SessionDestroyUserDelegate_;

        /// <summary>
        /// Tries to get cached JSON node.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        internal Json GetCachedJsonNode(String uri)
        {
            Json obj;

            if ((_JsonNodeCacheDict == null) || (!_JsonNodeCacheDict.TryGetValue(uri, out obj)))
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
        internal void AddJsonNodeToCache(String uri, Json obj) {
            // Checking if cached state dictionary is already created.
            if (null == _JsonNodeCacheDict)
                _JsonNodeCacheDict = new Dictionary<String, Json>();

            // Adding current URI to cache.
            _JsonNodeCacheDict[uri] = obj;
        }

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
        /// Current static session object.
        /// </summary>
        public static Session Current
        {
            get {
                return _Current;
            }

            set {
                // If we are replacing the JSON tree, we need to dispose previous one.
                if (_Current != null)
                    _Current.DisposeJsonRecursively(_Current._Data);

                // Creating new empty session.
                _Current = value;
            }
        }

        /// <summary>
        /// Sets session data.
        /// </summary>
        public Json Data {
            get {
                return _Data;
            }

            set {
                _Data = value;

                if (value != null) {
                    value._Session = this;
                }

                // Setting current session.
                Current = this;
            }
        }

        /// <summary>
        /// Setting or getting user object.
        /// </summary>
        public Object UserObject
        {
            get {
                return InternalSession.UserObject;
            }

            set {
                InternalSession.UserObject = value;
            }
        }

        /// <summary>
        /// Getting session creation time. 
        /// </summary>
        public DateTime Created
        {
            get {
                return InternalSession.Created;
            }
        }

        /// <summary>
        /// Getting last active session time. 
        /// </summary>
        public DateTime LastActive
        {
            get {
                return InternalSession.LastActive;
            }
        }

        /// <summary>
        /// Session timeout.
        /// </summary>
        public UInt64 TimeoutMinutes
        {
            get {
                return InternalSession.TimeoutMinutes;
            }
            set {
                InternalSession.TimeoutMinutes = value;
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
            // Updating last active date.
            InternalSession.UpdateLastActive();

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
            return _IsInUse;
        }

        /// <summary>
        /// Start using specific session.
        /// </summary>
        public void StartUsing() {
            _IsInUse = true;
        }

        /// <summary>
        /// Stop using specific session.
        /// </summary>
        public void StopUsing() {
            _IsInUse = false;
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
        public void SetSessionDestroyCallback(Action<Session> userDestroyMethod)
        {
            _SessionDestroyUserDelegate_ = userDestroyMethod;
        }

        /// <summary>
        /// Gets destroy callback if it was supplied before.
        /// </summary>
        /// <returns></returns>
        public Action<Session> GetDestroyCallback()
        {
            return _SessionDestroyUserDelegate_;
        }

        /// <summary>
        /// Destroys the session.
        /// </summary>
        public void Destroy()
        {
            if (_Data != null) {
                DisposeJsonRecursively(_Data);
                _Data = null;
            }

            if (InternalSession != null) {
                InternalSession.Destroy();
                InternalSession = null;
            }

            // Checking if destroy callback is supplied.
            if (null != _SessionDestroyUserDelegate_)
            {
                _SessionDestroyUserDelegate_(this);
                _SessionDestroyUserDelegate_ = null;
            }

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
