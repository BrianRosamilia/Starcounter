
using NUnit.Framework;
using Starcounter.Internal.XSON.Tests;
using Starcounter.Templates;
using System;
using System.Collections.Generic;

namespace Starcounter.Internal.XSON.JsonPatch.Tests {

    [TestFixture]
    class ArrayPatchTests : JsonPatchTests {
        [Test]
        public static void TestJsonPatchSimpleArray() {
			dynamic j = new Json();
			dynamic nicke = new Json();
			nicke.FirstName = "Nicke";

			j.FirstName = "Joachim";
			j.Friends = new List<Json>() { nicke };

            Session.Current = new Session() { Data = j };

			var before = ((Json)j).DebugString;
			//            Session.Current.CheckpointChangeLog();
			Session.Current.CreateJsonPatch(true);

			var x = j.Friends.Add();
			x.FirstName = "Henrik";
			x.LastName = "Boman";

			var after = ((Json)j).DebugString;
			var result = Session.Current.CreateJsonPatch(true);

			Console.WriteLine("Before");
			Console.WriteLine("=====");
			Console.WriteLine(before);
			Console.WriteLine("");
			Console.WriteLine("After");
			Console.WriteLine("=====");
			Console.WriteLine(after);
			Console.WriteLine("");
			Console.WriteLine("Changes");
			Console.WriteLine("=====");
			Console.WriteLine(result);
			Console.WriteLine("");

			string facit = @"[{""op"":""replace"",""path"":""/Friends/1"",""value"":{""FirstName"":""Henrik"",""LastName"":""Boman""}}]";
			Assert.AreEqual(facit, result);
        }

        /// <summary>
        /// Creates a new property in a dynamic JSON object and assigns it to a
        /// new array of JSON objects.
        /// </summary>
        [Test]
        public static void CreateANewArrayProperty() {
            dynamic j = new Json();
            dynamic nicke = new Json();

            Session.Current = new Session() { Data = j };

            j.FirstName = "Jack";
            nicke.FirstName = "Nicke";

            Session.Current.CreateJsonPatch(true); // Flushing

            j.Friends = new List<Json>() { nicke };

            Console.WriteLine("Dirty status");
            Console.WriteLine("============");
            Console.WriteLine(j.DebugString);

            var patch = Session.Current.CreateJsonPatch(true);

            Console.WriteLine("Changes:");
            Console.WriteLine("========");
            Console.WriteLine(patch);

            Assert.AreEqual(
               "[{\"op\":\"replace\",\"path\":\"/Friends\",\"value\":[{\"FirstName\":\"Nicke\"}]}]", patch);
        }

        /// <summary>
        /// Replaces an element in an array with a completelly new object. The resulting JSON-patch should
        /// contain a "replace" using a JSON pointer that ends with the index of the element in the array.
        /// </summary>
        [Test]       
		public static void ReplaceAnElementInAnArray() {

            TObject schema = new TObject() { ClassName = "Person" };
            TObject friendSchema = new TObject() { ClassName = "Friend" };

            schema.Add<TString>("FirstName");
            schema.Add<TArray<Json>>("Friends",friendSchema);

            friendSchema.Add<TString>("FirstName");

            dynamic j = new Json() { Template = schema };
            dynamic nicke = new Json() { Template = friendSchema };

            Session.Current = new Session() { Data = j };

			j.FirstName = "Jack";
			nicke.FirstName = "Nicke";
			j.Friends.Add(nicke);
			j["FirstName"] = "Jack";
			nicke["FirstName"] = "Nicke";
			(j["Friends"] as Json).Add( nicke );

            string str = Session.Current.CreateJsonPatch(true);

            dynamic henrik = new Json() { Template = friendSchema };

			//henrik.FirstName = "Henrik";
			//j.Friends
            henrik["FirstName"] = "Henrik";
            (j["Friends"] as Json)[0] = henrik;

            Console.WriteLine("Dirty status");
            Console.WriteLine("============");
            Console.WriteLine(j.DebugString);


            var patch = Session.Current.CreateJsonPatch(true);

            Console.WriteLine("Changes:");
            Console.WriteLine("========");
            Console.WriteLine(patch);

			Assert.AreEqual(
				"[{\"op\":\"replace\",\"path\":\"/Friends/0\",\"value\":{\"FirstName\":\"Henrik\"}}]", patch);
        }

        [Test]
        public static void AssignToEnumerable() {
            dynamic company = new Json();
            company.Name = "Starcounter";
            var person = new Person();
            var person2 = new Person();
            company.Contacts = new object[] { person, person2 };
            person.FirstName = "Timothy";
            person2.FirstName = "Douglas";

            Write("Status 1",company.DebugString);

            Session.Current = new Session() { Data = company };
			Write("JSON-Patch 1", Session.Current.CreateJsonPatch(true));
			
            Write("Before status",company.DebugString);

            var charlie = new Person();
            charlie.FirstName = "Charlie";
            company.Contacts = new object[] { charlie };

            Write("After status 2", company.DebugString);
            Write("JSON-Patch 2", Session.Current.CreateJsonPatch(true));

            company.Contacts = new object[] { person, person2 };

            Write("After status 3",company.DebugString);
            Write("JSON-Patch 3", Session.Current.CreateJsonPatch(true));

            Write("After status 4 (no changes)", company.DebugString);
            Write("JSON-Patch 4 (empty)", Session.Current.CreateJsonPatch(true));
        }

        [Test]
        public static void AssignArrayPropertyToNewArray() {
            dynamic company = new Json();
            company.Name = "Starcounter";
 
            Session.Current = new Session() { Data = company };
            Session.Current.CreateJsonPatch(true);

            dynamic person = new Json();
            dynamic person2 = new Json();
            person.FirstName = "Timothy";
            person2.FirstName = "Douglas";

            company.Contacts = new object[] { person, person2 };
            company.Success = true;


            Console.WriteLine("After status");
            Console.WriteLine("============");
            Console.WriteLine(company.DebugString);
            
            Console.WriteLine("JSON-Patch");
            Console.WriteLine("==========");
            Console.WriteLine(Session.Current.CreateJsonPatch(true));
        }

        [Test]
        public static void TestArrayPatches() {
            dynamic j = new Json();
            dynamic nicke = new Json();

            Session.Current = new Session() { Data = j };
            Assert.NotNull(Session.Current);

            j.FirstName = "Jack";
            nicke.FirstName = "Nicke";
            j.Friends = new List<Json>() { nicke };

            Console.WriteLine("Changes:");
            Console.WriteLine("========");
            Console.WriteLine(Session.Current.CreateJsonPatch(true));
        }

        [Test]
        public static void AddAnElements() {
            dynamic j = new Json();
            dynamic nicke = new Json();

            Session.Current = new Session() { Data = j };
            Assert.NotNull(Session.Current);

            j.FirstName = "Jack";
            nicke.FirstName = "Nicke";
            j.Friends = new List<Json>() { nicke };

            Session.Current.CreateJsonPatch(true);
            var p = j.Friends.Add();
            p.FirstName = "Marten";
            var p2 = j.Friends.Add();
            p2.FirstName = "Asa";

            Console.WriteLine("Changes:");
            Console.WriteLine("========");
            Console.WriteLine(Session.Current.CreateJsonPatch(true));
        }
    }
}
