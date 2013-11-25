// ***********************************************************************
// <copyright file="TestDynamicApps.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System.Collections.Generic;
using NUnit.Framework;
using Starcounter.Templates;
using System;
//using Starcounter.XSON.CodeGeneration;
using Starcounter.Advanced;
using Starcounter.Internal;
using Starcounter.Advanced.XSON;
using System.IO;
using Starcounter.Internal.XSON;
using Starcounter.XSON.Tests;
using TJson = Starcounter.Templates.TObject;
using TArr = Starcounter.Templates.TArray<Starcounter.Json>;

namespace Starcounter.Internal.XSON.Tests {

    /// <summary>
    /// Class AppTests
    /// </summary>
    [TestFixture]
    public static class AppTests {
        /// <summary>
        /// Sets up the test.
        /// </summary>
        [TestFixtureSetUp]
        public static void Setup() {
            StarcounterBase._DB = new FakeDbImpl();
        }

//        /// <summary>
//        /// 
//        /// </summary>
//        [Test]
//        public static void UseJsonWithNoTemplate() {
//            var json = new Json();
//            AssertCorrectErrorCodeIsThrown(() => { json.Data = new SubClass1(); }, Error.SCERRTEMPLATENOTSPECIFIED);
//            AssertCorrectErrorCodeIsThrown(() => { var str = json.ToJson(); }, Error.SCERRTEMPLATENOTSPECIFIED);
//        }

        private static void AssertCorrectErrorCodeIsThrown(Action action, uint expectedErrorCode) {
            uint ec;

            try {
                action();
                Assert.Fail("An exception with error " + ErrorCode.ToFacilityCode(expectedErrorCode) + " should have been thrown");
            } catch (Exception ex) {
                if (ErrorCode.TryGetCode(ex, out ec)) {
                    if (ec != expectedErrorCode)
                        Assert.Fail("An exception with error " + ErrorCode.ToFacilityCode(expectedErrorCode) + " should have been thrown");
                } else {
                    Assert.Fail("An exception with error " + ErrorCode.ToFacilityCode(expectedErrorCode) + " should have been thrown");
                }
            }
        }

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
        private static List<Json> _CreateTemplatesAndAppsByCode() {

            // First, let's create the schema (template)
            var personSchema = new TJson();
            var firstName = personSchema.Add<TString>("FirstName$");
            var lastName = personSchema.Add<TString>("LastName");
            var age = personSchema.Add<TLong>("Age");

            var phoneNumber = new TJson();
            var phoneNumbers = personSchema.Add<TArr>("Phonenumbers", phoneNumber);
            var number = phoneNumber.Add<TString>("Number");

            Assert.AreEqual("FirstName$", firstName.TemplateName);
            Assert.AreEqual("FirstName", firstName.PropertyName);

            // Now let's create instances using that schema
            var jocke = new Json() { Template = personSchema };
            var tim = new Json() { Template = personSchema };

            firstName.Setter(jocke, "Joachim");
            lastName.Setter(jocke, "Wester");
            age.Setter(jocke, 30);

            firstName.Setter(tim, "Timothy");
            lastName.Setter(tim, "Wester");
            age.Setter(tim, 16);

            Assert.AreEqual(0, firstName.TemplateIndex);
            Assert.AreEqual(1, lastName.TemplateIndex);
            Assert.AreEqual(2, age.TemplateIndex);
            Assert.AreEqual(3, phoneNumbers.TemplateIndex);
            Assert.AreEqual(0, number.TemplateIndex);

            Assert.AreEqual("Joachim", firstName.Getter(jocke));
            Assert.AreEqual("Wester", lastName.Getter(jocke));
            Assert.AreEqual("Timothy", firstName.Getter(tim));
            Assert.AreEqual("Wester", lastName.Getter(tim));

            var ret = new List<Json>();
            ret.Add(jocke);
            ret.Add(tim);
            return ret;
        }
        
