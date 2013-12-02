// ***********************************************************************
// <copyright file="_SqlProcessor.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using Starcounter.Query.Execution;
//using Sc.Server.Weaver.Schema;
using Starcounter.Binding;
using Starcounter.Internal;

namespace Starcounter.Query.Sql
{
internal static class SqlProcessor
{
    const Int32 MAX_INDEX_ATTRIBUTES = 10; // Maximum number of attributes in a combined index.
    static Dictionary<String, Type> supportedTypesDict = CreateSupportedTypesDictionary();

    /// <summary>
    /// Creates a dictionary of supported parameter types in stored procedures.
    /// </summary>
    /// <returns></returns>
    private static Dictionary<String, Type> CreateSupportedTypesDictionary()
    {
        Dictionary<String, Type> dictionary = new Dictionary<String, Type>();
        dictionary.Add("SqlEnumerator", Type.GetType("Starcounter.SqlEnumerator"));
        dictionary.Add("Byte", Type.GetType("System.Byte"));
        dictionary.Add("Boolean", Type.GetType("System.Boolean"));
        dictionary.Add("DateTime", Type.GetType("System.DateTime"));
        dictionary.Add("Decimal", Type.GetType("System.Decimal"));
        dictionary.Add("Double", Type.GetType("System.Double"));
        dictionary.Add("Int16", Type.GetType("System.Int16"));
        dictionary.Add("Int32", Type.GetType("System.Int32"));
        dictionary.Add("Int64", Type.GetType("System.Int64"));
        dictionary.Add("String", Type.GetType("System.String"));
        dictionary.Add("SByte", Type.GetType("System.SByte"));
        dictionary.Add("UInt16", Type.GetType("System.UInt16"));
        dictionary.Add("UInt32", Type.GetType("System.UInt32"));
        dictionary.Add("UInt64", Type.GetType("System.UInt64"));
        dictionary.Add("Starcounter.SqlEnumerator", Type.GetType("Starcounter.SqlEnumerator"));
        dictionary.Add("System.Boolean", Type.GetType("System.Boolean"));
        dictionary.Add("System.DateTime", Type.GetType("System.DateTime"));
        dictionary.Add("System.Decimal", Type.GetType("System.Decimal"));
        dictionary.Add("System.Double", Type.GetType("System.Double"));
        dictionary.Add("System.Int16", Type.GetType("System.Int16"));
        dictionary.Add("System.Int32", Type.GetType("System.Int32"));
        dictionary.Add("System.Int64", Type.GetType("System.Int64"));
        dictionary.Add("System.String", Type.GetType("System.String"));
        dictionary.Add("System.SByte", Type.GetType("System.SByte"));
        dictionary.Add("System.UInt16", Type.GetType("System.UInt16"));
        dictionary.Add("System.UInt32", Type.GetType("System.UInt32"));
        dictionary.Add("System.UInt64", Type.GetType("System.UInt64"));
        return dictionary;
    }

    //internal static ISqlEnumerator Process(String statement)
    //{
    //    if (statement == null)
    //    {
    //        throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect statement.");
    //    }
    //    List<String> tokenList = Tokenizer.Tokenize(statement);
    //    if ((tokenList.Count >= 2 && tokenList[0] == "$CREATE" && tokenList[1] == "$INDEX") ||
    //        (tokenList.Count >= 3 && tokenList[0] == "$CREATE" && tokenList[2] == "$INDEX"))
    //    {
    //        return ProcessCreateIndex(tokenList);
    //    }
    //    if (tokenList.Count >= 2 && tokenList[0] == "$CREATE" &&
    //        (tokenList[1] == "$PROC" || tokenList[1] == "$PROCEDURE"))
    //    {
    //        ProcessCreateProcedure(tokenList);
    //        return;
    //    }
    //    throw new SqlException("Unknown statement.");
    //}

    static Exception GetSqlExceptionForToken(String message, List<String> tokenList, int pos) {
        if (pos < tokenList.Count)
            return SqlException.GetSqlException(Error.SCERRSQLINCORRECTSYNTAX, message, tokenList[pos]);
        else
            return SqlException.GetSqlException(Error.SCERRSQLINCORRECTSYNTAX, message + " But no token is found (end of the query).");
    }

