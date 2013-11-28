using FastReflectionLib;
using Starcounter;
using Starcounter.Binding;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Applications.UsageTrackerApp.Export {
    /// <summary>
    /// 
    /// </summary>
    public class Export {


        /// <summary>
        /// {
        ///     "Tables": [
        ///         {
        ///             "Table": [
        ///                 {
        ///                     "Property": "Value"
        ///                 }
        ///             ]
        ///         }
        ///     ]
        /// }
        /// 
        /// </summary>
        public static void Start(string file) {

            if (string.IsNullOrEmpty(file)) {
                throw new ArgumentException(file);
            }

            Console.WriteLine(string.Format("NOTICE: Exporting to: {0} ", file));


            QueryResultRows<Starcounter.Metadata.MaterializedTable> result = Db.SlowSQL<Starcounter.Metadata.MaterializedTable>("SELECT o FROM MATERIALIZED_TABLE o");
            int tableCnt = 0;

            try {

                using (TextWriter writer = File.CreateText(file)) {

                    writer.WriteLine("{");
                    writer.Write("    \"Tables\": [");

                    foreach (Starcounter.Metadata.MaterializedTable table in result) {

                        // Exclude system tables from export.

                        if (
                            // Old system tables.
                            table.Name == "sys_table" || table.Name == "sys_index" || table.Name == "sys_column" ||
                            // New system tables.
                            table.Name == "materialized_table" || table.Name == "materialized_index" || table.Name == "materialized_column" || table.Name == "materialized_index_column" ||
                            // Even newer system tables
                            table.Name == "MaterializedTable" || table.Name == "MaterializedIndex" || table.Name == "MaterializedColumn" || table.Name == "MaterializedIndexColumn" ||
                            // Runtime view system tables
                            table.Name == "BaseType" || table.Name == "MaterializedType" || table.Name == "RuntimeType" || table.Name == "MappedType" || table.Name == "ReferenceType"
                            ) continue;

                        // Begin Table
                        if (tableCnt > 0) {
                            writer.Write(",");
                        }

                        int itemCnt = ExportTable(table, writer);
                        tableCnt++;
                        Console.WriteLine(string.Format("NOTICE: Exported table {0} ({1} items)", table.Name, itemCnt));

                    }

                    if (tableCnt > 0) {
                        writer.Write("{0}    ", writer.NewLine);
                    }
                    // End Table
                    writer.WriteLine("]");
                    writer.WriteLine("}");

                }
                Console.WriteLine(string.Format("NOTICE: Total exported tables: {0}", tableCnt));
            }
            catch (Exception) {

                if (File.Exists(file)) {
                    File.Delete(file);
                }
                throw;
            }

        }

        private static int ExportTable(Starcounter.Metadata.MaterializedTable table, TextWriter writer) {

            FastReflectionCaches.ClearAllCaches();
            Utils.ClearCache();

            writer.Write("{0}        ", writer.NewLine);
            writer.WriteLine("{");
            writer.Write("            \"{0}\": [", table.Name);

            Starcounter.Binding.TableDef tableDef = Db.LookupTable(table.Name);

            if (tableDef == null) {
                throw new KeyNotFoundException(string.Format("Failed to get definition for table: {0}", table.Name));
                //writer.WriteLine("]");
                //writer.Write("        }");
                //return;
            }

            QueryResultRows<object> items = Db.SlowSQL(string.Format("SELECT o FROM {0} o", table.Name));
            TypeDef typeDef = null;
            try {
                typeDef = Bindings.GetTypeDef((int)table.TableId);
            }
            catch (IndexOutOfRangeException) {
                throw new KeyNotFoundException(string.Format("Failed to get type definition for table: {0}, Is Host Executable running?", table.Name));
            }

            if (typeDef == null) {
                throw new KeyNotFoundException(string.Format("Failed to get type definition for table: {0}", table.Name));
                // Table removed?
                //writer.WriteLine("]");
                //writer.Write("        }");
                //return;
            }
            PropertyDef[] propDef = typeDef.PropertyDefs;


            //int maxItems = 3;
            int itemCnt = 0;

            if (propDef.Length > 0) {

                foreach (var item in items) {

                    if (itemCnt > 0) {
                        writer.Write(",");
                    }

                    // Begin Item
                    writer.Write("{0}                ", writer.NewLine);
                    writer.Write("{");

                    // Add ObjectNo
                    writer.Write("{0}                    ", writer.NewLine);
                    writer.Write("\"{0}\": {1}", "ObjectNo", item.GetObjectNo());
                    int propCnt = 1;

                    foreach (PropertyDef prop in propDef) {

                        if (propCnt > 0) {
                            writer.Write(",");
                        }

                        writer.Write("{0}                    ", writer.NewLine);

                        var propertyValue = Utils.GetValue(item, prop.Name);
                        string value = string.Empty;
                        string propertyName = prop.Name;

                        #region Type handling
                        switch (prop.Type) {
                            case DbTypeCode.String:

                                if (propertyValue == null) {
                                    value = "null";
                                }
                                else {

                                    value = propertyValue.ToString();

                                    value = value.Replace("\\", "\\\\");    //  \       => \\

                                    value = value.Replace("\"", "\\\"");    //  "       => \"

                                    value = value.Replace("\n", "\\n");     //  <\n>    => \n
                                    value = value.Replace("\r", "\\r");     //  <\r>    => \r

                                    value = value.Replace("\b", "\\b");     //  <\b>    => \b
                                    value = value.Replace("\f", "\\f");     //  <\f>    => \t
                                    value = value.Replace("\t", "\\t");     //  <\t>    => \f

                                    value = string.Format("\"{0}\"", value);

                                }

                                break;
                            case DbTypeCode.Boolean:
                                value = string.Format("{0}", propertyValue == null ? "null" : propertyValue.ToString().ToLower());
                                break;
                            case DbTypeCode.DateTime:
                                value = string.Format("{0}", propertyValue == null ? "null" : "\"" + ((DateTime)propertyValue).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF", CultureInfo.InvariantCulture) + "\"");
                                break;
                            case DbTypeCode.Object:
                                string prefixSuffix = "+REF+";
                                value = string.Format("\"{0}{1}:{2}:{3}{4}\"",
                                    prefixSuffix,
                                    prop.TargetTypeName,
                                    "ObjectNo",
                                    (propertyValue == null ? "null" : propertyValue.GetObjectNo().ToString()),
                                    prefixSuffix);
                                break;
                            case DbTypeCode.Decimal:
                            case DbTypeCode.Double:
                                value = string.Format("{0}", propertyValue == null ? "null" : propertyValue.ToString().Replace(',', '.'));
                                break;
                            case DbTypeCode.Byte:
                            case DbTypeCode.Int16:
                            case DbTypeCode.Int32:
                            case DbTypeCode.Int64:
                            case DbTypeCode.UInt16:
                            case DbTypeCode.UInt32:
                            case DbTypeCode.UInt64:
                            case DbTypeCode.SByte:
                            case DbTypeCode.Single:
                                value = string.Format("{0}", propertyValue == null ? "null" : propertyValue);
                                break;
                            case DbTypeCode.Binary:
                            case DbTypeCode.Key:
                            case DbTypeCode.LargeBinary:
                            default:
                                throw new NotSupportedException(prop.Type.ToString() + " type not supported");
                        }
                        #endregion

                        string newValue = FixDatabaseErrors(table.Name, propertyName, propertyValue);
                        if (newValue != null) {
                            value = newValue;
                        }
                        writer.Write("\"{0}\": {1}", propertyName, value);

                        propCnt++;

                    }

                    if (propCnt > 0) {
                        writer.Write("{0}                ", writer.NewLine);
                    }

                    // End Item
                    writer.Write("}");

                    itemCnt++;

                    //if (itemCnt >= maxItems) break;

                }

                if (itemCnt > 0) {
                    writer.Write("{0}            ", writer.NewLine);
                }
            }

            writer.WriteLine("]");
            writer.Write("        }");

            return itemCnt;
        }

        /// <summary>
        /// Fix errors in database
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        private static string FixDatabaseErrors(string tableName, string propertyName, object propertyValue) {

            if (tableName == "StarcounterApplicationWebSocket.VersionHandler.Model.VersionBuild" && propertyName == "DownloadKey") {
                if (propertyValue != null && ((string)propertyValue).Length > 24) {
                    // Invalid downloadkey
                    return "null";
                }
            }

            if (tableName == "StarcounterApplicationWebSocket.VersionHandler.Model.VersionBuild" && propertyName == "IPAdress") {
                if (propertyValue != null && "1.0.2.0".Equals(((string)propertyValue))) {
                    // Invalid ip
                    return "null";
                }
            }

            if (tableName == "Starcounter.Applications.UsageTrackerApp.Model.InstallerEnd" && propertyName == "IP") {
                if (propertyValue != null && "1.0.2.0".Equals(((string)propertyValue))) {
                    // Invalid ip
                    return "null";
                }
            }

            if (tableName == "Starcounter.Applications.UsageTrackerApp.Model.InstallerExecuting" && propertyName == "IP") {
                if (propertyValue != null && "1.0.2.0".Equals(((string)propertyValue))) {
                    // Invalid ip
                    return "null";
                }
            }

            if (tableName == "Starcounter.Applications.UsageTrackerApp.Model.InstallerFinish" && propertyName == "IP") {
                if (propertyValue != null && "1.0.2.0".Equals(((string)propertyValue))) {
                    // Invalid ip
                    return "null";
                }
            }

            if (tableName == "Starcounter.Applications.UsageTrackerApp.Model.InstallerStart" && propertyName == "IP") {
                if (propertyValue != null && "1.0.2.0".Equals(((string)propertyValue))) {
                    // Invalid ip
                    return "null";
                }
            }

            if (tableName == "Starcounter.Applications.UsageTrackerApp.Model.StarcounterUsage" && propertyName == "IP") {
                if (propertyValue != null && "1.0.2.0".Equals(((string)propertyValue))) {
                    // Invalid ip
                    return "null";
                }
            }

            return null;
        }

    }
}
