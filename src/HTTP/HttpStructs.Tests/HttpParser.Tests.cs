// ***********************************************************************
// <copyright file="HttpParser.Tests.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using NUnit.Framework;
using Starcounter;
using Starcounter.Advanced;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HttpParser.Tests
{
    /// <summary>
    /// Used for tests initialization/shutdown.
    /// </summary>
    [SetUpFixture]
    public class HttpParserTestsSetup
    {
        /// <summary>
        /// Tests initialization.
        /// </summary>
        [SetUp]
        public void InitHttpParserTestsSetup()
        {
            Request.sc_init_http_parser();
        }
    }

    /// <summary>
    /// Tests general HTTP requests.
    /// </summary>
    [TestFixture]
    public class GeneralAppsHttpParserTests
    {
        /// <summary>
        /// Tests simple correct HTTP request.
        /// </summary>
        [Test]
        public void TestSimpleCorrectHttpRequest()
        {
            String[] http_request_strings =
            {
                "GET /players/123 HTTP/1.0\r\n\r\n",

                //"GET /123\r\n", // Legal HTTP 0.9 request (from 1991 specification found here http://www.w3.org/Protocols/HTTP/AsImplemented.html)

                "GET /123\r\n\r\n",

                "DELETE /all\r\n\r\n",

                "GET /pub/WWW/TheProject.html HTTP/1.1\r\n" +
                "Host: www.w3.org\r\n" +
                "\r\n",
                                         
                "GET /get_funky_content_length_body_hello HTTP/1.0\r\n" +
                "Content-Length: 5\r\n" +
                "\r\n" +
                "HELLO",

                "GET /vi/Q1Nnm4AZv4c/hqdefault.jpg HTTP/1.1\r\n" +
                "Host: i2.ytimg.com\r\n" +
                "Connection: keep-alive\r\n" +
                "User-Agent: Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.4 (KHTML, like Gecko) Chrome/22.0.1229.94 Safari/537.4\r\n" +
                "Accept: */*\r\n" +
                "Referer: http://www.youtube.com/\r\n" +
                "Accept-Encoding: gzip,deflate,sdch\r\n" +
                "Accept-Language: en-US,en;q=0.8\r\n" +
                "Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.3\r\n" +
                "\r\n",

                "POST /post_identity_body_world?q=search#hey HTTP/1.1\r\n" +
                "Accept: */*\r\n" +
                "Cookie: name=value; name2=value2;   name3=value3\r\n" +
                "Transfer-Encoding: identity\r\n" +
                "Content-Length: 5\r\n" +
                "\r\n" +
                "World",

                "PATCH /file.txt HTTP/1.1\r\n" +
                "Host: www.example.com\r\n" +
                "Content-Type: application/example\r\n" +
                "If-Match: \"e0023aa4e\"\r\n" +
                "Cookie: MySuperCookie=MySuperValue\r\n" +
                "Content-Length: 10\r\n" +
                "\r\n" +
                "cccccccccc",

                "POST / HTTP/1.1\r\n" +
                "Host: www.example.com\r\n" +
                "Content-Type: application/x-www-form-urlencoded\r\n" +
                "Content-Length: 4\r\n" +
                "Connection: close\r\n" +
                "\r\n" +
                "q=42\r\n"
            };

            // Correct HTTP request URIs.
            String[] http_request_uris =
            {
                "/players/123",
                "/123",
                "/all",
                "/pub/WWW/TheProject.html",
                "/get_funky_content_length_body_hello",
                "/vi/Q1Nnm4AZv4c/hqdefault.jpg",
                "/post_identity_body_world?q=search#hey",
                "/file.txt",
                "/"
            };

            // Correct HTTP request hosts.
            String[] http_request_hosts =
            {
                null,
                null,
                null,
                "www.w3.org",
                null,
                "i2.ytimg.com",
                null,
                "www.example.com",
                "www.example.com"
            };

            // Correct HTTP request bodies.
            String[] http_request_bodies =
            {
                null,
                null,
                null,
                null,
                "HELLO",
                null,
                "World",
                "cccccccccc",
                "q=42"
            };

            // Correct HTTP request cookies.
            String[] http_request_cookies =
            {
                null,
                null,
                null,
                null,
                null,
                null,
                "name=value;name2=value2;name3=value3",
                "MySuperCookie=MySuperValue",
                null
            };

            // Collecting all parsed HTTP requests.
            for (Int32 i = 0; i < http_request_strings.Length; i++)
            {
                Console.WriteLine("Processing correct HTTP request: " + i);

                Byte[] http_request_bytes = Encoding.ASCII.GetBytes(http_request_strings[i]);

                Request http_request = new Request(http_request_bytes, http_request_bytes.Length);

                // Checking correct URIs.
                Assert.That(http_request.Uri == http_request_uris[i], Is.True);

                // Checking correct hosts.
                Assert.That(http_request["Host"] == http_request_hosts[i], Is.True);

                // Checking correct bodies.
                Assert.That(http_request.Body == http_request_bodies[i], Is.True);

                // Checking correct cookies.
                if (http_request_cookies[i] != null)
                {
                    String[] correctCookies = http_request_cookies[i].Split(new Char[] { ';' });
                    for (Int32 k = 0; k < correctCookies.Length; k++)
                        Assert.That(http_request.Cookies[k] == correctCookies[k], Is.True);
                }

                // Immediately destroying the structure.
                http_request.Destroy();

                // Checking if destroyed successfully.
                Assert.That(http_request.IsDestroyed(), Is.True);
            }
        }

        /// <summary>
        /// Tests incorrect HTTP request.
        /// </summary>
        [Test]
        public void TestIncorrectHttpRequest()
        {
            String simple_http_request =
                "GET /pub/WWW/TheProject.html HTTP/1.1\r\n" +
                "Ho st: www.w3.org\r\n" +
                "\r\n";

            Byte[] simple_http_request_bytes = Encoding.ASCII.GetBytes(simple_http_request);

            Request http_request = null;

            // Checking that only correct exceptions are thrown.
            Assert.DoesNotThrow(() =>
            {
                try
                {
                    // Should through only ScErrAppsHttpParserIncompleteHeaders, ScErrAppsHttpParserIncorrect.
                    http_request = new Request(simple_http_request_bytes, simple_http_request_bytes.Length);
                }
                catch (Exception e)
                {
                    UInt32 code;
                    if (!ErrorCode.TryGetCode(e, out code))
                        throw;

                    // Checking correct error codes.
                    if ((code != Error.SCERRAPPSHTTPPARSERINCOMPLETEHEADERS) &&
                        (code != Error.SCERRAPPSHTTPPARSERINCORRECT))
                        throw;
                }
            });

            Assert.That(http_request, Is.Null);
        }
    }
}
