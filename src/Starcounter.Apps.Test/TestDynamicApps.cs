﻿// ***********************************************************************
// <copyright file="TestDynamicApps.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System.Collections.Generic;
using NUnit.Framework;
using Starcounter.Client;
using Starcounter.Templates.Interfaces;
using Starcounter.Templates;

namespace Starcounter.Client.Tests.Application {

    /// <summary>
    /// Class AppTests
    /// </summary>
    [TestFixture]
    public class AppTests {

        /// <summary>
        /// Creates a template (schema) and Puppets using that schema in code.
        /// </summary>
        /// <remarks>
        /// Template schemas can be created on the fly in Starcounter using the API. It is not necessary
        /// to have compile time or run time .json files to create template schemas.
        /// </remarks>
        [Test]
        public static void CreateTemplatesAndAppsByCode() {
            _CreateTemplatesAndAppsByCode();
        }

        /// <summary>
        /// Creates some.
        /// </summary>
        /// <returns>List{App}.</returns>
        private static List<Puppet> _CreateTemplatesAndAppsByCode() {

            // First, let's create the schema (template)
            var personSchema = new TPuppet();
            var firstName = personSchema.Add<TString>("FirstName$");
            var lastName = personSchema.Add<TString>("LastName");
            var age = personSchema.Add<TLong>("Age");

            var phoneNumber = new TPuppet();
            var phoneNumbers = personSchema.Add<TArr<Puppet,TPuppet>>("Phonenumbers", phoneNumber);
            var number = phoneNumber.Add<TString>("Number");

            Assert.AreEqual("FirstName$", firstName.Name);
            Assert.AreEqual("FirstName", firstName.PropertyName);

            // Now let's create instances using that schema
            Puppet jocke = new Puppet() { Template = personSchema };
            Puppet tim = new Puppet() { Template = personSchema };

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

            var ret = new List<Puppet>();
            ret.Add(jocke);
            ret.Add(tim);
            return ret;
        }

        [Test]
        public static void TestDynamic() {
            // First, let's create the schema (template)
            var personSchema = new TPuppet();
            var firstName = personSchema.Add<TString>("FirstName$");
            var lastName = personSchema.Add<TString>("LastName");
            var age = personSchema.Add<TLong>("Age");

            var phoneNumber = new TPuppet();
            var phoneNumbers = personSchema.Add<TArr<Puppet, TPuppet>>("Phonenumbers", phoneNumber);
            var number = phoneNumber.Add<TString>("Number");

            Assert.AreEqual("FirstName$", firstName.Name);
            Assert.AreEqual("FirstName", firstName.PropertyName);

            // Now let's create instances using that schema
            dynamic jocke = new Puppet() { Template = personSchema };
            dynamic tim = new Puppet() { Template = personSchema };

            jocke.FirstName = "Joachim";
            jocke.LastName = "Wester";
            //jocke.Age = 30;

            tim.FirstName = "Timothy";
            tim.LastName = "Wester";
            //tim.Age = 16;

            Assert.AreEqual(0, firstName.Index);
            Assert.AreEqual(1, lastName.Index);
            Assert.AreEqual(2, age.Index);
            Assert.AreEqual(3, phoneNumbers.Index);
            Assert.AreEqual(0, number.Index);

            Assert.AreEqual("Joachim", jocke.GetValue(firstName));
            Assert.AreEqual("Wester", jocke.GetValue(lastName));
            Assert.AreEqual("Timothy", tim.GetValue(firstName));
            Assert.AreEqual("Wester", tim.GetValue(lastName));

            var ret = new List<Puppet>();
            ret.Add(jocke);
            ret.Add(tim);
        }

        /// <summary>
        /// Test using dynamic codegen
        /// </summary>
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

        /// <summary>
        /// Writes the dynamic.
        /// </summary>
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
