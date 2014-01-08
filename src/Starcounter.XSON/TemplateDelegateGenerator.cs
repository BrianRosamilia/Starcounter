
using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Starcounter.Internal;
using Starcounter.Templates;

namespace Starcounter.XSON {
	internal static class TemplateDelegateGenerator {
		private static MethodInfo dateTimeToStringInfo = typeof(DateTime).GetMethod("ToString", new Type[] { typeof(string) });
		private static MethodInfo dateTimeParseInfo = typeof(DateTime).GetMethod("Parse", new Type[] { typeof(string) });
		private static MethodInfo valueListInfo = typeof(Json).GetMethod("get_list", BindingFlags.NonPublic | BindingFlags.Instance);
		private static MethodInfo listGetMethodInfo = typeof(IList).GetMethod("get_Item");
		private static MethodInfo listSetMethodInfo = typeof(IList).GetMethod("set_Item");
		private static MethodInfo jsonGetDataInfo = typeof(Json).GetMethod("get_Data");
		private static MethodInfo propertyUseBindingInfo = typeof(TValue).GetMethod("UseBinding");
		
		private static string propNotCompatible = "Incompatible types for binding. Json property '{0}.{1}' ({2}), data property '{3}.{4}' ({5}).";

#if DEBUG
		private static MethodInfo debugView = typeof(Expression).GetMethod("get_DebugView", BindingFlags.Instance | BindingFlags.NonPublic);
#endif