        /// <summary>
        /// Tests dynamic.
        /// </summary>
        [Test]
        public static void TestDynamic() {
            // First, let's create the schema (template)
            var personSchema = new TJson();
            var firstName = personSchema.Add<TString>("FirstName$");
            var lastName = personSchema.Add<TString>("LastName");
            var age = personSchema.Add<TLong>("Age");

            var phoneNumber = new TJson();
            var phoneNumbers = personSchema.Add<TArr>("Phonenumbers", phoneNumber);
            var number = phoneNumber.Add<TString>("Number");

            Assert.AreEqual("FirstName$", firstName.TemplateName);
            Assert.AreEqual("FirstName", firstName.PropertyName);

            // Now let's create instances using that schema
            dynamic jocke = new Json() { Template = personSchema };
            dynamic tim = new Json() { Template = personSchema };

            jocke.FirstName = "Joachim";
            jocke.LastName = "Wester";
            jocke.Age = 30;

            tim.FirstName = "Timothy";
            tim.LastName = "Wester";
            tim.Age = 16;

            Assert.AreEqual(0, firstName.TemplateIndex);
            Assert.AreEqual(1, lastName.TemplateIndex);
            Assert.AreEqual(2, age.TemplateIndex);
            Assert.AreEqual(3, phoneNumbers.TemplateIndex);
            Assert.AreEqual(0, number.TemplateIndex);

            Assert.AreEqual("Joachim", jocke.FirstName);
            Assert.AreEqual("Wester", jocke.LastName);
            Assert.AreEqual("Timothy", tim.FirstName);
            Assert.AreEqual("Wester", tim.LastName);

            var ret = new List<Json>();
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

		[Test]
		public static void TestParentAssignment() {
			dynamic json = new Json();
			dynamic newchildJson = new Json();
			dynamic oldChildJson = new Json();

			json.Page = oldChildJson;
			Assert.IsNotNull(oldChildJson.Parent);

			json.Page = newchildJson;
			Assert.IsNotNull(newchildJson.Parent);
			Assert.IsNull(oldChildJson.Parent);
		}

        /// <summary>
        /// Tests TestCorrectJsonInstances.
        /// </summary>
        [Test]
        public static void TestCorrectJsonInstances() {
            TJson personSchema = CreateComplexPersonTemplate();

            dynamic p1 = personSchema.CreateInstance();
            dynamic n1 = p1.Fields.Add();
            dynamic n2 = p1.Fields.Add();

            Assert.IsInstanceOf<Json>(p1);
            Assert.IsInstanceOf<MyFieldMessage>(n1);
            Assert.IsInstanceOf<MyFieldMessage>(n2);

        }

		[Test]
		public static void TestChangeBoundObject() {
			var template = new TObject();
			var pageTemplate = template.Add<TJson>("Number");
			pageTemplate.Add<TString>("Number");

			dynamic msg = new Json { Template = template };

			var myDataObj = new PersonObject();
			var numberObj = new PhoneNumberObject() { Number = "123-555-7890" };
			myDataObj.Number = numberObj;
			msg.Data = myDataObj;

			string json = msg.ToJson();
			Assert.AreEqual(@"{""Number"":{""Number"":""123-555-7890""}}", json);

			var oldNumberJson = msg.Number;

			msg.Number = new Json();
			json = msg.ToJson();
			Assert.AreEqual(@"{""Number"":{}}", json);

			myDataObj.Number = numberObj;

			msg.Number = null;
			json = msg.ToJson();
			Assert.AreEqual(@"{""Number"":{}}", json);

			msg.Number = oldNumberJson;
			msg.Number.Data = numberObj;
			json = msg.ToJson();
			Assert.AreEqual(@"{""Number"":{""Number"":""123-555-7890""}}", json);

			msg.Number.Data = null;
			json = msg.ToJson();
			Assert.AreEqual(@"{""Number"":{""Number"":""""}}", json);
		}

        /// <summary>
        /// Tests TestDataBinding.
        /// </summary>
        //[Test]
        // TODO: Fix this test!
        public static void TestDataBinding() {
            dynamic msg = new Json { Template = CreateSimplePersonTemplateWithDataBinding() };

            var myDataObj = new PersonObject() { FirstName = "Kalle", Surname = "Kula", Age = 21, Misc = "Lorem Ipsum" };
            myDataObj.Number = new PhoneNumberObject() { Number = "123-555-7890" };
            msg.Data = myDataObj;

            msg.Updated = "2013-04-25 13:15:12";

            // Reading bound values.
            Assert.AreEqual("Kalle", msg.FirstName);
            Assert.AreEqual("Kula", msg.LastName);
            Assert.AreEqual(0, msg._Age); // Since Age shouldn't be bound we should get a zero back and not 21.
            Assert.IsNotNullOrEmpty(msg.Created);
            Assert.IsNullOrEmpty(msg.Misc); // Same as above. Not bound so no value should be retrieved.
            Assert.AreEqual("123-555-7890", msg._PhoneNumber.Number); // Should be bound even if the name start with '_' since we specify a binding when registering the template.

            // Setting bound values.
            msg.FirstName = "Allan";
            msg.LastName = "Ballan";
            msg._Age = 109L;
            msg._PhoneNumber.Number = "666";
            msg.Misc = "Changed!";
             
            // Check dataobject is changed.
            Assert.AreEqual("Allan", myDataObj.FirstName);
            Assert.AreEqual("Ballan", myDataObj.Surname);
            Assert.AreEqual(21, myDataObj.Age); // Age is not bound so updating the message should not alter the dataobject.
            Assert.AreEqual("666", myDataObj.Number.Number);
            Assert.AreEqual("Lorem Ipsum", myDataObj.Misc); // Not bound so updating the message should not alter the dataobject.
        }


        /// <summary>
        /// Creates a template from a JSON-by-example file
        /// </summary>
        /// <param name="filePath">The file to load</param>
        /// <returns>The newly created template</returns>
        private static TJson CreateJsonTemplateFromFile(string filePath) {
            string json = File.ReadAllText(filePath);
            string className = Path.GetFileNameWithoutExtension(filePath);
            var tobj = TJson.CreateFromMarkup<Json, TJson>("json", json, className);
            tobj.ClassName = className;
            return tobj;
        }

        /// <summary>
        /// Tests TestDataBinding.
        /// </summary>
        [Test]
        public static void TestDataBindingWithDifferentClasses() {
            // Bound to SimpleBase datatype.
            TJson tSimple = CreateJsonTemplateFromFile("simple.json");
            dynamic json = tSimple.CreateInstance();
            
            var o = new SubClass1(); 
            json.Data = o;
            json.BaseValue = "SubClass1";
            json.AbstractValue = "SubClass1";
            string virtualValue = json.VirtualValue;

            Assert.AreEqual("SubClass1", o.BaseValue);
            Assert.AreEqual("SubClass1", o.AbstractValue);
            Assert.AreEqual("SubClass1", virtualValue);

            var simpleData2 = new SubClass2();
            Assert.DoesNotThrow(() => { json.Data = simpleData2; });
            Assert.DoesNotThrow(() => { json.BaseValue = "SubClass2"; });
            Assert.DoesNotThrow(() => { json.AbstractValue = "SubClass2"; });
            virtualValue = json.VirtualValue;

            Assert.AreEqual("SubClass2", simpleData2.BaseValue);
            Assert.AreEqual("SubClass2", simpleData2.AbstractValue);
            Assert.AreEqual("SubClass2", virtualValue);

            var simpleData3 = new SubClass3();
            Assert.DoesNotThrow(() => { json.Data = simpleData3; });
            Assert.DoesNotThrow(() => { json.BaseValue = "SubClass3"; });
            Assert.DoesNotThrow(() => { json.AbstractValue = "SubClass3"; });
            virtualValue = json.VirtualValue;

            Assert.AreEqual("SubClass3", simpleData3.BaseValue);
            Assert.AreEqual("SubClass3", simpleData3.AbstractValue);
            Assert.AreEqual("SubClass3", virtualValue);
        }

		[Test]
		public static void TestUntypedObjectArray() {
			var schema = new TJson();
			schema.Add<TArray<Json>>("Items");
			schema.Add<TArray<Json>>("Items2");

			dynamic json = (Json)schema.CreateInstance();
			dynamic item = new Json();
			item.Header = "Apa papa";
			json.Items.Add(item);

			item = new Json();
			item.Name = "La la la";
			json.Items.Add(item);

			item = json.Items.Add();
			item.AProp = 19;

			Person p = new Person();
			item = json.Items.Add(p);

			var persons = new List<Person>();
			persons.Add(new Person() { FirstName = "Apa" });
			persons.Add(new Person() { FirstName = "Papa" });
			persons.Add(new Person() { FirstName = "Qwerty" });
			json.Items2 = persons;

			item = new Json();
			item.Value = 27;
			Assert.Throws<Exception>(() => json.Items2.Add(item));

			string str = json.ToJson();
			Console.WriteLine(str);
		}

		[Test]
		public static void TestTemplateGettersAndSetters() {
			bool bound;
			TJson tJson = CreateSimplePersonTemplateWithDataBinding();
			Json json = (Json)tJson.CreateInstance();

			var person = new PersonObject();
			person.FirstName = "Ture";
			person.Surname = "ApaPapa";
			person.Age = 19;

			person.Number = new PhoneNumberObject();
			person.Number.Number = "12345678";

			json.Data = person;

			// FirstName$
			TString property = tJson.Properties[0] as TString;
			Assert.IsNotNull(property);
			
			bound = (property.Bind != null);
			if (bound)
				property.GenerateBoundGetterAndSetter(json);
#if DEBUG
			PrintTemplateDebugInfo<String>(property);
#endif

			property.UnboundSetter(json, "Test!");
			Assert.AreEqual("Test!", property.UnboundGetter(json));

			if (bound) {
				Assert.AreEqual(person.FirstName, property.BoundGetter(json));
				property.BoundSetter(json, "NotTure");
				Assert.AreEqual("NotTure", person.FirstName);
			}

			// The property is bound so getter should return the bound value.
			Assert.AreEqual("NotTure", property.Getter(json));
			property.Setter(json, "SomeOther");
			Assert.AreEqual("SomeOther", person.FirstName);
		
			// LastName
			property = tJson.Properties[1] as TString;
			Assert.IsNotNull(property);
			
			bound = (property.Bind != null);
			if (bound)
				property.GenerateBoundGetterAndSetter(json);
#if DEBUG
			PrintTemplateDebugInfo<String>(property);
#endif

			property.UnboundSetter(json, "Test!");
			Assert.AreEqual("Test!", property.UnboundGetter(json));

			if (bound) {
				Assert.AreEqual(person.Surname, property.BoundGetter(json));
				property.BoundSetter(json, "NotApapapa");
				Assert.AreEqual("NotApapapa", person.Surname);
			}

			// The property is bound so getter should return the bound value.
			Assert.AreEqual("NotApapapa", property.Getter(json));
			property.Setter(json, "SomeOther");
			Assert.AreEqual("SomeOther", person.Surname);
			

			// Age
			TLong ageProperty = tJson.Properties[2] as TLong;
			Assert.IsNotNull(ageProperty);
			
			bound = (ageProperty.Bind != null);
			if (bound)
				ageProperty.GenerateBoundGetterAndSetter(json);
#if DEBUG
			PrintTemplateDebugInfo<long>(ageProperty);
#endif

			ageProperty.UnboundSetter(json, 199);
			Assert.AreEqual(199, ageProperty.UnboundGetter(json));

			if (bound) {
				Assert.AreEqual(person.Age, ageProperty.BoundGetter(json));
				ageProperty.BoundSetter(json, 213);
				Assert.AreEqual(213, person.Age);
			}

			// The property is not bound so getter should return the unbound value.
			Assert.AreEqual(199, ageProperty.Getter(json));
			ageProperty.Setter(json, 226);
			Assert.AreNotEqual(226, person.Age); // not bound, data value should not be changed.
			

			// PhoneNumber
			TObject pnProperty = tJson.Properties[8] as TObject;
			Assert.IsNotNull(pnProperty);

			bound = (pnProperty.Bind != null);
			if (bound)
				pnProperty.GenerateBoundGetterAndSetter(json);
#if DEBUG
			PrintTemplateDebugInfo(pnProperty);
#endif

			var pn = new PhoneNumberObject();
			Json pnJson = (Json)pnProperty.CreateInstance();
			pnJson.Data = pn;
			pnProperty.UnboundSetter(json, pnJson);
			Assert.AreEqual(pn, pnProperty.UnboundGetter(json).Data);

			if (bound) {
				Assert.AreEqual(person.Number, pnProperty.BoundGetter(json));
				pnProperty.BoundSetter(json, pn);
				Assert.AreEqual(pn, person.Number);
			}
		}

#if DEBUG
		private static void PrintTemplateDebugInfo<T>(Property<T> property){
			string str = property.TemplateName + " (index: " + property.TemplateIndex;
			bool bound = (property.Bind != null);

			if (bound) {
				str += ", bind: " + property.Bind;
			}

			str += ", Type: " + property.GetType().Name;
			str += ")";

			Console.WriteLine("------------------------------------------");
			Console.WriteLine("Property:");
			Console.WriteLine(str);
			Console.WriteLine();
			Console.WriteLine("UnboundGetter:");
			Console.WriteLine(property.DebugUnboundGetter);
			Console.WriteLine();
			Console.WriteLine("UnboundSetter:");
			Console.WriteLine(property.DebugUnboundSetter);
			Console.WriteLine();

			if (bound) {
				Console.WriteLine("BoundGetter:");
				Console.WriteLine(property.DebugBoundGetter);
				Console.WriteLine();
				Console.WriteLine("BoundSetter:");
				Console.WriteLine(property.DebugBoundSetter);
				Console.WriteLine();
			}
		}

		private static void PrintTemplateDebugInfo(TObject property) {
			string str = property.TemplateName + " (index: " + property.TemplateIndex;
			bool bound = (property.Bind != null);

			if (bound) {
				str += ", bind: " + property.Bind;
			}

			str += ", Type: " + property.GetType().Name;
			str += ")";

			Console.WriteLine("------------------------------------------");
			Console.WriteLine("Property:");
			Console.WriteLine(str);
			Console.WriteLine();
			Console.WriteLine("UnboundGetter:");
			Console.WriteLine(property.DebugUnboundGetter);
			Console.WriteLine();
			Console.WriteLine("UnboundSetter:");
			Console.WriteLine(property.DebugUnboundSetter);
			Console.WriteLine();

			if (bound) {
				Console.WriteLine("BoundGetter:");
				Console.WriteLine(property.DebugBoundGetter);
				Console.WriteLine();
				Console.WriteLine("BoundSetter:");
				Console.WriteLine(property.DebugBoundSetter);
				Console.WriteLine();
			}
		}
#endif

        private static TJson CreateSimplePersonTemplateWithDataBinding() {
            var personSchema = new TJson() { BindChildren = BindingStrategy.Bound };
            personSchema.Add<TString>("FirstName$"); // Bound to FirstName
            personSchema.Add<TString>("LastName", "Surname"); // Bound to Surname
            
			var t = personSchema.Add<TLong>("Age"); // Will not be bound
			t.BindingStrategy = BindingStrategy.Unbound;
            
			personSchema.Add<TString>("Created");
            personSchema.Add<TString>("Updated");
            personSchema.Add<TString>("AbstractValue");
            personSchema.Add<TString>("VirtualValue");
            
            var misc = personSchema.Add<TString>("Misc");
            misc.Bind = null; // Removing the binding for this specific template.

            var phoneNumber = personSchema.Add<TJson>("PhoneNumber", "Number");
            phoneNumber.BindChildren = BindingStrategy.Bound;
            phoneNumber.Add<TString>("Number"); // Bound to Number
            
            return personSchema;
        }

        private static TJson CreateSimplePersonTemplate() {
            var personSchema = new TJson();
            personSchema.Add<TString>("FirstName$");
            personSchema.Add<TString>("LastName");
            personSchema.Add<TLong>("Age");
            
            var phoneNumber = new TJson();
            phoneNumber.Add<TString>("Number");
            personSchema.Add<TArr>("PhoneNumbers", phoneNumber);

            return personSchema;
        }

        private static TJson CreateComplexPersonTemplate() {
            var personSchema = new TJson();
            personSchema.Add<TString>("FirstName$");
            personSchema.Add<TString>("LastName");
            personSchema.Add<TLong>("Age");
            personSchema.Add<TDecimal>("Stats");

            var field = new TJson();
            field.Add<TString>("Type");
            var info = field.Add<TJson>("Info");
            info.Add<TString>("Text");
            field.InstanceType = typeof(MyFieldMessage);
            personSchema.Add<TArray<MyFieldMessage>>("Fields", field);

            var extraInfo = personSchema.Add<TJson>("ExtraInfo");
            extraInfo.Add<TString>("Text");

            return personSchema;
        }
    }

    internal class MyFieldMessage : Json {
    }
}
