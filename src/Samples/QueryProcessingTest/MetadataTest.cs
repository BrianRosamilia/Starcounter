﻿using System;
using System.Diagnostics;
using Starcounter;
using Starcounter.Internal;
using Starcounter.Metadata;

namespace QueryProcessingTest {
    public static class MetadataTest {
        public static void TestPopulatedMetadata() {
            HelpMethods.LogEvent("Test populated meta-data");
            SimpleTestFirstMetadata();
            HelpMethods.LogEvent("Finished testing populated meta-data");
        }

        public static void SimpleTestFirstMetadata() {
            MaterializedType t = Db.SQL<MaterializedType>("select t from materializedType t order by primitivetype").First;
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
        }
    }
}
