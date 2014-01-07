// ***********************************************************************
// <copyright file="NApp.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Templates;
using Starcounter.XSON.Metadata;
using System;
using System.Collections.Generic;
using TJson = Starcounter.Templates.TObject;


namespace Starcounter.Internal.MsBuild.Codegen {

    /// <summary>
    /// Represents a Json instance class (a class that is 
    /// derived from Json&ltobject&gt or the Json&ltobject&gt
    /// class.
    /// </summary>
    public class AstJsonClass : AstInstanceClass {

        /// <summary>
        /// The constructor
        /// </summary>
        /// <param name="generator">The code-dom generator instance</param>
        public AstJsonClass(Gen2DomGenerator generator)
            : base(generator) {
        }

        /// <summary>
        /// Gets the template.
        /// </summary>
        /// <value>The template.</value>
        public TContainer Template {
            get { return (TContainer)(NTemplateClass.Template); }
        }

        public override string Generics {
            get {
                throw new Exception();
//                if (MatchedClass == null)
//                    return null;
//                return MatchedClass.GenericArg;
            }
        }

        /// <summary>
        /// The class name is linked to the name of the ClassName in the
        /// App template tree. If there is no ClassName, the property name
        /// of the App in the parent App is used.
        /// </summary>
        /// <value>The stem.</value>
        public string Stem {
            get {
                throw new Exception();
//                if (Template.ClassName != null)
//                    return Template.ClassName;
//                else if (Template.Parent is TObjArr) {
//                    var alt = (TObjArr)Template.Parent;
//                    return alt.PropertyName;
//                } else
//                    return Template.PropertyName;
            }
        }

        public AstOtherClass NJsonByExample = null;



        /// <summary>
        /// Returns false if there are no children defined. This indicates that the property
        /// that uses this node as a type should instead use the default Obj class (Json,Puppet) inside
        /// the Starcounter library. This is done by the NApp node pretending to be the App class
        /// node to make DOM generation easier (this cheating is intentional).
        /// </summary>
        /// <value><c>true</c> if this instance is custom app template; otherwise, <c>false</c>.</value>
        public bool IsCustomGeneratedClass {
            get {
                if (Template is TObject) {
                    return ((TObject)Template).Properties.Count > 0;
                }
                return false;
            }
        }

        /// <summary>
        /// Anonymous classes (classes for inner JSON objects) should have a nice
        /// name. We choose to use the name of the property it was created from with
        /// an added JSON ending. We cannot simply use the same name as the property
        /// as CSharp does not allow us to have the same name for an inner class as
        /// for a property name.
        /// </summary>
        /// <param name="name">The name to amend</param>
        /// <returns>A name that ends with the text "Json"</returns>
        public override string ClassStemIdentifier {
            get {
                if (CodebehindClass != null) {
                    return CodebehindClass.ClassName;
                }
                var template = NTemplateClass.Template;
                string className = null;
                if (template is TObject) {
                    className = (template as TObject).ClassName;
                }
                if (className == null) {
                    className = template.PropertyName;
                    className = className + "Json";
                }
                return className;
            }
            set {
                base.ClassStemIdentifier = value;
            }
        }

        public AstInstanceClass ParentProperty { get; set; }

    }
}
