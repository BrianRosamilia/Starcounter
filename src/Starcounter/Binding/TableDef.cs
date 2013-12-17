// ***********************************************************************
// <copyright file="TableDef.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Query.Execution;
using Starcounter.Internal;
using System;
using System.Text;
using System.Diagnostics;

namespace Starcounter.Binding
{

    /// <summary>
    /// Definition of a database table.
    /// </summary>
    public sealed class TableDef
    {

        /// <summary>
        /// </summary>
        internal unsafe static TableDef ConstructTableDef(sccoredb.SCCOREDB_TABLE_INFO tableInfo) {
            string name = new String(tableInfo.name);
            ushort tableId = tableInfo.table_id;
            uint columnCount = tableInfo.column_count;
            string baseName = null;

            if (tableInfo.inherited_table_id != ushort.MaxValue) {
                var r = sccoredb.sccoredb_get_table_info(tableInfo.inherited_table_id, out tableInfo);
                if (r == 0) {
                    baseName = new String(tableInfo.name);
                }
                else {
                    throw ErrorCode.ToException(r);
                }
            }

            ColumnDef[] columns = new ColumnDef[columnCount];
            for (ushort i = 0; i < columns.Length; i++) {
                sccoredb.SCCOREDB_COLUMN_INFO columnInfo;
                var r = sccoredb.sccoredb_get_column_info(tableId, i, out columnInfo);
                if (r == 0) {
                    columns[i] = new ColumnDef(
                        new string(columnInfo.name),
                        columnInfo.type,
                        (columnInfo.flags & sccoredb.MDB_ATTRFLAG_NULLABLE) != 0,
                        (columnInfo.flags & sccoredb.MDB_ATTRFLAG_DERIVED) != 0
                        );
                }
                else {
                    throw ErrorCode.ToException(r);
                }
            }
            return new TableDef(name, baseName, columns, tableId);
        }

        /// <summary>
        /// The name
        /// </summary>
        public string Name;

        /// <summary>
        /// The base name
        /// </summary>
        public string BaseName;

        /// <summary>
        /// The column defs
        /// </summary>
        public ColumnDef[] ColumnDefs;

        /// <summary>
        /// The table id
        /// </summary>
        public ushort TableId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TableDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="columnsDefs">The columns defs.</param>
        public TableDef(string name, ColumnDef[] columnsDefs) : this(name, null, columnsDefs, 0xFFFF) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="baseName">Name of the base.</param>
        /// <param name="columnsDefs">The columns defs.</param>
        public TableDef(string name, string baseName, ColumnDef[] columnsDefs) : this(name, baseName, columnsDefs, 0xFFFF) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableDef" /> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="baseName">Name of the base.</param>
        /// <param name="columnsDefs">The columns defs.</param>
        /// <param name="tableId">The table id.</param>
        public TableDef(string name, string baseName, ColumnDef[] columnsDefs, ushort tableId)
        {
            Name = name;
            BaseName = baseName;
            ColumnDefs = columnsDefs;

            TableId = tableId;
        }

