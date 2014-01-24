using Starcounter.Binding;
using Starcounter.Internal;
using System;

namespace Starcounter.Metadata {
    public abstract class RuntimeView : Entity {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_name;
            internal static int columnHandle_full_name;
            internal static int columnHandle_updatable;
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
                "Starcounter.Metadata.RuntimeView", null,
                "runtime_view", null,
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, false),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("full_name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("updatable", sccoredb.STAR_TYPE_ULONG, false, false)
                },
                new PropertyDef[] {
                    new PropertyDef("Name", DbTypeCode.String),
                    new PropertyDef("FullName", DbTypeCode.String),
                    new PropertyDef("Updatable", DbTypeCode.Boolean)
                });
        }

        /// <inheritdoc />
        public RuntimeView(Uninitialized u)
            : base(u) {
        }

        //internal RuntimeView() : this(null) {
        //    DbState.Insert(__starcounterTypeSpecification.tableHandle, ref this.__sc__this_id__, ref this.__sc__this_handle__);
        //}

        public string Name {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_name); }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_name,
                    value);
            }
        }

        public string FullName {
            get { return DbState.ReadString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_full_name); }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_full_name,
                    value);
            }
        }

        public bool Updatable {
            get { return DbState.ReadBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_updatable); }
            internal set {
                DbState.WriteBoolean(__sc__this_id__, __sc__this_handle__, __starcounterTypeSpecification.columnHandle_updatable,
                    value);
            }
        }
    }

    public abstract class VirtualTable : RuntimeView {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_name;
            internal static int columnHandle_full_name;
            internal static int columnHandle_updatable;
            internal static int columnHandle_table;
            internal static int columnHandle_base_virtual_table;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.VirtualTable", "Starcounter.Metadata.RuntimeView",
                "virtual_table", "runtime_view",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("full_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("table", sccoredb.STAR_TYPE_REFERENCE, true, false),
                    new ColumnDef("base_virtual_table", sccoredb.STAR_TYPE_REFERENCE, true, false)
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
                __starcounterTypeSpecification.columnHandle_table); }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_table, value);
            }
        }

        public VirtualTable BaseVirtualTable {
            get {
                return (VirtualTable)DbState.ReadObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_base_virtual_table);
            }
            internal set {
                DbState.WriteObject(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_base_virtual_table, value);
            }
        }
    }

    public sealed class RawView : VirtualTable {
        #region Infrastructure, reflecting what is emitted by the weaver.
#pragma warning disable 0649, 0169
        internal new class __starcounterTypeSpecification {
            internal static ushort tableHandle;
            internal static TypeBinding typeBinding;
            internal static int columnHandle_name;
            internal static int columnHandle_full_name;
            internal static int columnHandle_updatable;
            internal static int columnHandle_table;
            internal static int columnHandle_base_virtual_table;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.RawView", "Starcounter.Metadata.VirtualTable",
                "raw_view", "virtual_table",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("full_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("base_virtual_table", sccoredb.STAR_TYPE_REFERENCE, true, true)
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
            internal static int columnHandle_name;
            internal static int columnHandle_full_name;
            internal static int columnHandle_updatable;
            internal static int columnHandle_table;
            internal static int columnHandle_base_virtual_table;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.VMView", "Starcounter.Metadata.VirtualTable",
                "vm_view", "virtual_table",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("full_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("base_virtual_table", sccoredb.STAR_TYPE_REFERENCE, true, true)
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
            internal static int columnHandle_name;
            internal static int columnHandle_full_name;
            internal static int columnHandle_updatable;
            internal static int columnHandle_table;
            internal static int columnHandle_base_virtual_table;
            internal static int columnHandle_assembly_name;
            internal static int columnHandle_appdomain_name;
            internal static int columnHandle_full_class_name;
        }
#pragma warning disable 0628, 0169
        #endregion

        static new internal TypeDef CreateTypeDef() {
            return TypeDef.CreateTypeTableDef(
                "Starcounter.Metadata.ClrView", "Starcounter.Metadata.VMView",
                "clr_view", "vm_view",
                new ColumnDef[] {
                    new ColumnDef("__id", sccoredb.STAR_TYPE_KEY, false, true),
                    new ColumnDef("name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("full_name", sccoredb.STAR_TYPE_STRING, true, true),
                    new ColumnDef("updatable", sccoredb.STAR_TYPE_ULONG, false, true),
                    new ColumnDef("table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("base_virtual_table", sccoredb.STAR_TYPE_REFERENCE, true, true),
                    new ColumnDef("assembly_name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("appdomain_name", sccoredb.STAR_TYPE_STRING, true, false),
                    new ColumnDef("full_class_name", sccoredb.STAR_TYPE_STRING, true, false)
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
                    __starcounterTypeSpecification.columnHandle_assembly_name);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_assembly_name, value);
            }
        }

        public String AppdomainName {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_appdomain_name);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_appdomain_name, value);
            }
        }

        public String FullClassName {
            get {
                return DbState.ReadString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_full_class_name);
            }
            internal set {
                DbState.WriteString(__sc__this_id__, __sc__this_handle__,
                    __starcounterTypeSpecification.columnHandle_full_class_name, value);
            }
        }
    }
}
