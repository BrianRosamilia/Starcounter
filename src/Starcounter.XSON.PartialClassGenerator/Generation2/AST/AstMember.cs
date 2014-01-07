// ***********************************************************************
// <copyright file="NMember.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Internal.MsBuild.Codegen {


    /// <summary>
    /// Represents a property, a field or a function
    /// </summary>
    public class AstProperty : AstBase {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gen"></param>
        public AstProperty(Gen2DomGenerator gen)
            : base(gen) {
        }

//        public AstInstanceClass ElementTypeProperty { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public AstClass Type { get; set; }
        /// <summary>
        /// Gets the name of the member.
        /// </summary>
        /// <value>The name of the member.</value>
        public string MemberName {
            get {
                return Template.PropertyName;
            }
        }

		/// <summary>
		/// Returns the name of a backing field if this property is a value property.
		/// </summary>
		public string BackingFieldName {
			get {
				var tv = Template as TValue;
				if (tv != null && !(tv is TTrigger)) {
					return "__bf__" + MemberName + "__";
				}
				return null;
			}
		}

        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        /// <value>The template.</value>
        public Template Template { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString() {
            string str = MemberName;
//            if (FunctionGeneric != null) {
//                str += "<" + FunctionGeneric.FullClassName + ">";
//            }
            return "NMEMBER " + Type.ClassSpecifierWithoutOwners + " " + str;
        }
    }
}
