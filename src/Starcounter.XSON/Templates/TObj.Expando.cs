﻿

using System;
namespace Starcounter.Templates {
    partial class TObj {



        private bool _Dynamic = false;

        /// <summary>
        /// Returns true if this object support expando like (Javascript like) behaviour that
        /// lets you create properties without a preexisting schema.
        /// </summary>
        public bool IsDynamic {
            get {
                return _Dynamic;
            }
            set {
                _Dynamic = value;
            }
        }

        /// <summary>
        /// For dynamic Json objects, templates pertain to only a single object.
        /// </summary>
        internal Obj SingleInstance = null;

        /// <summary>
        /// Called when the user attempted to set a value on a dynamic obj without the object having
        /// the property defined (in its template). In the TDynamicObj template, this method will
        /// add the property to the template definition. The default behaviour in the implementation
        /// with a strict schema template, a exception will be called.
        /// </summary>
        /// <param name="property">The name of the missing property</param>
        /// <param name="Type">The type of the value being set</typeparam>
        internal void OnSetUndefinedProperty(string property, Type type) {
            if (this.IsDynamic) {
                this.Add(type, property);
            }
            else {
                throw new Exception(String.Format("An attempt was made to set the property {0} to a {1} on an Obj object not having the property defined in the template.", property, type.Name));
            }
        }
    }
}