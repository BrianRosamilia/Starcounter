// ***********************************************************************
// <copyright file="IndexScan.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter;
using Starcounter.Binding;
using Starcounter.Query.Sql;using System;using System.Collections;using System.Collections.Generic;using System.Text;using System.Text.RegularExpressions;using System.IO;using System.Runtime.InteropServices;//using Starcounter.Management.Win32;using Sc.Query.Execution;using Starcounter.Internal;
using System.Diagnostics;namespace Starcounter.Query.Execution{internal class IndexScan : ExecutionEnumerator, IExecutionEnumerator{    Int32 extentNumber = -1;    IndexInfo indexInfo = null;    List<String> strPathList = null; // List of strings describing the different paths included in the combined index.    List<IDynamicRange> dynamicRangeList = null; // List of dynamic ranges for the different paths included in the combined index.    ILogicalExpression postFilterCondition = null;    Boolean descending = true,        enumeratorCreated = false,        shouldRecalculateRange = true,        onlyEqualities = true; // True as long as all investigated static ranges are equality comparisons.        //rangeChanged = false,    Enumerator enumerator = null;    Row contextObject = null;    ByteArrayBuilder firstKeyBuilder = null,        secondKeyBuilder = null;    Byte[] firstKeyBuffer = null,        secondKeyBuffer = null;    RangeFlags rangeFlags = 0;    IteratorHelper iterHelper; // Stores cached iterator helper.    FilterCallback callbackManagedFilterCached = null;    UInt64 keyOID, keyETI; // Saved OID, ETI from recreation key.    Boolean enableRecreateObjectCheck = false; // Enables check for deleted object during enumerator recreation.    //Boolean triedEnumeratorRecreation = false; // Indicates if we should try enumerator recreation with supplied key.
    Boolean stayAtOffsetkey = false;
    public Boolean StayAtOffsetkey { get { return stayAtOffsetkey; } set { stayAtOffsetkey = value; } }
    Boolean useOffsetkey = true;
    public Boolean UseOffsetkey { get { return useOffsetkey; } set { useOffsetkey = value; } }
    Boolean isAtRecreatedKey = false;
    public Boolean IsAtRecreatedKey { get { return isAtRecreatedKey; } }

    internal IndexScan(byte nodeId,         RowTypeBinding rowTypeBind,        Int32 extentNum,        IndexInfo indexInfo,        List<String> pathList,        List<IDynamicRange> dynRangeList,        ILogicalExpression postFilterCond,        SortOrder sortOrder,        INumericalExpression fetchNumberExpr,
        INumericalExpression fetchOffsetExpr,         IBinaryExpression fetchOffsetKeyExpr,        Boolean innermostExtent,        VariableArray varArr, String query, Boolean topNode)        : base(nodeId, EnumeratorNodeType.IndexScan, rowTypeBind, varArr, topNode, 3)    {
        if (rowTypeBind == null)
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect rowTypeBind.");
        if (varArr == null)
            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect varArr.");
        if (indexInfo == null)            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect indexInfo.");        if (pathList == null && pathList.Count > 0)            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect pathList.");        if (dynRangeList == null || dynRangeList.Count != pathList.Count)            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect dynRangeList.");        if (postFilterCond == null)            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect queryCond.");

        Debug.Assert(OffsetTuppleLength == 3);        extentNumber = extentNum;        this.indexInfo = indexInfo;        strPathList = pathList;        dynamicRangeList = dynRangeList;        postFilterCondition = postFilterCond;        descending = (sortOrder == SortOrder.Descending);        this.fetchNumberExpr = fetchNumberExpr;
        this.fetchOffsetExpr = fetchOffsetExpr;        this.fetchOffsetKeyExpr = fetchOffsetKeyExpr;        this.innermostExtent = innermostExtent;        this.query = query;
        firstKeyBuilder = new ByteArrayBuilder();        secondKeyBuilder = new ByteArrayBuilder();        iterHelper = IteratorHelper.GetIndex(indexInfo.Handle);        // Creating empty enumerator at caching time (with managed level post-filter).        callbackManagedFilterCached = postFilterCondition.Instantiate(null).Filtrate;        enumerator = new Enumerator(callbackManagedFilterCached);        // Checking variables existence once again to calculate range keys.        if (variableArray.Length <= 0)        {            // Since we don't have any variables we can            // calculate and cache the range keys for iterators.            // However we need to think about joins and context            // objects when evaluation of range keys can't be            // performed statically (so we wrap with try-catch).            try            {                CalculateRangeKeys();            }            catch { }            // Determining if range should be recalculated.            shouldRecalculateRange = false;        }    }

    /// <summary>
    /// The type binding of the resulting objects of the query.
    /// </summary>
    public ITypeBinding TypeBinding
    {
        get
        {
            if (projectionTypeCode == null)
                return rowTypeBinding;

            // Singleton object.
            if (projectionTypeCode == DbTypeCode.Object)
                return rowTypeBinding.GetPropertyBinding(0).TypeBinding;

            // Singleton non-object.
            return null;
        }
    }

    public Int32 Depth    {        get        {            return 0;        }    }    Object IEnumerator.Current    {        get        {            return base.Current;        }    }

    public Row CurrentRow    {        get        {            if (currentObject != null)                return currentObject;            throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect currentObject.");        }    }    /// <summary>    /// Depending on query flags, populates the flags value.    /// </summary>    public unsafe override void PopulateQueryFlags(UInt32* flags)    {        // Checking if there is any post managed filter.        if (!(postFilterCondition is LogicalLiteral))            (*flags) |= SqlConnectivityInterface.FLAG_POST_MANAGED_FILTER;        // Calling base function to populate other flags.        base.PopulateQueryFlags(flags);    }

#if false // Old implementation
    private unsafe Byte* ValidateAndGetRecreateKey(Byte* rk) {
        Byte* staticDataOffset = ValidateAndGetStaticKeyOffset(rk); ;
        UInt16 dynDataOffset = (*(UInt16*)(staticDataOffset + 2));
        Debug.Assert(dynDataOffset != 0);
        return rk + dynDataOffset;
    }
#endif

    unsafe Byte[] ValidateAndGetRecreateKey(Byte* rk) {
        // In order to skip enumerator recreation next time.
        //triedEnumeratorRecreation = true;
        Debug.Assert(OffsetTuppleLength == 3);
        SafeTupleReaderBase64 thisEnumTuple = ValidateNodeAndReturnOffsetReader(rk, OffsetTuppleLength);
        return thisEnumTuple.ReadByteArray(2);
    }

    /// <summary>    /// Tries to recreate enumerator using provided key.    /// </summary>
    unsafe Boolean TryRecreateEnumerator(Byte* rk) {
        // In order to skip enumerator recreation next time.
        //triedEnumeratorRecreation = true;

        fixed (Byte* recreationKey = ValidateAndGetRecreateKey(rk)) {

            // Getting flags.
            UInt32 _flags = (UInt32)rangeFlags;
            if (descending)
                _flags |= sccoredb.SC_ITERATOR_SORTED_DESCENDING;

            // Getting lastkey
            Byte[] lastKey;
            if (onlyEqualities || descending)
                lastKey = firstKeyBuffer;
            else
                lastKey = secondKeyBuffer;

            // Trying to recreate the enumerator from key.
            if (iterHelper.RecreateEnumerator_NoCodeGenFilter(recreationKey, enumerator, _flags, lastKey)) {
                // Indicating that enumerator has been created.
                enumeratorCreated = true;

                // Checking if we found a deleted object.
                //if (!innermostExtent)
                //{
                // Obtaining saved OID and ETI.
                IteratorHelper.RecreateEnumerator_GetObjectInfo(recreationKey, out keyOID, out keyETI);

                // Enabling recreation object check.
                enableRecreateObjectCheck = true;
                //}
                return true;
            } else // Checking if we are in outer enumerator.
                if (!innermostExtent)
                    variableArray.FailedToRecreateObject = true;
        }
        return false;
    }    /// <summary>    /// Creates enumerator which than used in MoveNext.    /// </summary>    private Boolean CreateEnumerator()    {#if false // TODO EOH2: Transaction id        // Check that the current transaction has not been changed.        if (Transaction.Current == null)        {            if (variableArray.TransactionId != 0)                throw ErrorCode.ToException(Error.SCERRITERATORNOTOWNED);        }        else        {            if (variableArray.TransactionId != Transaction.Current.TransactionId)                throw ErrorCode.ToException(Error.SCERRITERATORNOTOWNED);        }#endif

        // Checking for query parameters.
        if (shouldRecalculateRange)
            CalculateRangeKeys();

        // Trying to recreate the enumerator.        unsafe        {            // Using offset key only if enumerator is not already in recreation mode.            if ((useOffsetkey) && (fetchOffsetKeyExpr != null))                //&& (variableArray.RecreationKeyData == null))            {                fixed (Byte* recrKey = (fetchOffsetKeyExpr as BinaryVariable).Value.Value.GetInternalBuffer())                {                    // Checking if recreation key is valid.                    if ((*(Int32*)recrKey) > IteratorHelper.RK_EMPTY_LEN)                        return TryRecreateEnumerator(recrKey + 4);                }            }#if false            // Checking if key data is available and if there is no failure during outer extent recreation.            if ((variableArray.RecreationKeyData != null) &&                (!triedEnumeratorRecreation) &&                (!variableArray.FailedToRecreateObject))            {                // Checking if recreation key is valid.                if ((*(Int32*)variableArray.RecreationKeyData) > IteratorHelper.RK_EMPTY_LEN)                    return TryRecreateEnumerator(variableArray.RecreationKeyData);            }#endif
        }        //Application.Profiler.Start("CreateEnumerator", 8);        //SqlDebugHelper.PrintDelimiter("Processing: \"" + query + "\"");        // Creating the enumerator with scan range depending on sorting type.        if (descending)        {            iterHelper.GetEnumeratorCached_NoCodeGenFilter(                (UInt32)rangeFlags | sccoredb.SC_ITERATOR_SORTED_DESCENDING,                secondKeyBuffer,                firstKeyBuffer,                enumerator);        }        else        {            iterHelper.GetEnumeratorCached_NoCodeGenFilter(                (UInt32)rangeFlags,                firstKeyBuffer,                secondKeyBuffer,                enumerator);        }        // Indicating that enumerator has been created.        enumeratorCreated = true;        //Application.Profiler.Stop(8);        return true;    }    /// <summary>    /// Calculates the range values.    /// Expensive, should be avoided with shortcuts when possible.    /// </summary>    private void CalculateRangeKeys()    {        //System.Diagnostics.Debugger.Break();        // Assuming that it will be an equality range.        onlyEqualities = true;        ComparisonOperator lastFirstOperator = ComparisonOperator.GreaterThanOrEqual;        ComparisonOperator lastSecondOperator = ComparisonOperator.LessThanOrEqual;        // Reseting previously cached key builders.        firstKeyBuilder.ResetCached();        secondKeyBuilder.ResetCached();        // Running through all ranges and accumulating values to key builders.        for (Int32 i = 0; i < dynamicRangeList.Count; i++)        {            if (onlyEqualities)            {                onlyEqualities = dynamicRangeList[i].Evaluate(contextObject, indexInfo.GetSortOrdering(i), firstKeyBuilder, secondKeyBuilder,                    ref lastFirstOperator, ref lastSecondOperator);            }            else            {                // If an non-equality comparison has occurred then the rest of the range conditions should be empty.                dynamicRangeList[i].CreateFillRange(indexInfo.GetSortOrdering(i), firstKeyBuilder, secondKeyBuilder,                    lastFirstOperator, lastSecondOperator);            }        }        // Calculate range flags (can do this only after all ranges has been ran through).        rangeFlags = 0;        switch (lastFirstOperator)        {            case ComparisonOperator.GreaterThan:            case ComparisonOperator.LessThan:                break;            case ComparisonOperator.GreaterThanOrEqual:            case ComparisonOperator.LessThanOrEqual:                rangeFlags = rangeFlags | RangeFlags.IncludeLesserKey;                break;            default:                throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect lastFirstOperator.");        }        switch (lastSecondOperator)        {            case ComparisonOperator.LessThan:            case ComparisonOperator.GreaterThan:                break;            case ComparisonOperator.LessThanOrEqual:            case ComparisonOperator.GreaterThanOrEqual:                rangeFlags = rangeFlags | RangeFlags.IncludeGreaterKey;                break;            default:                throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect lastSecondOperator.");        }        // Obtaining the byte buffer from the generated keys.        firstKeyBuffer = firstKeyBuilder.GetBufferCached();        secondKeyBuffer = firstKeyBuffer;        // Checking if its an equality comparison.        if (onlyEqualities == false)            secondKeyBuffer = secondKeyBuilder.GetBufferCached();        // Setting recalculation flag.        //rangeChanged = true;    }#if false // Never used    public Boolean MoveNextCodeGen()    {        Int32 errCode;        // Checking if we need to create enumerator.        if (!enumeratorCreated)        {            Byte[] byteArray = null;            if (variableArray.Length > 0)            {                variableArray.AddVariablesToByteArray();                byteArray = variableArray.ByteArray;            }            unsafe            {                fixed (Byte* queryParams = byteArray)                {                    errCode = NewCodeGen.NewCodeGen_InitEnumerator(UniqueQueryID, queryParams);                }            }            if (errCode == 0)                enumeratorCreated = true;        }        ObjectRef currentRef;        UInt16 currentCCI;        unsafe        {            errCode = NewCodeGen.NewCodeGen_MoveNext(UniqueQueryID, &currentRef.ObjectID, &currentRef.ETI, &currentCCI);        }        if (errCode == 0)        {            // Looking for corresponding type binding.
            TypeBinding typeBinding = TypeRepository.GetTypeBinding(currentCCI);            // Creating proxy object.            IObjectProxy newPersObject = typeBinding.NewInstanceUninit();            newPersObject.Bind(currentRef.ETI, currentRef.ObjectID, typeBinding);            // Creating Row.            currentObject = new Row(rowTypeBinding);            currentObject.AttachObject(extentNumber, newPersObject);            counter++;            return true;        }        // No objects found or error occurred.        currentObject = null;        return false;    }#endif    public Boolean MoveNextManaged()    {        // Calling enumerator creation code (ranges, flags, etc.).        if (!enumeratorCreated)        {            // Failure to create enumerator can only be caused            // by being in last position in the index tree.            if (!CreateEnumerator())                return false;        }        if (enumerator.MoveNext())        {            currentObject = new Row(rowTypeBinding);            currentObject.AttachObject(extentNumber, enumerator.Current);            counter++;            return true;        }        currentObject = null;        return false;    }

    public Boolean MoveNext() {
        if (counter == 0) {
            if (fetchOffsetExpr != null) {
                Debug.Assert(fetchOffsetKeyExpr == null);
                if (fetchOffsetExpr.EvaluateToInteger(null) != null) {
                    for (int i = 0; i < fetchOffsetExpr.EvaluateToInteger(null).Value; i++)
                        //if (HasCodeGeneration) {
                        //    if (!MoveNextCodeGen())
                        //        return false;
                        //} else 
                        if (!MoveNextManaged())
                            return false;
                    counter = 0;
                }
            }
            if (fetchNumberExpr != null) {
                if (fetchNumberExpr.EvaluateToInteger(null) != null)
                    fetchNumber = fetchNumberExpr.EvaluateToInteger(null).Value;
                else
                    fetchNumber = 0;
            }

            // Checking if there is a saved recreation object.
            if (counter < fetchNumber && MoveNextManaged()) {
                if (enableRecreateObjectCheck) {
                    // Fetching new object information.
                    // TODO/Entity:
                    IObjectProxy dbObject = enumerator.Current as IObjectProxy;

                    // Checking if its the same object.
                    // TODO/Entity:
                    // Enough to compare by identity, no?
                    if ((keyOID != dbObject.Identity) && (keyETI != dbObject.ThisHandle)) {
                        isAtRecreatedKey = false;
                        variableArray.FailedToRecreateObject = true;
                    } else isAtRecreatedKey = true;

                    // Disabling any further checks.
                    enableRecreateObjectCheck = false;
                    if (stayAtOffsetkey || !isAtRecreatedKey)
                        return true;
                    else
                        counter = 0;
                } else
                    return true;
            } else {
                enableRecreateObjectCheck = false;
                isAtRecreatedKey = false;
                currentObject = null;
                return false;
            }
        }


        if (counter >= fetchNumber) {
            currentObject = null;
            return false;
        }

        //if (HasCodeGeneration)
        //    return MoveNextCodeGen();

        return MoveNextManaged();
    }

    unsafe Byte* GetRecreationKeyFromKernel() {
        // Fetching current enumerator key.
        Byte* createdKey = null;
        UInt32 err = 0;

        // TODO/Entity:
        IObjectProxy dbObject = enumerator.CurrentRaw as IObjectProxy;
        if (dbObject != null)
            // Getting current position of the object in iterator.
            err = sccoredb.sc_get_index_position_key(
                indexInfo.Handle,
                dbObject.Identity,
                dbObject.ThisHandle,
                &createdKey
                );

        // Disposing iterator.
        enumerator.Dispose();
        // Checking the error.
        if (err != 0)
            throw ErrorCode.ToException(err);
        return createdKey;
    }

    public unsafe short SaveEnumerator(ref SafeTupleWriterBase64 enumerators, short expectedNodeId) {
        currentObject = null;
        Debug.Assert(expectedNodeId == nodeId);
        Debug.Assert(OffsetTuppleLength == 3);
        SafeTupleWriterBase64 tuple = new SafeTupleWriterBase64(enumerators.AtEnd, OffsetTuppleLength, OFFSETELEMNETSIZE, enumerators.AvailableSize);
        // Static data for validation
        tuple.WriteULong((byte)NodeType);
        tuple.WriteULong(nodeId);

        Byte* createdKey = GetRecreationKeyFromKernel();
        // Checking if it was last object.
        if (createdKey == null)
            return -1;
        // Copying the recreation key.
        UInt16 bytesWritten = *((UInt16*)createdKey);
        tuple.WriteByteArray(createdKey, bytesWritten);
        enumerators.HaveWritten(tuple.SealTuple());
        return (short)(expectedNodeId + 1);
    }

#if false // Old implementation
    /// <summary>
    /// Used to populate the recreation key.
    /// </summary>
    /// <param name="keyData">Pointer to the beginning of the key to populate.</param>
    /// <param name="globalOffset">The offset to place where to store static/dynamic data.</param>
    /// <param name="saveDynamicDataOnly">Specifies if dynamic or static data should be written.</param>
    /// <returns>The offset directly after data were stored or the offset to first dynamic data (reusing the key).</returns>
    public unsafe UInt16 SaveEnumerator(Byte* keyData, UInt16 globalOffset, Boolean saveDynamicDataOnly) {
        // Immediately preventing further accesses to current object.
        currentObject = null;

        // If we already tried to recreate the enumerator and we want to write static data,
        // just return first dynamic data offset.
        if (triedEnumeratorRecreation && (!saveDynamicDataOnly))
            return (*(UInt16*)(keyData + IteratorHelper.RK_FIRST_DYN_DATA_OFFSET));

        UInt16 origGlobalOffset = globalOffset;

        // Position of enumerator.
        UInt16 enumGlobalOffset = (ushort)((nodeId << 2) + IteratorHelper.RK_HEADER_LEN);

        // Writing static data.
        if (!saveDynamicDataOnly) {
            // In order to exclude double copy of last key.
            //rangeChanged = false;

            // Emptying static data position for this enumerator.
            (*(UInt16*)(keyData + enumGlobalOffset)) = 0;

            // Saving type of this node
            *((byte*)(keyData + globalOffset)) = (byte)NodeType;
            globalOffset += 1;

            // Saving position of the data for current extent.
            (*(UInt16*)(keyData + enumGlobalOffset)) = origGlobalOffset;

            // Saving absolute position of the first dynamic data.
            (*(UInt16*)(keyData + IteratorHelper.RK_FIRST_DYN_DATA_OFFSET)) = globalOffset;
        } else {
            // Writing dynamic data.

            // Points to dynamic data offset.
            UInt16* dynDataOffset = (UInt16*)(keyData + enumGlobalOffset + 2);

            // Emptying dynamic data position for this enumerator.
            (*dynDataOffset) = 0;

            // Fetching current enumerator key.
            Byte* createdKey = null;
            UInt32 err = 0;

            // TODO/Entity:
            IObjectProxy dbObject = enumerator.CurrentRaw as IObjectProxy;
            if (dbObject != null) {
                // Getting current position of the object in iterator.
                err = sccoredb.sc_get_index_position_key(
                    indexInfo.Handle,
                    dbObject.Identity,
                    dbObject.ThisHandle,
                    &createdKey
                    );

#if false                    // Fetching the local time of the iterator explicitly.                    UInt32 itLocalTime = enumerator.GetIteratorLocalTime();                    // Placing local time of the iterator into recreation key.                    (*(UInt32*)(createdKey + (*(UInt32*)createdKey) - 4)) = itLocalTime;#endif
            }

            // Disposing iterator.
            enumerator.Dispose();

            // Checking the error.
            if (err != 0)
                throw ErrorCode.ToException(err);

            // Since iterator is closed we have to mark it as so.
            //enumerator.MarkAsDisposed();

            // Checking if it was last object.
            if (createdKey == null) {
                Debug.Assert(origGlobalOffset == globalOffset);
                return origGlobalOffset;
            }

            // Copying the recreation key.
            UInt16 bytesWritten = *((UInt16*)createdKey);

            // Copying the buffer.
            //SqlDebugHelper.PrintByteBuffer("IndexScan Saving Recreation Key", createdKey, true);
            Kernel32.MoveByteMemory(keyData + globalOffset, createdKey, bytesWritten);
            globalOffset += bytesWritten;

            // Saving position of dynamic data.
            (*dynDataOffset) = origGlobalOffset;
        }

        return globalOffset;
    }#endif    /// <summary>    /// Moves data and updates data offsets for all enumerators.    /// </summary>    public static unsafe void MoveEnumStaticData(Byte* keyData, Int32 bytesToMove, Int32 oldDataEndPos, Int32 shiftBytesNum)    {        // Shifting memory around the ending of old static data.        Kernel32.MoveByteMemory(keyData + oldDataEndPos + shiftBytesNum,            keyData + oldDataEndPos,            bytesToMove);        // Calculating number of enumerators and updates.        Int32 numEnumerators = (*(Int32*)(keyData + 4));        Int32 totalUpdates = (numEnumerators << 1) + 1;        // Calculating data offset positions.        Int32* offsetPos = (Int32*)(keyData + 8);        for (Int32 i = 0; i < totalUpdates; i++)        {            // Checking if positions are after the extended data.            if ((*offsetPos) >= oldDataEndPos)                (*offsetPos) += shiftBytesNum;            offsetPos++;        }    }
    public Boolean MoveNextSpecial(Boolean force)    {        if (!force && MoveNext()) return true;        if (counter == 0 || force)        {            // Create a "null" object.            NullObject nullObj = new NullObject(rowTypeBinding.GetTypeBinding(extentNumber));            currentObject = new Row(rowTypeBinding);            currentObject.AttachObject(extentNumber, nullObj);            counter++;            return true;        }        currentObject = null;        return false;    }    /// <summary>    /// Resets the enumerator with a context object.    /// </summary>    /// <param name="obj">Context object from another enumerator.</param>    public override void Reset(Row obj)    {        if (HasCodeGeneration)        {            if (enumeratorCreated)                NewCodeGen.NewCodeGen_Reset(UniqueQueryID);        }        else        {            enumerator.Dispose();        }        enableRecreateObjectCheck = false;        enumeratorCreated = false;        //rangeChanged = false;        currentObject = null;        contextObject = obj;        counter = 0;        //triedEnumeratorRecreation = false;        // Checking the context object.        if (contextObject == null)        {            // Updating the enumerator filter to its default state.            enumerator.UpdateFilter(callbackManagedFilterCached);
            isAtRecreatedKey = false;
            stayAtOffsetkey = false;
            useOffsetkey = true;
        }        else        {            // We need to update filter if we have joins for example.            enumerator.UpdateFilter(postFilterCondition.Instantiate(contextObject).Filtrate);            // Switching the range recalculation automatically.            shouldRecalculateRange = true;        }    }    public override IExecutionEnumerator Clone(RowTypeBinding typeBindingClone, VariableArray varArrClone)    {        List<IDynamicRange> dynamicRangeListClone = new List<IDynamicRange>();        for (Int32 i = 0; i < dynamicRangeList.Count; i++)        {            dynamicRangeListClone.Add(dynamicRangeList[i].Clone(varArrClone));        }        SortOrder sortOrder = (descending == true ? SortOrder.Descending : SortOrder.Ascending);        INumericalExpression fetchNumberExprClone = null;        if (fetchNumberExpr != null)            fetchNumberExprClone = fetchNumberExpr.CloneToNumerical(varArrClone);        INumericalExpression fetchOffsetExprClone = null;        if (fetchOffsetExpr != null)            fetchOffsetExprClone = fetchOffsetExpr.CloneToNumerical(varArrClone);        IBinaryExpression fetchOffsetKeyExprClone = null;        if (fetchOffsetKeyExpr != null)            fetchOffsetKeyExprClone = fetchOffsetKeyExpr.CloneToBinary(varArrClone);        // NOTE: Variables array is supplied only during the cloning but not on initial creation.        return new IndexScan(nodeId,             typeBindingClone,            extentNumber,            indexInfo,            strPathList,            dynamicRangeListClone,            postFilterCondition.Clone(varArrClone),            sortOrder,            fetchNumberExprClone,            fetchOffsetExprClone,            fetchOffsetKeyExprClone,            innermostExtent,            varArrClone, query, TopNode);    }    public override void BuildString(MyStringBuilder stringBuilder, Int32 tabs)    {        stringBuilder.AppendLine(tabs, "IndexScan(");        //stringBuilder.AppendLine(tabs + 1, indexHandle.ToString());
        stringBuilder.AppendLine(tabs + 1, indexInfo.Name + " ON " + indexInfo.TableName);        stringBuilder.AppendLine(tabs + 1, extentNumber.ToString());        for (Int32 i = 0; i < strPathList.Count; i++)        {            stringBuilder.AppendLine(tabs + 1, strPathList[i]);            dynamicRangeList[i].BuildString(stringBuilder, tabs + 1);        }        postFilterCondition.BuildString(stringBuilder, tabs + 1);        if (descending)            stringBuilder.AppendLine(tabs + 1, "Descending");        else            stringBuilder.AppendLine(tabs + 1, "Ascending");
        base.BuildFetchString(stringBuilder, tabs + 1);
        stringBuilder.AppendLine(tabs, ")");    }    /// <summary>    /// Generates compilable code representation of this data structure.    /// </summary>    public void GenerateCompilableCode(CodeGenStringGenerator stringGen)    {        // Generating variables handling code.        VariableArray.GenerateQueryParamsCode(stringGen, variableArray);        String _num = "_" + extentNumber;        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.GLOBAL_DATA, "INTERNAL_DATA Scan *g_Scan" + extentNumber + " = 0;" + CodeGenStringGenerator.ENDL);        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.INIT_DATA, "g_Scan" + extentNumber + " = new Scan(" + indexInfo.Handle + ", Scan" + extentNumber + "_CalculateRange);");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.DECLARATIONS, "INTERNAL_FUNCTION INT32 Scan" + extentNumber + "_MoveNext();");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "INTERNAL_FUNCTION INT32 Scan" + extentNumber + "_MoveNext()");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "{");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "return g_Scan" + extentNumber + "->MoveNext();");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "}" + CodeGenStringGenerator.ENDL);        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.DECLARATIONS, "INTERNAL_FUNCTION INT32 Scan" + extentNumber + "_Reset();");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "INTERNAL_FUNCTION INT32 Scan" + extentNumber + "_Reset()");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "{");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "return g_Scan" + extentNumber + "->Reset();");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "}" + CodeGenStringGenerator.ENDL);        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.DECLARATIONS, "INTERNAL_FUNCTION VOID Scan" + extentNumber + "_CalculateRange(ScanRange *range);");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "INTERNAL_FUNCTION VOID Scan" + extentNumber + "_CalculateRange(ScanRange *range)");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "{");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "range->AppendData(g_QueryParam0, g_IsNullQueryParam0, true);");        stringGen.AppendLine(CodeGenStringGenerator.CODE_SECTION_TYPE.FUNCTIONS, "}" + CodeGenStringGenerator.ENDL);    }    /// <summary>    /// Gets the unique name for this enumerator.    /// </summary>    public String GetUniqueName(UInt64 seqNumber)    {        if (uniqueGenName == null)            uniqueGenName = "Scan" + extentNumber;        return uniqueGenName;    }}}