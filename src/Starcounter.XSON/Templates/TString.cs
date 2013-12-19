// ***********************************************************************
// <copyright file="TString.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using Starcounter.Advanced.XSON;

namespace Starcounter.Templates {

    /// <summary>
    /// 
    /// </summary>
    public class TString : PrimitiveProperty<string> {
        public override void ProcessInput(Json obj, byte[] rawValue) {
			string value = null;
			if (rawValue.Length > 0) {
				unsafe {
					fixed (byte* p = rawValue) {
						value = JsonHelper.DecodeString(p, rawValue.Length, rawValue.Length);
					}
				}
			}
            obj.ProcessInput<string>(this, value);
        }
        public override Type MetadataType {
            get { return typeof(StringMetadata<Json>); }
        }


        private string _DefaultValue = "";

        /// <summary>
        /// Gets or sets the default value.
        /// </summary>
        /// <value>The default value.</value>
        public string DefaultValue {
            get { return _DefaultValue; }
            set { _DefaultValue = value; }
        }

        /// <summary>
        /// Contains the default value for the property represented by this
        /// Template for each new App object.
        /// </summary>
        /// <value>The default value as object.</value>
        public override object DefaultValueAsObject {
            get {
                return DefaultValue;
            }
            set {
                DefaultValue = (string)value;
            }
        }

        /// <summary>
        /// The .NET type of the instance represented by this template.
        /// </summary>
        /// <value>The type of the instance.</value>
        public override Type InstanceType {
            get { return typeof(string); }
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="addToChangeLog"></param>
		internal override void CheckAndSetBoundValue(Json parent, bool addToChangeLog) {
			if (UseBinding(parent)) {
				string boundValue = BoundGetter(parent);
				string oldValue = UnboundGetter(parent);

				if ((boundValue == null && oldValue != null) 
					|| (boundValue != null && !boundValue.Equals(oldValue))) {
					UnboundSetter(parent, boundValue);
					if (addToChangeLog)
						parent.Session.UpdateValue(parent, this);
				}
			}	
		}

		internal override string ValueToJsonString(Json parent) {
			string value = Getter(parent);
			if (!string.IsNullOrEmpty(value)) {
				byte[] buffer = new byte[value.Length * 4];
				unsafe {
					fixed (byte* p = buffer) {
						int size = JsonHelper.WriteString((IntPtr)p, buffer.Length, value);
						return System.Text.Encoding.UTF8.GetString(buffer, 0, size);
					}
				}
			}
			return "\"\"";
		}
    }
}
