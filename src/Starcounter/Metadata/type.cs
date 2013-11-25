// ***********************************************************************
// <copyright file="materialized_column.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Binding;
using Starcounter.Internal;
using System.Reflection;

namespace Starcounter.Metadata {
    public sealed class type : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal sealed class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_table_id = 1;
            internal static int columnHandle_name = 2;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="materialized_column.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal TypeDef CreateTypeDef() {
            var systemTableDef = new TableDef(
                "type",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, false, false)
                });

            var sysColumnTypeDef = new TypeDef(
                "Starcounter.Metadata.type",
                null,
                new PropertyDef[] {
                    new PropertyDef("table_id", DbTypeCode.UInt64, false) { ColumnName = "table_id" },
                    new PropertyDef("name", DbTypeCode.String, true) { ColumnName = "name" }
                },
                new TypeLoader(new AssemblyName("Starcounter"), "Starcounter.Metadata.type"),
                systemTableDef,
                new DbTypeCode[] {
                    DbTypeCode.Key, DbTypeCode.String
                });

            return sysColumnTypeDef;
        }

    
        /// <inheritdoc />
        public type(Uninitialized u)
            : base(u) {
        }

        /// <summary>
        /// Name of the type
        /// </summary>
        public string name {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_name); }
        }
    }
}
