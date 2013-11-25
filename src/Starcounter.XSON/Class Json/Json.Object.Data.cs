// ***********************************************************************
// <copyright file="Obj.IBindable.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using Starcounter.Templates;
using Starcounter.Advanced;
using System.Collections;
using Starcounter.Internal.XSON;

namespace Starcounter {
    partial class Json {


        /// <summary>
        /// 
        /// </summary>
        internal IBindable DataAsBindable {
            get {
                return (IBindable)_data;
            }
        }

        /// <summary>
        /// Gets or sets the bound (underlying) data object (often a database Entity object). This enables
        /// the Obj to reflect the values of the bound object. The values are matched by property names by default.
        /// When you declare an Obj using generics, be sure to specify the type of the bound object in the class declaration.
        /// </summary>
        /// <value>The bound data object (often a database Entity)</value>
        public object Data {
            get {
                return _data;
            }
            set {
                if (IsArray) {
                    _PendingEnumeration = true;
                    _data = (IEnumerable)value;
                    this.Array_InitializeAfterImplicitConversion((Json)this.Parent, (TObjArr)this.Template);
                }
                else {
                    if (Template == null) {
                        (this as Json).CreateDynamicTemplate(); // If there is no template, we'll create a template
                    }
                    InternalSetData(value, (TObject)Template, false);
                }
            }
        }

        internal void AttachData(object data) {
            InternalSetData(data, (TObject)Template, true);
        }


		///// <summary>
		///// Gets the bound value from the dataobject.
		///// </summary>
		///// <typeparam name="TVal"></typeparam>
		///// <param name="template"></param>
		///// <returns></returns>
		//internal TVal GetBound<TVal>(Property<TVal> template) {
		//	if (template.UseBinding(this))
		//		return template.BoundGetter(this);
		//	return default(TVal);
		//}

		///// <summary>
		///// Sets the value to the dataobject.
		///// </summary>
		///// <param name="template"></param>
		///// <param name="value"></param>
		//internal void SetBound<TVal>(Property<TVal> template, TVal value) {
		//	if (template.UseBinding(this))
		//		template.BoundSetter(this, value);
		//}

		//internal object GetBound(TValue template) {
		//	IBindable data = DataAsBindable;
		//	var thisj = AssertIsObject();
		//	if (data == null)
		//		return null;

		//	return template.GetBoundValueAsObject(thisj);
		//}

		//internal void SetBound(TValue template, object value) {
		//	var thisj = AssertIsObject();
		//	IBindable data = DataAsBindable;
		//	if (data == null)
		//		return;

		//	template.SetBoundValueAsObject(thisj, value);
		//}

        /// <summary>
        /// For public API functions that does not operate on templates, this
        /// method should be used instead of a simple type cast to provide a 
        /// better error message for the developer.
        /// </summary>
        /// <returns>This as a Json object</returns>
        private Json AssertIsObject() {
            if (IsArray) {
                throw new Exception("You cannot use named properties on array Json objects");
            }
            return this as Json;
        }

		///// <summary>
		///// Gets the bound value from the dataobject.
		///// </summary>
		///// <remarks>
		///// This method assumes that the cached binding on the template 
		///// is correct and will not verify it.
		///// </remarks>
		///// <param name="template"></param>
		///// <returns></returns>
		//internal IEnumerable GetBound(TObjArr template) {
		//	IBindable data = DataAsBindable;
		//	if (data == null)
		//		return default(Rows<object>);

		//	return ((DataValueBinding<IEnumerable>)template.dataBinding).Get(data);
		//}

		///// <summary>
		///// 
		///// </summary>
		///// <param name="template"></param>
		///// <param name="value"></param>
		//internal void SetBound(TObjArr template, IEnumerable value) {
		//	IBindable data = DataAsBindable;
		//	if (data == null)
		//		return;
		//	var binding = (DataValueBinding<IEnumerable>)template.dataBinding;
		//	if (binding.HasSetBinding())
		//		binding.Set(data, value);
		//}

		///// <summary>
		///// Gets the bound value from the dataobject.
		///// </summary>
		///// <remarks>
		///// This method assumes that the cached binding on the template 
		///// is correct and will not verify it.
		///// </remarks>
		///// <param name="template"></param>
		///// <returns></returns>
		//internal IBindable GetBound(TObject template) {
		//	IBindable data = DataAsBindable;
		//	if (data == null)
		//		return null;
		//	return ((DataValueBinding<IBindable>)template.dataBinding).Get(data);
		//}

		///// <summary>
		///// Sets the value to the dataobject.
		///// </summary>
		///// <remarks>
		///// This method assumes that the cached binding on the template 
		///// is correct and will not verify it.
		///// </remarks>
		///// <param name="template"></param>
		///// <param name="value"></param>
		//internal void SetBound(TObject template, IBindable value) {
		//	IBindable data = DataAsBindable;
		//	if (data == null)
		//		return;
		//	((DataValueBinding<IBindable>)template.dataBinding).Set(data, value);
		//}

        /// <summary>
        /// Sets the underlying data object and refreshes all bound values.
        /// This function does not check for a valid transaction as the 
        /// public Data-property does.
        /// </summary>
        /// <param name="data">The bound data object (usually an Entity)</param>
        protected virtual void InternalSetData(object data, TObject template, bool readOperation ) {
            this._data = data;

			if (template != null) {
				if (template.BindingStrategy != BindingStrategy.Unbound) {
					var parent = ((Json)this.Parent);
					if (!readOperation && parent != null 
						&& template.UseBinding(parent) 
						&& template.BoundSetter != null) {
						template.BoundSetter(parent, data);
					}
				}

				InitBoundArrays(template);
			}
            OnData();
        }

        /// <summary>
        /// Initializes bound arrays when a new dataobject is set.
        /// </summary>
        private void InitBoundArrays(TObject template) {
            TObjArr child;
            for (Int32 i = 0; i < template.Properties.Count; i++) {
                child = template.Properties[i] as TObjArr;
                if (child != null && child.BindingStrategy != BindingStrategy.Unbound) {
					if (_data != null) {
						if (child.UseBinding(this))
							Refresh(child);
					} else {
                        var thisj = AssertIsObject();
						var arr = (Json)child.Getter(thisj);
						arr.Clear();
					}
                }
            }
        }

        /// <summary>
        /// Called after the Data property is set.
        /// </summary>
        protected virtual void OnData() {
        }
    }
}