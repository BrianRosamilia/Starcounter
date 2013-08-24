﻿// ***********************************************************************
// <copyright file="CSharpGenerator.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Templates.Interfaces;
using System.Text;
using System;
using Starcounter.Templates;

namespace Starcounter.Internal.MsBuild.Codegen {

    /// <summary>
    /// Class CSharpGenerator
    /// </summary>
    public class Gen2CSharpGenerator : ITemplateCodeGenerator 
    {

//        static CSharpGenerator() {
//            XSON.CodeGeneration.Initializer.InitializeXSON();
//        }


        /// <summary>
        /// The output
        /// </summary>
        internal StringBuilder Output = new StringBuilder();

        /// <summary>
        /// The root
        /// </summary>
        public AstRoot Root;
        /// <summary>
        /// Gets or sets the indentation.
        /// </summary>
        /// <value>The indentation.</value>
        public int Indentation { get; set; }

        /// <summary>
        /// The code generator can be used to generate typed JSON. My storing the generarator
        /// we can obtain the wanted default obj template, i.e.
        /// which template to use as the default template for new objects.
        /// </summary>
        public Gen2DomGenerator Generator;


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IReadOnlyTree GenerateAST() {
            return Root;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="root"></param>
        public Gen2CSharpGenerator(Gen2DomGenerator generator, AstRoot root ) {
            Generator = generator;
            Root = root;
            Indentation = 4;
        }

        /// <summary>
        /// Returns a multiline string representation of the code dom tree
        /// </summary>
        /// <returns>A multiline string</returns>
        public string DumpAstTree() {
            var sb = new StringBuilder();
            DumpTree(sb, Root, 0);
            return sb.ToString();
        }

        /// <summary>
        /// Dumps the tree.
        /// </summary>
        /// <param name="sb">The sb.</param>
        /// <param name="node">The node.</param>
        /// <param name="indent">The indent.</param>
        private void DumpTree( StringBuilder sb, AstBase node, int indent ) {
            sb.Append(' ', indent);
            sb.AppendLine(node.ToString());
            foreach (var kid in node.Children) {
                DumpTree(sb, kid, indent + 3);
            }
        }

        /// <summary>
        /// Generates source code for the simple code dom tree generated from the Starcounter
        /// application view document template.
        /// </summary>
        /// <returns>The .cs source code as a string</returns>
        public string GenerateCode() {
//            System.Diagnostics.Debugger.Launch();

            //return Old.GenerateCodeOld();
            ProcessAllNodes();

            WriteHeader(Root.AppClassClassNode.Template.CompilerOrigin.FileName, Output);
            foreach (var napp in Root.Children)
            {
                WriteNode(napp);
            }
            WriteFooter(Output);

            //return DumpTree();
            return Output.ToString();
        }

        /// <summary>
        /// Processes all nodes.
        /// </summary>
        /// <exception cref="System.Exception">Unable to generate code. Invalid node found. Expected App but found: </exception>
        private void ProcessAllNodes() {
            AstAppClass napp;
            AstBase previousKid;
            String previousNs;
            String currentNs = null;

            previousKid = null;
            previousNs = "";
            for (Int32 i = 0; i < Root.Children.Count; i++) {
                var kid = Root.Children[i];

                if (kid is AstAppClass) {
                    napp = kid as AstAppClass;
                    currentNs = napp.Template.Namespace;
                }
                //                    if (napp == null) {
                //                        throw new Exception("Unable to generate code. Invalid node found. Expected Puppet but found: " + Root.Children[i]);
                //                    }

                if (currentNs != previousNs) {
                    if (previousKid != null && !String.IsNullOrEmpty(previousNs)) {
                        previousKid.Suffix.Add("}");
                    }

                    if (!String.IsNullOrEmpty(currentNs)) {
                        kid.Prefix.Add("namespace " + currentNs + " {");
                    }
                }

                ProcessNode(kid);

                previousNs = currentNs;
                previousKid = kid;
            }

            if (previousKid != null && !String.IsNullOrEmpty(previousNs))
            {
                previousKid.Suffix.Add("}");
            }
        }

        /// <summary>
        /// Create prefix and suffix strings for a node and its children
        /// </summary>
        /// <param name="node">The syntax tree node. Use root to generate the complete source code.</param>
        private void ProcessNode(AstBase node) {
            var sb = new StringBuilder();
            if (node is AstClass) {
                if (node is AstObjMetadata) {
                    var n = node as AstObjMetadata;
                    sb.Append("public class ");
                    sb.Append(n.ClassName);
                    if (n.Inherits != null) {
                        sb.Append(" : ");
                        sb.Append(n.Inherits);
                    }
                    sb.Append(" {");
                    node.Prefix.Add(sb.ToString());
                    if (node is AstObjMetadata) {
                        WriteObjMetadataClassPrefix(node as AstObjMetadata);
                    }
                }
                else {
                    var n = node as AstClass;
                    sb.Append("public ");
                    if (n.IsStatic) {
                        sb.Append("static ");
                    }
                    if (n.IsPartial) {
                        sb.Append("partial ");
                    }
                    sb.Append("class ");
                    sb.Append(n.ClassDeclaration);
                    if (n.Inherits != null) {
                        sb.Append(" : ");
                        sb.Append(n.Inherits);
                    }
                    sb.Append(" {");
                    node.Prefix.Add(sb.ToString());
                    if (node is AstAppClass) {
                        WriteAppClassPrefix(node as AstAppClass);
                    }
                    else if (node is AstTAppClass) {
                        WriteTAppConstructor((node as AstTAppClass).Constructor);
                        WriteTAppCreateInstance(node as AstTAppClass);
                    }
                }
                node.Suffix.Add("}");
            }
            else if (node is AstProperty) {
                if (node.Parent is AstAppClass)
                    WriteAppMemberPrefix(node as AstProperty);
                else if (node.Parent is AstTAppClass)
                    WriteTAppMemberPrefix(node as AstProperty);
                else if (node.Parent is AstObjMetadata)
                    WriteObjMetadataMemberPrefix(node as AstProperty);
            } 

            foreach (var kid in node.Children) {
                ProcessNode(kid);
            }
        }

        /// <summary>
        /// Writes the node.
        /// </summary>
        /// <param name="node">The node.</param>
        private void WriteNode( AstBase node ) {
            foreach (var x in node.Prefix) {
                Output.Append(' ', node.Indentation);
                Output.Append(x);
                Output.Append('\n');
            }
            foreach (var kid in node.Children) {
                kid.Indentation = node.Indentation + Indentation;
                WriteNode(kid);
            }
            foreach (var x in node.Suffix) {
                Output.Append(' ', node.Indentation);
                Output.Append(x);
                Output.Append('\n');
            }
        }

        /// <summary>
        /// Writes the app member prefix.
        /// </summary>
        /// <param name="m">The m.</param>
        private void WriteAppMemberPrefix(AstProperty m) {
            var sb = new StringBuilder();
            sb.Append("public ");
            sb.Append(m.Type.GlobalClassSpecifier);
            sb.Append(' ');
            sb.Append(m.MemberName);
//            if (m.Type is NArr) {
//                sb.Append('<');
//                sb.Append(((NArr)m.Type).NApp.GlobalClassSpecifier);
//                sb.Append('>');
//            }
            if (m.FunctionGeneric != null) {
                sb.Append(" { get { return Get<");
                sb.Append(m.FunctionGeneric.GlobalClassSpecifier);
                sb.Append('>');
            }
            else {
                sb.Append(" { get { return Get");
            }
            sb.Append("(Template.");
            sb.Append(m.MemberName);
            sb.Append("); } set { Set");
            if (m.Type is AstArrXXXClass) {
                sb.Append('<');
                sb.Append(((AstArrXXXClass)m.Type).NApp.GlobalClassSpecifier);
                sb.Append('>');
            }
            sb.Append("(Template.");
            sb.Append(m.MemberName);
            sb.Append(", value); } }");
            m.Prefix.Add(sb.ToString());
        }

        /// <summary>
        /// Writes the app class prefix.
        /// </summary>
        /// <param name="a">A.</param>
        private void WriteAppClassPrefix(AstAppClass a) {
            a.Prefix.Add(
                "    public static " +
                a.ClassSpecifierWithoutNamespace + " GET(string uri) { return (" + 
                a.ClassSpecifierWithoutNamespace + ")X.GET(uri); }");
            a.Prefix.Add(
                "    public static " +
                a.NTemplateClass.ClassName +
                " DefaultTemplate = new " +
                a.NTemplateClass.ClassName +
                "();");

            a.Prefix.Add("    public " 
                         + a.ClassName 
                         + "() { Template = DefaultTemplate; }");
            a.Prefix.Add("    public " 
                         + a.ClassName 
                         + "("
                         + a.NTemplateClass.ClassName
                         + " template) { Template = template; }"); 
            a.Prefix.Add(
                "    public new " +
                a.NTemplateClass.ClassName +
                " Template { get { return (" +
                a.NTemplateClass.ClassName +
                ")(this as s::Json).Template; } set { (this as s::Json).Template = value; } }");
            a.Prefix.Add(
                "    public new " +
                a.NTemplateClass.NMetadataClass.ClassName +
                " Metadata { get { return (" +
                a.NTemplateClass.NMetadataClass.ClassName +
                ")(this as s::Json).Metadata; } }");
            if (a.Template.Parent != null) {
                string parentClass = GetParentPropertyType(a.NTemplateClass.Template).ClassName;
                a.Prefix.Add(
                    "    public new " +
                    parentClass +
                    " Parent { get { return (" +
                    parentClass +
                    ")(this as s::Json).Parent; } set { (this as s::Json).Parent = value; } }");
            }
        }

        private AstClass GetParentPropertyType(Template a) {
            var x = Generator.FindValueClass((Template)a.Parent);
            return x;
        }

        /// <summary>
        /// Writes the app template member prefix.
        /// </summary>
        /// <param name="m">The m.</param>
        private void WriteTAppMemberPrefix(AstProperty m) {
            var sb = new StringBuilder();
            sb.Append("public ");
            sb.Append(m.Type.GlobalClassSpecifier);
            sb.Append(' ');
            sb.Append(m.MemberName);
            sb.Append(";");
            m.Prefix.Add(sb.ToString());

            var objClassName = Generator.DefaultObjTemplate.InstanceType.Name; // "Puppet", "Json"
        }

        /// <summary>
        /// Writes the app metadata member prefix.
        /// </summary>
        /// <param name="m">The m.</param>
        private void WriteObjMetadataMemberPrefix(AstProperty m) {

            //var objClassName = DefaultObjTemplate.InstanceType.TemplateName;

            var sb = new StringBuilder();
            sb.Append("public ");
            sb.Append(m.Type.GlobalClassSpecifier);
            sb.Append(' ');
            sb.Append(m.MemberName);
            sb.Append(" { get { return __p_");
            sb.Append(m.MemberName);
            sb.Append(" ?? (__p_");
            sb.Append(m.MemberName);
            sb.Append(" = new ");
            sb.Append(m.Type.GlobalClassSpecifier);
            sb.Append('(');
            sb.Append("App"); // Property name .App TODO
            sb.Append(", ");
            sb.Append("App"); // Property name .App TODO
            sb.Append(".Template.");
            sb.Append(m.MemberName);
            sb.Append(")); } }");
            m.Prefix.Add(sb.ToString());

            sb.Clear();
            sb.Append("private ");
            sb.Append(m.Type.GlobalClassSpecifier);
            sb.Append(" __p_");
            sb.Append(m.MemberName);
            sb.Append(';');
            m.Prefix.Add(sb.ToString());
        }

        /// <summary>
        /// Writes override method for creating default appinstance from template.
        /// </summary>
        /// <param name="node"></param>
        private void WriteTAppCreateInstance(AstTAppClass node) {
            StringBuilder sb = new StringBuilder();
            sb.Append("    public override object CreateInstance(Container parent) { return new ");
            sb.Append(node.NValueClass.GlobalClassSpecifier);
            if (node.Template.Parent != null) {
                string parentClass = GetParentPropertyType(node.Template).GlobalClassSpecifier;
                sb.Append("(this) { Parent = (" + 
                    parentClass + 
                    ")parent }; }");
            }
            else {
                sb.Append("(this) { Parent = parent }; }");
            }
            node.Prefix.Add(sb.ToString());
        }

        /// <summary>
        /// Writes the class declaration and constructor for an TApp class
        /// </summary>
        /// <param name="cst">The CST.</param>
        private void WriteTAppConstructor(AstConstructor cst) {
            AstTAppClass a = (AstTAppClass)cst.Parent;
            
            var sb = new StringBuilder();
            sb.Append("    public ");
            sb.Append(a.ClassName);
            sb.Append("()");
            a.Prefix.Add(sb.ToString());
            a.Prefix.Add("        : base() {");

            if (a.AutoBindProperties)
                a.Prefix.Add("        BindChildren = true;");

            sb = new StringBuilder();
            sb.Append("        InstanceType = typeof(");
            sb.Append(a.NValueClass.GlobalClassSpecifier);
            sb.Append(");");
            a.Prefix.Add(sb.ToString());
            
            sb = new StringBuilder();
            sb.Append("        ClassName = \"");
            sb.Append(a.NValueClass.ClassName);
            sb.Append("\";");
            a.Prefix.Add(sb.ToString());

            a.Prefix.Add("        Properties.ClearExposed();");
            foreach (AstBase kid in cst.Children)
            {
                if (kid is AstProperty)
                {
                    var mn = kid as AstProperty;
                    sb = new StringBuilder();
                    sb.Append("        ");
                    sb.Append(mn.MemberName);
                    sb.Append(" = Add<");
                    sb.Append(mn.Type.GlobalClassSpecifier);

                    sb.Append(">(\"");
                    sb.Append(mn.Template.TemplateName);
                    sb.Append('"');
                    
                    TValue tv = mn.Template as TValue;
                    if (tv != null && ( tv.Bound == Bound.Yes ) && tv.PropertyName != tv.Bind) {
                        if (tv.Bind == null) {
                            sb.Append(", bind:null");
                        } else {
                            sb.Append(", bind:\"");
                            sb.Append(tv.Bind);
                            sb.Append('"');
                        }
                    }
                    sb.Append(");");
                    a.Prefix.Add(sb.ToString());

                    if (mn.Template.Editable) {
                        a.Prefix.Add("        " + mn.MemberName + ".Editable = true;");
                    }

                    string objClassName = Generator.DefaultObjTemplate.InstanceType.Name;
                    if ((mn.Template is TObjArr) && (!mn.FunctionGeneric.FullClassName.Equals( objClassName ))) { // TODO!
                        sb.Clear();
                        sb.Append("        ");
                        sb.Append(mn.MemberName);
                        sb.Append(".ElementType = ");
                        sb.Append(mn.FunctionGeneric.GlobalClassSpecifier);
                        sb.Append(".DefaultTemplate;");
                        a.Prefix.Add(sb.ToString());
                    }
                }
                else if (kid is AstInputBinding)
                {
                    a.Prefix.Add(GetAddInputHandlerCode((AstInputBinding)kid));
                }
            }
            a.Prefix.Add(
                "    }");
        }

        /// <summary>
        /// Gets the add input handler code.
        /// </summary>
        /// <param name="ib">The ib.</param>
        /// <returns>String.</returns>
        private String GetAddInputHandlerCode(AstInputBinding ib)
        {
            bool hasValue = ib.HasValue;
            StringBuilder sb = new StringBuilder();
            sb.Append("        ");
            sb.Append(ib.BindsToProperty.Template.PropertyName);       // {0}
            sb.Append(".AddHandler((Obj pup, TValue");

            if (hasValue) {
                sb.Append('<');
                sb.Append(ib.BindsToProperty.Template.JsonType);   // {1}
                sb.Append('>');
            }
            sb.Append(" prop");

            if (hasValue) {
                sb.Append(", ");
                sb.Append(ib.BindsToProperty.Template.JsonType);   // {1}
                sb.Append(" value");
            }
            sb.Append(") => { return (new ");
            sb.Append(ib.InputTypeName);                       // {2}
            sb.Append("() { App = (");
            sb.Append(ib.PropertyAppClass.ClassName);          // {3}
            sb.Append(")pup, Template = (");
            sb.Append(ib.BindsToProperty.Type.ClassName);      // {4}
            sb.Append(")prop");
            
            if (hasValue)
            {
                sb.Append(", Value = value");
            }

            sb.Append(" }); }, (Obj pup, Starcounter.Input");

            if (hasValue) {
                sb.Append('<');
                sb.Append(ib.BindsToProperty.Template.JsonType);   // {1}
                sb.Append('>');
            }

            sb.Append(" input) => { ((");
            sb.Append(ib.DeclaringAppClass.ClassName);         // {5}
            sb.Append(")pup");

            for (Int32 i = 0; i < ib.AppParentCount; i++)
            {
                sb.Append(".Parent");
            }

            sb.Append(").Handle((");
            sb.Append(ib.InputTypeName);                       // {2}
            sb.Append(")input); });");

            return sb.ToString();
        }

        /// <summary>
        /// Writes the class declaration and constructor for an TApp class
        /// </summary>
        /// <param name="a">The class declaration syntax node</param>
        private void WriteObjMetadataClassPrefix(AstMetadataClass a) {
            var sb = new StringBuilder();
            sb.Append("    public ");
            sb.Append(a.ClassName);

            string objClassName = Generator.DefaultObjTemplate.InstanceType.Name;
            string tobjClassName = Generator.DefaultObjTemplate.GetType().Name;

            sb.Append('(');
            sb.Append( objClassName ); // "Puppet", "Json"
            sb.Append(" obj, ");
            sb.Append(tobjClassName); // "TPuppet", "TJson"
            sb.Append(" template) : base(obj, template) { }");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("    public new ");
            sb.Append(a.NTemplateClass.NValueClass.GlobalClassSpecifier);
            sb.Append(' ');
            sb.Append( "App" ); // TODO! Property name .Json
            sb.Append(" { get { return (");
            sb.Append(a.NTemplateClass.NValueClass.GlobalClassSpecifier);
            sb.Append(")base.App; } }");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("    public new ");
            sb.Append(a.NTemplateClass.GlobalClassSpecifier);
            sb.Append(" Template { get { return (");
            sb.Append(a.NTemplateClass.GlobalClassSpecifier);
            sb.Append(")base.Template; } }");
            a.Prefix.Add(sb.ToString());
        }

        /// <summary>
        /// Writes the header of the CSharp file, including using directives.
        /// </summary>
        /// <param name="fileName">The name of the original json file</param>
        /// <param name="h">The h.</param>
        static internal void WriteHeader( string fileName, StringBuilder h ) {
            h.Append("// This is a system generated file (G2). It reflects the Starcounter App Template defined in the file \"");
            h.Append(fileName);
            h.Append('"');
            h.Append('\n');
            h.Append("// DO NOT MODIFY DIRECTLY - CHANGES WILL BE OVERWRITTEN\n");
            h.Append('\n');
            h.Append("using System;\n");
            h.Append("using System.Collections;\n");
            h.Append("using System.Collections.Generic;\n");
            h.Append("using Starcounter.Advanced;\n");
            h.Append("using Starcounter;\n");
            h.Append("using Starcounter.Internal;\n");
            h.Append("using Starcounter.Templates;\n");
            h.Append("using st = Starcounter.Templates;\n");
            h.Append("using s = Starcounter;\n");
            h.Append("#pragma warning disable 0108\n");
			h.Append("#pragma warning disable 1591\n");
			h.Append('\n');
        }

        static internal void WriteFooter(StringBuilder f) {
			f.Append("#pragma warning restore 1591\n");
            f.Append("#pragma warning restore 0108");
        }
    }
}