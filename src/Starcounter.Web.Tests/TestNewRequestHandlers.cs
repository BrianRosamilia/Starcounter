using NUnit.Framework;
using Starcounter.Advanced;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Starcounter.Internal.Tests;
using Starcounter.Rest;

namespace Starcounter.Internal.Test
{
    /// <summary>
    /// Tests user HTTP delegates registration and usage.
    /// </summary>
    [TestFixture]
    public class UserHttpDelegateTests
    {
        public static Response UserFunc1(Int32 p1, Int64 p2, Request r, String p3)
        {
            Assert.IsTrue(123 == p1);
            Assert.IsTrue(21 == p2);
            Assert.IsTrue("hehe!" == p3);
            Assert.IsTrue("/dashboard/123" == r.Uri);

            return "UserFunc1!";
        }

        public static Response UserFunc2(Int32 p1, Int64 p2, String p3, Decimal p4)
        {
            Assert.IsTrue(123 == p1);
            Assert.IsTrue(21 == p2);
            Assert.IsTrue("hehe!" == p3);
            Assert.IsTrue(3.141m == p4);

            return "UserFunc2!";
        }

        public static Response UserFunc3(Int32 p1, Int64 p2, String p3, Request r, Decimal p4, Double p5, Boolean p6)
        {
            Assert.IsTrue(123 == p1);
            Assert.IsTrue(21 == p2);
            Assert.IsTrue("hehe!" == p3);
            Assert.IsTrue(3.141m == p4);
            Assert.IsTrue(9.8 == p5);
            Assert.IsTrue(true == p6);
            Assert.IsTrue("/dashboard/123" == r.Uri);

            return "UserFunc3!";
        }

        public static Response UserFunc4(Int32 p1, Int64 p2, String p3, Decimal p4, Double p5, Boolean p6, DateTime p7, Request r)
        {
            Assert.IsTrue(123 == p1);
            Assert.IsTrue(21 == p2);
            Assert.IsTrue("hehe!" == p3);
            Assert.IsTrue(3.141m == p4);
            Assert.IsTrue(9.8 == p5);
            Assert.IsTrue(true == p6);
            Assert.IsTrue(0 == p7.CompareTo(DateTime.Parse("10-1-2009 19:34")));
            Assert.IsTrue("/dashboard/123" == r.Uri);

            return "UserFunc4!";
        }

        public static Response UserFunc5(Int32 p1, Decimal p2, Int32 p3, Int32 p4)
        {
            Assert.IsTrue(-3853984 == p1);
            Assert.IsTrue(-3535m == p2);
            Assert.IsTrue(1234 == p3);
            Assert.IsTrue(-78 == p4);

            return "UserFunc5!";
        }

        public static Response UserFunc6(Int64 p1, Decimal p2, Int32 p3, Int32 p4, PersonMessage m)
        {
            Assert.IsTrue(-3853984 == p1);
            Assert.IsTrue(-3535m == p2);
            Assert.IsTrue(1234 == p3);
            Assert.IsTrue(-78 == p4);

            dynamic dj = m; // Using dynamic just to use properties instead of template lookups.
            Assert.AreEqual("Allan", dj.FirstName);
            Assert.AreEqual("Ballan", dj.LastName);
            Assert.AreEqual(19, dj.Age);
            Assert.AreEqual("123-555-7890", dj.PhoneNumbers[0].Number);

            return "UserFunc6!";
        }

        public static Response UserFunc7(PersonMessage m, Int64 p1, Decimal p2, Int64 p3, Int32 p4, Request r)
        {
            Assert.IsTrue(-3853984 == p1);
            Assert.IsTrue(-3535m == p2);
            Assert.IsTrue(1234 == p3);
            Assert.IsTrue(-78 == p4);

            dynamic dj = m; // Using dynamic just to use properties instead of template lookups.
            Assert.AreEqual("Allan", dj.FirstName);
            Assert.AreEqual("Ballan", dj.LastName);
            Assert.AreEqual(19, dj.Age);
            Assert.AreEqual("123-555-7890", dj.PhoneNumbers[0].Number);

            return "UserFunc7!";
        }

