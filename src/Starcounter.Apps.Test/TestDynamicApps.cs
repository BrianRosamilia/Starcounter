﻿
using System.Collections.Generic;
using NUnit.Framework;
using Starcounter.Client;
using Starcounter.Templates.Interfaces;
using Starcounter.Templates;
using Starcounter.Internal.ExeModule;

namespace Starcounter.Client.Tests.Application {

    [TestFixture]
    public class AppTests {

        [Test]
        public static void ManualCreation() {
            AppExeModule.IsRunningTests = true;
            CreateSome();
        }

        private static List<App> CreateSome() {
            var personTmpl = new AppTemplate();
            var firstName = personTmpl.Add<StringProperty>("FirstName$");
            var lastName = personTmpl.Add<StringProperty>("LastName");
            var age = personTmpl.Add<IntProperty>("Age");

            var phoneNumber = new AppTemplate();
            var phoneNumbers = personTmpl.Add<ListingProperty>("Phonenumbers", phoneNumber);
            var number = phoneNumber.Add<StringProperty>("Number");

            Assert.AreEqual("FirstName$", firstName.Name);
            Assert.AreEqual("FirstName", firstName.PropertyName);

            App jocke = new App() { Template = personTmpl };
            App tim = new App() { Template = personTmpl };

            jocke.SetValue(firstName, "Joachim");
            jocke.SetValue(lastName, "Wester");
            jocke.SetValue(age, 30);

            tim.SetValue(firstName, "Timothy");
            tim.SetValue(lastName, "Wester");
            tim.SetValue(age, 16);

            Assert.AreEqual(0, firstName.Index);
            Assert.AreEqual(1, lastName.Index);
            Assert.AreEqual(2, age.Index);
            Assert.AreEqual(3, phoneNumbers.Index);
            Assert.AreEqual(0, number.Index);

            Assert.AreEqual("Joachim", jocke.GetValue(firstName));
            Assert.AreEqual("Wester", jocke.GetValue(lastName));
            Assert.AreEqual("Timothy", tim.GetValue(firstName));
            Assert.AreEqual("Wester", tim.GetValue(lastName));

            var ret = new List<App>();
            ret.Add(jocke);
            ret.Add(tim);
            return ret;
        }

        [Test]
        public static void ReadDynamic() {
            return;

            //List<App> apps = CreateSome();
            //dynamic jocke = apps[0];
            //dynamic tim = apps[1];

            //Assert.AreEqual("Joachim", jocke.FirstName);
            //Assert.AreEqual("Wester", jocke.LastName);
            //Assert.AreEqual(30, jocke.Age);

            //Assert.AreEqual("Timothy", tim.FirstName);
            //Assert.AreEqual("Wester", tim.LastName);
            //Assert.AreEqual(16, tim.Age);
        }

        [Test]
        public static void WriteDynamic() {
            return;
            //List<App> apps = CreateSome();
            //dynamic a = apps[0];
            //dynamic b = apps[1];
            //dynamic c = new App() { Template = b.Template };

            //a.FirstName = "Adrienne";
            //a.LastName = "Wester";
            //a.Age = 24;

            //b.FirstName = "Douglas";
            //b.LastName = "Wester";
            //b.Age = 7;

            //c.FirstName = "Charlie";
            //c.LastName = "Wester";
            //c.Age = 4;

            //Assert.AreEqual("Adrienne", a.FirstName);
            //Assert.AreEqual("Wester", a.LastName);
            //Assert.AreEqual(24, a.Age);

            //Assert.AreEqual("Douglas", b.FirstName);
            //Assert.AreEqual("Wester", b.LastName);
            //Assert.AreEqual(7, b.Age);

            //Assert.AreEqual("Charlie", c.FirstName);
            //Assert.AreEqual("Wester", c.LastName);
            //Assert.AreEqual(4, c.Age);
        }

    }
}