        /// <summary>
        /// Gets the short name.
        /// </summary>
        /// <value>The short name.</value>
        public string ShortName
        {
            get
            {
                var i = Name.LastIndexOf('.');
                if (i >= 0)
                {
                    return Name.Substring(i + 1);
                }
                return Name;
            }
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns>TableDef.</returns>
        public TableDef Clone()
        {
            ColumnDef[] clonedColumnDefs = new ColumnDef[ColumnDefs.Length];
            for (int i = 0; i < ColumnDefs.Length; i++)
            {
                clonedColumnDefs[i] = ColumnDefs[i].Clone();
            }
            return new TableDef(Name, BaseName, clonedColumnDefs, TableId);
        }

        /// <summary>
        /// Equalses the specified table def.
        /// </summary>
        /// <param name="tableDef">The table def.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Equals(TableDef tableDef)
        {
            bool b =
                Name == tableDef.Name &&
                BaseName == tableDef.BaseName &&
                ColumnDefs.Length == tableDef.ColumnDefs.Length
                ;
            if (b)
            {
                for (int i = 0; i < ColumnDefs.Length; i++)
                {
                    b = ColumnDefs[i].Equals(tableDef.ColumnDefs[i]);
                    if (!b) break;
                }
            }
            return b;
        }

        /// <summary>
        /// Gets all index infos.
        /// </summary>
        /// <returns>IndexInfo[][].</returns>
        internal IndexInfo[] GetAllIndexInfos()
        {
            uint ec;
            uint ic;
            sccoredb.SC_INDEX_INFO[] iis;
            IndexInfo[] iil;

            unsafe
            {
                ec = sccoredb.sccoredb_get_index_infos(
                    TableId,
                    &ic,
                    null
                    );
                if (ec != 0)
                {
                    throw ErrorCode.ToException(ec);
                }
                if (ic == 0)
                {
                    return new IndexInfo[0];
                }

                iis = new sccoredb.SC_INDEX_INFO[ic];
                fixed (sccoredb.SC_INDEX_INFO* pii = &(iis[0]))
                {
                    ec = sccoredb.sccoredb_get_index_infos(
                        TableId,
                        &ic,
                        pii
                        );
                    if (ec != 0)
                    {
                        throw ErrorCode.ToException(ec);
                    }

                    iil = new IndexInfo[ic];
                    for (int i = 0; i < ic; i++)
                    {
                        iil[i] = CreateIndexInfo(pii + i);
                    }
                    return iil;
                }
            }
        }

        /// <summary>
        /// Gets the index info.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>IndexInfo.</returns>
        internal IndexInfo GetIndexInfo(string name)
        {
            unsafe
            {
                sccoredb.SC_INDEX_INFO ii;
                uint r = sccoredb.sccoredb_get_index_info_by_name(TableId, name, &ii);
                if (r == 0) return CreateIndexInfo(&ii);
                if (r == Error.SCERRINDEXNOTFOUND) return null; // Index not found.
                throw ErrorCode.ToException(r);
            }
        }

        /// <summary>
        /// Creates the index info.
        /// </summary>
        /// <param name="pii">The pii.</param>
        /// <returns>IndexInfo.</returns>
        internal unsafe IndexInfo CreateIndexInfo(sccoredb.SC_INDEX_INFO* pii)
        {
            string name;
            short attributeCount;
            ushort tempSortMask;
            SortOrder[] sortOrderings;
            int[] columnIndexes;
            ColumnDef[] columnDefs;

            name = new String(pii->name);
            // Get the number of attributes.
            attributeCount = pii->attributeCount;
            if (attributeCount < 1 || attributeCount > 10)
            {
                throw ErrorCode.ToException(Error.SCERRSQLINTERNALERROR, "Incorrect attributeCount.");
            }
            // Get the sort orderings.
            sortOrderings = new SortOrder[attributeCount];
            tempSortMask = pii->sortMask;
            for (Int32 j = 0; j < attributeCount; j++)
            {
                if ((tempSortMask & 1) == 1)
                {
                    sortOrderings[j] = SortOrder.Descending;
                }
                else
                {
                    sortOrderings[j] = SortOrder.Ascending;
                }
                tempSortMask = (UInt16)(tempSortMask >> 1);
            }
            // Get the column definitions.
            columnIndexes = new int[attributeCount];
            columnDefs = new ColumnDef[attributeCount];
            for (Int32 j = 0; j < attributeCount; j++)
            {
                switch (j)
                {
                    case 0:
                        columnIndexes[j] = pii->attrIndexArr_0;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_0];
                        break;
                    case 1:
                        columnIndexes[j] = pii->attrIndexArr_1;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_1];
                        break;
                    case 2:
                        columnIndexes[j] = pii->attrIndexArr_2;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_2];
                        break;
                    case 3:
                        columnIndexes[j] = pii->attrIndexArr_3;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_3];
                        break;
                    case 4:
                        columnIndexes[j] = pii->attrIndexArr_4;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_4];
                        break;
                    case 5:
                        columnIndexes[j] = pii->attrIndexArr_5;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_5];
                        break;
                    case 6:
                        columnIndexes[j] = pii->attrIndexArr_6;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_6];
                        break;
                    case 7:
                        columnIndexes[j] = pii->attrIndexArr_7;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_7];
                        break;
                    case 8:
                        columnIndexes[j] = pii->attrIndexArr_8;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_8];
                        break;
                    case 9:
                        columnIndexes[j] = pii->attrIndexArr_9;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_9];
                        break;
                    case 10:
                        columnIndexes[j] = pii->attrIndexArr_10;
                        columnDefs[j] = ColumnDefs[pii->attrIndexArr_10];
                        break;
                }
            }
            return new IndexInfo(pii->handle, TableId, name, columnIndexes, columnDefs, sortOrderings);
        }
    }
}