        public static Response UserFunc8(PersonMessage m, dynamic dj, Int64 p1, Decimal p2, Int32 p3, Int64 p4, Request r)
        {
            Assert.IsTrue(-3853984 == p1);
            Assert.IsTrue(-3535m == p2);
            Assert.IsTrue(1234 == p3);
            Assert.IsTrue(-78 == p4);

            return "UserFunc8!";
        }

        public static Response UserFunc9(dynamic dj, Request r) {
            Assert.AreEqual("Allan", dj.FirstName);
            Assert.AreEqual("Ballan", dj.LastName);
            Assert.AreEqual(19, dj.Age);
            Assert.AreEqual("123-555-7890", dj.PhoneNumbers[0].Number);
            
            return "UserFunc9!";
        }

        public static Response UserFunc10(PersonMessage m, Request r) {
            dynamic dj = m; // Using dynamic just to use properties instead of template lookups.
            Assert.AreEqual("Allan", dj.FirstName);
            Assert.AreEqual("Ballan", dj.LastName);
            Assert.AreEqual(19, dj.Age);
            Assert.AreEqual("123-555-7890", dj.PhoneNumbers[0].Number);

            return "UserFunc10!";
        }


        /// <summary>
        /// Tests simple correct HTTP request.
        /// </summary>
        [Test]
        public void TestUserHttpHandlers()
        {
            Byte[] textHtml = Encoding.ASCII.GetBytes("ext/html");
            Byte[] applicationJson = Encoding.ASCII.GetBytes("ion/json");
            unsafe
            {
                fixed (Byte* textHtmlp = textHtml)
                {
                    UInt64 textHtmlNum = *(UInt64*)textHtmlp;
                }

                fixed (Byte* applicationJsonp = applicationJson)
                {
                    UInt64 applicationJsonNum = *(UInt64*)applicationJsonp;
                }
            }


            const Int32 numTests = 11;
            MixedCodeConstants.UserDelegateParamInfo[] paramsInfo = new MixedCodeConstants.UserDelegateParamInfo[numTests]
            {
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0),
                new MixedCodeConstants.UserDelegateParamInfo(0, 0)
            };


            String stringParams = "123#21#hehe!#3.141#9.8#true#10-1-2009 19:34#-3853984#-03535#0001234#-000078#";
            String jsonContent = "{\"FirstName\":\"Allan\",\"LastName\":\"Ballan\",\"Age\":19,\"PhoneNumbers\":[{\"Number\":\"123-555-7890\"}]}";
            Byte[] stringParamsBytes = Encoding.ASCII.GetBytes(stringParams);

            UInt16 curOffset = 0, prevOffset = 0;
            for (Int32 i = 0; i < numTests; i++)
            {
                curOffset = (UInt16)stringParams.IndexOf('#', prevOffset);
                paramsInfo[i].offset_ = prevOffset;
                paramsInfo[i].len_ = (UInt16)(curOffset - prevOffset);
                prevOffset = (UInt16)(curOffset + 1);
            }

