using System;
using System.Dynamic;using System.Linq.Expressions;using System.Reflection;using System.Linq;using Starcounter.Templates;using System.Diagnostics;
using System.Collections;
using Starcounter.Internal.XSON;namespace Starcounter {        public partial class Json : IDynamicMetaObjectProvider {        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(            Expression parameter) {            return new DynamicPropertyMetaObject(parameter, this);        }
        /// <summary>
        /// Provides late bound (dynamic) access to Json properties defined
        /// in the Template of the Json object. Also supports data binding 
        /// using the Json.Data property.
        /// </summary>        private class DynamicPropertyMetaObject : DynamicMetaObject {            internal DynamicPropertyMetaObject(                System.Linq.Expressions.Expression parameter,                Json value)                : base(parameter, BindingRestrictions.Empty, value) {            }            /// <summary>
            /// Getter implementation. See DynamicPropertyMetaObject.
            /// </summary>
            /// <param name="binder"></param>
            /// <returns></returns>            public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
                var app = (Json)Value;
                if (app.IsArray) {
                    return base.BindGetMember(binder);
                }
                else {
                    return BindGetMemberForJsonObject(app, (TObject)app.Template, binder);
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="template"></param>
            /// <param name="binder"></param>
            /// <returns></returns>
            private DynamicMetaObject BindGetMemberForJsonObject( Json app, TObject template, GetMemberBinder binder ) {

                MemberInfo pi = ReflectionHelper.FindPropertyOrField(RuntimeType, binder.Name);                if (pi != null)                    return base.BindGetMember(binder);

                TValue templ = (TValue)(template.Properties[binder.Name]);

                if (templ == null) {
                    if (app.Data != null) {
                        // Attempt to create the missing property if this is a dynamic template obj.
                        Type dataType = app.Data.GetType();
                        MemberInfo[] mis = dataType.GetMember(binder.Name);
                        Type proptype = null;
                        foreach (var mi in mis) {
                            if (mi is PropertyInfo) {
                                proptype = ((PropertyInfo)mi).PropertyType;
                                break;
                            }
                            if (mi is FieldInfo) {
                                proptype = ((FieldInfo)mi).FieldType;
                                break;
                            }
                        }
                        if (proptype != null) {
                            template.OnSetUndefinedProperty(binder.Name, proptype );
                            // Check if it is there now
                            templ = (TValue)(template.Properties[binder.Name]);
                        }
                        if (templ == null) {
                            throw new Exception(String.Format("Neither the Json object or the bound Data object (an instance of {0}) contains the property {1}", app.Data.GetType().Name, binder.Name));
                        }
                    }
                }
//                Column c = Column.LookupColumn(binder.Name);//                Debug.WriteLine("DynamicEntity Binding Get " + binder.Name);                MethodInfo method;                if (templ is TObjArr) {                    // The GetMethod does not deal with generic signatures causing an exception                    // for the ambiguous methods GetValue<T>( TObjArr x ) and GetValue( TObjArr x).                    // We need to call GetMethods instead (probably slower).                    // See http://stackoverflow.com/questions/11566613/how-do-i-distinguish-between-generic-and-non-generic-signatures-using-getmethod

					var t = templ.GetType();
					if (t.IsGenericType) {
						t = t.GetGenericArguments()[0];
						method = FindGetMember("TArray`1", t);
					} else {
						method = FindGetMember("TObjArr");
					}                } else if (templ is TObject){					// We have Get methods for both TObject and TValue and since we cannot specify return type					// when searching for methods, we have to look in all methods for the correct one.					// We will get an ambiguous match otherwise.
					method = FindGetMember("TObject");                }else {
                    if (templ == null) {
                        // There is no property with this name, use default late binding mechanism
                        return base.BindGetMember(binder);
                    }                    method = LimitType.GetMethod("Get", new Type[] { templ.GetType() });                }                /* (DynamicDurableProxy)this.Get(); */                Expression call = Expression.Call(Expression.Convert(this.Expression, this.LimitType), method, Expression.Constant(templ) );                // Expression wrapped = Expression.Block(call); // , Expression.New(typeof(object)));                Expression wrapped = Expression.Convert( call, binder.ReturnType );

                var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);
                restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(Expression, app));                return new DynamicMetaObject(wrapped, restrictions);
            }

			/// <summary>
			/// 
			/// </summary>
			/// <param name="parameterTypeName"></param>
			/// <param name="genericArgType"></param>
			/// <returns></returns>
			private MethodInfo FindGetMember(string parameterTypeName, Type genericArgType = null) {
				var mis = LimitType.GetMethods().Where(m => {
					if (m.Name.Equals("Get")) {
						var paris = m.GetParameters();
						if (paris.Length == 1) {
							var pari = paris[0];
							var found = (pari.ParameterType.Name.Equals(parameterTypeName));

							if (m.IsGenericMethod && genericArgType == null)
								found = false;
							return found;
						}
					}
					return false;
				});

				var mInfo = mis.First();
				if (genericArgType != null)
					mInfo = mInfo.MakeGenericMethod(genericArgType);
				return mInfo;
			}            /// <summary>
            /// Getter implementation. See DynamicPropertyMetaObject.
            /// </summary>
            /// <param name="binder"></param>
            /// <param name="value"></param>
            /// <returns></returns>            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
                var app = (Json)Value;
                if (app.IsArray) {
                    return base.BindSetMember(binder, value);
                }
                else {
                    return BindSetMemberForJsonObject(binder, value, app);
                }                
            }


            /// <summary>
            /// 
            /// </summary>
            /// <param name="binder"></param>
            /// <param name="value"></param>
            /// <param name="app"></param>
            /// <returns></returns>
            private DynamicMetaObject BindSetMemberForJsonObject( SetMemberBinder binder, DynamicMetaObject value, Json app ) {
                // Special handling of properties declared in the baseclass (Obj) and overriden
                // using 'new', like a generic override of Data returning the correct type.

//                if (binder.Name == "Data") {
//                    return base.BindSetMember(binder, value);
//                }

                var ot = (TObject)app.Template;
                if (ot == null) {
                    app.CreateDynamicTemplate();
                    ot = (TObject)app.Template; 
//                    app.Template = ot = new TDynamicObj(); // Make this an expando object like Obj
                }                MemberInfo pi = ReflectionHelper.FindPropertyOrField(this.RuntimeType,binder.Name);                if (pi != null)                    return base.BindSetMember(binder, value);
                TValue templ = (TValue)(ot.Properties[binder.Name]);                if (templ == null) {
                    ot.OnSetUndefinedProperty(binder.Name, value.LimitType);
                    return this.BindSetMember(binder, value); // Try again
//                    throw new Exception(String.Format("No Set(uint,uint,{0}) method found when binding.", value.LimitType.Name));
                }

                var propertyType = templ.GetType();
                MethodInfo method = LimitType.GetMethod("Set", new Type[] { propertyType, value.LimitType });

                //            Expression call = Expression.Call( Expression.Convert(this.Expression, this.LimitType), method, Expression.Constant(columnId), Expression.Constant(columnIndex), Expression.Convert(value.Expression, value.LimitType) );

                Expression wrapped;

                var @this = Expression.Convert(
                            this.Expression,
                            this.LimitType);

                if (templ is TObjArr) {
                    Expression call = Expression.Call(
                        @this, 
                        method,
                        Expression.Constant(templ),
                        Expression.Convert(value.Expression,typeof(IEnumerable))
                    );
                    wrapped = Expression.Block(call, Expression.Convert(value.Expression, typeof(object)));
                }
                else {
                    Expression call = Expression.Call(@this, method, Expression.Constant(templ), Expression.Convert(value.Expression, templ.InstanceType));
                    wrapped = Expression.Block(call, Expression.Convert(value.Expression, typeof(object)));
                }

                var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);
                restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(Expression, app));
                return new DynamicMetaObject(wrapped, restrictions);
            }        }    }}