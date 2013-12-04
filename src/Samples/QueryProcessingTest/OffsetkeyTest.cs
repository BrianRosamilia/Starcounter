using System;
using System.Collections.Generic;
using System.Diagnostics;
using Starcounter;
using Starcounter.Query.Execution;

namespace QueryProcessingTest {
    public static class OffsetkeyTest {
        public static void Master() {
            HelpMethods.LogEvent("Test offset key");
            ErrorCases();
            // Populate data
            User client = PopulateForTest();
            SomeTests(client);
            // Simple query
            TestDataModification("select a from account a where accountid > ?", client);
            Db.Transaction(delegate {
                TestDataModification("select a from account a where accountid > ?", client);
            });
            // Join with index scan only
            TestDataModification("select a1 from account a1, Account a2 where a1.accountid > ? and a1.AccountId = a2.accountid and a1.Client = a2.client", client);
            Db.Transaction(delegate {
                TestDataModification("select a1 from account a1, Account a2 where a1.accountid > ? and a1.AccountId = a2.accountid and a1.Client = a2.client", client);
            });
            // Join with index scan and full table scan
            TestDataModification("select a1 from account a1, Account a2 where a1.accountid > ? and a1.Client = a2.client and a1.Amount = a2.Amount", client);
            Db.Transaction(delegate {
                TestDataModification("select a1 from account a1, Account a2 where a1.accountid > ? and a1.Client = a2.client and a1.Amount = a2.Amount", client);
            });
            // Reference look up
            TestDataModification("select a1 from account a1, Account a2, User u where a1.accountid > ? and a1.AccountId = a2.accountid and a1.Client = u and u = a2.client", client);
            Db.Transaction(delegate {
                TestDataModification("select a1 from account a1, Account a2, User u where a1.accountid > ? and a1.AccountId = a2.accountid and a1.Client = u and u = a2.client", client);
            });
            // Multiple joins
            TestDataModification("select a1 from account a1, Account a2, Account a3, User u " +
                "where a1.accountid > ? and a1.AccountId = a2.accountid and a1.AccountId = a3.accountid and a2.client.userid = a3.client.userid and a1.Client = u and u = a2.client and a1.amount = a3.amount", client);
            Db.Transaction(delegate {
                TestDataModification("select a1 from account a1, Account a2, Account a3, User u " +
                    "where a1.accountid > ? and a1.AccountId = a2.accountid and a1.AccountId = a3.accountid and a2.client.userid = a3.client.userid and a1.Client = u and u = a2.client and a1.amount = a3.amount", client);
            });
            // Involving object identity lookup
            TestDataModification("select a1 from account a1, Account a2 where a1.accountid > ? and a1.ObjectNo = a2.ObjectNo", client);
            Db.Transaction(delegate {
                TestDataModification("select a1 from account a1, Account a2 where a1.accountid > ? and a1.ObjectNo = a2.ObjectNo", client);
            });
            // With operators
            // Drop data
            DropAfterTest();
            HelpMethods.LogEvent("Finished testing offset key");
        }

