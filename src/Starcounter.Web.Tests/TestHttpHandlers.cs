// ***********************************************************************
// <copyright file="TestHttpHandlers.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using NUnit.Framework;
using Starcounter.Internal.Uri;
using System;
using System.Text;
using Starcounter.Advanced;
using Starcounter.Rest;

namespace Starcounter.Internal.Test {

    // TODO: Reenable all comments.
    // TODO: Reenable all comments.
    // TODO: Reenable all comments.
    // TODO: Reenable all comments.
    // TODO: Reenable all comments.


    /// <summary>
    /// Used for tests initialization/shutdown.
    /// </summary>
    [SetUpFixture]
    class TestHttpHandlersSetup
    {
        /// <summary>
        /// Tests initialization.
        /// </summary>
        [SetUp]
        public void InitTestHttpHandlersSetup()
        {
            RequestHandler.InitREST();
        }
    }

    /// <summary>
    /// Class TestRoutes
    /// </summary>
    [TestFixture]
    class TestRoutes {

        public static void RegisterSimpleHandlers() {

            RequestHandler.Reset();

            Handle.GET("/__vm/{?}", (int p1) => "" );
            Handle.PATCH("/__vm/{?}", (int p1) => "" );
            Handle.GET("/{?}", (string p1) => "" );
            Handle.GET("/{?}/{?}", (string p1, string p2) => "" );
            Handle.GET("/{?}/{?}/{?}", (string p1, string p2,string p3) => "" );
            Handle.GET("/ab", () => "" );            
            /*
                                    Handle.GET("/{?}", (int x) => {
                                        Console.WriteLine("Root int called with x " + x );
                                        return "Handle.GET /@i";
                                    });
                                    Handle.GET("/{?}/{?}", (string a, int x) => {
                                        Console.WriteLine("Root int called with string " + a + " and int " + x);
                                        return "Handle.GET /@s/@i";
                                    });
                                     */
        }

