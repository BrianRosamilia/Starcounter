﻿// ***********************************************************************
// <copyright file="TTrigger.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using Starcounter.Templates.Interfaces;

namespace Starcounter.Templates {

    /// <summary>
    /// Represents an action property in a Puppet. An Action property does not contain a value, 
    /// but can be triggered by the client.
    /// </summary>
    /// <remarks>
    /// When you create a Json-by-example file with a null property (i.e. "myfield":null),
    /// the schema template for that property becomes an TTrigger.
    /// </remarks>
    public class TTrigger : TValue
    {
        /// <summary>
        /// </summary>
        private Func<Puppet, TValue, Input> CustomInputEventCreator = null;
     
        /// <summary>
        /// </summary>
        public List<Action<Puppet, Input>> CustomInputHandlers = new List<Action<Puppet, Input>>();

        /// <summary>
        /// Gets a value indicating whether this instance has instance value on client.
        /// </summary>
        /// <value><c>true</c> if this instance has instance value on client; otherwise, <c>false</c>.</value>
        public override bool HasInstanceValueOnClient {
            get { return true; }
        }

        /// <summary>
        /// </summary>
        /// <value></value>
        public override string JsonType {
            get { return "function"; }
        }

        /// <summary>
        /// </summary>
        /// <value></value>
        public string OnRun { get; set; }

        /// <summary>
        /// Creates the default value of this property.
        /// </summary>
        /// <param name="parent">The object that will hold the created value (the host Obj or host Array)</param>
        /// <returns>Always returns false or is it null????? TODO!??</returns>
        public override object CreateInstance(Container parent) {
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="createInputEvent"></param>
        /// <param name="handler"></param>
        public void AddHandler(
            Func<Obj, TValue, Input> createInputEvent = null,
            Action<Obj, Input> handler = null) {
            this.CustomInputEventCreator = createInputEvent;
            this.CustomInputHandlers.Add(handler);
        }

        /// <summary>
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="rawValue"></param>
        internal override void ProcessInput(Obj obj, byte[] rawValue) {
            Input input = null;

            if (CustomInputEventCreator != null)
                input = CustomInputEventCreator.Invoke((Puppet)obj, this);

            if (input != null) {
                foreach (var h in CustomInputHandlers) {
                    h.Invoke((Puppet)obj, input);
                }
            } 
        }
    }
}
