﻿
using Starcounter;
using System;
using NUnit.Framework;
using HttpStructs;

namespace PlayersDemoApp {

    partial class MainApp : RequestHandler {

        public static void Main() {

//            GET("/{x}/test/test", (int playerId) => {
//                Assert.AreEqual(123, playerId);
//                Console.WriteLine("playerId=" + playerId);
//                return null;
//            });

//            GET("/{x}", () => {
//                return "404 Not Found";
//            });

            GET("/players/{x}", (int playerId) => {
                Assert.AreEqual(123, playerId);
                Console.WriteLine("playerId=" + playerId);
                return null;
            });

            GET("/dashboard/{x}", (int playerId) => {
                Assert.AreEqual(123, playerId);
                Console.WriteLine("playerId=" + playerId);
                return null;
            });

            GET("/players?f={x}", (string fullName) => {
                Assert.AreEqual("KalleKula", fullName);
                Console.WriteLine("fullName=" + fullName);
                return null;
            });

            PUT("/players/{x}", (int playerId, HttpRequest request) => {
                Assert.AreEqual(123, playerId);
//                Assert.IsNotNull(request);
                Console.WriteLine("playerId: " + playerId ); //+ ", request: " + request);
                return null;
            });

            POST("/transfer?f={x}&t={x}&x={x}",  (int from, int to, int amount) => {
                Assert.AreEqual(99, from);
                Assert.AreEqual(365, to);
                Assert.AreEqual(46, amount);
                Console.WriteLine("From: " + from + ", to: " + to + ", amount: " + amount);
                return null;
            });

            POST("/deposit?a={x}&x={x}", (int to, int amount) => {
                Assert.AreEqual(56754, to);
                Assert.AreEqual(34653, amount);
                Console.WriteLine("To: " + to + ", amount: " + amount);
                return null;
            });

            DELETE("/all", () => {
                Console.WriteLine("deleteAll");
                return null;
            });
        }
    }
}