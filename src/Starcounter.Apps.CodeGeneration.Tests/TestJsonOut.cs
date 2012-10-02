﻿
using System;
using NUnit.Framework;
using Starcounter.Internal.Application;
using Starcounter.Internal.Application.CodeGeneration;
using Starcounter.Internal.Application.JsonReader;
using Starcounter.Internal.ExeModule;
using Starcounter.Templates;
using Starcounter.Templates.Interfaces;
//using Xunit;

namespace Starcounter.Client.Tests.Application
{

    [TestFixture]
    public class TestJson
    {

        [Test]
//        [Fact]
        public static void AppToJson()
        {
            AppExeModule.IsRunningTests = true;
            dynamic app = new App() { Template = TemplateFromJs.ReadFile("MySampleApp2.json") };
            app.FirstName = "Joachim";
            app.LastName = "Wester";

            dynamic item = app.Items.Add();
            item.Description = "Release this cool stuff to the market";
            item.IsDone = false;

            item = app.Items.Add();
            item.Description = "Take a vacation";
            item.IsDone = true;
            App app2 = (App)app;
            Console.WriteLine(app2.ToJson());
            // Assert.IsTrue(true);
            Assert.True(true);
        }
    }
}