        public static void Main() {
            UserHandlerCodegen.ResetHandlersManager();
            RequestHandler.Reset();

            /*Handle.GET("/@s/viewmodels/subs/@s", (string app, string vm) => {
                return "404 Not Found";
            });
            Handle.GET("/@s/viewmodels/@s", (string app, string vm) => {
                return "404 Not Found";
            });
            */

            Handle.GET("/", () => {
                Console.WriteLine("Handle.GET / called");
                return "Handle.GET /";
            });

            Handle.GET("/test", () => {
                Console.WriteLine("Handle.GET /test called");
                return null;
            });

            Handle.GET("/uri-with-req", (Request r) => {
                Assert.IsNotNull(r);
                return "Handle.GET /uri-with-req";
            });

            Handle.GET("/uri-with-req/{?}", (Request r, int i) => {
                Assert.AreEqual(123, i);
                Assert.IsNotNull(r);
                return "Handle.GET /uri-with-req/@i";
            });

            Handle.GET("/uri-with-req/{?}", (string s, Request r) => {
                Assert.AreEqual("KalleKula", s);
                Assert.IsNotNull(r);
                return "Handle.GET /uri-with-req/@s";
            });

            Handle.GET("/admin/apapapa/{?}", (int i, Request r) => {
                Assert.AreEqual(19, i);
                Assert.IsNotNull(r);
                return "Handle.GET /admin/apapapa/@i";
            });

            Handle.GET("/admin/{?}", (string s, Request r) => {
                Assert.AreEqual("KalleKula", s);
                Assert.IsNotNull(r);
                return "Handle.GET /admin/@s";
            });

            Handle.GET("/admin/{?}/{?}", (string s, int i, Request r) => {
                Assert.AreEqual("KalleKula", s);
                Assert.AreEqual(123, i);
                Assert.IsNotNull(r);
                return "Handle.GET /admin/@s/@i";
            });

            Handle.GET("/players", () => {
                Console.WriteLine("players");
                return "Handle.GET /players";
            });

            Handle.GET("/players/{?}/abc/{?}", (int playerId, string a) => {
                Assert.AreEqual(123, playerId);
                Console.WriteLine("playerId=" + playerId);
                return "Handle.GET /players/@i/abc/@s";
            });

            Handle.GET("/dashboard/{?}", (int playerId) => {
                Assert.AreEqual(123, playerId);
                Console.WriteLine("playerId=" + playerId);
                return "Handle.GET /dashboard/@i";
            });

            Handle.GET("/players/{?}", (int id) => {
                Assert.AreEqual(123, id);
                return "Handle.GET /players/@i";
            });

            Handle.GET("/players?{?}", (string fullName) => {
                Assert.AreEqual("KalleKula", fullName);
                Console.WriteLine("f=" + fullName);
                return "Handle.GET /players?@s";
            });

            Handle.GET("/whatever/{?}/more/{?}/{?}", (string v1, int v2, string v3) => {
                Assert.AreEqual("apapapa", v1);
                Assert.AreEqual(5547, v2);
                Assert.AreEqual("KalleKula", v3);
                return "Handle.GET /whatever/@s/more/@i/@s";
            });

            Handle.GET("/ordinary", () => {
                return "Handle.GET /ordinary";
            });

            Handle.GET("/ordAnary", () => {
                return "Handle.GET /ordAnary";
            });

            Handle.GET("/aaaaa/{?}/bbbb", (int v) => {
                Assert.AreEqual(90510, v);
                return "Handle.GET /aaaaa/@i/bbbb";
            });

            Handle.GET("/whatever/{?}/xxYx/{?}", (string v1, int v2) => {
                Assert.AreEqual("abrakadabra", v1);
                Assert.AreEqual(911, v2);
                return "Handle.GET /whatever/@s/xxYx/@i";
            });

            Handle.GET("/whatever/{?}/xxZx/{?}", (string v1, int v2) => {
                Assert.AreEqual("abrakadabra", v1);
                Assert.AreEqual(911, v2);
                return "Handle.GET /whatever/@s/xxZx/@i";
            });

            Handle.GET("/whatmore/{?}/xxZx/{?}", (string v1, int v2) => {
                Assert.AreEqual("abrakadabra", v1);
                Assert.AreEqual(911, v2);
                return "Handle.GET /whatmore/@s/xxZx/@i";
            });

            Handle.GET("/test-decimal/{?}", (decimal val) => {
                Assert.AreEqual(99.123m, val);
                return "Handle.GET /test-decimal/@m";
            });

            Handle.GET("/test-double/{?}", (double val) => {
                Assert.AreEqual(99.123d, val);
                return "Handle.GET /test-double/@d";
            });

            Handle.GET("/test-bool/{?}", (bool val) => {
                Assert.AreEqual(true, val);
                return "Handle.GET /test-bool/@b";
            });

            Handle.GET("/test-datetime/{?}", (DateTime val) => {
                DateTime expected;
                DateTime.TryParse("2013-01-17", out expected);
                Assert.AreEqual(expected, val);
                return "Handle.GET /test-datatime/@t";
            });

            Handle.GET("/static{?}/{?}", (string part, string last, Request request) => {
                Assert.AreEqual("marknad", part);
                Assert.AreEqual("nyhetsbrev", last);
                Assert.IsNotNull(request);
                return "Handle.GET /static@s/@s";
            });

            Handle.PUT("/players/{?}", (int playerId) => {
                Assert.AreEqual(123, playerId);
                //                Assert.IsNotNull(request);
                Console.WriteLine("playerId: " + playerId); //+ ", request: " + request);
                return "Handle.PUT /players/@i";
            });

            Handle.POST("/transfer?{?}", (int from) => {
                Assert.AreEqual(99, from);
                Console.WriteLine("From: " + from);
                return "Handle.POST /transfer?@i";
            });

            Handle.POST("/deposit?{?}", (int to) => {
                Assert.AreEqual(56754, to);
                Console.WriteLine("To: " + to);
                return "Handle.POST /deposit?@i";
            });

            Handle.POST("/find-player?firstname={?}&lastname={?}&age={?}", (string fn, string ln, int age) => {
                Assert.AreEqual("Kalle", fn);
                Assert.AreEqual("Kula", ln);
                Assert.AreEqual(19, age);
                return "Handle.POST /find-player?firstname=@s&lastname=@s&age=@i";
            });

            Handle.DELETE("/all", () => {
                Console.WriteLine("deleteAll");
                return "Handle.DELETE /all";
            });

        }

