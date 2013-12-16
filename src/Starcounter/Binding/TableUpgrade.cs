// ***********************************************************************
// <copyright file="TableUpgrade.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Internal;
using System;
using System.Collections.Generic;

namespace Starcounter.Binding
{

    /// <summary>
    /// Class TableUpgrade
    /// </summary>
    public class TableUpgrade
    {

        /// <summary>
        /// The pending update table name prefix
        /// </summary>
        public const string PendingUpdateTableNamePrefix = "0";

        /// <summary>
        /// Creates the name of the pending update table.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        public static string CreatePendingUpdateTableName(string name)
        {
            // TODO: Restrict name size and format on non upgrade table.
            return string.Concat(PendingUpdateTableNamePrefix, name); 
        }

        /// <summary>
        /// Delegate RecordHandler
        /// </summary>
        /// <param name="obj">The obj.</param>
        private delegate void RecordHandler(ObjectRef obj);

        /// <summary>
        /// The table name_
        /// </summary>
        private readonly string tableName_;
        /// <summary>
        /// The old table def_
        /// </summary>
        private readonly TableDef oldTableDef_;
        /// <summary>
        /// The new table def_
        /// </summary>
        private TableDef newTableDef_;

        /// <summary>
        /// The column value transfer set_
        /// </summary>
        private ColumnValueTransfer[] columnValueTransferSet_;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableUpgrade" /> class.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="oldTableDef">The old table def.</param>
        /// <param name="newTableDef">The new table def.</param>
        public TableUpgrade(string tableName, TableDef oldTableDef, TableDef newTableDef)
        {
            tableName_ = tableName;
            oldTableDef_ = oldTableDef;
            newTableDef_ = newTableDef;
        }

        /// <summary>
        /// Evals this instance.
        /// </summary>
        /// <returns>TableDef.</returns>
        public TableDef Eval()
        {
            CreateNewTable();

            // We do the inheriting tables first so that only the records of
            // current table remains when we scan the,

            TableDef[] directlyInheritedTableDefs = GetDirectlyInheritedTableDefs(oldTableDef_.TableId);
            for (int i = 0; i < directlyInheritedTableDefs.Length; i++)
            {
                UpgradeInheritingTable(directlyInheritedTableDefs[i]);
            }

            BuildColumnValueTransferSet();

            MoveIndexesToNewTable();

            MoveRecordsToNewTable();

            DropOldTable();

            RenameNewTable();

            Db.Transaction(() =>
            {
                newTableDef_ = Db.LookupTable(tableName_);
            });

            return newTableDef_;
        }

        /// <summary>
        /// Continues the eval.
        /// </summary>
        /// <returns>TableDef.</returns>
        public TableDef ContinueEval()
        {
            if (oldTableDef_ != null)
            {
                // Start or conclude any upgrades on inherited tables.
                
                // If an inherited table already has been upgraded it won't
                // show in this list since it will no longer inherit the old
                // table but will instead inherit the new table.

                TableDef[] directlyInheritedTableDefs;
                directlyInheritedTableDefs = GetDirectlyInheritedTableDefs(oldTableDef_.TableId);
                for (int i = 0; i < directlyInheritedTableDefs.Length; i++)
                {
                    ContinueUpgradeInheritingTable(directlyInheritedTableDefs[i]);
                }

                // If an upgrade of an inherited table is concluded but the
                // table rename was not completed we will have a table
                // inheriting the new table but with an illegal name. So we
                // check all the tables inheriting the new table and make sure
                // they have the final name.
                //
                // This we do not have to do recursivly since if the replaced
                // inherited table is dropped then all upgrades on table
                // inherting this table will have been completed.

                directlyInheritedTableDefs = GetDirectlyInheritedTableDefs(newTableDef_.TableId);
                for (int i = 0; i < directlyInheritedTableDefs.Length; i++)
                {
                    var directlyInheritedTableDef = directlyInheritedTableDefs[i];
                    var inheritedTableName = directlyInheritedTableDef.Name;
                    if (inheritedTableName.StartsWith(PendingUpdateTableNamePrefix))
                    {
                        inheritedTableName = inheritedTableName.Substring(PendingUpdateTableNamePrefix.Length);
                        Db.RenameTable(directlyInheritedTableDef.TableId, inheritedTableName);
                    }
                }

                BuildColumnValueTransferSet();

                MoveIndexesToNewTable();

                MoveRecordsToNewTable();

                DropOldTable();
            }

            RenameNewTable();

            Db.Transaction(() =>
            {
                newTableDef_ = Db.LookupTable(tableName_);
            });

            return newTableDef_;
        }

