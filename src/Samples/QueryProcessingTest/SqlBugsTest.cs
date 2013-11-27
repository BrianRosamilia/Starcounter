using System;
using System.Collections;
using System.Diagnostics;
using Starcounter;
using Starcounter.Binding;
using Starcounter.Metadata;

namespace QueryProcessingTest {
    public static class SqlBugsTest {
        public static void QueryTests() {
            TestConjunctionBug1350();
            TestFetchOrderBy();
            TestLike();
            TestProjectionName();
            HelpMethods.LogEvent("Some tests on variables and case insensitivity");
            Account account = (Account)Db.SQL("select a from account a where client.firstname = ?", null).First;
            Trace.Assert(account == null);
            var row = Db.SlowSQL("select Client, count(accountid) from account group by Client").First;
            var row2 = Db.SlowSQL("select Client, count(accountid) from account group by client").First;
            Trace.Assert(row is IObjectView);
            Trace.Assert(row2 is IObjectView);
            Trace.Assert((row as IObjectView).GetObject(0).GetObjectNo() == (row2 as IObjectView).GetObject(0).GetObjectNo());
            Trace.Assert((row as IObjectView).GetInt64(1) == (row2 as IObjectView).GetInt64(1));
            account = Db.SQL<Account>("select a from account a where accountid = ?", null).First;
            Trace.Assert(account == null);
            HelpMethods.LogEvent("Finished some tests on variables and case insensitivity");
            TestComparison();
            TestEnumerators();
            QueryResultMismatch();
            TestIndexQueryOptimization();
            TestShortClassNames();
            TestDDLStmts();
        }

        public static void TestFetchOrderBy() {
            HelpMethods.LogEvent("Test queries with fetch and order by");
            // Test query with FETCH and ORDER BY
            decimal amounts = 0;
            int nrs = 0;
            var accounts = Db.SQL("select a from account a order by a.\"when\" desc fetch ?", 10);
            Account acc = (Account)accounts.First;
            QueryResultRows<dynamic> aquery = Db.SQL("select a from account a order by a.\"when\" desc fetch ?", 10);
            foreach (var a in aquery) {
                amounts += a.Amount;
                nrs++;
            }
            Trace.Assert(nrs == 10);
            nrs = 0;
            foreach (Account a in Db.SQL("select a from account a order by a.\"when\" desc fetch ?", 20))
                nrs++;
            Trace.Assert(nrs == 20);
            nrs = 0;
            foreach (Account a in Db.SQL("select a from account a order by a.accountid desc fetch ?", 10))
                nrs++;
            Trace.Assert(nrs == 10);
            nrs = 0;
            foreach (IObjectView obj in Db.SlowSQL("select client, count(\"when\") from account group by client order by client fetch ?", 10))
                nrs++;
            Trace.Assert(nrs == 10);
            nrs = 0;
            foreach (IObjectView obj in Db.SlowSQL("select client, count(\"when\") from account group by client order by client fetch ?", 20))
                nrs++;
            Trace.Assert(nrs == 20);
            TestOffsetkeyWithSorting();
            HelpMethods.LogEvent("Finished test query with fetch and sorting");
        }

        public static void TestLike() {
            HelpMethods.LogEvent("Test queries with like");
            // Test queries with LIKE ?
            //HelpMethods.PrintQueryPlan("select u from user u where userid like ?");
            int nrs = 0;
            var users = Db.SQL<User>("select u from user u where userid like ?", "kati%");
            foreach (User u in users)
                nrs++;
            Trace.Assert(nrs == 157);
            nrs = 0;
            foreach (User u in Db.SQL<User>("select u from user u where userid like ?", "%ti1%"))
                nrs++;
            Trace.Assert(nrs == 544);
            nrs = 0;
            foreach (User u in Db.SQL<User>("select u from user u where userid like ?", "kati1")) {
                Trace.Assert(u.UserId == "kati1");
                nrs++;
            }
            Trace.Assert(nrs == 1);
            HelpMethods.LogEvent("Finished test queries with like");
        }

