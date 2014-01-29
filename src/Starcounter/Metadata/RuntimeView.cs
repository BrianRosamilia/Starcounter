using Starcounter.Binding;
using Starcounter.Internal;
using System;

namespace Starcounter.Metadata {
    public abstract class BaseTable : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_Name;
            internal static int columnHandle_FullName;
            internal static int columnHandle_Updatable;
        }
#pragma warning disable 0628, 0169
        #endregion

        /// <summary>
        /// Creates the database binding <see cref="TypeDef"/> representing
        /// the type in the database and holding its table- and column defintions.
        /// </summary>
        /// <remarks>
        /// Developer note: if you extend or change this class in any way, make
        /// sure to keep the <see cref="MaterializedColumn.__starcounterTypeSpecification"/>
        /// class in sync with what is returned by this method.
        /// </remarks>
        /// <returns>A <see cref="TypeDef"/> representing the current
        /// type.</returns>
        static internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.BaseTable", null,
                "BaseTable", null,
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("FullName", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("Updatable", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("FullName", DbTypeCode.String),
                    new PropertyDef("Updatable", DbTypeCode.Boolean)
                });
        }

        /// <inheritdoc />
        public BaseTable(Uninitialized u)
            : base(u) {
        }

        //internal BaseTable() : this(null) {
        //    DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        //}

        public string Name {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_Name); }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_Name,
                    value);
            }
        }

        public string FullName {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_FullName); }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_FullName,
                    value);
            }
        }

        public bool Updatable {
            get { return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_Updatable); }
            internal set {
                DbState.WriteBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_Updatable,
                    value);
            }
        }
    }

    public abstract class VirtualTable : BaseTable {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_Name;
            internal static int columnHandle_FullName;
            internal static int columnHandle_Updatable;
            internal static int columnHandle_Table;
            internal static int columnHandle_BaseVirtualTable;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.VirtualTable", "Starcounter.Metadata.BaseTable",
                "VirtualTable", "BaseTable",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("FullName", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("Updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("Table", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("BaseVirtualTable", sccoredb.STAR_TYPE_REFERENCE, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("FullName", DbTypeCode.String),
                    new PropertyDef("Updatable", DbTypeCode.Boolean),
                    new PropertyDef("Table", DbTypeCode.Object, "Starcounter.Metadata.MaterializedTable"),
                    new PropertyDef("BaseVirtualTable", DbTypeCode.Object, "Starcounter.Metadata.VirtualTable")
                });
        }

        public VirtualTable(Uninitialized u) : base(u) { }

        //internal VirtualTable()
        //    : this(null) {
        //        DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        //}

        public MaterializedTable Table {
            get {
                return (MaterializedTable)DbState.ReadObject(__sc__this_id__, __sc__this_handle__, 
                __starcounterTypeSpecification.columnHandle_Table); }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_Table, value);
            }
        }

        public VirtualTable BaseVirtualTable {
            get {
                return (VirtualTable)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_BaseVirtualTable);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_BaseVirtualTable, value);
            }
        }
    }

    public sealed class RawView : VirtualTable {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_Name;
            internal static int columnHandle_FullName;
            internal static int columnHandle_Updatable;
            internal static int columnHandle_Table;
            internal static int columnHandle_BaseVirtualTable;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.RawView", "Starcounter.Metadata.VirtualTable",
                "RawView", "VirtualTable",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("FullName", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("Updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("Table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("BaseVirtualTable", sccoredb.STAR_TYPE_REFERENCE, true, true)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("FullName", DbTypeCode.String),
                    new PropertyDef("Updatable", DbTypeCode.Boolean),
                    new PropertyDef("Table", DbTypeCode.Object, "Starcounter.Metadata.MaterializedTable"),
                    new PropertyDef("BaseVirtualTable", DbTypeCode.Object, "Starcounter.Metadata.VirtualTable")
                });
        }

        public RawView(Uninitialized u) : base(u) { }
        internal RawView() : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }
    }

    public abstract class VMView : VirtualTable {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_Name;
            internal static int columnHandle_FullName;
            internal static int columnHandle_Updatable;
            internal static int columnHandle_Table;
            internal static int columnHandle_BaseVirtualTable;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.VMView", "Starcounter.Metadata.VirtualTable",
                "VMView", "VirtualTable",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("FullName", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("Updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("Table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("BaseVirtualTable", sccoredb.STAR_TYPE_REFERENCE, true, true)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("FullName", DbTypeCode.String),
                    new PropertyDef("Updatable", DbTypeCode.Boolean),
                    new PropertyDef("Table", DbTypeCode.Object, "Starcounter.Metadata.MaterializedTable"),
                    new PropertyDef("BaseVirtualTable", DbTypeCode.Object, "Starcounter.Metadata.VirtualTable")
                });
        }

        public VMView(Uninitialized u) : base(u) { }
    }

    public sealed class ClrView : VMView {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_Name;
            internal static int columnHandle_FullName;
            internal static int columnHandle_Updatable;
            internal static int columnHandle_Table;
            internal static int columnHandle_BaseVirtualTable;
            internal static int columnHandle_AssemblyName;
            internal static int columnHandle_AppdomainName;
            internal static int columnHandle_FullClassName;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.ClrView", "Starcounter.Metadata.VMView",
                "ClrView", "VMView",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("Name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("FullName", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("Updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("Table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("BaseVirtualTable", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("AssemblyName", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("AppdomainName", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("FullClassName", sccoredb.STAR_TYPE_STRING, true, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("FullName", DbTypeCode.String),
                    new PropertyDef("Updatable", DbTypeCode.Boolean),
                    new PropertyDef("Table", DbTypeCode.Object, "Starcounter.Metadata.MaterializedTable"),
                    new PropertyDef("BaseVirtualTable", DbTypeCode.Object, "Starcounter.Metadata.VirtualTable"),
                    new PropertyDef("AssemblyName", DbTypeCode.String),
                    new PropertyDef("AppdomainName", DbTypeCode.String),
                    new PropertyDef("FullClassName", DbTypeCode.String)
                });
        }

        public ClrView(Uninitialized u) : base(u) { }
        internal ClrView()
            : this(null) {
            DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        }

        public String AssemblyName {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_AssemblyName);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_AssemblyName, value);
            }
        }

        public String AppdomainName {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_AppdomainName);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_AppdomainName, value);
            }
        }

        public String FullClassName {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_FullClassName);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_FullClassName, value);
            }
        }
    }
}
