﻿using System;
using System.Dynamic;
using System.Collections;
using Starcounter.Internal.XSON;

        /// Provides late bound (dynamic) access to Json properties defined
        /// in the Template of the Json object. Also supports data binding 
        /// using the Json.Data property.
        /// </summary>
            /// Getter implementation. See DynamicPropertyMetaObject.
            /// </summary>
            /// <param name="binder"></param>
            /// <returns></returns>
                // Special handling of properties declared in the baseclass (Obj) and overriden
                // using 'new', like a generic override of Data returning the correct type.
                
                //if (binder.Name == "Data") {
                //    return base.BindGetMember(binder);
                //}

                MemberInfo pi = ReflectionHelper.FindPropertyOrField(RuntimeType, binder.Name);

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
                            app.Template.OnSetUndefinedProperty(binder.Name, proptype );
                            // Check if it is there now
                            templ = (TValue)(app.Template.Properties[binder.Name]);
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
                // Special handling of properties declared in the baseclass (Obj) and overriden
                // using 'new', like a generic override of Data returning the correct type.

//                if (binder.Name == "Data") {
//                    return base.BindSetMember(binder, value);
//                }
                if (ot == null) {
                    app.CreateDynamicTemplate();
                    ot = app.Template; 
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