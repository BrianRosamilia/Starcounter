// ***********************************************************************
// <copyright file="Obj.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Advanced;
using Starcounter.Templates;
using System;
using Starcounter.Internal;
using Starcounter.Templates.Interfaces;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using Starcounter.Internal.XSON;

namespace Starcounter {
    /// <summary>
    /// A Json object represents tree of values that can be serialized to or from a 
    /// JSON string.
    /// 
    /// A value can be a primitive such as numbers, strings and booleans or they can be other Json objects.or
    /// arrays of values.
    /// 
    /// The objects mimics the types of trees inducable from the JSON string format.
    /// The difference from the Json induced object tree in Javascript is
    /// foremost that Obj supports multiple numeric types, time and higher precision numerics.
    /// 
    /// While JSON is a text based notation format, the Json class is a materialized
    /// tree of arrays, objects and primitive values than can be serialized 
    /// to and from the corresponding JSON text.
    /// 
    /// The types supported are:
    ///
    /// Object			    (can contain properties of any supported type)
    /// List			    (typed array/list/vector of any supported type),
    /// null            
    /// Time 			    (datetime)
    /// Boolean
    /// String 			    (variable length Unicode string),
    /// Integer 		    (variable length up to 64 bit, signed)
    /// Unsigned Integer	(variable length up to 64 bit, unsigned)
    /// Decimal			    (base-10 floating point up to 64 bit),
    /// Float			    (base-2 floating point up to 64 bit)
    /// </summary>
    /// <remarks>
    /// The current implementation has a few shortcommings. Currently Json only supports arrays of objects.
    /// Also, all objects in the array must use the same Schema.
    /// 
    /// In the release version of Starcounter, Obj objects trees will be optimized for storage in "blobs" rather than on
    /// the garbage collected heap. This is such that stateful sessions can employ them without causing unnecessary system
    /// stress.
    ///
    /// A Json object can be data bound to a database object such as its bound properties
    /// merely reflect the values of the database objects.
    /// </remarks>
    public partial class Json : StarcounterBase {

        /// <summary>
        /// Base classes to be derived by Json-by-example classes.
        /// </summary>
        public static class JsonByExample {
            /// <summary>
            /// Used by to support inheritance when using Json-by-example compiler
            /// </summary>
            public class Schema : Starcounter.Templates.TObject {
            }

            /// <summary>
            /// Used by to support inheritance when using Json-by-example compiler
            /// </summary>
			/// <typeparam name="SchemaType">The schema for the Json.</typeparam>
            /// <typeparam name="JsonType">The Json instance type described by this schema</typeparam>
            public class Metadata<SchemaType, JsonType> : Starcounter.Templates.ObjMetadata<SchemaType, JsonType>
                where SchemaType : Starcounter.Templates.TObject
                where JsonType : Json {

                public Metadata(JsonType app, SchemaType template) : base(app, template) { }
            }
        }