        /// <summary>
        /// Creates the new table.
        /// </summary>
        private void CreateNewTable()
        {
            newTableDef_ = newTableDef_.Clone();
            newTableDef_.Name = CreatePendingUpdateTableName(newTableDef_.Name);
            var tableCreate = new TableCreate(newTableDef_);
            newTableDef_ = tableCreate.Eval();
        }

        /// <summary>
        /// Upgrades the inheriting table.
        /// </summary>
        /// <param name="oldInheritingTableDef">The old inheriting table def.</param>
        private void UpgradeInheritingTable(TableDef oldInheritingTableDef)
        {
            List<ColumnDef> newColumnDefs = new List<ColumnDef>();
            ColumnDef[] inheritedColumnDefs = newTableDef_.ColumnDefs;
            for (int i = 0; i < inheritedColumnDefs.Length; i++)
            {
                var inheritedColumnDef = inheritedColumnDefs[i].Clone();
                inheritedColumnDef.IsInherited = true;
                newColumnDefs.Add(inheritedColumnDef);
            }

            ColumnDef[] columnDefs = oldInheritingTableDef.ColumnDefs;
            for (int i = oldTableDef_.ColumnDefs.Length; i < columnDefs.Length; i++)
            {
                newColumnDefs.Add(columnDefs[i].Clone());
            }

            string tableName = oldInheritingTableDef.Name;

            TableDef newInheritingTableDef = new TableDef(
                tableName,
                newTableDef_.Name,
                newColumnDefs.ToArray()
                );

            var tableUpgrade = new TableUpgrade(tableName, oldInheritingTableDef, newInheritingTableDef);
            tableUpgrade.Eval();
        }

        /// <summary>
        /// Continues the upgrade inheriting table.
        /// </summary>
        /// <param name="oldInheritingTableDef">The old inheriting table def.</param>
        private void ContinueUpgradeInheritingTable(TableDef oldInheritingTableDef)
        {
            var tableName = oldInheritingTableDef.Name;
            TableDef newInheritingTableDef = null;

            Db.Transaction(() =>
            {
                newInheritingTableDef = Db.LookupTable(CreatePendingUpdateTableName(tableName));
            });

            if (newInheritingTableDef != null)
            {
                var tableUpgrade = new TableUpgrade(tableName, oldInheritingTableDef, newInheritingTableDef);
                tableUpgrade.ContinueEval();
            }
            else
            {
                UpgradeInheritingTable(oldInheritingTableDef);
            }
        }

