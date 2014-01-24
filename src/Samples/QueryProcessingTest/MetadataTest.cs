using System;
using System.Diagnostics;
using Starcounter;
using Starcounter.Internal;
using Starcounter.Metadata;

namespace QueryProcessingTest {
    public static class MetadataTest {
        public static void TestPopulatedMetadata() {
            HelpMethods.LogEvent("Test populated meta-data");
            TestTypeMetadata();
            TestRuntimeColumnMetadata();
            HelpMethods.LogEvent("Finished testing populated meta-data");
        }

        public static void TestTypeMetadata() {
            MaterializedType t = Db.SQL<MaterializedType>("select t from materializedType t order by primitivetype").First;
            Trace.Assert(t != null);
            Trace.Assert(t.Name == "string");
            Trace.Assert(t.PrimitiveType == sccoredb.STAR_TYPE_STRING);
            ulong acc = 0;
            int count = 0;
            foreach (MaterializedType mt in Db.SQL<MaterializedType>("select t from materializedType t")) {
                acc += mt.PrimitiveType;
                count++;
            }
            Trace.Assert(count == 10);
            Trace.Assert(acc == 55);

            MappedType mapt = Db.SQL<MappedType>("select t from mappedtype t where name = ?", "Int16").First;
            Trace.Assert(mapt != null);
            Trace.Assert(mapt.VMName == "CLR");
            Trace.Assert(mapt.MaterializedType.Name == "long");
            Trace.Assert(!mapt.WriteLoss);
            Trace.Assert(mapt.ReadLoss);
            acc = 0;
            count = 0;
            foreach (MappedType mpt in Db.SQL<MappedType>("select t from mappedtype t")) {
                Trace.Assert(mpt.MaterializedType != null);
                count++;
            }
            Trace.Assert(count == 16);
            MaterializedTable m = Db.SQL<MaterializedTable>("select m from MaterializedTable m where name = ?", "base_type").First;
            Trace.Assert(m != null);
            Trace.Assert(m.BaseTable == null);
            Trace.Assert(m.Name == "base_type");
            MaterializedColumn c = Db.SQL<MaterializedColumn>("select c from materializedcolumn c where name = ?", "base_virtual_table").First;
            Trace.Assert(c != null);
            Trace.Assert(c.Table.Name == "virtual_table");
            RawView rv = Db.SQL<RawView>("select rw from rawview rw where name = ?", "base_type").First;
            Trace.Assert(rv != null);
            Trace.Assert(rv.FullName == "base_type.Raw.Starcounter");
            Trace.Assert(rv.Table != null);
            Trace.Assert(rv.Table.Name == rv.Name);
            Trace.Assert(!rv.Updatable);
            Trace.Assert(rv.BaseVirtualTable == null);
            rv = Db.SQL<RawView>("select rw from rawview rw where name = ?", "clr_view").First;
            Trace.Assert(rv != null);
            Trace.Assert(rv.FullName == "clr_view.Raw.Starcounter");
            Trace.Assert(rv.Table != null);
            Trace.Assert(rv.Table.Name == rv.Name);
            Trace.Assert(!rv.Updatable);
            Trace.Assert(rv.BaseVirtualTable != null);
            Trace.Assert(rv.BaseVirtualTable.Name == "vm_view");
            Trace.Assert(rv.BaseVirtualTable.BaseVirtualTable != null);
            Trace.Assert(rv.BaseVirtualTable.BaseVirtualTable.BaseVirtualTable != null);
            Trace.Assert(rv.BaseVirtualTable.BaseVirtualTable.BaseVirtualTable.Name == "runtime_view");
            Trace.Assert(rv.BaseVirtualTable.BaseVirtualTable.BaseVirtualTable.BaseVirtualTable == null);
            count = 0;
            foreach (RawView v in Db.SQL<RawView>("select rv from rawView rv")) {
                Trace.Assert(v.Table != null);
                Trace.Assert(v.Table.Name == v.Name);
                count++;
            }
            Trace.Assert(count == 17);
            rv = Db.SQL<RawView>("select rw from rawview rw where name = ?", "materialized_index").First;
            Trace.Assert(rv != null);
            Trace.Assert(rv.FullName == "materialized_index.Raw.Starcounter");
            Trace.Assert(rv.Table != null);
            Trace.Assert(rv.Table.Name == rv.Name);
            Trace.Assert(!rv.Updatable);
            Trace.Assert(rv.BaseVirtualTable == null);
        }

        public static void TestRuntimeColumnMetadata() {
            TableColumn c = Db.SQL<TableColumn>("select c from TableColumn c where name = ?", "materialized_column").First;
            Trace.Assert(c != null);
            Trace.Assert(c.Name == "materialized_column");
            Trace.Assert(c.RuntimeView != null);
            Trace.Assert(c.RuntimeView.Name == "table_column");
            Trace.Assert(c.RuntimeView is RawView);
            Trace.Assert((c.RuntimeView as RawView).BaseVirtualTable != null);
        }
    }
}
