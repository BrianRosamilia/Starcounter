﻿using System;
using System.Dynamic;
using System.Collections;
using Starcounter.Internal.XSON;
    public partial class Json : IDynamicMetaObjectProvider {

        /// Provides late bound (dynamic) access to Json properties defined
        /// in the Template of the Json object. Also supports data binding 
        /// using the Json.Data property.
        /// </summary>
            /// Getter implementation. See DynamicPropertyMetaObject.
            /// </summary>
            /// <param name="binder"></param>
            /// <returns></returns>
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

                MemberInfo pi = ReflectionHelper.FindPropertyOrField(RuntimeType, binder.Name);

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

                            var paris = m.GetParameters();
                                var found = (pari.ParameterType.Name.Equals("TObjArr")) && !m.IsGenericMethod;
                    if (templ == null) {
                        // There is no property with this name, use default late binding mechanism
                        return base.BindGetMember(binder);
                    }
            /// Getter implementation. See DynamicPropertyMetaObject.
            /// </summary>
            /// <param name="binder"></param>
            /// <param name="value"></param>
            /// <returns></returns>
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
                }

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
            }