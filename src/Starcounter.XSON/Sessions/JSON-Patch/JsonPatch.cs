// ***********************************************************************
// <copyright file="JsonPatch.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Starcounter.Internal;
using Starcounter.Templates;


namespace Starcounter.Internal.JsonPatch {
    /// <summary>
    /// Struct AppAndTemplate
    /// </summary>
    internal struct AppAndTemplate {
        /// <summary>
        /// The app
        /// </summary>
        public readonly Json App;
        /// <summary>
        /// The template
        /// </summary>
        public readonly Template Template;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppAndTemplate" /> struct.
        /// </summary>
        /// <param name="app">The app.</param>
        /// <param name="template">The template.</param>
        public AppAndTemplate(Json app, Template template) {
            App = app;
            Template = template;
        }
    }

    internal enum JsonPatchMember {
        Invalid,
        Op,
        Path,
        Value
    }

    /// <summary>
    /// Class for evaluating, handling and creating json-patch to and from typed json objects 
    /// and logged changes done in a typed json object during a request.
    /// 
    /// The json-patch is implemented according to http://tools.ietf.org/html/draft-ietf-appsawg-json-patch-10
    /// </summary>
    public partial class JsonPatch {
        public const Int32 UNDEFINED = 0;
        public const Int32 REMOVE = 1;
        public const Int32 REPLACE = 2;
        public const Int32 ADD = 3;

        private static string[] _patchTypeToString;
        private static byte[] _addPatchArr;
        private static byte[] _removePatchArr;
        private static byte[] _replacePatchArr;

        /// <summary>
        /// Initializes static members of the <see cref="JsonPatch" /> class.
        /// </summary>
        static JsonPatch() {
            _patchTypeToString = new String[4];
            _patchTypeToString[UNDEFINED] = "undefined";
            _patchTypeToString[REMOVE] = "remove";
            _patchTypeToString[REPLACE] = "replace";
            _patchTypeToString[ADD] = "add";

            _addPatchArr = Encoding.UTF8.GetBytes(_patchTypeToString[ADD]);
            _removePatchArr = Encoding.UTF8.GetBytes(_patchTypeToString[REMOVE]);
            _replacePatchArr = Encoding.UTF8.GetBytes(_patchTypeToString[REPLACE]);
        }

        /// <summary>
        /// Patches the type to string.
        /// </summary>
        /// <param name="patchType">Type of the patch.</param>
        /// <returns>String.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">patchType</exception>
        private static String PatchTypeToString(Int32 patchType) {
            if ((patchType < 0) || (patchType >= _patchTypeToString.Length))
                throw new ArgumentOutOfRangeException("patchType");
            return _patchTypeToString[patchType];
        }

        // TODO:
        // Change this to return a bytearray since that is the way we are going to send it
        // in the response, or make it so it creates a series of json patches in a submitted
        // buffer instead of allocating a new one here.
        /// <summary>
        /// Builds the json patch.
        /// </summary>
        /// <param name="patchType">Type of the patch.</param>
        /// <param name="nearestApp">The nearest app.</param>
        /// <param name="from">From.</param>
        /// <param name="index">The index.</param>
        /// <returns>String.</returns>
        public static String BuildJsonPatch(Int32 patchType, Json nearestApp, TValue from, Int32 index) {
            List<String> pathList = new List<String>();
            StringBuilder sb = new StringBuilder(40);
			Json childJson = null;

            sb.Append("{\"op\":\"");
            sb.Append(PatchTypeToString(patchType));
            sb.Append("\",\"path\":\"");

			if (from != null) {
				IndexPathToString(sb, from, nearestApp);
			} else {
				sb.Append('/');
				childJson = nearestApp;
			}

          if (index != -1) {
              sb.Append('/');
              sb.Append(index);
          }

            sb.Append('"');

            if (patchType != REMOVE) {
                sb.Append(",\"value\":");
				if (childJson == null && from is TContainer) {
					childJson = ((TContainer)from).GetValue(nearestApp);
					if (index != -1)
						childJson = (Json)childJson._GetAt(index);

					if (childJson == null)
						sb.Append("{}");
				}

				if (childJson != null) {
					sb.Append(childJson.ToJson());
					childJson.SetBoundValuesInTuple();
				} else {
                    sb.Append(from.ValueToJsonString(nearestApp));
                }
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Indexes the path to string.
        /// </summary>
        /// <param name="sb">The sb.</param>
        /// <param name="from">From.</param>
        /// <param name="nearestApp">The nearest app.</param>
        private static void IndexPathToString(StringBuilder sb, Template from, Json nearestApp) {
            Json app;
            Json parent;
            Boolean nextIndexIsPositionInList;
            Int32[] path;
            Json list;
            TObjArr listProp;
            Template template;

            // Find the root app.
            parent = nearestApp;
            while (parent.Parent != null)
                parent = parent.Parent;
            app = (Json)parent;

            nextIndexIsPositionInList = false;
            listProp = null;
            path = nearestApp.IndexPathFor(from);
            for (Int32 i = 0; i < path.Length; i++) {
                if (nextIndexIsPositionInList) {
                    nextIndexIsPositionInList = false;
					list = listProp.GetValue(app);
                    app = (Json)list._GetAt(path[i]);
                    sb.Append('/');
                    sb.Append(path[i]);
                } else {
                    if (app.IsArray) {
                        throw new NotImplementedException();
                    }
                    template = ((TObject)app.Template).Properties[path[i]];
                    sb.Append('/');
                    sb.Append(template.TemplateName);

                    if (template is TObjArr) {
                        // next index in the path is the index in the list.
                        listProp = (TObjArr)template;
                        nextIndexIsPositionInList = true;
                    }
                    else if (template is TObject) {
                        app = ((TObject)template).Getter(app);
                    }
                }
            }
        }
    }
}