        [Test]
        public void GenerateSimpleCsAstTreeOverview() {

            UserHandlerCodegen.ResetHandlersManager();
            RequestHandler.Reset();

            RegisterSimpleHandlers(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;
            var tree = umb.CreateAstTree(false);
            tree.Namespace = "__simple_urimatcher__";
            Console.WriteLine(tree.ToString());
        }


        [Test]
        public void GenerateSimpleCppAstTreeOverview() {

            RequestHandler.Reset();
            RegisterSimpleHandlers();
//            Main(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;
            var tree = umb.CreateAstTree(true);
            tree.Namespace = "__simple_urimatcher__";
            Console.WriteLine(tree.ToString());
        }

        /// <summary>
        /// Generates the parse tree overview.
        /// </summary>
        /*[Test]
        public void GenerateSimpleParseTreeOverview() {

            RequestHandler.Reset();

            RegisterSimpleHandlers(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;
//            umb.
//            string str;
            Console.WriteLine(umb.CreateParseTree().ToString());
        }*/

        /// <summary>
        /// Generates the parse tree details.
        /// </summary>
        /*[Test]
        public void GenerateSimpleParseTreeDetails() {
            RequestHandler.Reset();
            RegisterSimpleHandlers(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;
            Console.WriteLine(umb.CreateParseTree().ToString(true));
        }*/

        /// <summary>
        /// Generates the request processor in the C# language.
        /// </summary>
        [Test]
        public void GenerateCSharpRequestProcessor() {
            Main(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;

            var ast = umb.CreateAstTree(false);
            ast.Namespace = "__big_urimatcher__";
            var compiler = umb.CreateCompiler();
            var str = compiler.GenerateRequestProcessorCSharpSourceCode( ast );
            Console.WriteLine( str );
        }


        /// <summary>
        /// Generates the request processor in the C++ language.
        /// </summary>
        [Test]
        public void GenerateCppRequestProcessor() {

            Main(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;

            var ast = umb.CreateAstTree(true);
            ast.Namespace = "__big_urimatcher__";
            var compiler = umb.CreateCompiler();
            var str = compiler.GenerateRequestProcessorCppSourceCode(ast);

            Console.WriteLine(str);
        }


        /// <summary>
        /// Generates the request processor in the C++ language.
        /// </summary>
        [Test]
        public void GenerateSimpleCppRequestProcessor() {

            UserHandlerCodegen.ResetHandlersManager();
            var file = new System.IO.StreamReader("facit.cpp.txt");
            var facit = file.ReadToEnd();
            file.Close();

            RegisterSimpleHandlers(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;

            var ast = umb.CreateAstTree(true);
            ast.Namespace = "__simple_urimatcher__";
            var compiler = umb.CreateCompiler();
            var str = compiler.GenerateRequestProcessorCppSourceCode(ast);

         //   Assert.AreEqual(facit, str);

        //    Console.WriteLine("Complete codegenerated C/C++ file");
            Console.WriteLine(str);
        }


        /// <summary>
        /// Generates the request processor in the C++ language.
        /// </summary>
        [Test]
        public void GenerateSimpleCsRequestProcessor() {
            UserHandlerCodegen.ResetHandlersManager();
            RegisterSimpleHandlers(); // Register some handlers
            var umb = RequestHandler.UriMatcherBuilder;

            var ast = umb.CreateAstTree(false);
            ast.Namespace = "__simple_urimatcher__";
            var compiler = umb.CreateCompiler();
            var str = compiler.GenerateRequestProcessorCSharpSourceCode(ast);

            Console.WriteLine(str);

        }

        /// <summary>
        /// Generates the request processor in the C++ language.
        /// </summary>
        [Test]
        public void GenerateBigCsRequestProcessor() {
           Main(); // Register some handlers
           var umb = RequestHandler.UriMatcherBuilder;

           var ast = umb.CreateAstTree(false);
           ast.Namespace = "__big_urimatcher__";
           var compiler = umb.CreateCompiler();
           var str = compiler.GenerateRequestProcessorCSharpSourceCode(ast);

           Console.WriteLine(str);

        }

       /// <summary>
        /// </summary>
        /*[Test]
        public void TestSimpleUriConflict() {

            Reset();

            Handle.GET("/ab", () => {
                Console.WriteLine("Handler /ab was called" );
                return 2;
            });

            Handle.GET("/{?}", (string rest) => {
                Console.WriteLine("Handler / was called");
                return 1;
            });

            var umb = RequestHandler.UriMatcherBuilder;

            var pt = umb.CreateParseTree();
            var ast = umb.CreateAstTree(false);
            ast.Namespace = "__urimatcher__";
            
            var compiler = umb.CreateCompiler();
            var str = compiler.GenerateRequestProcessorCSharpSourceCode(ast);

            Console.WriteLine(pt.ToString(false));
//            Console.WriteLine(pt.ToString(true));
            Console.WriteLine(ast.ToString());
            Console.WriteLine(str);

            byte[] h1 = Encoding.UTF8.Handle.GETBytes("Handle.GET /\r\n\r\n");
            byte[] h2 = Encoding.UTF8.Handle.GETBytes("Handle.GET /ab\r\n\r\n");

            var um = RequestHandler.RequestProcessor;
            object resource;
            Assert.True(um.Invoke(new Request(h1), out resource));
            Assert.AreEqual(1, (int)resource);
            Assert.True(um.Invoke(new Request(h2), out resource));
            Assert.AreEqual(2, (int)resource);
        }*/


        /// <summary>
        /// Tests the simple rest handler.
        /// </summary>
        /*[Test]
        public void TestSimpleRestHandler() {

            Reset();

            Handle.GET("/", () => {
                Console.WriteLine("Handler 1 was called");
                return "Handle.GET /";
            });

            Handle.GET("/products/{?}", (string prodid) => {
                Console.WriteLine("Handler 2 was called with " + prodid);
                return "Handle.GET /products/@s";
            });

            var umb = RequestHandler.UriMatcherBuilder;

            var pt = umb.CreateParseTree();
            var ast = umb.CreateAstTree(false);
            var compiler = umb.CreateCompiler();
            var str = compiler.GenerateRequestProcessorCSharpSourceCode(ast);

            Console.WriteLine(pt.ToString(false));
            //            Console.WriteLine(pt.ToString(true));
            Console.WriteLine(ast.ToString());
            Console.WriteLine(str);

            byte[] h1 = Encoding.UTF8.Handle.GETBytes("Handle.GET /\r\n\r\n");
            byte[] h2 = Encoding.UTF8.Handle.GETBytes("Handle.GET /products/Test\r\n\r\n");

            var um = RequestHandler.RequestProcessor;
           
           object resource;
           um.Invoke(new Request(h1), out resource);
           Assert.AreEqual("Handle.GET /", resource);
           um.Invoke(new Request(h2), out resource);
           Assert.AreEqual("Handle.GET /products/@s", resource);
        }*/

        /// <summary>
        /// Tests the rest handler.
        /// </summary>
        /*[Test]
        public void TestRestHandler() {

           byte[] h1 = Encoding.UTF8.Handle.GETBytes("Handle.GET /players/123\r\n\r\n");
           byte[] h2 = Encoding.UTF8.Handle.GETBytes("Handle.GET /dashboard/123\r\n\r\n");
           byte[] h3 = Encoding.UTF8.Handle.GETBytes("Handle.GET /players?KalleKula\r\n\r\n");
           byte[] h4 = Encoding.UTF8.Handle.GETBytes("Handle.PUT /players/123\r\n\r\n");
           byte[] h5 = Encoding.UTF8.Handle.GETBytes("Handle.POST /transfer?99\r\n\r\n");
           byte[] h6 = Encoding.UTF8.Handle.GETBytes("Handle.POST /deposit?56754\r\n\r\n");
           byte[] h7 = Encoding.UTF8.Handle.GETBytes("Handle.DELETE /all\r\n\r\n");
           byte[] h8 = Encoding.UTF8.Handle.GETBytes("Handle.DELETE /allanballan\r\n\r\n");
           byte[] h9 = Encoding.UTF8.Handle.GETBytes("Handle.GET /test\r\n\r\n");
           byte[] h10 = Encoding.UTF8.Handle.GETBytes("Handle.GET /testing\r\n\r\n");
           byte[] h11 = Encoding.UTF8.Handle.GETBytes("Handle.PUT /players/123/\r\n\r\n");
           byte[] h12 = Encoding.UTF8.Handle.GETBytes("Handle.PUT /players/123\r\n\r\n");
           byte[] h13 = Encoding.UTF8.Handle.GETBytes("Handle.GET /\r\n\r\n");

           Main(); // Register some handlers
           var um = RequestHandler.RequestProcessor;

           object resource;

           um.Invoke(new Request(h13), out resource);
           Assert.AreEqual("Handle.GET /",resource);

           um.Invoke(new Request(h1), out resource);
           Assert.AreEqual("Handle.GET /players/@i", resource);

           um.Invoke(new Request(h2), out resource);
           Assert.AreEqual("Handle.GET /dashboard/@i", resource);

            Assert.True(um.Invoke(new Request(h3), out resource));
            Assert.True(um.Invoke(new Request(h4), out resource));
            Assert.True(um.Invoke(new Request(h5), out resource));
            Assert.True(um.Invoke(new Request(h6), out resource));
            Assert.True(um.Invoke(new Request(h7), out resource));
            Assert.True(um.Invoke(new Request(h9), out resource));
            Assert.False(um.Invoke(new Request(h10), out resource));
            Assert.True(um.Invoke(new Request(h12), out resource));
            Assert.False(um.Invoke(new Request(h11), out resource), "Handle.PUT /players/123/ should not match a handler (there is a trailing slash)");
            Assert.False(um.Invoke(new Request(h8), out resource), "There is no handler Handle.DELETE /allanballan. How could it be called.");

        }*/


        /*[Test]
        public static void TestAssemblyCache() {
            Main();

            var umb = RequestHandler.UriMatcherBuilder;

            Console.WriteLine("Assembly signature:" + umb.HandlerSetChecksum);

        }*/

    }

}