        static void ErrorCases() {
            // Test getting offset key outside enumerator
            string f = "fetch ?";
            dynamic n = 4;
            String query = "select u from user u ";
            IRowEnumerator<dynamic> e = Db.SQL(query + f, n).GetEnumerator();
            byte[] k = e.GetOffsetKey();
            Trace.Assert(k == null);
            e.Dispose();
            e = Db.SQL(query + f, n).GetEnumerator();
            while (e.MoveNext())
                Trace.Assert(e.Current is User);
            k = e.GetOffsetKey();
            Trace.Assert(k == null);
            e.Dispose();
            // Test correct example
            f = "fetch ?";
            n = 4;
            e = Db.SQL(query + f, n).GetEnumerator();
            e.MoveNext();
            k = e.GetOffsetKey();
            e.Dispose();
            f = "offsetkey ?";
            n = k;
            e = Db.SQL(query + f, n).GetEnumerator();
            e.MoveNext();
            Trace.Assert(e.Current is User);
            e.Dispose();
            // Test offset key with incorrect object identity in a query
            ulong objectno = Db.SQL<ulong>("select objectno from account where accountid = ?", 1).First;
            e = Db.SQL("select a from account a where objectno = ?", objectno).GetEnumerator();
            Trace.Assert(e.MoveNext());
            k = e.GetOffsetKey();
            e.Dispose();
            objectno = Db.SQL<ulong>("select objectno from account where accountid = ?", 2).First;
            e = Db.SQL("select a from account a where objectno = ? offsetkey ?", objectno, k).GetEnumerator();
            Trace.Assert(!e.MoveNext());
            // Test using offset key for queries with different query plans but the same extent.
            // Obtain on index scan and try on full table scan
            e = Db.SQL("select a from account a").GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(IndexScan));
            Trace.Assert(e.MoveNext());
            k = e.GetOffsetKey();
            e.Dispose();
            Trace.Assert(k != null);
            e = Db.SQL("select a from account a where amount > ? offsetkey ?", 10, k).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(FullTableScan));
            bool isException = false;
            try {
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            }
            e.Dispose();
            Trace.Assert(isException);
            // Obtain on full table scan and try on index scan
            e = Db.SQL("select a from account a where amount > ?", 10).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(FullTableScan));
            Trace.Assert(e.MoveNext());
            k = e.GetOffsetKey();
            e.Dispose();
            Trace.Assert(k != null);
            e = Db.SQL("select a from account a where accountid < ? offsetkey ?", 10, k).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(IndexScan));
            isException = false;
            try {
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            }
            e.Dispose();
            Trace.Assert(isException);
            // Obtain on index scan and try on object identity lookup
            e = Db.SQL("select a from account a, account a2 where a.accountid = ? and a.accountid < a2.accountid", 10).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(Join));
            Trace.Assert(((Join)((SqlEnumerator<dynamic>)e).subEnumerator).LeftEnumerator.GetType() == typeof(IndexScan));
            Trace.Assert(e.MoveNext());
            objectno = ((Account)e.Current).GetObjectNo();
            k = e.GetOffsetKey();
            e.Dispose();
            Trace.Assert(k != null);
            e = Db.SQL("select a from account a, account a2 where a.objectno = ? and a.accountid < a2.accountid offsetkey ?", objectno, k).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(Join));
            Trace.Assert(((Join)((SqlEnumerator<dynamic>)e).subEnumerator).LeftEnumerator.GetType() == typeof(ObjectIdentityLookup));
            isException = false;
            try {
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            }
            e.Dispose();
            Trace.Assert(isException);
            // Obtain on index scan and try on refernce lookup
            e = Db.SQL("select a from account a, user u where a.accountid = ? and u.useridnr = ?", 10, 2).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(Join));
            Trace.Assert(((Join)((SqlEnumerator<dynamic>)e).subEnumerator).RightEnumerator.GetType() == typeof(FullTableScan));
            Trace.Assert(e.MoveNext());
            k = e.GetOffsetKey();
            e.Dispose();
            Trace.Assert(k != null);
            e = Db.SQL("select a from account a, user u where a.accountid = ? and a.client = u offsetkey ?", 10, k).GetEnumerator();
            Trace.Assert(((SqlEnumerator<dynamic>)e).subEnumerator.GetType() == typeof(Join));
            Trace.Assert(((Join)((SqlEnumerator<dynamic>)e).subEnumerator).RightEnumerator.GetType() == typeof(ReferenceLookup));
            isException = false;
            try {
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            }
            e.Dispose();
            Trace.Assert(isException);
            // Test changes in the size of the node tree
            e = Db.SQL("select a from account a, user u where a.accountid = ? and a.client = u", 10).GetEnumerator();
            Trace.Assert(e.MoveNext());
            k = e.GetOffsetKey();
            e.Dispose();
            Trace.Assert(k != null);
            e = Db.SQL("select a from account a where a.accountid = ? offsetkey ?", 10, k).GetEnumerator();
            isException = false;
            try {
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            }
            e.Dispose();
            Trace.Assert(isException);
            // More complex test
            e = Db.SQL("select a from account a, user u where a.accountid = ? and a.client = u", 10).GetEnumerator();
            Trace.Assert(e.MoveNext());
            k = e.GetOffsetKey();
            e.Dispose();
            Trace.Assert(k != null);
            e = Db.SQL("select a from account a, user u where a.accountid = ? and a.client.userid = u.userid offsetkey ?", 10, k).GetEnumerator();
            isException = false;
            try {
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            }
            e.Dispose();
            Trace.Assert(isException);
            // Test offsetkey on the query with the offset key from another query
            isException = false;
            e = Db.SQL("select u from user u fetch ?", 4).GetEnumerator();
            e.MoveNext();
            Trace.Assert(e.Current is User);
            k = e.GetOffsetKey();
            e.Dispose();
            try {
                e = Db.SQL("select u from user u where useridnr < ? offsetkey ?", 5, k).GetEnumerator();
                e.MoveNext();
                Trace.Assert(e.Current is User);
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            } finally {
                e.Dispose();
            }
            Trace.Assert(isException);
            isException = false;
            e = Db.SQL("select u from user u where useridnr < ? fetch ?", 5, 4).GetEnumerator();
            e.MoveNext();
            Trace.Assert(e.Current is User);
            k = e.GetOffsetKey();
            e.Dispose();
            try {
                e = Db.SQL("select u from user u offsetkey ?", k).GetEnumerator();
                e.MoveNext();
                Trace.Assert(e.Current is User);
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            } finally {
                e.Dispose();
            };
            Trace.Assert(isException);

            isException = false;
            e = Db.SQL("select u from user u fetch ?", 4).GetEnumerator();
            for (int i = 0; e.MoveNext(); i++) {
                Trace.Assert(e.Current is User);
                if (i == 3)
                    k = e.GetOffsetKey();
            }
            e.Dispose();
            Trace.Assert(k != null);
            try {
                e = Db.SQL("select u from user u where useridnr < ? offsetkey ?", 3, k).GetEnumerator();
                e.MoveNext();
            } catch (Exception ex) {
                uint error = (uint)ex.Data[ErrorCode.EC_TRANSPORT_KEY];
                Trace.Assert(error == Error.SCERRINVALIDOFFSETKEY);
                isException = true;
            } finally {
                e.Dispose();
            }
            Trace.Assert(isException);
