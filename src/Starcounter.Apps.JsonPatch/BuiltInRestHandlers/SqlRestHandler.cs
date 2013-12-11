// ***********************************************************************
// <copyright file="InternalHandlers.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using Starcounter.Internal.Web;
using Starcounter.Advanced;
using System.Diagnostics;
using Starcounter.Binding;
using Codeplex.Data;
using System.Net;
using HttpStructs;
using System.Collections.Generic;
using System.IO;
using Starcounter.Query.Execution;
using jp = Starcounter.Internal.JsonPatch;
using System.Runtime.CompilerServices;
using Starcounter.Templates;
using Starcounter.Internal.Uri;

[assembly: InternalsVisibleTo("Starcounter.Bootstrap, PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]


namespace Starcounter.Internal {

    /// <summary>
    /// Every Starcounter application comes with built in REST handlers to allow communication
    /// with public Session.Data (aka. view-models or puppets) objects and/or with a potentially exposed SQL engine.
    /// </summary>
    public static class SqlRestHandler {
        private static List<UInt16> registeredPorts = new List<UInt16>();

        private static StreamWriter consoleWriter;

        /// <summary>
        /// Registers the built in REST handlers.
        /// </summary>
        /// <param name="defaultUserHttpPort">The public session data objects (view-models) are accessed using the same port as the user code REST handlers</param>
        /// <param name="defaultSystemHttpPort">The SQL access uses the system port</param>
        public static void Register(UInt16 defaultUserHttpPort, UInt16 defaultSystemHttpPort) {
            string dbName = Db.Environment.DatabaseNameLower;

            List<Session>[] WebSocketSessions = new List<Session>[Db.Environment.SchedulerCount];

            Debug.Assert(Db.Environment != null, "Db.Environment is not initialized");
            Debug.Assert(string.IsNullOrEmpty(Db.Environment.DatabaseNameLower) == false, "Db.Environment.DatabaseName is empty or null");

            HandlersManagement.SetHandlerRegisteredCallback(HandlerRegistered);

            Handle.GET(defaultSystemHttpPort, ScSessionClass.DataLocationUriPrefix + "sessions", () => {
                // Collecting number of sessions on all schedulers.
                return "Active sessions for '" + dbName + "':" + Environment.NewLine +
                    GlobalSessions.AllGlobalSessions.GetActiveSessionsStats();
            });

            if (Db.Environment.HasDatabase) {
                Console.WriteLine("Database {0} is listening for SQL commands.", Db.Environment.DatabaseNameLower);
                Handle.POST(defaultSystemHttpPort, ScSessionClass.DataLocationUriPrefix + "sql", (Request req) => {
                    return ExecuteQuery(req.Body);
                });
            }

            // Do not redirect the administrator console output
            if (!StarcounterEnvironment.IsAdministratorApp) {

                Handle.GET(defaultSystemHttpPort, ScSessionClass.DataLocationUriPrefix + "console", (Request req) => {
                    return GetConsoleOutput();
                });

                // Handle Console WebSocket connections
                Handle.GET(defaultSystemHttpPort, ScSessionClass.DataLocationUriPrefix + "console/ws", (Request req, Session session) => {


                    Byte schedId = ThreadData.Current.Scheduler.Id;
                    lock (consoleWriter) {
                        if (!WebSocketSessions[schedId].Contains(session)) {
                            WebSocketSessions[schedId].Add(session);
                            session.SetSessionDestroyCallback((Session s) => {
                                WebSocketSessions[schedId].Remove(s);
                            });
                        }
                    }

                    try {
                        return GetConsoleOutputRaw();
                    }
                    catch (Exception) {
                        session.StopUsing(); // TODO: Is this the correct way to close an socket sesstion?
                    }

                    return "";

                });

                // Setup console handling and callbacks to sessions etc..
                SetupConsoleHandling(WebSocketSessions);
            }

        }

        private static void HandlerRegistered(string uri, ushort port) {
            if (registeredPorts.Contains(port))
                return;

            string dbName = Db.Environment.DatabaseNameLower;

            // We add the internal handlers for stateful access to json-objects
            // for each new port that is used.
            registeredPorts.Add(port);

            Handle.GET(port, ScSessionClass.DataLocationUriPrefix + Handle.UriParameterIndicator, (Session session) => {

                Debug.Assert(null != session);

                Json json = null;
                if (null != Session.Current)
                    json = Session.Current.Data;

                if (json == null) {
                    return HttpStatusCode.NotFound;
                }

                return new Response() {
                    BodyBytes = json.ToJsonUtf8(),
                    ContentType = MimeTypeHelper.MimeTypeAsString(MimeType.Application_Json)
                };
            });


            Handle.PATCH(port, ScSessionClass.DataLocationUriPrefix + Handle.UriParameterIndicator, (Session session, Request request) => {

                Debug.Assert(null != session);

                Json root = null;

                try {
                    if (null != Session.Current)
                        root = Session.Current.Data;

                    jp::JsonPatch.EvaluatePatches(root, request.BodyBytes);

                    return root;
                }
                catch (NotSupportedException nex) {
                    var response = new Response();
                    response.StatusCode = 415;
                    response.Body = nex.Message;
                    return response;
                }
            });
        }

        private static void SetupConsoleHandling(List<Session>[] WebSocketSessions) {

            DbSession dbSession = new DbSession();
            for (Byte i = 0; i < Db.Environment.SchedulerCount; i++) {
                WebSocketSessions[i] = new List<Session>();
            }

            CircularStream circularStream = new CircularStream(2048, (String text) => {

                dbSession.RunSync(() => {


                    lock (consoleWriter) {

                        // When someting is writing to the console we will get a callback here.
                        for (Byte i = 0; i < Db.Environment.SchedulerCount; i++) {
                            Byte k = i;

                            // TODO: Avoid calling RunAsync when there is no "listeners"


                            Byte sched = k;

                            for (Int32 m = 0; m < WebSocketSessions[sched].Count; m++) {
                                Session s = WebSocketSessions[sched][m];

                                // Checking if session is not yet dead.
                                if (s.IsAlive()) {
                                    s.Push(text);
                                }
                                else {
                                    // Removing dead session from broadcast.
                                    WebSocketSessions[sched].Remove(s);
                                }
                            }


                            //dbSession.RunAsync(() => {

                            //    Byte sched = k;

                            //    for (Int32 m = 0; m < WebSocketSessions[sched].Count; m++) {
                            //        Session s = WebSocketSessions[sched][m];

                            //        // Checking if session is not yet dead.
                            //        if (s.IsAlive()) {
                            //            s.Push(text);
                            //        }
                            //        else {
                            //            // Removing dead session from broadcast.
                            //            WebSocketSessions[sched].Remove(s);
                            //        }
                            //    }
                            //}, i);
                        }
                    }
                });


            });


            // Redirect console output to circular memory buffer
            SqlRestHandler.consoleWriter = new StreamWriter(circularStream);
            SqlRestHandler.consoleWriter.AutoFlush = true;
            Console.SetOut(SqlRestHandler.consoleWriter);

        }

        private static string GetConsoleOutputRaw() {

            CircularStream circularMemoryStream = (CircularStream)SqlRestHandler.consoleWriter.BaseStream;
            byte[] buffer = new byte[circularMemoryStream.Length];
            int count = circularMemoryStream.Read(buffer, 0, (int)circularMemoryStream.Length);
            if (count > 0) {
                return System.Text.Encoding.UTF8.GetString(buffer);
            }

            return string.Empty;

        }

        private static string GetConsoleOutput() {

            dynamic resultJson = new DynamicJson();
            resultJson.console = null;
            resultJson.exception = null;

            try {
                resultJson.console = GetConsoleOutputRaw();
            }
            catch (Exception e) {
                resultJson.exception = new { message = e.Message, helpLink = e.HelpLink, stackTrace = e.StackTrace };
            }

            return resultJson.ToString();

        }

        /// <summary>
        /// Executes the query and returns a json string of the result
        /// </summary>
        /// <param name="query"></param>
        /// <returns>SqlQueryResult</returns>
        private static SqlQueryResult ExecuteQuery(string query) {

            Starcounter.SqlEnumerator<object> sqle = null;
            ITypeBinding resultBinding;
            IPropertyBinding[] props;

            SqlQueryResult results = new SqlQueryResult();

            TObject rowObjectTemplate = new TObject();
            TObjArr rowsTemplate = rowObjectTemplate.Add<TObjArr>("rows");
            TObject rowItemTemplate = new TObject();
            rowsTemplate.ElementType = rowItemTemplate;

            results.rows = (Json)rowObjectTemplate.CreateInstance();

			Json rowArr = rowsTemplate.Getter(results.rows);

            try {

                QueryResultRows<dynamic> sqlResult = Db.SlowSQL(query);

                if (sqlResult != null) {
                    sqle = (Starcounter.SqlEnumerator<object>)sqlResult.GetEnumerator();

                    results.queryPlan = sqle.ToString();

                    #region Retrive Columns

                    if (sqle.ProjectionTypeCode != null) {

                        SingleProjectionBinding singleProjectionBinding = new SingleProjectionBinding() { TypeCode = (DbTypeCode)sqle.ProjectionTypeCode };

                        props = new IPropertyBinding[1];
                        props[0] = singleProjectionBinding;

                        singleProjectionBinding.Name = sqle.ProjectionTypeCode.ToString();
                        //                      resultJson.columns[0] = new { title = props[0].Name, value = props[0].Name, type = props[0].TypeCode.ToString() };

                        var col = results.columns.Add();
                        col.title = props[0].Name;
                        col.value = props[0].Name;
                        col.type = props[0].TypeCode.ToString();

                        AddProperty(rowItemTemplate, col, props[0].TypeCode);
                    }
                    else {
                        resultBinding = sqle.TypeBinding;
                        props = new IPropertyBinding[resultBinding.PropertyCount];
                        for (int i = 0; i < resultBinding.PropertyCount; i++) {
                            PropertyMapping pm = (PropertyMapping)resultBinding.GetPropertyBinding(i);
                            props[i] = pm;
                            //                            resultJson.columns[i] = new { title = pm.DisplayName, value = "_" + props[i].Name, type = props[i].TypeCode.ToString() };

                            var col = results.columns.Add();
                            col.title = pm.DisplayName;
                            col.value = "_" + props[i].Name;
                            col.type = props[i].TypeCode.ToString();

                            AddProperty(rowItemTemplate, col, props[i].TypeCode);
                        }
                    }
                    #endregion

                    #region Retrive Rows
                    int index = 0;
                    while (sqle.MoveNext()) {

                        object row = sqle.Current;
                        var jsonRow = rowArr.Add();

                        if (sqle.ProjectionTypeCode != null) {
                            #region GetValue
                            switch (sqle.ProjectionTypeCode) {
                                //case DbTypeCode.Binary:
                                //    value = obj.GetBinary(prop.Index);
                                //    break;
                                case DbTypeCode.Boolean:
                                    ((TBool)rowItemTemplate.Properties[0]).Setter(jsonRow, (bool)row);
                                    break;
                                case DbTypeCode.Byte:
                                    ((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                case DbTypeCode.DateTime:
									((TString)rowItemTemplate.Properties[0]).Setter(jsonRow, ((DateTime)row).ToString());
                                    break;
                                case DbTypeCode.Decimal:
                                    ((TDecimal)rowItemTemplate.Properties[0]).Setter(jsonRow, (Decimal)row);
                                    break;
                                case DbTypeCode.Double:
                                    ((TDouble)rowItemTemplate.Properties[0]).Setter(jsonRow, (Double)row);
                                    break;
                                case DbTypeCode.Int16:
									((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                case DbTypeCode.Int32:
									((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                case DbTypeCode.Int64:
									((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                //case DbTypeCode.LargeBinary:
                                //    value = obj.GetBinary(prop.Index);
                                //    break;
                                case DbTypeCode.Object:
                                    IObjectView value = (IObjectView)row;
                                    if (value != null) {
                                        ((TString)rowItemTemplate.Properties[0]).Setter(jsonRow, DbHelper.GetObjectNo(value).ToString());
                                    }
                                    break;
                                case DbTypeCode.SByte:
									((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                case DbTypeCode.Single:
									((TDouble)rowItemTemplate.Properties[0]).Setter(jsonRow, (Double)(Single)row);
                                    break;
                                case DbTypeCode.String:
									((TString)rowItemTemplate.Properties[0]).Setter(jsonRow, (String)row);
                                    break;
                                case DbTypeCode.UInt16:
									((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                case DbTypeCode.UInt32:
									((TLong)rowItemTemplate.Properties[0]).Setter(jsonRow, (long)row);
                                    break;
                                case DbTypeCode.UInt64:
									var col = rowItemTemplate.Properties[0];
									if (col is TString) {
										((TString)col).Setter(jsonRow, row.ToString());
									} else {
										((TLong)col).Setter(jsonRow, (long)row);
									}
                                    break;
                                default:
                                    throw new NotImplementedException(string.Format("The handling of the TypeCode {0} has not yet been implemented", sqle.ProjectionTypeCode.ToString()));
                            }
                            #endregion
                        }
                        else {

                            IObjectView obj = (IObjectView)row;

                            IPropertyBinding prop;
                            for (int pi = 0; pi < props.Length; pi++) {
                                prop = props[pi];

                                switch (prop.TypeCode) {
                                    #region GetValue
                                    //case DbTypeCode.Binary:
                                    //    value = obj.GetBinary(prop.Index);
                                    //    break;
                                    case DbTypeCode.Boolean:
                                        ((TBool)rowItemTemplate.Properties[pi]).Setter(jsonRow, (bool)obj.GetBoolean(prop.Index));
                                        break;
                                    case DbTypeCode.Byte:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetByte(prop.Index));
                                        break;
                                    case DbTypeCode.DateTime:
                                        ((TString)rowItemTemplate.Properties[pi]).Setter(jsonRow, obj.GetDateTime(prop.Index).ToString());
                                        break;
                                    case DbTypeCode.Decimal:
                                        ((TDecimal)rowItemTemplate.Properties[pi]).Setter(jsonRow, (Decimal)obj.GetDecimal(prop.Index));
                                        break;
                                    case DbTypeCode.Double:
                                        ((TDouble)rowItemTemplate.Properties[pi]).Setter(jsonRow, (Double)obj.GetDouble(prop.Index));
                                        break;
                                    case DbTypeCode.Int16:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetInt16(prop.Index));
                                        break;
                                    case DbTypeCode.Int32:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetInt32(prop.Index));
                                        break;
                                    case DbTypeCode.Int64:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetInt64(prop.Index));
                                        break;
                                    //case DbTypeCode.LargeBinary:
                                    //    value = obj.GetBinary(prop.Index);
                                    //    break;
                                    case DbTypeCode.Object:

                                        IObjectView value = obj.GetObject(prop.Index);
                                        if (value != null) {
                                            ((TString)rowItemTemplate.Properties[pi]).Setter(jsonRow, DbHelper.GetObjectNo(value).ToString());
                                        }
                                        break;
                                    case DbTypeCode.SByte:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetSByte(prop.Index));
                                        break;
                                    case DbTypeCode.Single:
                                        ((TDouble)rowItemTemplate.Properties[pi]).Setter(jsonRow, (Double)obj.GetSingle(prop.Index));
                                        break;
                                    case DbTypeCode.String:
                                        ((TString)rowItemTemplate.Properties[pi]).Setter(jsonRow, obj.GetString(prop.Index));
                                        break;
                                    case DbTypeCode.UInt16:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetUInt16(prop.Index));
                                        break;
                                    case DbTypeCode.UInt32:
                                        ((TLong)rowItemTemplate.Properties[pi]).Setter(jsonRow, (long)obj.GetUInt32(prop.Index));
                                        break;
                                    case DbTypeCode.UInt64:
										var col = rowItemTemplate.Properties[pi];
										if (col is TString) {
											((TString)col).Setter(jsonRow, obj.GetUInt64(prop.Index).ToString());
										} else {
											((TLong)col).Setter(jsonRow, (long)obj.GetUInt64(prop.Index));
										}
										break;
                                    default:
                                        throw new NotImplementedException(string.Format("The handling of the TypeCode {0} has not yet been implemented", prop.TypeCode.ToString()));
                                    #endregion
                                }
                            }
                        }


                        index++;

                    }

                    #endregion
                }
            }
            catch (Starcounter.SqlException ee) {

                results.sqlException.beginPosition = ee.BeginPosition;
                results.sqlException.endPosition = ee.EndPosition;
                results.sqlException.helpLink = ee.HelpLink;
                results.sqlException.message = ee.Message;
                results.sqlException.scErrorCode = ee.ScErrorCode;
                results.sqlException.token = ee.Token;
                results.sqlException.stackTrace = ee.StackTrace;
                results.hasSqlException = true;
            }
            catch (Exception e) {

                LogSources.Sql.LogException(e);

                results.exception.helpLink = e.HelpLink;
                results.exception.message = e.GetType().ToString() + ": " + e.Message;
                results.exception.stackTrace = e.StackTrace;
                results.hasException = true;
            }
            finally {
                if (sqle != null)
                    sqle.Dispose();
            }

            return results;
        }

        private static void AddProperty(TObject parent, SqlQueryResult.columnsElementJson col, DbTypeCode dbTypeCode) {
            switch (dbTypeCode) {
                case DbTypeCode.Binary:
                case DbTypeCode.LargeBinary:
                    throw new NotSupportedException();
                case DbTypeCode.Object:
                    parent.Add<TString>(col.value);
                    break;
                case DbTypeCode.Boolean:
                    parent.Add<TBool>(col.value);
                    break;
                case DbTypeCode.Byte:
                case DbTypeCode.Int16:
                case DbTypeCode.Int32:
                case DbTypeCode.Int64:
                case DbTypeCode.SByte:
                case DbTypeCode.UInt16:
                case DbTypeCode.UInt32:
					parent.Add<TLong>(col.value);
                    break;

                case DbTypeCode.UInt64:
					if (col.title == "ObjectNo") {
						col.type = DbTypeCode.String.ToString();
						parent.Add<TString>(col.value);
					} else {
						parent.Add<TLong>(col.value);
					}
                    break;

                case DbTypeCode.DateTime:
                case DbTypeCode.String:
                    parent.Add<TString>(col.value);
                    break;
                case DbTypeCode.Decimal:
                    parent.Add<TDecimal>(col.value);
                    break;
                case DbTypeCode.Double:
                case DbTypeCode.Single:
                    parent.Add<TDouble>(col.value);
                    break;
            }
        }

        internal class SingleProjectionBinding : IPropertyBinding {
            private DbTypeCode typeCode;

            public bool AssertEquals(IPropertyBinding other) {
                throw new NotImplementedException();
            }

            public int Index {
                get { return 0; }
            }

            public string Name {
                get;
                set;
            }

            public string DisplayName { get; set; }

            public ITypeBinding TypeBinding {
                get { return null; }
            }

            public DbTypeCode TypeCode {
                get { return typeCode; }
                set { typeCode = value; }
            }
        }


    }


    internal class CircularStream : Stream {

        private long _Position = 0;
        private bool _IsBufferFull = false;
        private byte[] _Buffer;
        Action<string> CallbackDelegate;

        #region properties

        public override bool CanRead {
            get { return true; }
        }

        public override bool CanSeek {
            get { return false; }
        }

        public override bool CanWrite {
            get { return true; }
        }

        #endregion


        public CircularStream(long size) {
            if (size <= 0) throw new ArgumentException("size");
            this._Buffer = new byte[size];
        }

        public CircularStream(long size, Action<string> callbackDelegate) {
            if (size <= 0) throw new ArgumentException("size");
            this._Buffer = new byte[size];
            CallbackDelegate = callbackDelegate;
        }

        public override void Flush() {

        }

        public override long Length {
            get {
                if (this._IsBufferFull) return this._Buffer.Length;
                return this._Position;
            }
        }

        public override long Position {
            get {
                throw new NotImplementedException();
            }
            set {
                throw new NotImplementedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {

            lock (this) {
                long pos = 0;
                if (this._IsBufferFull) {
                    pos = this._Position;
                }

                int i = 0;
                for (; i < count && i <= (this.Length - 1); i++) {
                    buffer[offset + i] = this._Buffer[pos++];
                    if (pos > (this._Buffer.Length - 1)) {
                        pos = 0;
                    }
                }
                return i;
            }
        }


        public override void Write(byte[] buffer, int offset, int count) {

            lock (this) {

                for (int i = 0; i < count; i++) {

                    _Buffer[this._Position++] = buffer[i];

                    if (this._Position == this._Buffer.Length) {
                        this._Position = 0;
                        this._IsBufferFull = true;
                    }
                }

                if (CallbackDelegate != null) {
                    CallbackDelegate.Invoke(System.Text.Encoding.Default.GetString(buffer, offset, count));
                }

            }

        }
    }
}