        /// <summary>
        /// Static constructor to automatically initialize XSON.
        /// </summary>
        static Json() {
            // TODO! Whay is this and why is it needed?
            HelperFunctions.LoadNonGACDependencies();
            //    XSON.CodeGeneration.Initializer.InitializeXSON();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obj" /> class.
        /// </summary>
        public Json()
            : base() {
            _cacheIndexInArr = -1;
            _transaction = null;
            //LogChanges = false;
            if (_Template == null) {
                Template = GetDefaultTemplate();
            }
        }

        /// <summary>
        /// Json objects can be stored on the server between requests as session data.
        /// </summary>
        public Session Session {
            get {
                if (_Session == null && Parent != null) {
                    return Parent.Session;
                }
                return _Session;
            }

            set {
                _Session = value;
                _Session.Data = this;
            }
        }

        /// <summary>
        /// The schema element of this app instance
        /// </summary>
        /// <value>The template.</value>
        /// <exception cref="System.Exception">Template is already set for App. Cannot change template once it is set</exception>
        public Template Template {
            set {
                //if (_Template != null) {
                //    throw new Exception("Template is already set for App. Cannot change template once it is set");
                //}
                _Template = (TContainer)value;

                if (_Template is TObject && ((TObject)_Template).IsDynamic) {
                    TObject t = (TObject)_Template;
                    if (t.SingleInstance != null && t.SingleInstance != this) {
                        throw new Exception(String.Format("You cannot assign a Template ({0}) for a dynamic Json object (i.e. an Expando like object) to a new Json object ({0})", value, this));
                    }
                    ((TObject)_Template).SingleInstance = (Json)this;
                }
                else if (_Template == null) {
                    return;
                }
                else {
                    _Template.Sealed = true;
                }
#if QUICKTUPLE
                _InitializeValues();
#endif
            }
            get {
                return _Template;
            }
        }

        ///// <summary>
        ///// Called when [set parent].
        ///// </summary>
        ///// <param name="child">The child.</param>
        //internal virtual void OnSetParent(Container child) {
        //    //child._parent = this;
        //}

        public virtual void ChildArrayHasAddedAnElement(TObjArr property, int elementIndex) {
        }

        public virtual void ChildArrayHasRemovedAnElement(TObjArr property, int elementIndex) {
        }

        public virtual void ChildArrayHasReplacedAnElement(TObjArr property, int elementIndex) {
        }

        /// <summary>
        /// Called when a Obj or Arr property value has been removed from its parent.
        /// </summary>
        /// <param name="property">The name of the property</param>
        /// <param name="child">The old value of the property</param>
        private void HasRemovedChild(Json child) {
            // This Obj or Arr has been removed from its parent and should be deleted from the
            // URI cache.
            //
            // TheCache.RemoveEntry( child );
            //
        }

        /// <summary>
        /// Returns True if current Obj is within the given tree.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public Boolean HasThisRoot(Json treeRoot) {
            Json r = this;
            while (r.Parent != null)
                r = r.Parent;
            Json root = (Json)r;

            if (treeRoot == root)
                return true;

            return false;
        }

        protected virtual Template GetDefaultTemplate() {
            return null;
        }

        /// <summary>
        /// Refreshes the specified property of this Obj.
        /// </summary>
        /// <param name="property">The property</param>
        public void Refresh(Template property) {
			if (property is TObjArr) {
				TObjArr tarr = (TObjArr)property;
				if (tarr.UseBinding(this)) {
					var jsonArr = tarr.UnboundGetter(this);
					jsonArr.CheckBoundArray(tarr.BoundGetter(this));
				}
			} else if (property is TObject) {
				var at = (TObject)property;
				if (at.UseBinding(this)) {
					CheckBoundObject(at.BoundGetter(this));
				}
			} else {
				TValue p = property as TValue;
				if (p != null) {
					HasChanged(p);
				}
			}
        }

        /// <summary>
        /// Is overridden by Puppet to log changes.
        /// </summary>
        /// <remarks>
        /// The puppet needs to log all changes as they will need to be sent to the client (the client keeps a mirrored view model).
        /// See MVC/MVVM (TODO! REF!). See Puppets (TODO REF)
        /// </remarks>
        /// <param name="property">The property that has changed in this Obj</param>
        protected virtual void HasChanged(TValue property) {
            //throw new Exception();
            //var s = Session;
            //if (s!=null)
            //    Session.UpdateValue(this, property);
        }

