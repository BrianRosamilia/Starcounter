using System;

namespace Starcounter.Templates {
	/// <summary>
	/// 
	/// </summary>
	public abstract class TValue : Template {
		private BindingStrategy strategy = BindingStrategy.UseParent;
		private string bind;
		private bool hasBackingField;
		internal Type dataTypeForBinding;
		internal bool isVerifiedUnbound;

		/// <summary>
		/// Gets a value indicating whether this instance has instance value on client.
		/// </summary>
		/// <value><
		/// c>true</c> if this instance has instance value on client; otherwise, <c>false</c>.                         
		/// </value>
		public override bool HasInstanceValueOnClient {
			get { return true; }
		}

		/// <summary>
		/// 
		/// </summary>
		public bool HasBackingField {
			get { return hasBackingField; }
			set { hasBackingField = value; }
		}

		/// <summary>
		/// Gets or sets the name of the property this template is bound to.
		/// </summary>
		/// <value>The name of the property to bind.</value>
		public string Bind {
			get {
				if (bind == null && BindingStrategy != BindingStrategy.Unbound) {
					return this.PropertyName;
				}
				return bind;
			}
			set {
				if (hasBackingField) {
					throw new Exception("TODO! Not allowed when backing field is used.");
				}

				bind = value;
				var b = !string.IsNullOrEmpty(bind);
				if (b) {
					strategy = BindingStrategy.Bound;
				} else {
					strategy = BindingStrategy.Unbound;
				}
				InvalidateBoundGetterAndSetter();
			}
		}

		/// <summary>
		/// Gets a value indicating how this template should handle binding
		/// to a dataobject.
		/// </summary>
		/// <value></value>
		public BindingStrategy BindingStrategy {
			get {
				if (strategy == Templates.BindingStrategy.UseParent) {
					var parent = Parent;
					if (parent == null)
						return BindingStrategy.Unbound;

					while (!(parent is TObject))
						parent = Parent;
					return ((TObject)parent).BindChildren;
				}

				return strategy;
			}
			set {
				if (hasBackingField) {
					throw new Exception("TODO! Not allowed when backing field is used.");
				}

				strategy = value;

				// After we set the value we retrieve it again just to get the correct
				// binding to use in case we read the value from the parent.
				var real = BindingStrategy;
				if (real == Templates.BindingStrategy.Unbound)
					bind = null;
				else if (bind == null)
					bind = PropertyName;
				InvalidateBoundGetterAndSetter();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="rawValue"></param>
		/// TODO!
		/// Should be changed to accept IntPtr to rawValue using an int size parameter. This way is not fast enough.
		public abstract void ProcessInput(Json obj, Byte[] rawValue);

		/// <summary>
		/// 
		/// </summary>
		internal abstract bool GenerateBoundGetterAndSetter(Json parent);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="json"></param>
		internal abstract void GenerateUnboundGetterAndSetter();

		/// <summary>
		/// 
		/// </summary>
		internal abstract void CheckAndSetBoundValue(Json parent, bool addToChangeLog);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		internal abstract object GetValueAsObject(Json parent);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		internal abstract void SetValueAsObject(Json parent, object value);

		/// <summary>
		/// 
		/// </summary>
		internal virtual void InvalidateBoundGetterAndSetter() {
			isVerifiedUnbound = false;
			dataTypeForBinding = null;
		}

		/// <summary>
		/// 
		/// </summary>
		internal virtual void OnPropertySet(Json parent) {
			if (parent.HasBeenSent)
				parent.MarkAsReplaced(TemplateIndex);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="json"></param>
		internal virtual void Checkpoint(Json parent) {
			parent.CheckpointAt(TemplateIndex);
		}

		internal virtual string ValueToJsonString(Json parent) {
			return "";
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="toTemplate"></param>
		public override void CopyTo(Template toTemplate) {
			base.CopyTo(toTemplate);
			((TValue)toTemplate).Bind = Bind;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal bool UseBinding(Json parent) {
			BindingStrategy strategy;
			object data;

			data = parent.Data;
			strategy = BindingStrategy;
			if (data == null || strategy == BindingStrategy.Unbound)
				return false;

			if (dataTypeForBinding != null && VerifyBinding(data.GetType(), false))
				return !isVerifiedUnbound;

			return GenerateBoundGetterAndSetter(parent);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dataType"></param>
		/// <returns></returns>
		private bool VerifyBinding(Type dataType, bool throwExceptionOnFail) {
			if (dataType.Equals(dataTypeForBinding) || dataType.IsSubclassOf(dataTypeForBinding))
				return true;

			if (throwExceptionOnFail)
				throw new Exception("TODO!");
			//                throw new Exception(string.Format(warning, DataBindingFactory.GetParentClassName(this) + "." + this.TemplateName));
			//			logSource.LogWarning(string.Format(warning, GetParentClassName(template) + "." + template.TemplateName));          
			return false;
		}
	}
}
