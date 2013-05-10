﻿// ***********************************************************************
// <copyright file="App.Json.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Starcounter.Templates;
using Starcounter.Internal;

namespace Starcounter {
    // TODO: 
    // Not sure where this class should be 
    public abstract class CodegeneratedJsonSerializer {
        public abstract int Serialize(IntPtr buffer, int bufferSize, dynamic obj);
        public abstract int Populate(IntPtr buffer, int bufferSize, dynamic obj);
    }

    /// <summary>
    /// Class App
    /// </summary>
    public partial class Obj {
        public int ToJson(IntPtr buffer, int bufferSize) {
            var codeGenSerializer = Template.GetSerializer();
            if (codeGenSerializer != null) {
                return codeGenSerializer.Serialize(buffer, bufferSize, this);
            } else {
                throw new NotImplementedException();
            }
        }

        public int Populate(IntPtr buffer, int bufferSize) {
            var codeGenSerializer = Template.GetSerializer();
            if (codeGenSerializer != null) {
                return codeGenSerializer.Populate(buffer, bufferSize, this);
            } else {
                throw new NotImplementedException();
            }
        }

        public string ToJson() {
            return null;
        }

        /// <summary>
        /// Serializes the current instance to a bytearray containing UTF8 encoded bytes.
        /// </summary>
        /// <returns>A bytearray containing the serialized json.</returns>
        /// <remarks>Needs optimization. Should build JSON directly from TurboText or static UTF8 bytes
        /// to UTF8. This suboptimal version first builds Windows UTF16 strings that are ultimatelly
        /// not used.</remarks>
        public byte[] ToJsonUtf8() {
            return Encoding.UTF8.GetBytes(ToJsonSlow());
        }

        /// <summary>
        /// To the json.
        /// </summary>
        /// <returns>System.String.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual string ToJsonSlow() { 
#if QUICKTUPLE
            var sb = new StringBuilder();
            var templ = this.Template;
            int t = 0;
            //if (includeSchema)
            //    sb.Append("{$$:{},");
            //else
                sb.Append('{');

            bool needsComma = false;

//            sb.Append('$');
            foreach (object val in _Values) {
                Template prop = templ.Properties[t++];
                /*                    if (includeSchema) {
                                if (prop.IsVisibleOnClient) {
                                        if (includeSchema && (!prop.HasInstanceValueOnClient || !prop.HasDefaultPropertiesOnClient)) {
                                            if (needsComma) {
                                                sb.Append(',');
                                            }
                                            int tt = 0;
                                            sb.Append('"');
                                            sb.Append('$');
                                            sb.Append(prop.TemplateName);
                                            sb.Append('"');
                                            sb.Append(':');
                                            sb.Append('{');
                                            if (!prop.HasInstanceValueOnClient) {
                                                if (tt++ > 0)
                                                    sb.Append(',');
                                                sb.Append("Type:\"");
                                                sb.Append(prop.JsonType);
                                                sb.Append('"');
                                            }
                                            if (!prop.Editable) {
                                                if (tt++ > 0)
                                                    sb.Append(',');
                                                sb.Append("Editable:false");
                                            }
                                            sb.Append('}');
                                            needsComma = true;
                                        }
                                    }
                 */
                if (prop.HasInstanceValueOnClient) {
                    if (needsComma) {
                        sb.Append(',');
                    }
                    sb.Append('"');
                    sb.Append(prop.TemplateName);
                    sb.Append('"');
                    sb.Append(':');
                    if (prop is TObjArr) {
                        sb.Append('[');
                        int i = 0;
                        foreach (var x in val as Arr) {
                            if (i++ > 0) {
                                sb.Append(',');
                            }
                            sb.Append(x.ToJson());
                        }
                        sb.Append(']');
                    }
                    else if (prop is TObj) {
//                       var x = includeViewContent;
//                       if (x == IncludeView.Default)
//                          x = IncludeView.Always;
                       sb.Append(((Obj)val).ToJson());
                    }
                    else {
                        object papa = val;
                        TValue valueProperty = prop as TValue;
                        if (valueProperty != null && valueProperty.Bound)
                            papa = valueProperty.GetBoundValueAsObject(this);
                       
                       sb.Append(JsonConvert.SerializeObject(papa));
                    }
                    needsComma = true;
                }
            }
//            var view = Media.FileName ?? templ.PropertyName;