            Func<Request, IntPtr, IntPtr, Response> genDel1 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}", new Func<Int32, Int64, Request, String, Response>(UserHttpDelegateTests.UserFunc1));
            Func<Request, IntPtr, IntPtr, Response> genDel2 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}", new Func<Int32, Int64, String, Decimal, Response>(UserHttpDelegateTests.UserFunc2));
            Func<Request, IntPtr, IntPtr, Response> genDel3 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}/{?}/{?}", new Func<Int32, Int64, String, Request, Decimal, Double, Boolean, Response>(UserHttpDelegateTests.UserFunc3));
            Func<Request, IntPtr, IntPtr, Response> genDel4 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}/{?}/{?}/{?}", new Func<Int32, Int64, String, Decimal, Double, Boolean, DateTime, Request, Response>(UserHttpDelegateTests.UserFunc4));
            Func<Request, IntPtr, IntPtr, Response> genDel5 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}", new Func<Int32, Decimal, Int32, Int32, Response>(UserHttpDelegateTests.UserFunc5));
            Func<Request, IntPtr, IntPtr, Response> genDel6 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}", new Func<Int64, Decimal, Int32, Int32, PersonMessage, Response>(UserHttpDelegateTests.UserFunc6));
            Func<Request, IntPtr, IntPtr, Response> genDel7 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}", new Func<PersonMessage, Int64, Decimal, Int64, Int32, Request, Response>(UserHttpDelegateTests.UserFunc7));
            Func<Request, IntPtr, IntPtr, Response> genDel8 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /{?}/{?}/{?}/{?}", new Func<PersonMessage, Object, Int64, Decimal, Int32, Int64, Request, Response>(UserHttpDelegateTests.UserFunc8));

            Func<Request, IntPtr, IntPtr, Response> genDel9 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /a", new Func<Object, Request, Response>(UserHttpDelegateTests.UserFunc9));
            Func<Request, IntPtr, IntPtr, Response> genDel10 = UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(80, "GET /b", new Func<PersonMessage, Request, Response>(UserHttpDelegateTests.UserFunc10));

            unsafe
            {
                fixed (Byte* p1 = stringParamsBytes)
                {
                    fixed (MixedCodeConstants.UserDelegateParamInfo* p2 = paramsInfo)
                    {
                        Byte[] requestStrNoContent = Encoding.ASCII.GetBytes("GET /dashboard/123\r\nCrazyHeader: FAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKE\r\n\r\n");
                        Byte[] requestStrWithContent =
                            Encoding.ASCII.GetBytes("GET /dashboard/123\r\nCrazyHeader: FAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKE\r\nContent-Length:" 
                            + Encoding.ASCII.GetByteCount(jsonContent) 
                            + "\r\n\r\n" 
                            + jsonContent);

                        Assert.IsTrue("UserFunc1!" == genDel1(new Request(requestStrNoContent, requestStrNoContent.Length), (IntPtr)p1, (IntPtr)p2).Body);
                        Assert.IsTrue("UserFunc2!" == (String)genDel2(new Request(requestStrNoContent, requestStrNoContent.Length), (IntPtr)p1, (IntPtr)p2).Body);
                        Assert.IsTrue("UserFunc3!" == (String)genDel3(new Request(requestStrNoContent, requestStrNoContent.Length), (IntPtr)p1, (IntPtr)p2).Body);
                        Assert.IsTrue("UserFunc4!" == (String)genDel4(new Request(requestStrNoContent, requestStrNoContent.Length), (IntPtr)p1, (IntPtr)p2).Body);
                        Assert.IsTrue("UserFunc5!" == (String)genDel5(new Request(requestStrNoContent, requestStrNoContent.Length), (IntPtr)p1, (IntPtr)(p2 + 7)).Body);

                        Request req = new Request(requestStrWithContent, requestStrWithContent.Length);
                        req.ArgMessageObjectType = typeof(PersonMessage);

                        Assert.IsTrue("UserFunc6!" == (String)genDel6(req, (IntPtr)p1, (IntPtr)(p2 + 7)).Body);
                        Assert.IsTrue("UserFunc7!" == (String)genDel7(req, (IntPtr)p1, (IntPtr)(p2 + 7)).Body);
                        Assert.IsTrue("UserFunc8!" == (String)genDel8(req, (IntPtr)p1, (IntPtr)(p2 + 7)).Body);

                        Assert.IsTrue("UserFunc9!" == (String)genDel9(req, (IntPtr)p1, (IntPtr)(p2 + 7)).Body);
                        Assert.IsTrue("UserFunc10!" == (String)genDel10(req, (IntPtr)p1, (IntPtr)(p2 + 7)).Body);
                    }
                }
            }
        }

        /// <summary>
        /// Tests performance.
        /// </summary>
        [Test]
        public void TestUserHttpHandlersPerformance()
        {
            /*
            String intString = "-12345";
            Byte[] intStringBytes = Encoding.ASCII.GetBytes(intString);

            Stopwatch sw = new Stopwatch();

            sw.Start();
            Int64 r = 0;
            for (Int64 i = 0; i < 100000000; i++)
            {
                unsafe
                {
                    fixed (Byte* b = intStringBytes)
                    {
                        IntPtr ib = (IntPtr)b;
                        r += UserHandlerCodegen.FastParseInt(ib, 6); ///Int64.Parse(intString)
                    }
                }
            }

            sw.Stop();
            Console.WriteLine("Time ms: " + sw.ElapsedMilliseconds + " result: " + r);
            */
        }
    }
}