        /// <summary>
        /// Here you can set properties for each property in this Obj (such as Editable, Visible and Enabled).
        /// The changes only affect this instance.
        /// If you which to change properties for the template, use the Template property instead.
        /// </summary>
        /// <value>The metadata.</value>
        /// <remarks>It is much less expensive to set this kind of metadata for the
        /// entire template (for example to mark a property for all Obj instances as Editable).</remarks>
        public ObjMetadata<TObject, Json> Metadata {
            get {
                return _Metadata;
            }
        }

        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        /// <value>The parent.</value>
        /// <exception cref="System.Exception">Cannot change parent in Apps</exception>
        public Json Parent {
            get {
                return _parent;
            }
            set {
                if (_parent != null && _parent != value) {
                    throw new Exception("Cannot change parent of objects in Typed JSON trees");
                }
                SetParent(value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        internal void SetParent(Json value) {
            if (value == null) {
                if (_parent != null) {
                    _parent.HasRemovedChild(this);
                }
            }
            _parent = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsArray {
            get {
                if (Template == null) {
                    return false;
                }
                return Template is TObjArr;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsPrimitive {
            get {
                if (Template == null) {
                    return false;
                }
                return Template.IsPrimitive;
            }
        }

		public object this[int index] {
			get {
				if (this.IsArray) {
					// TODO: 
					// Should be delegate on property as well.
					return _GetAt(index);
				} else {
					TValue property = (TValue)((TObject)Template).Properties[index];
					return property.GetValueAsObject(this);
				}
			}
			set {
				if (this.IsArray) {
					// TODO: 
					// Should be delegate on property as well.
					_SetAt(index, value);
					if (ArrayAddsAndDeletes == null)
						ArrayAddsAndDeletes = new List<Change>();
					ArrayAddsAndDeletes.Add(Change.Update(Parent, (TValue)Template, index));
					MarkAsReplaced(index);
					Dirtyfy();
				} else {
					TValue property = (TValue)((TObject)Template).Properties[index];
					property.SetValueAsObject(this, value);
				}
			}
		}

		public object this[string key] {
			get {
				var template = (TObject)this.Template;
				var prop = template.Properties[key];
				if (prop == null) {
					return null;
				}
				return this[prop.TemplateIndex];
			}
			set {
				if (Template == null)
					CreateDynamicTemplate();

				var template = (TObject)this.Template;
				var prop = template.Properties[key];
				if (prop == null) {
					Type type;
					if (value == null) {
						type = typeof(Json);
					} else {
						type = value.GetType();
					}
					template.OnSetUndefinedProperty(key, type);
					this[key] = value;
					return;
				}
				this[prop.TemplateIndex] = value;
			}
		}

		internal void CheckBoundObject(object boundValue) {
			if (!CompareDataObjects(boundValue, Data))
				AttachData(boundValue);
		}

		internal void CheckBoundArray(IEnumerable boundValue) {
			Json oldJson;
			Json newJson;
			int index = 0;
			TObjArr tArr = Template as TObjArr;
			bool hasChanged = false;

			foreach (object value in boundValue) {
				if (_list.Count <= index) {
					newJson = (Json)tArr.ElementType.CreateInstance();
					Add(newJson);
					newJson.Data = value;
					hasChanged = true;
				} else {
					oldJson = (Json)_list[index];
					if (!CompareDataObjects(oldJson.Data, value)) {
						oldJson.Data = value;
						if (ArrayAddsAndDeletes == null)
							ArrayAddsAndDeletes = new List<Change>();
						ArrayAddsAndDeletes.Add(Change.Update((Json)this.Parent, tArr, index));
						hasChanged = true;
					}
				}
				index++;
			}

			for (int i = _list.Count - 1; i >= index; i--) {
				RemoveAt(i);
				hasChanged = true;
			}

			if (hasChanged)
				this.Parent.HasChanged(tArr);
		}

		private bool CompareDataObjects(object obj1, object obj2) {
			if (obj1 == null && obj2 == null)
				return true;

			if (obj1 == null && obj2 != null)
				return false;

			if (obj1 != null && obj2 == null)
				return false;

			var bind1 = obj1 as IBindable;
			var bind2 = obj2 as IBindable;

			if (bind1 == null || bind2 == null)
				return obj1.Equals(obj2);

			return (bind1.Identity == bind2.Identity);
		}
    }
}