            t += InsertAdditionalJsonProperties(sb, t > 0);

//            if (t > 0)
//                sb.Append(',');
//            sb.Append("$Class:");
//            sb.Append(JsonConvert.SerializeObject(templ.ClassName));

            sb.Append('}');
            return sb.ToString();
#else
            throw new NotImplementedException();
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="addComma"></param>
        /// <returns></returns>
        protected virtual int InsertAdditionalJsonProperties(StringBuilder sb, bool addComma) {
            return 0;
        }

        /// <summary>
        /// Populates the current object with values parsed from the specified json string.
        /// </summary>
        /// <param name="json"></param>
        public void PopulateFromJson(string json) {
            using (JsonTextReader reader = new JsonTextReader(new StringReader(json))) {
                if (reader.Read()) {
                    if (!(reader.TokenType == JsonToken.StartObject)) {
                        throw new Exception("Invalid json data. Cannot populate object");
                    }
                    PopulateObject(this, reader);
                }
            }
        }

        /// <summary>
        /// Poplulates the object with values from read from the jsonreader. This method is recursively
        /// called for each new object that is parsed from the json.
        /// </summary>
        /// <param name="obj">The object to set the parsed values in</param>
        /// <param name="reader">The JsonReader containing the json to be parsed.</param>
        private void PopulateObject(Obj obj, JsonReader reader) {
            bool insideArray = false;
            Template tChild = null;
            TObj tobj = obj.Template;

            try {
                while (reader.Read()) {
                    switch (reader.TokenType) {
                        case JsonToken.StartObject:
                            Obj newObj;
                            if (insideArray) {
                                newObj = obj.Get((TObjArr)tChild).Add();
                            } else {
                                newObj = obj.Get((TObj)tChild);
                            }
                            PopulateObject(newObj, reader);
                            break;
                        case JsonToken.EndObject:
                            return;
                        case JsonToken.PropertyName:
                            var tname = (string)reader.Value;
                            tChild = tobj.Properties.GetTemplateByName(tname);
                            if (tChild == null) {
                                throw ErrorCode.ToException(Error.SCERRJSONPROPERTYNOTFOUND, string.Format("Property=\"{0}\"", tname), (msg, e) => {
                                    return new FormatException(msg, e);
                                });
                            }
                            break;
                        case JsonToken.String:
                            obj.Set((TString)tChild, (string)reader.Value);
                            break;
                        case JsonToken.Integer:
                            obj.Set((TLong)tChild, (long)reader.Value);
                            break;
                        case JsonToken.Boolean:
                            obj.Set((TBool)tChild, (bool)reader.Value);
                            break;
                        case JsonToken.Float:
                            if (tChild is TDecimal) {
                                obj.Set((TDecimal)tChild, Convert.ToDecimal(reader.Value));
                            } else {
                                obj.Set((TDouble)tChild, (double)reader.Value);
                            }
                            break;
                        case JsonToken.StartArray:
                            insideArray = true;
                            break;
                        case JsonToken.EndArray:
                            insideArray = false;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            } catch (InvalidCastException castException) {
                switch (reader.TokenType) {
                    case JsonToken.String:
                    case JsonToken.Integer:
                    case JsonToken.Boolean:
                    case JsonToken.Float:
                        throw ErrorCode.ToException(
                            Error.SCERRJSONVALUEWRONGTYPE,
                            castException,
                            string.Format("Property=\"{0} ({1})\", Value=\"{2}\"", tChild.PropertyName, tChild.JsonType, reader.Value.ToString()), 
                            (msg, e) => {
                            return new FormatException(msg, e);
                        });
                    default:
                        throw;
                }
            }
        }
    }
}