    internal static Boolean ParseNonSelectQuery(String query, Boolean slowSql, params Object[] values) {
        if (query.Length == 0)
            return false;
        switch (query[0]) {
            case 'C':
            case 'c':
                SqlProcessor.ProcessCreateIndex(query);
                return true;
            case 'D':
            case 'd':
                if (SqlProcessor.ProcessDQuery(slowSql, query, values))
                    return true;
                else
                    return false;
            case ' ':
            case '\t':
                query = query.TrimStart(' ', '\t');
                if (query.Length == 0)
                    return false;
                switch (query[0]) {
                    case 'C':
                    case 'c':
                        SqlProcessor.ProcessCreateIndex(query);
                        return true;
                    case 'D':
                    case 'd':
                        if (SqlProcessor.ProcessDQuery(slowSql, query, values))
                            return true;
                        else
                            return false;
                    default:
                        return false;
                }

            default:
                return false;
        }
    }

    // CREATE [UNIQUE] INDEX indexName ON typeName (propName1 [ASC/DESC], ...)
    internal static void ProcessCreateIndex(String statement)
    {
        Int16[] attributeIndexArr;
        Int32 factor;
        UInt16 sortMask;
        UInt32 errorCode;
        UInt32 flags = 0;
        
        // Parse the statement and prepare variables to call kernel
        List<String> tokenList = Tokenizer.Tokenize(statement);
        if (tokenList == null || tokenList.Count < 2)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect tokenList.");
        }
        Int32 pos = 0;
        if (!Token("$CREATE", tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected word CREATE.", tokenList, pos);
        }
        pos++;
        if (Token("$UNIQUE", tokenList, pos))
        {
            flags |= sccoredb.SC_INDEXCREATE_UNIQUE_CONSTRAINT;
            pos++;
        }
        if (!Token("$INDEX", tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected word INDEX.", tokenList, pos);
        }
        pos++;
        if (!IdentifierToken(tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected identifier.", tokenList, pos);
        }
        String indexName = tokenList[pos];
        pos++;
        if (!Token("$ON", tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected word ON.", tokenList, pos);
        }
        pos++;
        
        // Parse the type (relation) name, which contains namespaces.
        Int32 beginPos = pos;
        String typePath = ProcessIdentifierPath(tokenList, ref pos);
        Int32 endPos = pos - 1;
        // Parse properties (column) names
        if (!Token("(", tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected opening bracket '('.", tokenList, pos);
        }
        pos++;
        List<String> propertyList = new List<String>(); // List of properties/columns
        factor = 1;
        sortMask = 0;
        propertyList.Add(ProcessProperty(tokenList, ref pos, -1, null));
        if (ProcessSortOrdering(tokenList, ref pos) == SortOrder.Descending)
        {
            sortMask = (UInt16)(sortMask + factor);
        }
        factor = factor * 2;
        while (Token(",", tokenList, pos))
        {
            pos++;
            propertyList.Add(ProcessProperty(tokenList, ref pos, -1, null));
            if (ProcessSortOrdering(tokenList, ref pos) == SortOrder.Descending)
            {
                sortMask = (UInt16)(sortMask + factor);
            }
            factor = factor * 2;
        }

        if (!Token(")", tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected closing bracket ')'.", tokenList, pos);
        }
        pos++;

        if (propertyList.Count > MAX_INDEX_ATTRIBUTES)
        {
            throw SqlException.GetSqlException(Error.SCERRTOOMANYINDEXCOLUMNS, "Compoind index to create contains " + 
                propertyList.Count + "columns, while " + MAX_INDEX_ATTRIBUTES + " columns are supported for index creation.");
        }

        if (pos < tokenList.Count)
        {
            //throw new SqlException("Expected no more tokens.", tokenList, pos);
            throw SqlException.GetSqlException(Error.SCERRSQLINCORRECTSYNTAX, "Found token after end of statement.");
        }

        // Prepare array of attributes
        TypeBinding typeBind;
        try {
            typeBind = Bindings.GetTypeBindingInsensitive(typePath);
        } catch (DbException e) {
            if ((uint)e.Data[ErrorCode.EC_TRANSPORT_KEY] == Error.SCERRSCHEMACODEMISMATCH)
                typeBind = null;
            else
                throw;
        }
        PropertyBinding propBind = null;
        //if (typeBind == null)
        //    TypeRepository.TryGetTypeBindingByShortName(typePath, out typeBind);
        if (typeBind == null)
            throw SqlException.GetSqlException(Error.SCERRSQLUNKNOWNNAME, "Table \"" + typePath + "\" is not found");
        attributeIndexArr = new Int16[propertyList.Count + 1];
        for (Int32 i = 0; i < propertyList.Count; i++)
        {
            propBind = typeBind.GetPropertyBindingInsensitive(propertyList[i]);
            if (propBind == null)
                throw SqlException.GetSqlException(Error.SCERRSQLUNKNOWNNAME, "Column " + propertyList[i] + " is not found in table " + typeBind.Name);
            attributeIndexArr[i] = (Int16)propBind.GetDataIndex();
        }

        // Set the last position in the array to -1 (terminator).
        attributeIndexArr[attributeIndexArr.Length - 1] = -1;

        // Call kenrel
        unsafe
        {
            var tableId = typeBind.TableId;
            fixed (Int16* attributeIndexesPointer = &(attributeIndexArr[0]))
            {
                errorCode = sccoredb.sccoredb_create_index(tableId, indexName, sortMask, attributeIndexesPointer, flags);
            }
        }
        if (errorCode != 0)
        {
            Exception ex = ErrorCode.ToException(errorCode);
            if (errorCode == Error.SCERRTRANSACTIONLOCKEDONTHREAD)
                ex = ErrorCode.ToException(Error.SCERRCANTEXECUTEDDLTRANSACTLOCKED, ex, "Cannot execute CREATE INDEX statement.");
            throw ex;
        }
    }

    internal static bool ProcessDQuery(bool slowSQL, String statement, params Object[] values)
    {
        List<String> tokenList = Tokenizer.Tokenize(statement);
        if (tokenList == null || tokenList.Count < 2)
        {
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect tokenList.");
        }
        Int32 pos = 0;
        if (Token("$DROP", tokenList, pos))
        {
            pos++;
            if (Token("$INDEX", tokenList, pos))
            {
                pos++;
                ProcessDropIndex(statement, tokenList, pos);
                return true;
            }
            if (Token("$TABLE", tokenList, pos))
            {
                pos++;
                ProcessDropTable(statement, tokenList, pos);
                return true;
            }
                throw GetSqlExceptionForToken("Unexpected token after DROP", tokenList, pos);
        }
        if (Token("$DELETE", tokenList, pos) && slowSQL)
        {
            pos++;
            ProcessDelete(statement, values);
            return true;
        }
        return false;
    }

    // Naive impelementation of "DELETE FROM tableName WHERE condition".
    private static void ProcessDelete(String statement, params Object[] values)
    {
        if (statement == null)
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect statement.");

        if (statement.Length < 12 && statement.ToUpperInvariant().Substring(0, 12) != "DELETE FROM ")
            throw SqlException.GetSqlException(Error.SCERRSQLINCORRECTSYNTAX, "Expected words DELETE FROM.");

        String tableName = statement.Substring(12);

        Int32 wherePos = tableName.ToUpperInvariant().IndexOf(" WHERE ");
        String whereClause = "";
        if (wherePos >= 0)
        {
            whereClause = tableName.Substring(wherePos);
            tableName = tableName.Substring(0, wherePos);
        }

        var result = Db.SlowSQL("SELECT x FROM " + tableName + " x" + whereClause, values);
        foreach (IObjectView entity in result)
        {
            entity.Delete();
        }
    }

    /// <summary>
    /// Continue processing statement DROP INDEX indexName ON [namespaces.]tableName
    /// </summary>
    /// <param name="statement">The query</param>
    /// <param name="tokenList">List of tokens for the query</param>
    /// <param name="pos">Position to continue in the token list</param>
    private static void ProcessDropIndex(String statement, List<String> tokenList, Int32 pos)
    {
        // Parse the rest of the statement and prepare variables to call kernel
        if (!IdentifierToken(tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected identifier.", tokenList, pos);
        }
        String indexName = tokenList[pos];
        pos++;
        if (!Token("$ON", tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected word ON.", tokenList, pos);
        }
        pos++;

        // Parse the type (relation) name, which contains namespaces.
        Int32 beginPos = pos;
        String typePath = ProcessIdentifierPath(tokenList, ref pos);
        Int32 endPos = pos - 1;

        if (pos < tokenList.Count)
        {
            //throw new SqlException("Expected no more tokens.", tokenList, pos);
            throw SqlException.GetSqlException(Error.SCERRSQLINCORRECTSYNTAX, "Found token after end of statement.");
        }

        TypeBinding typeBind;
        // Obtain correct table name
        Exception exc = null;
        try {
            typeBind = Bindings.GetTypeBindingInsensitive(typePath);
        } catch (DbException e) {
            if ((uint)e.Data[ErrorCode.EC_TRANSPORT_KEY] == Error.SCERRSCHEMACODEMISMATCH) {
                typeBind = null;
                exc = e;
            } else
                throw;
        }
        if (typeBind == null)
            throw SqlException.GetSqlException(Error.SCERRSQLUNKNOWNNAME, "Table \"" + typePath + "\" is not found", exc);

        // Call kernel
        UInt32 errorCode;
        unsafe
        {
            errorCode = sccoredb.sccoredb_drop_index(typeBind.Name, indexName);
        }
        if (errorCode != 0) {
            Exception ex = ErrorCode.ToException(errorCode);
            if (errorCode == Error.SCERRTRANSACTIONLOCKEDONTHREAD)
                ex = ErrorCode.ToException(Error.SCERRCANTEXECUTEDDLTRANSACTLOCKED, ex, "Cannot execute CREATE INDEX statement.");
            throw ex;
        }
    }

    /// <summary>
    /// Continue processing statement DROP TABLE [namespaces.]tableName
    /// </summary>
    /// <param name="statement">The query</param>
    /// <param name="tokenList">List of tokens for the query</param>
    /// <param name="pos">Position to continue in the token list</param>
    private static void ProcessDropTable(String statement, List<String> tokenList, Int32 pos)
    {
        // Parse the rest of the statement and prepare variables to call kernel
        // Parse the type (relation) name, which contains namespaces.
        Int32 beginPos = pos;
        String typePath = ProcessIdentifierPath(tokenList, ref pos);
        Int32 endPos = pos - 1;

        if (pos < tokenList.Count)
        {
            //throw new SqlException("Expected no more tokens.", tokenList, pos);
            throw SqlException.GetSqlException(Error.SCERRSQLINCORRECTSYNTAX, "Found token after end of statement.");
        }

        // Call kernel
        UInt32 errorCode;
        unsafe
        {
            errorCode = sccoredb.sccoredb_drop_table(typePath);
        }
        if (errorCode != 0) {
            Exception ex = ErrorCode.ToException(errorCode);
            if (errorCode == Error.SCERRTRANSACTIONLOCKEDONTHREAD)
                ex = ErrorCode.ToException(Error.SCERRCANTEXECUTEDDLTRANSACTLOCKED, ex, "Cannot execute CREATE INDEX statement.");
            throw ex;
        }
    }

    // CREATE {PROC|PROCEDURE} procedureName @parameterName1 typeName1 [OUT|OUTPUT], ... 
    // AS methodSpecifier
    //internal static void ProcessCreateProcedure(List<String> tokenList)
    //{
    //    if (tokenList == null || tokenList.Count < 2)
    //    {
    //        throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect tokenList.");
    //    }
    //    Int32 pos = 0;
    //    if (!Token("$CREATE", tokenList, pos))
    //    {
    //        throw new SqlException("Expected word CREATE.", tokenList, pos);
    //    }
    //    pos++;
    //    if (!Token("$PROC", tokenList, pos) && !Token("$PROCEDURE", tokenList, pos))
    //    {
    //        throw new SqlException("Expected word PROCEDURE.", tokenList, pos);
    //    }
    //    pos++;
    //    // Stored procedure name.
    //    if (!IdentifierToken(tokenList, pos))
    //    {
    //        throw new SqlException("Expected identifier.", tokenList, pos);
    //    }
    //    String procedureName = tokenList[pos];
    //    Int32 procedureNamePos = pos;
    //    pos++;
    //    // Handle parameters.
    //    List<String> parameterNameList = new List<String>();
    //    List<Type> parameterTypeList = new List<Type>();
    //    List<Boolean> parameterOutputList = new List<Boolean>();
    //    List<Int32> parameterBeginPosList = new List<Int32>();
    //    List<Int32> parameterEndPosList = new List<Int32>();

    //    while (Token("@", tokenList, pos))
    //    {
    //        parameterBeginPosList.Add(pos);
    //        pos++;
    //        ProcessProcedureParameter(tokenList, ref pos, parameterNameList, parameterTypeList,
    //            parameterOutputList);
    //        parameterEndPosList.Add(pos - 1);
    //    }
    //    if (!Token("$AS", tokenList, pos))
    //    {
    //        throw new SqlException("Expected word AS.", tokenList, pos);
    //    }
    //    pos++;
    //    Int32 beginPos = pos;
    //    String identifierPath = ProcessIdentifierPath(tokenList, ref pos);
    //    Int32 endPos = pos - 1;
    //    String className = identifierPath.Substring(0, identifierPath.LastIndexOf('.'));
    //    String methodName = identifierPath.Substring(identifierPath.LastIndexOf('.') + 1);
    //    TypeSearcher typeSearcher = Application.Current.TypeSearcher;
    //    Type classType = typeSearcher.FindByFullName(className);
    //    if (classType == null)
    //    {
    //        throw new SqlException("Unknown type " + className + ".", tokenList, beginPos, endPos);
    //    }

    //    String argumentSpec = "(";
    //    for (Int32 i = 0; i < parameterTypeList.Count; i++)
    //    {
    //        if (i != 0)
    //        {
    //            argumentSpec += ", ";
    //        }
    //        argumentSpec += parameterTypeList[i].FullName;
    //    }
    //    argumentSpec += ")";

    //    MethodInfo methodInfo = null;
    //    try
    //    {
    //        methodInfo = classType.GetMethod(methodName, parameterTypeList.ToArray());
    //    }
    //    catch (AmbiguousMatchException)
    //    {
    //        throw new SqlException("Ambiguous method " + methodName + argumentSpec + ".", 
    //            tokenList, beginPos, endPos);
    //    }
    //    if (methodInfo == null)
    //    {
    //        throw new SqlException("Unknown method " + methodName + argumentSpec + ".", 
    //            tokenList, beginPos, endPos);
    //    }
    //    ParameterInfo[] parameterInfoArr = methodInfo.GetParameters();
    //    for (Int32 i = 0; i < parameterInfoArr.Length; i++)
    //    {
    //        if (parameterInfoArr[i].IsOut && !parameterOutputList[i])
    //        {
    //            throw new SqlException("Expected parameter to be declared OUTPUT.",
    //                tokenList, parameterBeginPosList[i], parameterEndPosList[i]);
    //        }
    //        if (!parameterInfoArr[i].IsOut && parameterOutputList[i])
    //        {
    //            throw new SqlException("Expected parameter not to be declared OUTPUT.",
    //                tokenList, parameterBeginPosList[i], parameterEndPosList[i]);
    //        }
    //    }

    //    if (StoredProcedureRepository.Exist(procedureName))
    //        throw new SqlException("A stored procedure with name " + procedureName + " already exists.", 
    //            tokenList, procedureNamePos);

    //    StoredProcedureRepository.AddMethod(procedureName, methodInfo);
    //}

    //internal static void ProcessProcedureParameter(List<String> tokenList, ref Int32 pos, 
    //    List<String> parameterNameList, List<Type> parameterTypeList, List<Boolean> parameterOutputList)
    //{
    //    // Parameter name.
    //    if (!IdentifierToken(tokenList, pos))
    //    {
    //        throw new SqlException("Expected identifier.", tokenList, pos);
    //    }
    //    parameterNameList.Add(tokenList[pos]);
    //    pos++;
    //    // Parameter type.
    //    Int32 beginPos = pos;
    //    String parameterTypeName = ProcessIdentifierPath(tokenList, ref pos);
    //    Int32 endPos = pos - 1;
    //    Type parameterType = null;
    //    if (!supportedTypesDict.TryGetValue(parameterTypeName, out parameterType))
    //    {
    //        throw new SqlException("Unknown or unsupported type " + parameterTypeName + ".", tokenList, beginPos, endPos); 
    //    }
        
    //    // Output parameter?
    //    if (Token("$OUT", tokenList, pos) || Token("$OUTPUT", tokenList, pos))
    //    {
    //        parameterType = parameterType.MakeByRefType();
    //        parameterOutputList.Add(true);
    //        pos++;
    //    }
    //    else
    //    {
    //        parameterOutputList.Add(false);
    //    }
    //    parameterTypeList.Add(parameterType);
    //    if (Token(",", tokenList, pos)) pos++;
    //}
        
    internal static Boolean Token(String word, List<String> tokenList, Int32 pos)
    {
        return (tokenList.Count > pos && tokenList[pos] == word);
    }

    internal static Boolean StringToken(List<String> tokenList, Int32 pos)
    {
        return (tokenList.Count > pos && tokenList[pos].Length > 1 && Tokenizer.Quote(tokenList[pos][0]) &&
                Tokenizer.Quote(tokenList[pos][tokenList[pos].Length - 1]));
    }

    internal static Boolean UIntegerToken(List<String> tokenList, Int32 pos)
    {
        return (tokenList.Count > pos && tokenList[pos].Length > 0 && Tokenizer.Digit(tokenList[pos][0]));
    }

    internal static Boolean IdentifierToken(List<String> tokenList, Int32 pos)
    {
        if (tokenList.Count <= pos || tokenList[pos].Length == 0 || !Tokenizer.Letter(tokenList[pos][0]) || Tokenizer.InternalReservedWord(tokenList[pos]))
        {
            return false;
        }
        else
            for (Int32 i = 1; i < tokenList[pos].Length; i++)
            {
                if (!Tokenizer.Letter(tokenList[pos][i]) && !Tokenizer.Digit(tokenList[pos][i]) && !Tokenizer.Underscore(tokenList[pos][i]))
                {
                    return false;
                }
            }
        return true;
    }

    internal static Boolean LiteralPattern(List<String> tokenList, Int32 pos)
    {
        Int32 length;
        return (Token("$NULL", tokenList, pos) || Token("$TRUE", tokenList, pos) || Token("$FALSE", tokenList, pos) ||
                Token("$DATE", tokenList, pos) || Token("$TIME", tokenList, pos) || Token("$TIMESTAMP", tokenList, pos) ||
                Token("$OBJECT", tokenList, pos) || StringToken(tokenList, pos) || IntegerPattern(tokenList, pos, out length));
    }

    internal static Boolean DoublePattern(List<String> tokenList, Int32 pos, out Int32 length)
    {
        length = 0;
        Int32 length1;
        if (!DecimalPattern(tokenList, pos, out length1))
        {
            return false;
        }
        if (!Token("E", tokenList, pos + length1))
        {
            return false;
        }
        Int32 length2;
        if (!IntegerPattern(tokenList, pos + length1 + 1, out length2))
        {
            return false;
        }
        length = length1 + 1 + length2;
        return true;
    }

    internal static Boolean DecimalPattern(List<String> tokenList, Int32 pos, out Int32 length)
    {
        length = 0;
        Int32 length1;
        if (!IntegerPattern(tokenList, pos, out length1))
        {
            return false;
        }
        if (!Token(".", tokenList, pos + length1))
        {
            return false;
        }
        if (!UIntegerToken(tokenList, pos + length1 + 1))
        {
            return false;
        }
        length = length1 + 2;
        return true;
    }

    internal static Boolean IntegerPattern(List<String> tokenList, Int32 pos, out Int32 length)
    {
        if (UIntegerToken(tokenList, pos))
        {
            length = 1;
            return true;
        }
        if ((Token("+", tokenList, pos) || Token("-", tokenList, pos)) && UIntegerToken(tokenList, pos + 1))
        {
            length = 2;
            return true;
        }
        length = 0;
        return false;
    }

    internal static Boolean IntegerType(IValueExpression expr)
    {
        if (expr.DbTypeCode == DbTypeCode.Int64 || expr.DbTypeCode == DbTypeCode.Int32 ||
            expr.DbTypeCode == DbTypeCode.Int16 || expr.DbTypeCode == DbTypeCode.SByte)
        {
            return true;
        }
        return false;
    }

    internal static Boolean UIntegerType(IValueExpression expr)
    {
        if (expr.DbTypeCode == DbTypeCode.UInt64 || expr.DbTypeCode == DbTypeCode.UInt32 ||
            expr.DbTypeCode == DbTypeCode.UInt16 || expr.DbTypeCode == DbTypeCode.Byte)
        {
            return true;
        }
        return false;
    }

    internal static Boolean DoubleType(IValueExpression expr)
    {
        if (expr.DbTypeCode == DbTypeCode.Double || expr.DbTypeCode == DbTypeCode.Single)
        {
            return true;
        }
        return false;
    }

    internal static String ProcessIdentifierPath(List<String> tokenList, ref Int32 pos)
    {
        if (!IdentifierToken(tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected identifier.", tokenList, pos);
        }
        String identifierPath = tokenList[pos];
        pos++;
        while (Token(".", tokenList, pos) && IdentifierToken(tokenList, pos + 1))
        {
            identifierPath += "." + tokenList[pos + 1];
            pos += 2;
        }
        return identifierPath;
    }

#if false
    internal static TypeBinding GetTypeBinding(String name, List<String> tokenList, Int32 beginPos, Int32 endPos)
    {
        TypeBinding typeBind = null;
        // Short type name.
        if (name.IndexOf('.') == -1)
        {
            Int32 result = TypeRepository.TryGetTypeBindingByShortName(name, out typeBind);
            switch (result)
            {
                case 0:
                    throw new SqlException("Unknown type.", tokenList, beginPos, endPos);
                case 1:
                    return typeBind;
                case 2:
                    throw new SqlException("Ambiguous type.", tokenList, beginPos, endPos);
                default:
                    throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect result.");
            }
        }
        // Full type name.
        typeBind = TypeRepository.GetTypeBinding(name);
        if (typeBind == null)
        {
            throw new SqlException("Unknown type.", tokenList, beginPos, endPos);
        }
        return typeBind;
    }
#endif

    // TODO: Implement support of methods in path.
    //internal static IPath ProcessPath(List<String> tokenList, ref Int32 pos, Int32 extentNum, ITypeBinding typeBind)
    //{
    //    List<IObjectPathItem> path = new List<IObjectPathItem>();
    //    // First path item.
    //    PropertyBinding prop = ProcessProperty(tokenList, ref pos, extentNum, typeBind);
    //    // Eventually more path items.
    //    while (Token(".", tokenList, pos))
    //    {
    //        if (prop.TypeCode != DbTypeCode.Object)
    //        {
    //            throw new SqlException("Unexpected token.", tokenList, pos);
    //        }
    //        pos++;
    //        typeBind = (prop as ObjectProperty).TypeBinding;
    //        path.Add(prop as IObjectPathItem);
    //        prop = ProcessProperty(tokenList, ref pos, -1, typeBind);
    //    }
    //    if (path.Count == 0)
    //    {
    //        return (prop as IPath);
    //    }
    //    switch (prop.DbTypeCode)
    //    {
    //        case DbTypeCode.Binary:
    //            return new BinaryPath(extentNum, path, prop as BinaryProperty);
    //        case DbTypeCode.Boolean:
    //            return new BooleanPath(extentNum, path, prop as BooleanProperty);
    //        case DbTypeCode.DateTime:
    //            return new DateTimePath(extentNum, path, prop as DateTimeProperty);
    //        case DbTypeCode.Decimal:
    //            return new DecimalPath(extentNum, path, prop as DecimalProperty);
    //        case DbTypeCode.Double:
    //        case DbTypeCode.Single:
    //            return new DoublePath(extentNum, path, prop as DoubleProperty);
    //        case DbTypeCode.Int64:
    //        case DbTypeCode.Int32:
    //        case DbTypeCode.Int16:
    //        case DbTypeCode.SByte:
    //            return new IntegerPath(extentNum, path, prop as IntegerProperty);
    //        case DbTypeCode.Object:
    //            return new ObjectPath(extentNum, path, prop as ObjectProperty);
    //        case DbTypeCode.String:
    //            return new StringPath(extentNum, path, prop as StringProperty);
    //        case DbTypeCode.UInt64:
    //        case DbTypeCode.UInt32:
    //        case DbTypeCode.UInt16:
    //        case DbTypeCode.Byte:
    //            return new UIntegerPath(extentNum, path, prop as UIntegerProperty);
    //        default:
    //            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect TypeCode: " + prop.DbTypeCode);
    //    }
    //}

    private static String ProcessProperty(List<String> tokenList, ref Int32 pos, Int32 extentNum, ITypeBinding typeBind)
    {
        String propertyName;
        if (!IdentifierToken(tokenList, pos))
        {
            throw GetSqlExceptionForToken("Expected identifier.", tokenList, pos);
        }
        propertyName = tokenList[pos];
        pos++;
        return propertyName;
    }

    //internal static Property ProcessProperty(IPropertyBinding propBind, Int32 extentNum, ITypeBinding typeBind)
    //{
    //    switch (propBind.TypeCode)
    //    {
    //        case DbTypeCode.Binary:
    //            return new BinaryProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.Boolean:
    //            return new BooleanProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.DateTime:
    //            return new DateTimeProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.Decimal:
    //            return new DecimalProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.Double:
    //        case DbTypeCode.Single:
    //            return new DoubleProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.Int64:
    //        case DbTypeCode.Int32:
    //        case DbTypeCode.Int16:
    //        case DbTypeCode.SByte:
    //            return new IntegerProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.Object:
    //            return new ObjectProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.String:
    //            return new StringProperty(extentNum, typeBind, propBind);
    //        case DbTypeCode.UInt64:
    //        case DbTypeCode.UInt32:
    //        case DbTypeCode.UInt16:
    //        case DbTypeCode.Byte:
    //            return new UIntegerProperty(extentNum, typeBind, propBind);
    //        default:
    //            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect TypeCode: " + propBind.TypeCode);
    //    }
    //}

    internal static SortOrder ProcessSortOrdering(List<String> tokenList, ref Int32 pos)
    {
        if (Token("$ASC", tokenList, pos))
        {
            pos++;
            return SortOrder.Ascending;
        }
        if (Token("$DESC", tokenList, pos))
        {
            pos++;
            return SortOrder.Descending;
        }
        return SortOrder.Ascending;
    }

    private static Boolean TryGetTypeCode(String typePath, out DbTypeCode typeCode)
    {
        switch (typePath)
        {
            case "Binary":
                typeCode = DbTypeCode.Binary;
                return true;
            case "Boolean":
                typeCode = DbTypeCode.Boolean;
                return true;
            case "Byte":
                typeCode = DbTypeCode.Byte;
                return true;
            case "DateTime":
                typeCode = DbTypeCode.DateTime;
                return true;
            case "Decimal":
                typeCode = DbTypeCode.Decimal;
                return true;
            case "Double":
                typeCode = DbTypeCode.Double;
                return true;
            case "Single":
                typeCode = DbTypeCode.Single;
                return true;
            case "Int64":
                typeCode = DbTypeCode.Int64;
                return true;
            case "Int32":
                typeCode = DbTypeCode.Int32;
                return true;
            case "Int16":
                typeCode = DbTypeCode.Int16;
                return true;
            case "SByte":
                typeCode = DbTypeCode.SByte;
                return true;
            case "String":
                typeCode = DbTypeCode.String;
                return true;
            case "UInt64":
                typeCode = DbTypeCode.UInt64;
                return true;
            case "UInt32":
                typeCode = DbTypeCode.UInt32;
                return true;
            case "UInt16":
                typeCode = DbTypeCode.UInt16;
                return true;
            case "Starcounter.Binary":
                typeCode = DbTypeCode.Binary;
                return true;
            case "System.Boolean":
                typeCode = DbTypeCode.Boolean;
                return true;
            case "System.Byte":
                typeCode = DbTypeCode.Byte;
                return true;
            case "System.DateTime":
                typeCode = DbTypeCode.DateTime;
                return true;
            case "System.Decimal":
                typeCode = DbTypeCode.Decimal;
                return true;
            case "System.Double":
                typeCode = DbTypeCode.Double;
                return true;
            case "System.Single":
                typeCode = DbTypeCode.Single;
                return true;
            case "System.Int64":
                typeCode = DbTypeCode.Int64;
                return true;
            case "System.Int32":
                typeCode = DbTypeCode.Int32;
                return true;
            case "System.Int16":
                typeCode = DbTypeCode.Int16;
                return true;
            case "System.SByte":
                typeCode = DbTypeCode.SByte;
                return true;
            case "System.String":
                typeCode = DbTypeCode.String;
                return true;
            case "System.UInt64":
                typeCode = DbTypeCode.UInt64;
                return true;
            case "System.UInt32":
                typeCode = DbTypeCode.UInt32;
                return true;
            case "System.UInt16":
                typeCode = DbTypeCode.UInt16;
                return true;
            default:
                typeCode = DbTypeCode.Object;
                return false;
        }
    }
}
}
