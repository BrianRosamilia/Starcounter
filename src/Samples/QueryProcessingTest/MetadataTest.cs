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
        }
    }
}