#if false // Tests do not fail any more, since static data are not read from the recreation key.
#endif
        }

        static void SomeTests(User client) {
            byte[] key = null;
            int[] a1FetchExpected = new int[] { 30006, 30009 };
            int[] a2FetchExpected = new int[] { 30003, 30003 };
            using (IRowEnumerator<IObjectView> res =
                Db.SQL<IObjectView>("select a1,a2 from account a1, account a2 where a1.accountid > ? and a1.accountid > a2.accountid and a2.accountid > ? fetch ?",
                29999, 29999, 2).GetEnumerator()) {
                for (int i = 0; res.MoveNext(); i++) {
                    Trace.Assert(((Account)res.Current.GetObject(0)).AccountId == a1FetchExpected[i]);
                    Trace.Assert(((Account)res.Current.GetObject(1)).AccountId == a2FetchExpected[i]);
                    if (i == 1)
                        key = res.GetOffsetKey();
                }
            }
            Trace.Assert(key != null);
            int[] a1OffsetkeyExpected = new int[] { 30009, 30012 };
            int[] a2OffsetkeyExpected = new int[] { 30006, 30003 };
            using (IRowEnumerator<IObjectView> res =
                Db.SQL<IObjectView>("select a1,a2 from account a1, account a2 where a1.accountid > ? and a1.accountid > a2.accountid and a2.accountid > ? fetch ? offsetkey ?",
                29999, 29999, 2, key).GetEnumerator()) {
                int nrs = 0;
                while (res.MoveNext()) {
                    Account a1 = (Account)res.Current.GetObject(0);
                    Trace.Assert(a1.AccountId == a1OffsetkeyExpected[nrs]);
                    Account a2 = (Account)res.Current.GetObject(1);
                    Trace.Assert(a2.AccountId == a2OffsetkeyExpected[nrs]);
                    if (nrs == 1)
                        key = res.GetOffsetKey();
                    nrs++;
                }
                Trace.Assert(nrs == 2);
            }
            Trace.Assert(key != null);
            a1OffsetkeyExpected = new int[] { 30012, 30012 };
            a2OffsetkeyExpected = new int[] { 30006, 30009 };
            using (IRowEnumerator<IObjectView> res =
                Db.SQL<IObjectView>("select a1,a2 from account a1, account a2 where a1.accountid > ? and a1.accountid > a2.accountid and a2.accountid > ? fetch ? offsetkey ?",
                29999, 29999, 2, key).GetEnumerator()) {
                int nrs = 0;
                while (res.MoveNext()) {
                    Account a1 = (Account)res.Current.GetObject(0);
                    Trace.Assert(a1.AccountId == a1OffsetkeyExpected[nrs]);
                    Account a2 = (Account)res.Current.GetObject(1);
                    Trace.Assert(a2.AccountId == a2OffsetkeyExpected[nrs]);
                    if (nrs == 1)
                        key = res.GetOffsetKey();
                    nrs++;
                }
                Trace.Assert(nrs == 2);
                key = res.GetOffsetKey();
            }
            Trace.Assert(key == null);

            // Object identity test
            ulong objectno = Db.SQL<ulong>("select objectno from account where accountid = ?", 30003).First;
            using (IRowEnumerator<Account> res = Db.SQL<Account>("select a from account a where objectno = ?", objectno).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.GetObjectNo());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
                Trace.Assert(!res.MoveNext());
            }
            using (IRowEnumerator<Account> res = Db.SQL<Account>("select a from account a where objectno = ? offsetkey ?", objectno, key).GetEnumerator()) {
                Trace.Assert(!res.MoveNext());
                key = res.GetOffsetKey();
                Trace.Assert(key == null);
            }
            objectno = Db.SQL<ulong>("select objectno from user where useridnr = ?", 10005).First;
            using (IRowEnumerator<Account> res = Db.SQL<Account>("select a from account a where client.objectno = ?", objectno).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.Client.GetObjectNo());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
            }
            using (IRowEnumerator<Account> res = Db.SQL<Account>("select a from account a where client.objectno = ? offsetkey ?", objectno, key).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.Client.GetObjectNo());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
            }
            objectno = Db.SQL<ulong>("select objectno from user where useridnr = ?", 1).First;
            using (IRowEnumerator<Account> res = Db.SQL<Account>("select a from account a where client.objectno = ? offsetkey ?", 1, key).GetEnumerator()) {
                Trace.Assert(!res.MoveNext());
            }
            objectno = Db.SQL<ulong>("select objectno from user where useridnr = ?", 5).First;
            using (IRowEnumerator<User> res = Db.SQL<User>("select u1 from user u1, user u2 where u1.objectno = ? and u2.objectno < u1.objectno", objectno).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.GetObjectNo());
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.GetObjectNo());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
            }
            using (IRowEnumerator<User> res = Db.SQL<User>("select u1 from user u1, user u2 where u1.objectno = ? and u2.objectno < u1.objectno offsetkey ?", objectno, key).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.GetObjectNo());
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.GetObjectNo());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
            }
            using (IRowEnumerator<User> res = Db.SQL<User>("select u1 from user u1, user u2 where u1.objectno = ? and u2.objectno < u1.objectno offsetkey ?", objectno, key).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectno == res.Current.GetObjectNo());
                Trace.Assert(!res.MoveNext());
                key = res.GetOffsetKey();
                Trace.Assert(key == null);
            }
            String objectid = Db.SQL<string>("select objectid from user where useridnr = ?", 5).First;
            //Console.WriteLine(Db.SQL("select u1 from user u1, user u2 where u1.objectid = ? and u2.objectno < u1.objectno", objectid).GetEnumerator().ToString());
            using (IRowEnumerator<User> res = Db.SQL<User>("select u1 from user u1, user u2 where u1.objectid = ? and u2.objectno < u1.objectno", objectid).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectid == res.Current.GetObjectID());
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectid == res.Current.GetObjectID());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
            }
            using (IRowEnumerator<User> res = Db.SQL<User>("select u1 from user u1, user u2 where u1.objectid = ? and u2.objectno < u1.objectno offsetkey ?", objectid, key).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectid == res.Current.GetObjectID());
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectid == res.Current.GetObjectID());
                key = res.GetOffsetKey();
                Trace.Assert(key != null);
            }
            using (IRowEnumerator<User> res = Db.SQL<User>("select u1 from user u1, user u2 where u1.objectid = ? and u2.objectno < u1.objectno offsetkey ?", objectid, key).GetEnumerator()) {
                Trace.Assert(res.MoveNext());
                Trace.Assert(objectid == res.Current.GetObjectID());
                Trace.Assert(!res.MoveNext());
                key = res.GetOffsetKey();
                Trace.Assert(key == null);
            }
        }

        static void TestDataModification(String query, User client) {
            // Do set of tests
            byte[] key;
            // Do nothing
            key = DoFetch(query);
            DoOffsetkey(query, key, new int[] { GetAccountId(3), GetAccountId(4), GetAccountId(5) }); // offsetkey does not move forward
            // Drop inside fetched
            key = DoFetch(query);
            Db.Transaction(delegate {
                var o = Db.SQL("select a from account a where accountid = ?", GetAccountId(1)).First;
                o.Delete();
            });
            DoOffsetkey(query, key, new int[] {GetAccountId(3), GetAccountId(4), GetAccountId(5)}); // offsetkey does not move forward
            InsertAccount(GetAccountId(1), client);
            // Insert inside fetched
            key = DoFetch(query);
            InsertAccount(GetAccountId(1) + 1, client);
            DoOffsetkey(query, key, new int[] { GetAccountId(3), GetAccountId(4), GetAccountId(5) }); // offsetkey does not move forward
            Db.Transaction(delegate {
                Db.SQL<Account>("select a from account a where accountid = ?", GetAccountId(1) + 1).First.Delete();
            });
            // Insert later after offsetkey
            key = DoFetch(query);
            InsertAccount(GetAccountId(3) + 1, client);
            DoOffsetkey(query, key, new int[] { GetAccountId(3), GetAccountId(3) + 1, GetAccountId(4), GetAccountId(5) });
            Db.Transaction(delegate {
                Db.SQL<Account>("select a from account a where accountid = ?", GetAccountId(3) + 1).First.Delete();
            });
            // Insert after offsetkey
            key = DoFetch(query);
            InsertAccount(GetAccountId(2) + 1, client);
            DoOffsetkey(query, key, new int[] { GetAccountId(2) + 1, GetAccountId(3), GetAccountId(4), GetAccountId(5) });
            Db.Transaction(delegate {
                Db.SQL<Account>("select a from account a where accountid = ?", GetAccountId(2) + 1).First.Delete();
            });
            // Delete after offsetkey
            key = DoFetch(query);
            Db.Transaction(delegate {
                Db.SQL<Account>("select a from account a where accountid = ?", GetAccountId(3)).First.Delete();
            });
            DoOffsetkey(query, key, new int[] { GetAccountId(4), GetAccountId(5) });
            InsertAccount(GetAccountId(3), client);
            // Delete the offset key
            key = DoFetch(query);
            Db.Transaction(delegate {
                Db.SQL<Account>("select a from account a where accountid = ?", GetAccountId(2)).First.Delete();
            });
            DoOffsetkey(query, key, new int[] { GetAccountId(3), GetAccountId(4), GetAccountId(5) });
            InsertAccount(GetAccountId(2), client);
            // Delete and insert the offset key
            key = DoFetch(query);
            Db.Transaction(delegate {
                Db.SQL<Account>("select a from account a where accountid = ?", GetAccountId(2)).First.Delete();
            });
            InsertAccount(GetAccountId(2), client);
            DoOffsetkey(query, key, new int[] { GetAccountId(2), GetAccountId(3), GetAccountId(4), GetAccountId(5) }, true);
        }

        static byte[] DoFetch(String query) {
            int nrs = 0;
            byte[] key = null;
            Db.Transaction(delegate {
                using (IRowEnumerator<Account> res = Db.SQL<Account>(query + " fetch ?", AccountIdLast, 3).GetEnumerator())
                    while (res.MoveNext()) {
                        Trace.Assert(res.Current.AccountId == GetAccountId(nrs));
                        nrs++;
                        if (nrs == 3)
                            key = res.GetOffsetKey();
                    }
            });
            Trace.Assert(key != null);
            Trace.Assert(nrs == 3);
            return key;
        }

        static void DoOffsetkey(String query, byte[] key, int[] expectedResult, Boolean keyReinsert) {
            int nrs = 0;
            Boolean keyIncluded = true;
            Db.Transaction(delegate {
                foreach (Account a in Db.SQL<Account>(query + " fetch ? offsetkey ?", AccountIdLast, expectedResult.Length, key)) {
                    if (nrs == 0 && keyReinsert)
                        keyIncluded = a.AccountId == expectedResult[nrs];
                    if (keyIncluded)
                        Trace.Assert(a.AccountId == expectedResult[nrs]);
                    else
                        Trace.Assert(a.AccountId == expectedResult[nrs + 1]);
                    nrs++;
                }
            });
            if (keyIncluded)
                Trace.Assert(nrs == expectedResult.Length);
            else
                Trace.Assert(nrs + 1 == expectedResult.Length);
        }

        static void DoOffsetkey(String query, byte[] key, int[] expectedResult) {
            DoOffsetkey(query, key, expectedResult, false);
        }

        static User PopulateForTest() {
            User client = null;
            Db.Transaction(delegate {
                Trace.Assert(Db.SQL<Account>("select a from account a order by accountid desc").First.AccountId == AccountIdLast);
                Trace.Assert(Db.SlowSQL<long>("select count(u) from user u").First == 10000);
                client = new User {
                    UserIdNr = 10005,
                    BirthDay = new DateTime(1983, 03, 23),
                    FirstName = "Test",
                    LastName = "User",
                    UserId = DataPopulation.FakeUserId(10005)
                };
                for (int i = 0; i < 6; i++)
                    InsertAccount(GetAccountId(i), client);
            });
            return client;
        }

        static void DropAfterTest() {
            User client = null;
            Db.Transaction(delegate {
                int nrs = 0;
                foreach (Account a in Db.SQL<Account>("select a from account a where accountid > ?", AccountIdLast)) {
                    client = a.Client;
                    a.Delete();
                    nrs++;
                }
                Trace.Assert(client.GetObjectNo() == Db.SlowSQL<User>("select u from user u where firstname = 'Test'").First.GetObjectNo());
                Trace.Assert(client.Equals(Db.SlowSQL<User>("select u from user u where firstname = 'Test'").First));
                Trace.Assert(client != Db.SlowSQL<User>("select u from user u where firstname = 'Test'").First);
                Db.SlowSQL("delete from user where firstname = 'Test'");
                //Trace.Assert(client == null);
                Trace.Assert(nrs == 6);
                Trace.Assert(Db.SQL<Account>("select a from account a order by accountid desc").First.AccountId == AccountIdLast);
                Trace.Assert(Db.SlowSQL<long>("select count(u) from user u").First == 10000);
            });
        }

        static int GetAccountId(int i) {
            return 10000 * 3 + (i + 1) * 3;
        }
        static int AccountIdLast = 10000 * 3 - 1;

        static Account InsertAccount(int accountId, User client) {
            Account a = null;
            Db.Transaction(delegate {
                a = new Account { AccountId = accountId, Amount = 100.0m - (accountId - AccountIdLast) * 3, Client = client, When = DateTime.Now };
            });
            return a;
        }
    }
    /*
     * I. Queries (index, non-index, codegen)
     * I.1. Simple select
     * I.2. With where clause
     * I.3. With arithmetic expression
     * I.4. With path expression
     * I.5. With equi-join
     * I.6. With join
     * I.7. With multiple join
     * I.8. With outer join
     * I.9. With fetch
     * 
     * II. Iterations and offset key fetching
     * II.1. Fetch inside and iterate to the end
     * II.2. Fetch inside and iterate to the middle
     * II.4. Fetch to the last
     * II.5. Fetch outside
     * 
     * III. Data
     * III.1. No updates
     * III.2/3. Insert/Delete later
     * III.4/5. Insert/Delete before
     * III.6. Delete offset key
     * III.7. Delete the next
     * III.8. Insert the next after the offset key
     * III.9. Insert next after the next
     * III.10. Delete and insert the offset key
     * III.11. Delete and insert the next
     * III.12. Delete the offset key and insert next
     * III.13. Non-unique inserts (with deletes) around the offset key
     * 
     * IV. Transactions
     * IV.1. No transaction scope
     * IV.2. One transaction scope insert inside/outside
     * IV.3. One transaction scope with snapshot isolation inside/outside
     * IV.4. Two separate transaction scopes
     */
}
