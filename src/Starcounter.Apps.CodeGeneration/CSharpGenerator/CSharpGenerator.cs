﻿using Starcounter.Templates.Interfaces;
using System.Text;
using System;

namespace Starcounter.Internal.Application.CodeGeneration  {
    public class CSharpGenerator : ITemplateCodeGenerator {
        internal StringBuilder Output = new StringBuilder();

        public NRoot Root;
        public int Indentation { get; set; }

        public CSharpGenerator(NRoot root ) {
            Root = root;
            Indentation = 4;
        }

        /// <summary>
        /// Returns a multiline string representation of the code dom tree
        /// </summary>
        /// <returns>A multiline string</returns>
        public string DumpTree() {
            var sb = new StringBuilder();
            DumpTree(sb, Root, 0);
            return sb.ToString();
        }

        private void DumpTree( StringBuilder sb, NBase node, int indent ) {
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
        /// <param name="root">The dom tree has a single root passed in here</param>
        /// <returns>The .cs source code as a string</returns>
        public string GenerateCode() {
            //return Old.GenerateCodeOld();
            ProcessAllNodes();

            WriteHeader(Root.AppClassNode.Template.CompilerOrigin.FileName, Output);
            foreach (NApp napp in Root.Children)
            {
                WriteNode(napp);
            }

            //return DumpTree();
            return Output.ToString();
        }

        private void ProcessAllNodes() {
            NApp napp;
            NApp previousApp;
            String previousNs;
            String currentNs;

            previousApp = null;
            previousNs = "";
            for (Int32 i = 0; i < Root.Children.Count; i++)
            {
                napp = Root.Children[i] as NApp;
                if (napp == null)
                {
                    throw new Exception("Unable to generate code. Invalid node found. Expected App but found: " + Root.Children[i]);
                }

                currentNs = napp.Template.Namespace;
                if (currentNs != previousNs)
                {
                    if (previousApp != null && !String.IsNullOrEmpty(previousNs))
                    {
                        previousApp.Suffix.Add("}");
                    }

                    if (!String.IsNullOrEmpty(currentNs))
                    {
                        napp.Prefix.Add("namespace " + currentNs + " {");
                    }
                }

                ProcessNode(napp);

                previousNs = currentNs;
                previousApp = napp;
            }

            if (previousApp != null && !String.IsNullOrEmpty(previousNs))
            {
                previousApp.Suffix.Add("}");
            }
        }

        /// <summary>
        /// Create prefix and suffix strings for a node and its children
        /// </summary>
        /// <param name="node">The syntax tree node. Use root to generate the complete source code.</param>
        private void ProcessNode(NBase node) {
            var sb = new StringBuilder();
            if (node is NClass) {
                if (node is NAppMetadata) {
                    var n = node as NAppMetadata;
                    sb.Append("public class ");
                    sb.Append(n.ClassName);
                    if (n.Inherits != null) {
                        sb.Append(" : ");
                        sb.Append(n.Inherits);
                    }
                    sb.Append(" {");
                    node.Prefix.Add(sb.ToString());
                    if (node is NAppMetadata) {
                        WriteAppMetadataClassPrefix(node as NAppMetadata);
                    }
                }
                else {
                    var n = node as NClass;
                    sb.Append("public ");
                    if (n.IsStatic) {
                        sb.Append("static ");
                    }
                    if (n.IsPartial) {
                        sb.Append("partial ");
                    }
                    sb.Append("class ");
                    sb.Append(n.ClassName);
                    if (n.Inherits != null) {
                        sb.Append(" : ");
                        sb.Append(n.Inherits);
                    }
                    sb.Append(" {");
                    node.Prefix.Add(sb.ToString());
                    if (node is NApp) {
                        WriteAppClassPrefix(node as NApp);
                    }
                    else if (node is NAppTemplate) {
                        WriteAppTemplateClassPrefix(node as NAppTemplate);
                    }
                }
                node.Suffix.Add("}");
            }
            else if (node is NProperty) {
                if (node.Parent is NApp)
                    WriteAppMemberPrefix(node as NProperty);
                else if (node.Parent is NAppTemplate)
                    WriteAppTemplateMemberPrefix(node as NProperty);
                else if (node.Parent is NAppMetadata)
                    WriteAppMetadataMemberPrefix(node as NProperty);
            }
            foreach (var kid in node.Children) {
                ProcessNode(kid);
            }
        }

        private void WriteNode( NBase node ) {
            foreach (var x in node.Prefix) {
                Output.Append(' ', node.Indentation);
                Output.AppendLine(x);
            }
            foreach (var kid in node.Children) {
                kid.Indentation = node.Indentation + Indentation;
                WriteNode(kid);
            }
            foreach (var x in node.Suffix) {
                Output.Append(' ', node.Indentation);
                Output.AppendLine(x);
            }
        }

        private void WriteAppMemberPrefix(NProperty m) {
            var sb = new StringBuilder();
            sb.Append("public ");
            sb.Append(m.Type.FullClassName);
            sb.Append(' ');
            sb.Append(m.MemberName);
            sb.Append(" { get { return GetValue");
//            if (m.Type is NListing) {
//                sb.Append('<');
//                sb.Append(((NListing)m.Type).NApp.FullClassName);
//                sb.Append('>');
//            }
            if (m.FunctionGeneric != null) {
                sb.Append('<');
                sb.Append( m.FunctionGeneric.FullClassName );
                sb.Append('>');
            }
            sb.Append("(Template.");
            sb.Append(m.MemberName);
            sb.Append("); } set { SetValue");
            if (m.Type is NListingXXXClass) {
                sb.Append('<');
                sb.Append(((NListingXXXClass)m.Type).NApp.FullClassName);
                sb.Append('>');
            }
            sb.Append("(Template.");
            sb.Append(m.MemberName);
            sb.Append(", value); } }");
            m.Prefix.Add(sb.ToString());
        }

        private void WriteAppClassPrefix(NApp a) {
            a.Prefix.Add(
                "    public static " +
                a.TemplateClass.ClassName +
                " DefaultTemplate = new " +
                a.TemplateClass.ClassName +
                "();");
/*            var sb = new StringBuilder();

            sb.Append("    public ");
            sb.Append(a.ClassName);
            sb.Append("( Entity data ) {");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("        Data = data;");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("    }");
            a.Prefix.Add(sb.ToString());
            */

            a.Prefix.Add(
                "    public " +
                a.ClassName +
                "() {");
            a.Prefix.Add(
                "        Template = DefaultTemplate;");
            a.Prefix.Add(
                "    }");
            a.Prefix.Add(
                "    public new " +
                a.TemplateClass.ClassName +
                " Template { get { return (" +
                a.TemplateClass.ClassName +
                ")base.Template; } set { base.Template = value; } }");
            a.Prefix.Add(
                "    public new " +
                a.MetaDataClass.ClassName +
                " Metadata { get { return (" +
                a.MetaDataClass.ClassName +
                ")base.Metadata; } }");
        }


        private void WriteAppTemplateMemberPrefix(NProperty m) {
            var sb = new StringBuilder();
            sb.Append("public ");
            sb.Append(m.Type.FullClassName);
            sb.Append(' ');
            sb.Append(m.MemberName);
            sb.Append(";");
            m.Prefix.Add(sb.ToString());
        }

        private void WriteAppMetadataMemberPrefix(NProperty m) {
            var sb = new StringBuilder();
            sb.Append("public ");
            sb.Append(m.Type.FullClassName);
            sb.Append(' ');
            sb.Append(m.MemberName);
            sb.Append(" { get { return __p_");
            sb.Append(m.MemberName);
            sb.Append(" ?? (__p_");
            sb.Append(m.MemberName);
            sb.Append(" = new ");
            sb.Append(m.Type.FullClassName);
            sb.Append("(App, App.Template.");
            sb.Append(m.MemberName);
            sb.Append(")); } } private ");
            sb.Append(m.Type.FullClassName);
            sb.Append(" __p_");
            sb.Append(m.MemberName);
            sb.Append(';');
            m.Prefix.Add(sb.ToString());
        }

        /// <summary>
        /// Writes the class declaration and constructor for an AppTemplate class
        /// </summary>
        /// <param name="a">The class declaration syntax node</param>
        private void WriteAppTemplateClassPrefix(NAppTemplate a) {
            var sb = new StringBuilder();
            sb.Append("    public ");
            sb.Append(a.ClassName);
            sb.Append("()");
            a.Prefix.Add(sb.ToString());
            a.Prefix.Add("        : base() {");
            sb = new StringBuilder();
            sb.Append("        InstanceType = typeof(");
            sb.Append(a.AppNode.FullClassName);
            sb.Append(");");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("        ClassName = \"");
            sb.Append(a.AppNode.ClassName);
            sb.Append("\";");
            a.Prefix.Add(sb.ToString());
            foreach (var kid in a.Children) {
                if (kid is NProperty) {
                    var mn = kid as NProperty;
                    sb = new StringBuilder();
                    sb.Append("        ");
                    sb.Append(mn.MemberName);
                    sb.Append(" = Register<");
                    sb.Append(mn.Type.FullClassName);
                    sb.Append(">(\"");
                    sb.Append(mn.MemberName);
                    sb.Append('"');
                    if (mn.Template.Editable) {
                        sb.Append(", Editable = true);");
                    }
                    else {
                        sb.Append(");");
                    }
                    a.Prefix.Add(sb.ToString());
                }
            }
            a.Prefix.Add(
                "    }");
        }


        /// <summary>
        /// Writes the class declaration and constructor for an AppTemplate class
        /// </summary>
        /// <param name="a">The class declaration syntax node</param>
        private void WriteAppMetadataClassPrefix(NAppMetadata a) {
            var sb = new StringBuilder();
            sb.Append("    public ");
            sb.Append(a.ClassName);
            sb.Append("(App app, AppTemplate template) : base(app, template) { }");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("    public new ");
            sb.Append(a.AppNode.FullClassName);
            sb.Append(" App { get { return (");
            sb.Append(a.AppNode.FullClassName);
            sb.Append(")base.App; } }");
            a.Prefix.Add(sb.ToString());
            sb = new StringBuilder();
            sb.Append("    public new ");
            sb.Append(a.AppNode.TemplateClass.FullClassName);
            sb.Append(" Template { get { return (");
            sb.Append(a.AppNode.TemplateClass.FullClassName);
            sb.Append(")base.Template; } }");
            a.Prefix.Add(sb.ToString());
            /*            sb = new StringBuilder();
                        sb.Append("        InstanceType = typeof(");
                        sb.Append(a.AppNode.FullClassName);
                        sb.Append(");");
                        a.Prefix.Add(sb.ToString());
                        sb = new StringBuilder();
                        sb.Append("        ClassName = \"");
                        sb.Append(a.AppNode.ClassName);
                        sb.Append("\";");
                        a.Prefix.Add(sb.ToString());
                        foreach (var kid in a.Children) {
                            if (kid is MemberNode) {
                                var mn = kid as MemberNode;
                                sb = new StringBuilder();
                                sb.Append("        ");
                                sb.Append(mn.MemberName);
                                sb.Append(" = Register<");
                                sb.Append(mn.Type.FullClassName);
                                sb.Append(">( Name = \"");
                                sb.Append(mn.MemberName);
                                sb.Append('"');
                                if (mn.Template.Editable) {
                                    sb.Append(", Editable = true );");
                                }
                                else {
                                    sb.Append(" );");
                                }
                                a.Prefix.Add(sb.ToString());
                            }
                        }
                        a.Prefix.Add(
                            "    }");
             */
        }


        /// <summary>
        /// Writes the header of the CSharp file, including using directives.
        /// </summary>
        /// <param name="fileName">The name of the original json file</param>
        static internal void WriteHeader( string fileName, StringBuilder h ) {
            h.Append("// This is a system generated file. It reflects the Starcounter App Template defined in the file \"");
            h.Append(fileName);
            h.Append('"');
            h.AppendLine();
            h.AppendLine("// DO NOT MODIFY DIRECTLY - CHANGES WILL BE OVERWRITTEN");
            h.AppendLine();
            h.AppendLine("using System;");
            h.AppendLine("using System.Collections.Generic;");
            h.AppendLine("using Starcounter;");
            h.AppendLine("using Starcounter.Internal;");
            h.AppendLine("using Starcounter.Templates;");
            //         h.AppendLine("using System.ComponentModel.Composition;");
            h.AppendLine();
        }
    }
}