        public static void TestOffsetkeyWithSorting() {
            // Offset key byte buffer.
            Byte[] offsetKey = null;

            // Starting some SQL query.
            //HelpMethods.PrintQueryPlan("SELECT a FROM Account a ORDER BY a.accountid FETCH ?");
            using (var sqlEnum = Db.SQL("SELECT a FROM Account a ORDER BY a.accountid FETCH ?", 5).GetEnumerator()) {
                for (Int32 i = 0; i < 5; i++) {
                    sqlEnum.MoveNext();
                    Trace.Assert(((Account)sqlEnum.Current).AccountId == i);
                }

                // Fetching the offset key.
                offsetKey = sqlEnum.GetOffsetKey();
                Trace.Assert(offsetKey != null);
                Trace.Assert(!sqlEnum.MoveNext());
            }

            // Now recreating the enumerator state using the offset key.
            //HelpMethods.PrintQueryPlan("SELECT a FROM Account a ORDER BY a.accountid FETCH ? OFFSETKEY ?");
            using (var sqlEnum = Db.SQL("SELECT a FROM Account a ORDER BY a.accountid FETCH ? OFFSETKEY ?", 10, offsetKey).GetEnumerator()) {
                for (Int32 i = 0; i < 10; i++) {
                    Trace.Assert(sqlEnum.MoveNext());
                    Account a = (Account)sqlEnum.Current;
                    Trace.Assert(a.AccountId == i+5);
                }
                Trace.Assert(!sqlEnum.MoveNext());
            }

#if false   // does not work
            // Another query with sorting on non-index property
            // Starting some SQL query.
            using (var sqlEnum = Db.SQL("SELECT a FROM Account a ORDER BY a.client FETCH ?", 5).GetEnumerator()) {
                for (Int32 i = 0; i < 5; i++) {
                    sqlEnum.MoveNext();
                    Trace.Assert(((Account)sqlEnum.Current).AccountId == i);
                }

                // Fetching the offset key.
                offsetKey = sqlEnum.GetOffsetKey();
                Trace.Assert(offsetKey != null);
                Trace.Assert(!sqlEnum.MoveNext());
            }

            // Now recreating the enumerator state using the offset key.
            using (var sqlEnum = Db.SQL("SELECT a FROM Account a ORDER BY a.client FETCH ? OFFSETKEY ?", 10, offsetKey).GetEnumerator()) {
                for (Int32 i = 0; i < 10; i++) {
                    sqlEnum.MoveNext();
                    Trace.Assert(((Account)sqlEnum.Current).AccountId == i + 4);
                }
                Trace.Assert(!sqlEnum.MoveNext());
            }
#endif
        }