        /// <summary>
        /// Builds the column value transfer set.
        /// </summary>
        /// <exception cref="System.NotSupportedException"></exception>
        private void BuildColumnValueTransferSet()
        {
            List<ColumnValueTransfer> output = new List<ColumnValueTransfer>();

            ColumnDef[] oldColumnDefs = oldTableDef_.ColumnDefs;
            ColumnDef[] newColumnDefs = newTableDef_.ColumnDefs;

            for (int oi = 0; oi < oldColumnDefs.Length; oi++)
            {
                var oldColumnDef = oldColumnDefs[oi];
                for (int ni = 0; ni < newColumnDefs.Length; ni++)
                {
                    var newColumnDef = newColumnDefs[ni];
                    if (oldColumnDef.Equals(newColumnDef))
                    {
                        switch (newColumnDef.Type)
                        {
#if false
                        case DbTypeCode.Boolean:
                            output.Add(new BooleanColumnValueTransfer(oi, ni));
                            break;
#endif
                        case sccoredb.STAR_TYPE_ULONG:
                            output.Add(new UInt64ColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_DECIMAL:
                            output.Add(new DecimalColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_FLOAT:
                            output.Add(new SingleColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_DOUBLE:
                            output.Add(new DoubleColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_LONG:
                            output.Add(new Int64ColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_REFERENCE:
                            output.Add(new ObjectColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_STRING:
                            output.Add(new StringColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_BINARY:
                            output.Add(new BinaryColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_LBINARY:
                            output.Add(new LargeBinaryColumnValueTransfer(oi, ni));
                            break;
                        case sccoredb.STAR_TYPE_KEY: break;
                        default:
                            throw new NotSupportedException();
                        }
                    }
                }
            }

            columnValueTransferSet_ = output.ToArray();
        }

        /// <summary>
        /// Moves all existing indexes to the new table.
        /// </summary>
        /// <remarks>
        /// If an index is specified on column(s) that no longer exists the index will be dropped.
        /// </remarks>
        private void MoveIndexesToNewTable() 
        {
            short[] attrIndexArr;
            bool createIndex;
            uint ec;
            sccoredb.SC_INDEX_INFO index;
            sccoredb.SC_INDEX_INFO[] indexArr;
            uint indexCount;
            ColumnDef newColumn;
            ColumnDef oldColumn;
            string[] indexNameArr;
            
            unsafe 
            {
                ec = sccoredb.sccoredb_get_index_infos(
                    oldTableDef_.TableId,
                    &indexCount,
                    null
                    );
                if (ec != 0) throw ErrorCode.ToException(ec);
                if (indexCount == 0) return;

                indexArr = new sccoredb.SC_INDEX_INFO[indexCount];
                fixed (sccoredb.SC_INDEX_INFO* pii = &(indexArr[0])) 
                {
                    ec = sccoredb.sccoredb_get_index_infos(
                        oldTableDef_.TableId,
                        &indexCount,
                        pii
                        );
                }
                if (ec != 0) throw ErrorCode.ToException(ec);

                // The names of all indexes is saved in the internal threadbuffer which is also used by 
                // the createindex function, so we need to store them locally before we create any new indexes.
                indexNameArr = new string[indexCount];
                for (int i = 0; i < indexCount; i++) {
                    indexNameArr[i] = new string(indexArr[i].name);
                }

                for (int i = 0; i < indexCount; i++)
                {
                    index = indexArr[i];
                    createIndex = true;
                    attrIndexArr = new short[index.attributeCount + 1];
                    attrIndexArr[attrIndexArr.Length - 1] = -1; // Terminator.

                    for (int oai = 0; oai < index.attributeCount; oai++) 
                    {
                        oldColumn = null;
                        switch (oai) {
                            case 0:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_0];
                                break;
                            case 1:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_1];
                                break;
                            case 2:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_2];
                                break;
                            case 3:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_3];
                                break;
                            case 4:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_4];
                                break;
                            case 5:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_5];
                                break;
                            case 6:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_6];
                                break;
                            case 7:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_7];
                                break;
                            case 8:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_8];
                                break;
                            case 9:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_9];
                                break;
                            case 10:
                                oldColumn = oldTableDef_.ColumnDefs[index.attrIndexArr_10];
                                break;
                        }

                        // Find the new column
                        short newAttributeIndex = -1;
                        for (short nai = 0; nai < newTableDef_.ColumnDefs.Length; nai++) 
                        {
                            newColumn = newTableDef_.ColumnDefs[nai];
                            if (newColumn.Name.Equals(oldColumn.Name)) 
                            {
                                newAttributeIndex = nai;
                                break;
                            }
                        }

                        if (newAttributeIndex == -1) 
                        {
                            createIndex = false;
                            break;
                        }
                        attrIndexArr[oai] = newAttributeIndex;
                    } // End for attributeCount.

                    if (createIndex) 
                    {
                        fixed (Int16* paii = &(attrIndexArr[0])) 
                        {
                            ec = sccoredb.star_create_index(0, newTableDef_.TableId, indexNameArr[i], index.sortMask, paii, index.flags);
                        }

                        if (ec != 0) 
                        {
                            if (ec == Error.SCERRNAMEDINDEXALREADYEXISTS)
                                continue;
                            throw ErrorCode.ToException(ec);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Moves the records to new table.
        /// </summary>
        private void MoveRecordsToNewTable()
        {
            var indexInfo = oldTableDef_.GetAllIndexInfos()[0];
            Byte[] lk, hk;
            BuildScanRangeKeys(indexInfo, out lk, out hk);
            var recordHandler = new RecordHandler(MoveRecord);
            ulong c = 0;
            do
            {
                Db.Transaction(() =>
                {
                    c = ScanDo(indexInfo.Handle, lk, hk, 1000, recordHandler);
                });
            }
            while (c != 0);
        }

        /// <summary>
        /// </summary>
        private unsafe TableDef[] GetDirectlyInheritedTableDefs(ushort baseTableId)
        {
            TableDef[] output = null;

            Db.Transaction(() =>
            {
                sccoredb.SCCOREDB_TABLE_INFO tableInfo;
                sccoredb.sccoredb_get_table_info(baseTableId, out tableInfo);

                var tableIds = new ushort[tableInfo.inheriting_table_count];
                for (var i = 0; i < tableIds.Length; i++)
                {
                    tableIds[i] = tableInfo.inheriting_table_ids[i];
                }

                var tableDefs = new List<TableDef>((int)tableInfo.inheriting_table_count);
                for (var i = 0; i < tableIds.Length; i++)
                {
                    var tableId = tableIds[i];
                    sccoredb.sccoredb_get_table_info(tableId, out tableInfo);
                    if (tableInfo.inherited_table_id == baseTableId)
                    {
                        tableDefs.Add(TableDef.ConstructTableDef(tableInfo));
                    }
                }

                output = tableDefs.ToArray();
            });

            return output;
        }

        /// <summary>
        /// Drops the old table.
        /// </summary>
        private void DropOldTable()
        {
            Db.DropTable(oldTableDef_.Name);
        }

        /// <summary>
        /// Renames the new table.
        /// </summary>
        private void RenameNewTable()
        {
            Db.RenameTable(newTableDef_.TableId, tableName_);
        }

        /// <summary>
        /// Moves the record.
        /// </summary>
        /// <param name="source">The source.</param>
        private void MoveRecord(ObjectRef source)
        {
            ColumnValueTransfer[] columnValueTransfers = columnValueTransferSet_;
            for (int i = 0; i < columnValueTransfers.Length; i++)
            {
                var columnValueTransfer = columnValueTransfers[i];
                columnValueTransfer.Read(source);
            }

            uint e;
            unsafe
            {
                e = sccoredb.sccoredb_replace(source.ObjectID, source.ETI, newTableDef_.TableId);
            }
            if (e == 0)
            {
                ObjectRef target = source;
                for (int i = 0; i < columnValueTransfers.Length; i++)
                {
                    var columnValueTransfer = columnValueTransfers[i];
                    columnValueTransfer.Write(target);
                }
            }
            else
            {
                throw ErrorCode.ToException(e);
            }
        }

        /// <summary>
        /// Builds the scan range keys.
        /// </summary>
        /// <param name="indexInfo">The index info.</param>
        /// <param name="lk">The lk.</param>
        /// <param name="hk">The hk.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <exception cref="System.NotSupportedException"></exception>
        private unsafe void BuildScanRangeKeys(IndexInfo indexInfo, out Byte[] lk, out Byte[] hk)
        {
            lk = new Byte[64];
            hk = new Byte[64];
            
            fixed (byte* ulk = lk, uhk = hk)
            {
                byte* l = ulk; l += 4;
                byte* h = uhk; h += 4;

                for (int i = 0; i < indexInfo.AttributeCount; i++)
                {
                    // TODO: Handle sort order. Not all key types are supported.
                    *l = 0; l++;
                    *h = 1; h++;
                    switch (indexInfo.GetColumnType(i))
                    {
                        case sccoredb.STAR_TYPE_ULONG:
                        case sccoredb.STAR_TYPE_REFERENCE:
                        case sccoredb.STAR_TYPE_KEY:
                            *((ulong*)h) = ulong.MaxValue; h += 8;
                            break;
                        case sccoredb.STAR_TYPE_DECIMAL:
                            throw new NotImplementedException();
                        case sccoredb.STAR_TYPE_LONG:
                            *((long*)h) = long.MaxValue; h += 8;
                            break;
                        case sccoredb.STAR_TYPE_STRING:
                            byte* s;
                            sccoredb.SCConvertUTF16StringToNative("", 1, &s);
                            uint sl = *((uint*)s) + 4;
                            for (uint si = 0; si < sl; si++) *h++ = *s++;
                            break;
                        case sccoredb.STAR_TYPE_BINARY:
                            throw new NotImplementedException();
                        default:
                            throw new NotSupportedException();
                    }
                }

                *((uint*)ulk) = (uint)(l - ulk);
                *((uint*)uhk) = (uint)(h - uhk);
            }
        }

        /// <summary>
        /// Scans the do.
        /// </summary>
        /// <param name="indexHandle">The index handle.</param>
        /// <param name="lk">The lk.</param>
        /// <param name="hk">The hk.</param>
        /// <param name="max">The max.</param>
        /// <param name="handler">The handler.</param>
        /// <returns>System.UInt64.</returns>
        private unsafe ulong ScanDo(ulong indexHandle, byte[] lk, byte[] hk, ulong max, RecordHandler handler)
        {
            uint e;

            ushort filterTableId = oldTableDef_.TableId;

            ulong count = 0;

            ulong hiter;
            ulong viter;
            fixed (byte* ulk = lk, uhk = hk)
            {
                e = sccoredb.SCIteratorCreate(
                    indexHandle,
                    0,
                    ulk,
                    uhk,
                    &hiter,
                    &viter
                    );
            }
            if (e == 0)
            {
                try
                {
                    for (; ; )
                    {
                        ObjectRef source;
                        ushort tableId;
                        ulong dummy;

                        e = sccoredb.SCIteratorNext(hiter, viter, &source.ObjectID, &source.ETI, &tableId, &dummy);
                        if (e == 0)
                        {
                            if (source.ObjectID != sccoredb.MDBIT_OBJECTID)
                            {
                                if (tableId == filterTableId)
                                {
                                    handler(source);
                                    if (++count == max) break;
                                }
                            }
                            else break;
                        }
                        else break;
                    }
                }
                finally
                {
                    e = sccoredb.SCIteratorFree(hiter, viter);
                }
            }
            if (e != 0) throw ErrorCode.ToException(e);
            return count;
        }
    }


    /// <summary>
    /// Class ColumnValueTransfer
    /// </summary>
    internal abstract class ColumnValueTransfer
    {

        /// <summary>
        /// Class UpgradeRecord
        /// </summary>
        internal sealed class UpgradeRecord
        {
            // Psa 21/3 2013: Couldn't see in what way this class
            // had to extend Entity since all it does is to keep a
            // reference to an ObjectRef.
            //   Check this, because it's probably something I have
            // missed or don't understand with the design.
            public ObjectRef ThisRef;
        }

        /// <summary>
        /// The rec_
        /// </summary>
        protected UpgradeRecord rec_;
        /// <summary>
        /// The source index_
        /// </summary>
        protected readonly int sourceIndex_;
        /// <summary>
        /// The target index_
        /// </summary>
        protected readonly int targetIndex_;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public ColumnValueTransfer(int sourceIndex, int targetIndex)
        {
            rec_ = new UpgradeRecord();
            sourceIndex_ = sourceIndex;
            targetIndex_ = targetIndex;
        }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public abstract void Read(ObjectRef source);

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public abstract void Write(ObjectRef target);
    }

    /// <summary>
    /// Class BooleanColumnValueTransfer
    /// </summary>
    internal class BooleanColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private bool? value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="BooleanColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public BooleanColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadNullableBoolean(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            if (value_.HasValue)
                DbState.WriteBoolean(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_.Value);
        }
    }