		/// <summary>
		/// Generates an expression-tree for getting and setting values on a json object, either bound 
		/// or unbound. The expression will be compiled to a delegate and set on the property. 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="property"></param>
		/// <param name="json"></param>
		internal static void GenerateBoundOrUnboundDelegates<T>(Property<T> property) {
			throw new NotImplementedException();
//			var getLambda = GenerateBoundOrUnboundGetExpression<T>();
//			var setLambda = GenerateBoundOrUnboundSetExpression<T>();

//			property.Getter = getLambda.Compile();
//			property.Setter = setLambda.Compile();

//#if DEBUG
//			property.DebugGetter = (string)debugView.Invoke(getLambda, new object[0]);
//			property.DebugSetter = (string)debugView.Invoke(setLambda, new object[0]);
//#endif
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting unbound values on a json object. The
		/// expression will be compiled to a delegate and set on the property. If useBackingField is true
		/// the expression will use a backing field instead of accessing a list.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="property"></param>
		/// <param name="directAccess"></param>
		internal static void GenerateUnboundDelegates<T>(Property<T> property, bool useBackingField) {
			if (useBackingField)
				throw new NotImplementedException();

			var getLambda = GenerateUnboundGetExpression<T>(typeof(Json), property.TemplateIndex);
			var setLambda = GenerateUnboundSetExpression<T>(typeof(Json), property.TemplateIndex);

			property.UnboundGetter = getLambda.Compile();
			property.UnboundSetter = setLambda.Compile();

#if DEBUG
			property.DebugUnboundGetter = (string)debugView.Invoke(getLambda, new object[0]);
			property.DebugUnboundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting unbound values on a json object. The
		/// expression will be compiled to a delegate and set on the property. If useBackingField is true
		/// the expression will use a backing field instead of accessing a list.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="directAccess"></param>
		internal static void GenerateUnboundDelegates(TObject property, bool useBackingField) {
			if (useBackingField)
				throw new NotImplementedException();

			var getLambda = GenerateUnboundGetExpression<Json>(typeof(Json), property.TemplateIndex);
			var setLambda = GenerateUnboundSetExpression<Json>(typeof(Json), property.TemplateIndex);

			property.UnboundGetter = getLambda.Compile();
			property.UnboundSetter = setLambda.Compile();

#if DEBUG
			property.DebugUnboundGetter = (string)debugView.Invoke(getLambda, new object[0]);
			property.DebugUnboundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting unbound values on a json object. The
		/// expression will be compiled to a delegate and set on the property. If useBackingField is true
		/// the expression will use a backing field instead of accessing a list.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="directAccess"></param>
		internal static void GenerateUnboundDelegates(TObjArr property, bool useBackingField) {
			if (useBackingField)
				throw new NotImplementedException();

			var getLambda = GenerateUnboundGetExpression<Json>(typeof(Json), property.TemplateIndex);
			var setLambda = GenerateUnboundSetExpression<Json>(typeof(Json), property.TemplateIndex);

			property.UnboundGetter = getLambda.Compile();
			property.UnboundSetter = setLambda.Compile();

#if DEBUG
			property.DebugUnboundGetter = (string)debugView.Invoke(getLambda, new object[0]);
			property.DebugUnboundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting unbound values on a json object. The
		/// expression will be compiled to a delegate and set on the property. If useBackingField is true
		/// the expression will use a backing field instead of accessing a list.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="directAccess"></param>
		internal static void GenerateUnboundDelegates<T>(TArray<T> property, bool useBackingField) where T : Json, new() {
			if (useBackingField)
				throw new NotImplementedException();

			var getLambda = GenerateUnboundGetExpression<Arr<T>>(typeof(Json), property.TemplateIndex);
			var setLambda = GenerateUnboundSetExpression<Arr<T>>(typeof(Json), property.TemplateIndex);

			property.UnboundGetter = getLambda.Compile();
			property.UnboundSetter = setLambda.Compile();

#if DEBUG
			property.DebugUnboundGetter = (string)debugView.Invoke(getLambda, new object[0]);
			property.DebugUnboundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting bound values on a json or dataobject. The
		/// expression will be compiled to a delegate and set on the property.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="property"></param>
		/// <param name="json"></param>
		internal static void GenerateBoundDelegates<T>(Property<T> property, Json json) {
			BindingInfo bInfo;
			bool throwException;
			Expression<Func<Json, T>> getLambda;
			Expression<Action<Json, T>> setLambda = null;
			
			throwException = (property.BindingStrategy == Templates.BindingStrategy.Bound);

            bInfo = DataBindingHelper.SearchForBinding(json, property.Bind, property, throwException);
			if (bInfo.Member != null) {
				getLambda = GenerateBoundGetExpression<T>(bInfo);
				property.BoundGetter = getLambda.Compile();

				if (DataBindingHelper.HasSetter(bInfo.Member)) {
					setLambda = GenerateBoundSetExpression<T>(bInfo);
					property.BoundSetter = setLambda.Compile();
				}

#if DEBUG
				property.DebugBoundGetter = (string)debugView.Invoke(getLambda, new object[0]);

				if (setLambda != null)
					property.DebugBoundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
			} else {
				property.isVerifiedUnbound = true; // Auto binding where property not match.
			}
            property.dataTypeForBinding = bInfo.BoundToType;
            property.isBoundToParent = bInfo.IsBoundToParent;
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting bound values on a json or dataobject. The
		/// expression will be compiled to a delegate and set on the property.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="json"></param>
		internal static void GenerateBoundDelegates(TObject property, Json json) {
			BindingInfo bInfo;
			bool throwException;
			Expression<Func<Json, object>> getLambda;
			Expression<Action<Json, object>> setLambda = null;
			
			throwException = (property.BindingStrategy == Templates.BindingStrategy.Bound);
			bInfo = DataBindingHelper.SearchForBinding(json, property.Bind, property, throwException);
			if (bInfo.Member != null) {
				getLambda = GenerateBoundGetExpression<object>(bInfo);
				property.BoundGetter = getLambda.Compile();

				if (DataBindingHelper.HasSetter(bInfo.Member)) {
					setLambda = GenerateBoundSetExpression<object>(bInfo);
					property.BoundSetter = setLambda.Compile();
				}
#if DEBUG
				property.DebugBoundGetter = (string)debugView.Invoke(getLambda, new object[0]);

				if (setLambda != null)
					property.DebugBoundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
			} else {
                property.isVerifiedUnbound = true; // Auto binding where property not match.
			}
            property.dataTypeForBinding = bInfo.BoundToType;
            property.isBoundToParent = bInfo.IsBoundToParent;
		}

		/// <summary>
		/// Generates an expression-tree for getting and setting bound values on a json or dataobject. The
		/// expression will be compiled to a delegate and set on the property.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="json"></param>
		internal static void GenerateBoundDelegates(TObjArr property, Json json) {
			BindingInfo bInfo;
			bool throwException;
			Expression<Func<Json, IEnumerable>> getLambda;
			Expression<Action<Json, IEnumerable>> setLambda = null;
			object dataObject = json.Data;

			throwException = (property.BindingStrategy == Templates.BindingStrategy.Bound);
			bInfo = DataBindingHelper.SearchForBinding(json, property.Bind, property, throwException);
			if (bInfo.Member != null) {
				getLambda = GenerateBoundGetExpression<IEnumerable>(bInfo);
				property.BoundGetter = getLambda.Compile();

				if (DataBindingHelper.HasSetter(bInfo.Member)) {
					setLambda = GenerateBoundSetExpression<IEnumerable>(bInfo);
					property.BoundSetter = setLambda.Compile();
				}

#if DEBUG
				property.DebugBoundGetter = (string)debugView.Invoke(getLambda, new object[0]);

				if (setLambda != null)
					property.DebugBoundSetter = (string)debugView.Invoke(setLambda, new object[0]);
#endif
			} else {
                property.isVerifiedUnbound = true; // Auto binding where property not match.
			}
            property.dataTypeForBinding = bInfo.BoundToType;
            property.isBoundToParent = bInfo.IsBoundToParent;
		}

		private static Expression<Func<Json, T>> GenerateBoundOrUnboundGetExpression<T>(ParameterExpression property) {
			return null;
		}

		private static Expression<Action<Json, T>> GenerateBoundOrUnboundSetExpression<T>() {
			return null;
		}

		private static Expression<Func<Json, T>> GenerateUnboundGetExpression<T>(Type jsonType, int templateIndex) {
			var jsonParam = Expression.Parameter(typeof(Json));
			var valueParam = Expression.Parameter(typeof(T));
			Expression expr = Expression.Call(jsonParam, valueListInfo);

			expr = Expression.Call(expr, listGetMethodInfo, Expression.Constant(templateIndex));
			expr = Expression.Convert(expr, typeof(T));

			return Expression.Lambda<Func<Json, T>>(expr, jsonParam);
		}

		private static Expression<Action<Json, T>> GenerateUnboundSetExpression<T>(Type jsonType, int templateIndex) {
			var jsonParam = Expression.Parameter(typeof(Json));
			var valueParam = Expression.Parameter(typeof(T));
			Expression expr = Expression.Call(jsonParam, valueListInfo);

			expr = Expression.Call(expr, 
								   listSetMethodInfo, 
								   Expression.Constant(templateIndex), 
								   Expression.Convert(valueParam, typeof(object)));

			return Expression.Lambda<Action<Json, T>>(expr, jsonParam, valueParam);
		}

		private static Expression<Func<Json, T>> GenerateBoundGetExpression<T>(BindingInfo bInfo) {
			var instance = Expression.Parameter(typeof(Json));
            Expression expression;

            if (bInfo.IsBoundToParent)
                expression = instance;
            else 
			    expression = Expression.Call(instance, jsonGetDataInfo);

			expression = CreateBinding<T>(bInfo, true, expression);

			if (expression != null) {
				return Expression.Lambda<Func<Json, T>>(expression, instance);
			}
			return null;
		}

		private static Expression<Action<Json, T>> GenerateBoundSetExpression<T>(BindingInfo bInfo) {
			var instance = Expression.Parameter(typeof(Json));
            Expression expression;

            if (bInfo.IsBoundToParent)
                expression = instance;
            else
                expression = Expression.Call(instance, jsonGetDataInfo);
			
			var value = Expression.Parameter(typeof(T));
			expression = CreateBinding<T>(bInfo, false, expression, value);

			if (expression != null) {
				return Expression.Lambda<Action<Json, T>>(expression, instance, value);
			}
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="bInfo"></param>
		/// <param name="createGetBinding"></param>
		/// <returns></returns>
		private static Expression CreateBinding<T>(BindingInfo bInfo, 
												   bool createGetBinding, 
												   Expression instance, 
												   ParameterExpression value = null) {
			BlockExpression block;
			Expression[] callArr;
			Expression bindingExpr = null;
			Expression notNullCheck;
			Expression lastVar;
			ParameterExpression[] variables;
			Type memberType;

			if (bInfo.Path != null) {
				variables = new ParameterExpression[bInfo.Path.Count];
				callArr = new Expression[bInfo.Path.Count];
				lastVar = instance;

				//				this.dataType = GetMemberDeclaringType(bInfo.Path[0]);

				// Create expressions for each call including local variables to store 
				// the returnvalue from the call.
				for (int i = 0; i < variables.Length; i++) {
					Type type = DataBindingHelper.GetMemberReturnType(bInfo.Path[i]);
					variables[i] = Expression.Variable(type, "localvar" + i);
					bindingExpr = CreateGetMemberBinding<T>(bInfo.Path[i], lastVar, false);
					bindingExpr = Expression.Assign(variables[i], bindingExpr);
					callArr[i] = bindingExpr;
					lastVar = variables[i];
				}

				if (createGetBinding)
					bindingExpr = CreateGetMemberBinding<T>(bInfo.Member, lastVar, true);
				else
					bindingExpr = CreateSetMemberBinding<T>(bInfo.Member, lastVar, value, true);

				// Create null-check branches for each local stored value
				memberType = DataBindingHelper.GetMemberReturnType(bInfo.Member);
				for (int i = variables.Length - 1; i >= 0; i--) {
					if ((i + 1) < callArr.Length)
						bindingExpr = callArr[i + 1];
					notNullCheck = Expression.NotEqual(variables[i], Expression.Constant(null));

					if (createGetBinding)
						bindingExpr = Expression.Condition(notNullCheck, bindingExpr, Expression.Default(memberType));
					else {
						// TODO:
						// Ignore if null or throw exception when setting the value?

						//var exception = Expression.Throw(Expression.Constant(new NullReferenceException()));
						//bindingExpr = Expression.IfThenElse(notNullCheck, bindingExpr, exception);
						bindingExpr = Expression.IfThen(notNullCheck, bindingExpr);
					}
					block = Expression.Block(new[] { variables[i] }, callArr[i], bindingExpr);
					callArr[i] = block;
				}
				bindingExpr = callArr[0];
			} else {
				//				this.dataType = GetMemberDeclaringType(bInfo.Member);
				if (createGetBinding)
					bindingExpr = CreateGetMemberBinding<T>(bInfo.Member, instance, true);
				else
					bindingExpr = CreateSetMemberBinding<T>(bInfo.Member, instance, value, true);
			}
			return bindingExpr;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="member"></param>
		/// <param name="expr"></param>
		/// <returns></returns>
		private static Expression CreateGetMemberBinding<T>(MemberInfo member, Expression variable, bool convertType) {
			MethodInfo methodInfo;
			PropertyInfo bindToProperty = member as PropertyInfo;
			Expression newExpr = null;

			if (bindToProperty != null) {
				methodInfo = bindToProperty.GetGetMethod();
				if (methodInfo != null) {
					// We need to check if this is an abstract or virtual method.
					// In that case we want to create the binding for the type that 
					// declares it in first hand.
					if (methodInfo.IsVirtual) {
						methodInfo = methodInfo.GetBaseDefinition();
					}
					newExpr = CreatePropertyGetBinding<T>(methodInfo, variable, convertType);
				}
			} else {
				var fieldInfo = (FieldInfo)member;
				newExpr = CreateFieldGetBinding<T>(fieldInfo, variable, convertType);
			}
			return newExpr;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="member"></param>
		/// <param name="expr"></param>
		/// <returns></returns>
		private static Expression CreateSetMemberBinding<T>(MemberInfo member,
													 Expression variable,
													 ParameterExpression value,
													 bool convertType) {
			MethodInfo methodInfo;
			PropertyInfo bindToProperty = member as PropertyInfo;
			Expression newExpr = null;

			if (bindToProperty != null) {
				methodInfo = bindToProperty.GetSetMethod();
				if (methodInfo != null) {
					// We need to check if this is an abstract or virtual method. In that case we 
					// want to create the binding for the type that declares it in first hand.
					if (methodInfo.IsVirtual) {
						methodInfo = methodInfo.GetBaseDefinition();
					}
					newExpr = CreatePropertySetBinding<T>(methodInfo, variable, value, convertType);
				}
			} else {
				var fieldInfo = (FieldInfo)member;
				newExpr = CreateFieldSetBinding<T>(fieldInfo, variable, value, convertType);
			}
			return newExpr;
		}

		/// <summary>
		/// Generate a delegate (IL code) that accepts an object and
		/// that reads and returns the value of a a specific field 
		/// </summary>
		/// <param name="field">The field to read</param>
		private static Expression CreateFieldGetBinding<T>(FieldInfo field, Expression variable, bool convertType) {
			Expression expr;

			if (variable.Type != field.DeclaringType)
				expr = Expression.Convert(variable, field.DeclaringType);
			else
				expr = variable;

			expr = Expression.Field(expr, field);
			if (convertType && !field.FieldType.Equals(typeof(T))) {
				expr = AddTypeConversionIfPossible(expr, field.FieldType, typeof(T));
			}
			return expr;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="field"></param>
		/// <param name="expr"></param>
		/// <param name="convertType"></param>
		/// <returns></returns>
		private static Expression CreateFieldSetBinding<T>(FieldInfo field,
													Expression variable,
													ParameterExpression value,
													bool convertType) {
			Expression expr;
			Type valueType = field.FieldType;

			if (variable.Type != field.DeclaringType)
				expr = Expression.Convert(variable, field.DeclaringType);
			else
				expr = variable;

			expr = Expression.Field(expr, field);

			Expression setValue = value;
			if (convertType && !valueType.Equals(typeof(T))) {
				setValue = AddTypeConversionIfPossible(value, typeof(T), valueType);
			}
			return Expression.Assign(expr, setValue);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="getMethod"></param>
		/// <param name="expr"></param>
		/// <param name="convertType"></param>
		/// <returns></returns>
		private static Expression CreatePropertyGetBinding<T>(MethodInfo getMethod,
													   Expression variable,
													   bool convertType) {
			Expression expr;

			if (variable.Type != getMethod.DeclaringType)
				expr = Expression.Convert(variable, getMethod.DeclaringType);
			else
				expr = variable;

			expr = Expression.Call(expr, getMethod);
			if (convertType && !getMethod.ReturnType.Equals(typeof(T))) {
				expr = AddTypeConversionIfPossible(expr, getMethod.ReturnType, typeof(T));
			}
			return expr;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setMethod"></param>
		/// <param name="expr"></param>
		/// <param name="convertType"></param>
		/// <returns></returns>
		private static Expression CreatePropertySetBinding<T>(MethodInfo setMethod,
													   Expression variable,
													   ParameterExpression value,
													   bool convertType) {
			Expression expr;
			Type valueType = setMethod.GetParameters()[0].ParameterType;

			if (variable.Type != setMethod.DeclaringType)
				expr = Expression.Convert(variable, setMethod.DeclaringType);
			else
				expr = variable;

			Expression setValue = value;
			if (convertType && !valueType.Equals(typeof(T))) {
				setValue = AddTypeConversionIfPossible(value, typeof(T), valueType);
			}
			return Expression.Call(expr, setMethod, setValue);
		}

		/// <summary>
		/// Checks the types and adds a conversion if possible to the expression tree.
		/// </summary>
		/// <param name="expr">The current node in the expression tree</param>
		/// <param name="from">The original type</param>
		/// <param name="to">The type to convert to if possible</param>
		/// <returns>The new node in the expression tree</returns>
		private static Expression AddTypeConversionIfPossible(Expression expr, Type from, Type to) {
			Expression newExpr = null;

			if (to.Equals(typeof(string))) {
				if (from.Equals(typeof(DateTime))) {
					// TODO:
					// What format should be used and how much information?
					// "u": datetime universal sortable format.
					newExpr = Expression.Call(expr, dateTimeToStringInfo, Expression.Constant("u"));
				}
			} else if (to.Equals(typeof(DateTime))) {
				if (from.Equals(typeof(string))) {
					newExpr = Expression.Call(null, dateTimeParseInfo, expr);
				}
			}

			if (newExpr == null) {
				try {
					newExpr = Expression.Convert(expr, to);
				} catch (Exception ex) {
					throw ErrorCode.ToException(Error.SCERRCREATEDATABINDINGFORJSON,
												ex,
												string.Format(propNotCompatible,
															  "TODO!", //DataBindingFactory.GetParentClassName(Template),
															  "TODO!", //Template.TemplateName,
															  "TODO!", //Template.JsonType,
															  "TODO!", //dataType.FullName,
															  from.Name,
															  from.FullName));
				}
			}
			return newExpr;
		}
	}
}
