// ***********************************************************************
// <copyright file="TTrigger.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;

namespace Starcounter.Templates {

    /// <summary>
    /// Represents an action property in a typed json object. An Action property does not contain a value, 
    /// but can be triggered by the client.
    /// </summary>
    /// <remarks>
    /// When you create a Json-by-example file with a null property (i.e. "myfield":null),
    /// the schema template for that property becomes an TTrigger.
    /// </remarks>
    public class TTrigger : TValue {
        public override bool IsPrimitive {
            get { return true; }
        }

        public override Type MetadataType {
            get { return typeof(ActionMetadata<Json>); }
        }

        /// <summary>
        /// </summary>
        private Func<Json, TValue, Input> CustomInputEventCreator = null;
     
        /// <summary>
        /// </summary>
        public List<Action<Json, Input>> CustomInputHandlers = new List<Action<Json, Input>>();

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
        public override object CreateInstance(Json parent) {
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="createInputEvent"></param>
        /// <param name="handler"></param>
        public void AddHandler(
            Func<Json, TValue, Input> createInputEvent = null,
            Action<Json, Input> handler = null) {
            this.CustomInputEventCreator = createInputEvent;
            this.CustomInputHandlers.Add(handler);
        }

        /// <summary>
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="rawValue"></param>
        public override void ProcessInput(Json obj, byte[] rawValue) {
            Input input = null;

            if (CustomInputEventCreator != null)
                input = CustomInputEventCreator.Invoke(obj, this);

            if (input != null) {
                foreach (var h in CustomInputHandlers) {
                    h.Invoke(obj, input);
                }
            } 
        }

		internal override void SetDefaultValue(Json parent) {
		}

		internal override void InvalidateBoundGetterAndSetter() {
			base.InvalidateBoundGetterAndSetter();
		}

		internal override bool GenerateBoundGetterAndSetter(Json json) {
			return false;
		}

		internal override void GenerateUnboundGetterAndSetter() {
		}

		internal override void CheckAndSetBoundValue(Json json, bool addToChangeLog) {
		}

		internal override object GetUnboundValueAsObject(Json parent) {
			return null;
		}

		internal override object GetValueAsObject(Json parent) {
			return null;
		}

		internal override void SetValueAsObject(Json parent, object value) {
		}

		internal override void CopyValueDelegates(Template toTemplate) {
		}
    }
}
