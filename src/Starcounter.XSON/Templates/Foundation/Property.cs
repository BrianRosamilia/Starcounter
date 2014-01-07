// ***********************************************************************
// <copyright file="ValueTemplate.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using Starcounter.XSON;

namespace Starcounter.Templates {
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">The primitive system type of this property.</typeparam>
    public abstract class Property<T> : TValue {
		public Action<Json, T> Setter;
		public Func<Json, T> Getter;
		internal Action<Json, T> BoundSetter;
		internal Func<Json, T> BoundGetter;
		internal Action<Json, T> UnboundSetter;
		internal Func<Json, T>  UnboundGetter;

        internal Func<Json, Property<T>, T, Input<T>> CustomInputEventCreator = null;
        internal List<Action<Json, Input<T>>> CustomInputHandlers = new List<Action<Json, Input<T>>>();

		public Property() {
			Getter = BoundOrUnboundGet;
			Setter = BoundOrUnboundSet;
		}

		private T BoundOrUnboundGet(Json parent) {
			if (UseBinding(parent))
				return BoundGetter(parent);
			return UnboundGetter(parent);
		}

		private void BoundOrUnboundSet(Json parent, T value) {
			if (UseBinding(parent)) {
				if (BoundSetter != null)
					BoundSetter(parent, value);
			} else
				UnboundSetter(parent, value);

			if (parent.HasBeenSent)
				parent.MarkAsReplaced(TemplateIndex);

			parent._CallHasChanged(this);
		}

		/// <summary>
		/// Sets the getter and setter delegates for unbound values to the submitted delegates.
		/// </summary>
		/// <param name="getter"></param>
		/// <param name="setter"></param>
		public void SetCustomAccessors(Func<Json, T> getter, 
									   Action<Json, T> setter,
									   bool overwriteExisting = true) {
			bool overwrite = (overwriteExisting || !hasCustomAccessors);

			if (BindingStrategy == BindingStrategy.Unbound) {
				if (overwrite || Getter == null)
					Getter = getter;
				if (overwrite || Setter == null)
					Setter = setter;
			}

			if (overwrite || UnboundGetter == null) {
				UnboundGetter = getter;
#if DEBUG
				DebugUnboundGetter = "<custom>";
#endif
			}

			if (overwrite || UnboundSetter == null) {
				UnboundSetter = setter;
#if DEBUG
				DebugUnboundSetter = "<custom>";
#endif
			}

			hasCustomAccessors = true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		internal override void SetDefaultValue(Json parent) {
			UnboundSetter(parent, (T)CreateInstance(parent));
		}

		/// <summary>
		/// 
		/// </summary>
		internal override void InvalidateBoundGetterAndSetter() {
			BoundGetter = null;
			BoundSetter = null;
			base.InvalidateBoundGetterAndSetter();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data"></param>
		internal override bool GenerateBoundGetterAndSetter(Json parent) {
			TemplateDelegateGenerator.GenerateBoundDelegates<T>(this, parent);
			return (BoundGetter != null);
		}

		/// <summary>
		/// 
		/// </summary>
		internal override void GenerateUnboundGetterAndSetter() {
			if (UnboundGetter == null)
				TemplateDelegateGenerator.GenerateUnboundDelegates<T>(this, false);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		internal override object GetUnboundValueAsObject(Json parent) {
			return UnboundGetter(parent);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		internal override object GetValueAsObject(Json parent) {
			return Getter(parent);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		internal override void SetValueAsObject(Json parent, object value) {
			Setter(parent, (T)value);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		internal override void Checkpoint(Json parent) {
			if (UseBinding(parent))
				UnboundSetter(parent, BoundGetter(parent));
			base.Checkpoint(parent);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="addToChangeLog"></param>
		internal override void CheckAndSetBoundValue(Json parent, bool addToChangeLog) {
			if (UseBinding(parent)) {
				T boundValue = BoundGetter(parent);
				T oldValue = UnboundGetter(parent);

				// Since all values except string are valuetypes (and cannot be null),
				// the default implementation does no nullchecks. This method is overriden
				// in TString where we check for null as well.
				if (!boundValue.Equals(oldValue)) {
					UnboundSetter(parent, boundValue);
					if (addToChangeLog)
						parent.Session.UpdateValue(parent, this);
				}
			}
		}

		internal override string ValueToJsonString(Json parent) {
		    return Getter(parent).ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="from"></param>
		internal override void CopyValueDelegates(Template toTemplate) {
			var p = toTemplate as Property<T>;
			if (p != null) {
				p.UnboundGetter = UnboundGetter;
				p.UnboundSetter = UnboundSetter;
				p.hasCustomAccessors = hasCustomAccessors;

#if DEBUG
				p.DebugUnboundGetter = DebugUnboundGetter;
				p.DebugUnboundSetter = DebugUnboundSetter;
#endif
			}
		}

        /// <summary>
        /// Adds an inputhandler to this property.
        /// </summary>
        /// <param name="createInputEvent"></param>
        /// <param name="handler"></param>
        public void AddHandler(
            Func<Json, Property<T>, T, Input<T>> createInputEvent = null,
            Action<Json, Input<T>> handler = null) {
            this.CustomInputEventCreator = createInputEvent;
            this.CustomInputHandlers.Add(handler);
        }
	}

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class PrimitiveProperty<T> : Property<T> {
		public override bool IsPrimitive {
			get { return true; }
		}
	}
}
