// ***********************************************************************
// <copyright file="MaterializedIndex.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Binding;
using Starcounter.Internal;
using System.Reflection;

namespace Starcounter.Metadata {
    /// <summary>
    /// Class MaterializedIndex
    /// </summary>
    public sealed class MaterializedIndex : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal sealed class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_index_id = 1;
            internal static int columnHandle_table_id = 2;
            internal static int columnHandle_table = 3;
            internal static int columnHandle_name = 4;
            internal static int columnHandle_unique = 5;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="MaterializedIndex.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal TypeDef CreateTypeDef() {
            var systemTableDef = new TableDef(
                "materialized_index",
                new ColumnDef[]
                {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("index_id", sccoredb.STAR_TYPE_ULONG, false, false),
                    new ColumnDef("table_id", sccoredb.STAR_TYPE_ULONG, false, false),
                    new ColumnDef("table", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("unique", sccoredb.STAR_TYPE_ULONG, false, false),
                }
                );

            var sysIndexTypeDef = new TypeDef(
                "Starcounter.Metadata.MaterializedIndex",
                null,
                new PropertyDef[]
                {
                    new PropertyDef("IndexId", DbTypeCode.UInt64, false) { ColumnName = "index_id" },
                    new PropertyDef("TableId", DbTypeCode.UInt64, false) { ColumnName = "table_id" },
                    new PropertyDef("Table", DbTypeCode.Object, true, "Starcounter.Metadata.MaterializedTable") { ColumnName = "table" },
                    new PropertyDef("Name", DbTypeCode.String, true) { ColumnName = "name" },
                    new PropertyDef("Unique", DbTypeCode.Boolean, false) { ColumnName = "unique" },
                },
                new TypeLoader(new AssemblyName("Starcounter"), "Starcounter.Metadata.MaterializedIndex"),
                systemTableDef,
                new DbTypeCode[] {
                    DbTypeCode.Key, DbTypeCode.UInt64, DbTypeCode.UInt64, DbTypeCode.Object,
                    DbTypeCode.String, DbTypeCode.UInt64
                }
                );

            return sysIndexTypeDef;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterializedIndex" /> class.
        /// </summary>
        /// <param name="u">The u.</param>
        public MaterializedIndex(Uninitialized u) : base(u) { }

        /// <summary>
        /// </summary>
        public ulong IndexId {
            get { return DbState.ReadUInt64(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_index_id); }
        }

        /// <summary>
        /// </summary>
        public ulong TableId {
            get { return DbState.ReadUInt64(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_table_id); }
        }

        /// <summary>
        /// </summary>
        public MaterializedTable Table {
            get { return (MaterializedTable)DbState.ReadObject(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_table); }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_name); }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="MaterializedIndex" /> is unique.
        /// </summary>
        /// <value><c>true</c> if unique; otherwise, <c>false</c>.</value>
        public bool Unique {
            get { return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_unique); }
        }
    }

    /// <summary>
    /// Class materialized_index_column
    /// </summary>
    public sealed class materialized_index_column : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal sealed class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_index = 1;
            internal static int columnHandle_place = 2;
            internal static int columnHandle_column = 3;
            internal static int columnHandle_order = 4;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="materialized_index_column.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal TypeDef CreateTypeDef() {
            var systemTableDef = new TableDef(
                "materialized_index_column",
                new ColumnDef[]
                {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("index", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("place", sccoredb.STAR_TYPE_ULONG, false, false),
                    new ColumnDef("column", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("order", sccoredb.STAR_TYPE_ULONG, false, false)
                }
                );

            var sysIndexTypeDef = new TypeDef(
                "Starcounter.Metadata.materialized_index_column",
                null,
                new PropertyDef[]
                {
                    new PropertyDef("index", DbTypeCode.Object, true, "Starcounter.Metadata.MaterializedIndex") { ColumnName = "index" },
                    new PropertyDef("place", DbTypeCode.UInt64, false) { ColumnName = "place" },
                    new PropertyDef("column", DbTypeCode.Object, true, "Starcounter.Metadata.MaterializedColumn") { ColumnName = "column" },
                    new PropertyDef("order", DbTypeCode.UInt64, false) { ColumnName = "order" }
                },
                new TypeLoader(new AssemblyName("Starcounter"), "Starcounter.Metadata.materialized_index_column"),
                systemTableDef,
                new DbTypeCode[] {
                    DbTypeCode.Key, DbTypeCode.Object, DbTypeCode.UInt64, DbTypeCode.Object,
                    DbTypeCode.UInt64
                }
                );

            return sysIndexTypeDef;
        }

        /// <summary>
        /// </summary>
        public materialized_index_column(Uninitialized u) : base(u) { }

        /// <summary>
        /// </summary>
        public MaterializedIndex index {
            get { return (MaterializedIndex)DbState.ReadObject(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_index); }
        }


        /// <summary>
        /// </summary>
        public ulong place {
            get { return DbState.ReadUInt64(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_place); }
        }

        /// <summary>
        /// </summary>
        public MaterializedColumn column {
            get { return (MaterializedColumn)DbState.ReadObject(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_column); }
        }

        /// <summary>
        /// </summary>
        public ulong order {
            get { return DbState.ReadUInt64(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_order); }
        }
    }
}