    /// <summary>
    /// Class BinaryColumnValueTransfer
    /// </summary>
    internal class BinaryColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private Binary value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public BinaryColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadBinary(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            DbState.WriteBinary(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_);
        }
    }

    /// <summary>
    /// Class DecimalColumnValueTransfer
    /// </summary>
    internal class DecimalColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private decimal? value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="DecimalColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public DecimalColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadNullableDecimal(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            if (value_.HasValue)
                DbState.WriteDecimal(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_.Value);
        }
    }

    /// <summary>
    /// Class DoubleColumnValueTransfer
    /// </summary>
    internal class DoubleColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private double? value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public DoubleColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadNullableDouble(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            if (value_.HasValue)
                DbState.WriteDouble(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_.Value);
        }
    }

    /// <summary>
    /// Class Int64ColumnValueTransfer
    /// </summary>
    internal class Int64ColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private long? value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="Int64ColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public Int64ColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadNullableInt64(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            if (value_.HasValue)
                DbState.WriteInt64(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_.Value);
        }
    }

    /// <summary>
    /// Class LargeBinaryColumnValueTransfer
    /// </summary>
    internal class LargeBinaryColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private LargeBinary value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="LargeBinaryColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public LargeBinaryColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadLargeBinary(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            DbState.WriteLargeBinary(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_);
        }
    }

    /// <summary>
    /// Class ObjectColumnValueTransfer
    /// </summary>
    internal class ObjectColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private ObjectRef value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public ObjectColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            value_ = DoRead(source);
        }

        /// <summary>
        /// Does the read.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>ObjectRef.</returns>
        private ObjectRef DoRead(ObjectRef source)
        {
            UInt16 flags;
            ObjectRef value;
            UInt16 cci;
            UInt32 ec;
            flags = 0;
            unsafe
            {
                sccoredb.Mdb_ObjectReadObjRef(
                    source.ObjectID,
                    source.ETI,
                    sourceIndex_,
                    &value.ObjectID,
                    &value.ETI,
                    &cci,
                    &flags
                );
            }
            if ((flags & sccoredb.Mdb_DataValueFlag_Exceptional) == 0)
            {
                if ((flags & sccoredb.Mdb_DataValueFlag_Null) == 0)
                {
                    return value;
                }
                else
                {
                    value.ObjectID = sccoredb.MDBIT_OBJECTID;
                    value.ETI = sccoredb.INVALID_RECORD_ADDR;
                    return value;
                }
            }
            ec = sccoredb.star_get_last_error();
            throw ErrorCode.ToException(ec);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            if (value_.ObjectID != sccoredb.MDBIT_OBJECTID)
            {
                Boolean br;
                br = sccoredb.Mdb_ObjectWriteObjRef(
                         target.ObjectID,
                         target.ETI,
                         targetIndex_,
                         value_.ObjectID,
                         value_.ETI
                     );
                if (br)
                {
                    return;
                }
                throw ErrorCode.ToException(sccoredb.star_get_last_error());
            }
        }
    }

    /// <summary>
    /// Class SingleColumnValueTransfer
    /// </summary>
    internal class SingleColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private float? value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public SingleColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadNullableSingle(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            if (value_.HasValue)
                DbState.WriteSingle(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_.Value);
        }
    }

    /// <summary>
    /// Class StringColumnValueTransfer
    /// </summary>
    internal class StringColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private string value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public StringColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadString(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            DbState.WriteString(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_);
        }
    }

    /// <summary>
    /// Class UInt64ColumnValueTransfer
    /// </summary>
    internal class UInt64ColumnValueTransfer : ColumnValueTransfer
    {

        /// <summary>
        /// The value_
        /// </summary>
        private ulong? value_;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt64ColumnValueTransfer" /> class.
        /// </summary>
        /// <param name="sourceIndex">Index of the source.</param>
        /// <param name="targetIndex">Index of the target.</param>
        public UInt64ColumnValueTransfer(int sourceIndex, int targetIndex) : base(sourceIndex, targetIndex) { }

        /// <summary>
        /// Reads the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        public override void Read(ObjectRef source)
        {
            rec_.ThisRef = source;
            value_ = DbState.ReadNullableUInt64(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, sourceIndex_);
        }

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        public override void Write(ObjectRef target)
        {
            rec_.ThisRef = target;
            if (value_.HasValue)
                DbState.WriteUInt64(rec_.ThisRef.ObjectID, rec_.ThisRef.ETI, targetIndex_, value_.Value);
        }
    }
}
