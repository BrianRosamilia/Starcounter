﻿

using Starcounter.Templates;
using System;
using System.Collections.Generic;
namespace Starcounter.Internal.Application.CodeGeneration {

    /// <summary>
    /// The source code representation of the AppTemplate class.
    /// </summary>
    public class NAppTemplate : NClass {
        public NApp AppNode;

        public static Dictionary<AppTemplate, NClass> Instances = new Dictionary<AppTemplate, NClass>();

        public override string ClassName {
            get {
                if (AppNode == null)
                    return "Unknown";
                return AppNode.ClassName + "Template";
            }
        }

        public string _Inherits;

        public override string Inherits {
            get { return _Inherits; }
        }

    }
}