        public static void TestProjectionName() {
            HelpMethods.LogEvent("Start testing projections");
            var q = Db.SlowSQL("select userid, useridnr from User u where useridnr <?", 2);
            var e = q.GetEnumerator();
            string n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0)).DisplayName;
            Trace.Assert(n == "UserId");
            Trace.Assert(((SqlEnumerator<object>)e).PropertyBinding == null);
            q = Db.SlowSQL("select userid, FirstName||' '||LastName from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1)).DisplayName;
            Trace.Assert(n == "1");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1).DisplayName;
            Trace.Assert(n == "1");
            Trace.Assert(((SqlEnumerator<object>)e).PropertyBinding == null);
            q = Db.SlowSQL("select userid from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            Trace.Assert(((SqlEnumerator<object>)e).TypeBinding == null);
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).PropertyBinding).DisplayName;
            Trace.Assert(n == "UserId");
            n = ((SqlEnumerator<object>)e).PropertyBinding.DisplayName;
            Trace.Assert(n == "UserId");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).PropertyBinding).Name;
            Trace.Assert(n == "0");
            n = ((SqlEnumerator<object>)e).PropertyBinding.Name;
            Trace.Assert(n == "0");
            var t = ((SqlEnumerator<object>)e).PropertyBinding.TypeCode;
            Trace.Assert(t == DbTypeCode.String);
            q = Db.SlowSQL("select userid, FirstName||' '||LastName as Name from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1)).DisplayName;
            Trace.Assert(n == "Name");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1).DisplayName;
            Trace.Assert(n == "Name");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1)).Name;
            Trace.Assert(n == "Name");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1).Name;
            Trace.Assert(n == "Name");
            Trace.Assert(((SqlEnumerator<object>)e).PropertyBinding == null);
            q = Db.SlowSQL("select userid, Name from User u where useridnr < ?", 2);
            e = q.GetEnumerator();
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1)).DisplayName;
            Trace.Assert(n == "Name");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1).DisplayName;
            Trace.Assert(n == "Name");
            Trace.Assert(((SqlEnumerator<object>)e).PropertyBinding == null);
            q = Db.SlowSQL("select name from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            Trace.Assert(((SqlEnumerator<object>)e).TypeBinding == null);
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).PropertyBinding).DisplayName;
            Trace.Assert(n == "Name");
            n = ((SqlEnumerator<object>)e).PropertyBinding.DisplayName;
            Trace.Assert(n == "Name");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).PropertyBinding).Name;
            Trace.Assert(n == "0");
            n = ((SqlEnumerator<object>)e).PropertyBinding.Name;
            Trace.Assert(n == "0");
            q = Db.SlowSQL("select name as FullName from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            Trace.Assert(((SqlEnumerator<object>)e).TypeBinding == null);
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).PropertyBinding).DisplayName;
            Trace.Assert(n == "FullName");
            n = ((SqlEnumerator<object>)e).PropertyBinding.DisplayName;
            Trace.Assert(n == "FullName");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).PropertyBinding).Name;
            Trace.Assert(n == "FullName");
            q = Db.SlowSQL("select name,firstname from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            Trace.Assert(((SqlEnumerator<object>)e).PropertyBinding == null);
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0)).DisplayName;
            Trace.Assert(n == "Name");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0).DisplayName;
            Trace.Assert(n == "Name");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0)).Name;
            Trace.Assert(n == "0");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1)).DisplayName;
            Trace.Assert(n == "FirstName");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1).DisplayName;
            Trace.Assert(n == "FirstName");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(1)).Name;
            Trace.Assert(n == "1");

            q = Db.SlowSQL("select * from User u where useridnr <?", 2);
            e = q.GetEnumerator();
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0)).DisplayName;
            Trace.Assert(n == "UserId");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0).DisplayName;
            Trace.Assert(n == "UserId");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0)).Name;
            Trace.Assert(n == "0");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(0).Name;
            Trace.Assert(n == "0");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(8)).DisplayName;
            Trace.Assert(n == "Name");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(8).DisplayName;
            Trace.Assert(n == "Name");
            n = ((Starcounter.Query.Execution.PropertyMapping)((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(8)).Name;
            Trace.Assert(n == "8");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(8).Name;
            Trace.Assert(n == "8");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(10).DisplayName;
            Trace.Assert(n == "ObjectNo");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(10).Name;
            Trace.Assert(n == "10");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(11).DisplayName;
            Trace.Assert(n == "ObjectID");
            n = ((SqlEnumerator<object>)e).TypeBinding.GetPropertyBinding(11).Name;
            Trace.Assert(n == "11");
            HelpMethods.LogEvent("Finished testing projections");
        }

        public static void TestComparison() {
            HelpMethods.LogEvent("Start testing queries on comparison bug");
            ulong accountTableId = Db.SQL<ulong>("select table_id from materialized_table where name = ?", "QueryProcessingTest.Account").First;
            var e = Db.SQL<materialized_table>("select s from materialized_table s where table_id = ?", accountTableId).GetEnumerator();
            Trace.Assert(e.MoveNext());
            materialized_table s = e.Current;
            Trace.Assert(s.name == "QueryProcessingTest.Account");
            Trace.Assert(s.table_id == accountTableId);
            e.Dispose();
            e = Db.SlowSQL<materialized_table>("select s from materialized_table s where table_id = "+accountTableId).GetEnumerator();
            Trace.Assert(e.MoveNext());
            s = e.Current;
            Trace.Assert(s.name == "QueryProcessingTest.Account");
            Trace.Assert(s.table_id == accountTableId);
            e.Dispose();
            e = Db.SlowSQL<materialized_table>("select s from materialized_table s where table_id = 10").GetEnumerator();
            Trace.Assert(e.MoveNext());
            e.Dispose();
            e = Db.SlowSQL<materialized_table>("select s from materialized_table s where table_id = 1.0E1").GetEnumerator();
            Trace.Assert(e.MoveNext());
            e.Dispose();
            HelpMethods.LogEvent("Finished testing queries on comparison bug");
        }

        public static void TestConjunctionBug1350() {
            HelpMethods.LogEvent("Start testing queries on conjunction bug (1350)");
            int nrs = 0;
            foreach (Account a in Db.SlowSQL<Account>("select a from account a where accounttype = ? and notactive = ?",
                DataPopulation.DAILYACCOUNT, false)) {
                    Trace.Assert(!a.NotActive);
                    Trace.Assert(a.AccountType == DataPopulation.DAILYACCOUNT);
                    nrs++;
            }
            Trace.Assert(nrs == 30000);
            string dailyChannel = "DailyBuilds";
            string nightlyChannel = "NightlyBuilds";
            Db.Transaction(delegate {
                new VersionSource {
                    BuildError = false,
                    Channel = nightlyChannel,
                    Version = "2.0.1191.3",
                    VersionDate = new DateTime(2013, 11, 16, 01, 00, 00)
                };
                new VersionSource {
                    BuildError = false,
                    Channel = nightlyChannel,
                    Version = "2.0.1197.3",
                    VersionDate = new DateTime(2013, 11, 18, 08, 29, 00)
                };
                new VersionSource {
                    BuildError = false,
                    Channel = dailyChannel,
                    Version = "2.0.5823.2",
                                    VersionDate = new DateTime(2013, 11, 18, 22, 25, 00) };
                new VersionSource {
                    BuildError = false, Channel = dailyChannel, Version = "2.0.5835.2",
                 VersionDate = new DateTime(2013, 11, 19, 12, 25, 00) };
                new VersionSource { BuildError = false, Channel = dailyChannel, Version = "2.0.5837.2",
                    VersionDate = new DateTime(2013, 11, 19, 15, 00, 00) };
            });
            var vsources = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Channel=? AND o.BuildError=?",
                dailyChannel, false).GetEnumerator();
            nrs = 0;
            while (vsources.MoveNext()){
                VersionSource v = vsources.Current;
                Trace.Assert(v.Channel == dailyChannel);
                Trace.Assert(!v.BuildError);
                nrs++;
            }
            Trace.Assert(nrs == 3);
            VersionSource latest = Db.SQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Channel=? AND o.BuildError=? order by versiondate desc",
                dailyChannel, false).First;
            Trace.Assert(latest != null);
            Trace.Assert(latest.Channel == dailyChannel);
            Trace.Assert(!latest.BuildError);
            HelpMethods.LogEvent("Finished testing queries on conjunction bug (1350)");
        }

        public static void TestEnumerators() {
            HelpMethods.LogEvent("Test enumerator related bugs");
            QueryResultRows<dynamic> accounts = Db.SQL("select accountid as accountid, client.name as name, amount as amount from account where accountid = ?", 1);
            Type t = accounts.First.GetType();
            Trace.Assert(t == typeof(Starcounter.Query.Execution.Row));
            t = accounts.First.GetType();
            Trace.Assert(t == typeof(Starcounter.Query.Execution.Row));
            long accountid = accounts.First.accountid;
            accountid = accounts.First.AccountId;
            Trace.Assert(accountid == 1);
            decimal amount = accounts.First.amount;
            amount = accounts.First.Amount;

            //Console.WriteLine(Db.SQL("select u from user u where nickname = ?", "Nk1").GetEnumerator().ToString());
#if false // Does not work
            accounts.First.Amount += 10;
            decimal newAmount = accounts.First.Amount;
            Trace.Assert(amount + 10 == newAmount);
#endif
            HelpMethods.LogEvent("Finished testing enumerator related bugs");
        }

        public static void QueryResultMismatch() {
            HelpMethods.LogEvent("Start testing query result mismatch errors.");
            bool wasException = false;
            try {
                var accs = Db.SQL<Account>("select * from account a");
                var a = accs.First;
            } catch (Exception exc) {
                if (exc.Data[ErrorCode.EC_TRANSPORT_KEY] == null || (uint)exc.Data[ErrorCode.EC_TRANSPORT_KEY] != Error.SCERRQUERYRESULTTYPEMISMATCH)
                    throw exc;
                wasException = true;
            }
            Trace.Assert(wasException);
            wasException = false;
            try {
                var users = Db.SQL<User>("select * from user u");
                using (var res = users.GetEnumerator()) {
                    Trace.Assert(res.MoveNext());
                    var row = res.Current;
                }
            } catch (Exception exc) {
                if (exc.Data[ErrorCode.EC_TRANSPORT_KEY] == null || (uint)exc.Data[ErrorCode.EC_TRANSPORT_KEY] != Error.SCERRQUERYRESULTTYPEMISMATCH)
                    throw exc;
                wasException = true;
            }
            Trace.Assert(wasException);
            wasException = false;
            try {
                var users2 = Db.SQL<Account>("select a.client from account a");
                using (var res = users2.GetEnumerator()) {
                    Trace.Assert(res.MoveNext());
                    var row = res.Current;
                }
            } catch (Exception exc) {
                if (exc.Data[ErrorCode.EC_TRANSPORT_KEY] == null || (uint)exc.Data[ErrorCode.EC_TRANSPORT_KEY] != Error.SCERRQUERYRESULTTYPEMISMATCH)
                    throw exc;
                wasException = true;
            }
            Trace.Assert(wasException);
            wasException = false;
            try {
                var astrs = Db.SQL<Account>("select name from user");
                using (var res = astrs.GetEnumerator()) {
                    Trace.Assert(res.MoveNext());
                    var row = res.Current;
                }
            } catch (Exception exc) {
                if (exc.Data[ErrorCode.EC_TRANSPORT_KEY] == null || (uint)exc.Data[ErrorCode.EC_TRANSPORT_KEY] != Error.SCERRQUERYRESULTTYPEMISMATCH)
                    throw exc;
                wasException = true;
            }
            Trace.Assert(wasException);
            wasException = false;
            try {
                var astrs = Db.SQL<Account>("select name from user");
                var decsres = astrs.First;
            } catch (Exception exc) {
                if (exc.Data[ErrorCode.EC_TRANSPORT_KEY] == null || (uint)exc.Data[ErrorCode.EC_TRANSPORT_KEY] != Error.SCERRQUERYRESULTTYPEMISMATCH)
                    throw exc;
                wasException = true;
            }
            Trace.Assert(wasException);
            // No exceptions
            var res1 = Db.SQL("select * from account").First;
            using (var query2 = Db.SQL<Starcounter.Query.Execution.Row>("select * from account").GetEnumerator()) {
                Trace.Assert(query2.MoveNext());
                var res2 = query2.Current;
            }
            var res3 = Db.SQL<Account>("select a from account a").First;
            var res4 = Db.SQL<Decimal>("select amount from account").First;
            var res5 = Db.SQL<Decimal>("select accountid from account").First;

            wasException = false;
            try {
                var astrs = Db.SQL<String>("select name from user").GetEnumerator();
                var decsres = astrs.Current;
            } catch (Exception exc) {
                if (exc.Data[ErrorCode.EC_TRANSPORT_KEY] == null || (uint)exc.Data[ErrorCode.EC_TRANSPORT_KEY] != Error.SCERRINVALIDCURRENT)
                    throw exc;
                wasException = true;
            }
            Trace.Assert(wasException);
            HelpMethods.LogEvent("Finished testing query result mismatch errors.");
        }

        public static void TestIndexQueryOptimization() {
            HelpMethods.LogEvent("Test query optimization with indexes");
            // Issue #563
            Trace.Assert(((SqlEnumerator<User>)Db.SQL<User>("select u from user u where nickname = ?", "Nk1").GetEnumerator()).subEnumerator.GetType() == typeof(Starcounter.Query.Execution.IndexScan));

            // Issue #645
            var query = Db.SQL<User>("select u from user u where nickname = ? and lastname = ?", "Nk2", "Ln2");
            var qEnum = query.GetEnumerator();
            String enumStr = qEnum.ToString();
            Trace.Assert(!enumStr.Contains("ComparisonString"));
            HelpMethods.LogEvent("Finished testing query optimization with indexes");
        }

        public static void TestShortClassNames() {
            HelpMethods.LogEvent("Test queries with classes having similar short names");
            Trace.Assert(Db.SQL<commonclass>("select c from commonclass c").First == null);
            HelpMethods.LogEvent("Finished testing queries with classes having similar short names");
        }

        public static void TestDDLStmts() {
            HelpMethods.LogEvent("Test DDL statements");
            Db.SQL("create index whenindx on account (when)");
            Db.SQL("create index whereindx on account (\"where\")");
            try {
                Db.SQL("create index anwhereindx on account (where)");
            } catch (SqlException) { }
            Trace.Assert(Db.SQL<materialized_index>("select s from materialized_index s where name = ?", "whenindx").First != null);
            Trace.Assert(Db.SQL<materialized_index>("select s from materialized_index s where name = ?", "whereindx").First != null);
            Trace.Assert(Db.SQL<materialized_index>("select s from materialized_index s where name = ?", "anwhereindx").First == null);
            Db.SQL("drop index whenindx on account ");
            Db.SQL("drop index whereindx on account");
            Trace.Assert(Db.SQL<materialized_index>("select s from materialized_index s where name = ?", "whenindx").First == null);
            Trace.Assert(Db.SQL<materialized_index>("select s from materialized_index s where name = ?", "whereindx").First == null);
            Trace.Assert(Db.SQL<materialized_index>("select s from materialized_index s where name = ?", "anwhereindx").First == null);
            HelpMethods.LogEvent("Finished testing DDL statements");
        }
    }
}
