using System;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using Starcounter.Internal.Application.CodeGeneration;
using Starcounter.Templates;
using Starcounter.Advanced.XSON;
using Modules;
using TJson = Starcounter.Templates.TObject;
using Starcounter.XSON.Serializer.Parsetree;
using Starcounter.XSON.Serializer;


namespace Starcounter.Internal.XSON.Serializer.Tests {
    /// <summary>
    /// 
    /// </summary>
    public static class JsonSerializeTest {
  //      private static NewtonsoftSerializer newtonSerializer;
        private static StandardJsonSerializer defaultSerializer;
		private static FasterThanJsonSerializer ftjSerializer;

        [TestFixtureSetUp]
        public static void InitializeTest() {
  //          newtonSerializer = new NewtonsoftSerializer();
			defaultSerializer = new StandardJsonSerializer();
			ftjSerializer = new FasterThanJsonSerializer();
        }

        [Test]
        public static void TestSerializeJsonString() {
            
            // needed size includes the quotations around the string.
            SerializeString("First!", 8);
            SerializeString("FirstWithSpecial\n", 20);
            SerializeString("\n\b\t", 8);
            SerializeString("\u001f", 8); 
        }

        private static void SerializeString(string value, int neededSize) {
            byte[] dest;
            int written;

            unsafe {
                // Assert that we dont write outside of the available space.
                dest = new byte[neededSize - 2];
                fixed (byte* pdest = dest) {
                    written = JsonHelper.WriteString((IntPtr)pdest, dest.Length, value);
                    Assert.AreEqual(-1, written);
                }

                // Assert that we write correct amount of bytes.
                dest = new byte[neededSize*2];
                fixed (byte* pdest = dest) {
                    written = JsonHelper.WriteString((IntPtr)pdest, dest.Length, value);
                    Assert.AreEqual(neededSize, written);
                }
            }
        }

		[Test]
		public static void TestFTJSerializer() {
			RunFTJSerializerTest("jsstyle.json", File.ReadAllText("jsstyle.json"), false);
			RunFTJSerializerTest("person.json", File.ReadAllText("person.json"), false);
			RunFTJSerializerTest("supersimple.json", File.ReadAllText("supersimple.json"), false);
			RunFTJSerializerTest("simple.json", File.ReadAllText("simple.json"), false);
			RunFTJSerializerTest("TestMessage.json", File.ReadAllText("TestMessage.json"), false);
		}

		[Test]
		public static void TestFTJCodegenSerializer() {
			RunFTJSerializerTest("jsstyle.json", File.ReadAllText("jsstyle.json"), true);
			RunFTJSerializerTest("person.json", File.ReadAllText("person.json"), true);
			RunFTJSerializerTest("supersimple.json", File.ReadAllText("supersimple.json"), true);
			RunFTJSerializerTest("simple.json", File.ReadAllText("simple.json"), true);
			RunFTJSerializerTest("TestMessage.json", File.ReadAllText("TestMessage.json"), true);
		}

		[Test]
		public static void TestStandardSerializer() {
			RunStandardSerializerTest("jsstyle.json", File.ReadAllText("jsstyle.json"), false);
			RunStandardSerializerTest("person.json", File.ReadAllText("person.json"), false);
			RunStandardSerializerTest("supersimple.json", File.ReadAllText("supersimple.json"), false);
			RunStandardSerializerTest("simple.json", File.ReadAllText("simple.json"), false);
			RunStandardSerializerTest("TestMessage.json", File.ReadAllText("TestMessage.json"), false);
		}

		[Test]
		public static void TestStandardCodegenSerializer() {
			RunStandardSerializerTest("jsstyle.json", File.ReadAllText("jsstyle.json"), true);
			RunStandardSerializerTest("person.json", File.ReadAllText("person.json"), true);
			RunStandardSerializerTest("supersimple.json", File.ReadAllText("supersimple.json"), true);
			RunStandardSerializerTest("simple.json", File.ReadAllText("simple.json"), true);
			RunStandardSerializerTest("TestMessage.json", File.ReadAllText("TestMessage.json"), true);
		}

		private static void RunFTJSerializerTest(string name, string jsonStr, bool useCodegen) {
			byte[] ftj = null;
			int serializedSize = 0;
			int afterPopulateSize = 0;
			TObject tObj;
			Json original;
			Json newJson;

			TJson.UseCodegeneratedSerializer = false;
			
			tObj = CreateJsonTemplate(Path.GetFileNameWithoutExtension(name), jsonStr);
			original = (Json)tObj.CreateInstance();

			// using standard json serializer to populate object with values.
			original.PopulateFromJson(jsonStr);

			TJson.UseCodegeneratedSerializer = useCodegen;
			TJson.DontCreateSerializerInBackground = true;

			serializedSize = tObj.ToFasterThanJson(original, out ftj);

			unsafe {
				fixed (byte* p = ftj) {
					newJson = (Json)tObj.CreateInstance();
					afterPopulateSize = tObj.PopulateFromFasterThanJson(newJson, (IntPtr)p, serializedSize);
				}
			}

			Assert.AreEqual(serializedSize, afterPopulateSize);
			AssertAreEqual(original, newJson);
		}

		private static void RunStandardSerializerTest(string name, string jsonStr, bool useCodegen) {
			byte[] jsonArr = null;
			int serializedSize = 0;
			int afterPopulateSize = 0;
			TObject tObj;
			Json original;
			Json newJson;

			TJson.UseCodegeneratedSerializer = false;
			
			tObj = CreateJsonTemplate(Path.GetFileNameWithoutExtension(name), jsonStr);
			original = (Json)tObj.CreateInstance();

			// TODO:
			// Change to newtonsoft for verification.

			// using standard json serializer to populate object with values.
			original.PopulateFromJson(jsonStr);

			TJson.UseCodegeneratedSerializer = useCodegen;
			TJson.DontCreateSerializerInBackground = true;

			serializedSize = tObj.ToJsonUtf8(original, out jsonArr);

			unsafe {
				fixed (byte* p = jsonArr) {
					newJson = (Json)tObj.CreateInstance();
					afterPopulateSize = tObj.PopulateFromJson(newJson, (IntPtr)p, serializedSize);
				}
			}

			Assert.AreEqual(serializedSize, afterPopulateSize);
			AssertAreEqual(original, newJson);
		}

        [Test]
        public static void TestIncorrectInputJsonForDefaultSerializer() {
            TJson tObj = CreateJsonTemplateFromFile("supersimple.json");

            TJson.UseCodegeneratedSerializer = false;
            var obj = (Json)tObj.CreateInstance();

            string invalidJson = "message";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "PlayerId: \"Hey!\" }";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "{ PlayerId: \"Hey!\" ";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "{ PlayerId: Hey }";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "{ PlayerId: 123";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));
        }

 //       [Test]
        public static void TestIncorrectInputJsonForCodegenSerializer() {
            TJson tObj = CreateJsonTemplateFromFile("supersimple.json");

            TJson.UseCodegeneratedSerializer = true;
            TJson.DontCreateSerializerInBackground = true;
            var obj = (Json)tObj.CreateInstance();

            string invalidJson = "message";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "PlayerId: \"Hey!\" }";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "{ PlayerId: \"Hey!\" ";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "{ PlayerId: Hey }";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));

            invalidJson = "{ PlayerId: 123";
            Assert.Catch(() => obj.PopulateFromJson(invalidJson));
        }

        private static TJson CreateJsonTemplate(string className, string json) {
            var tobj = TJson.CreateFromJson(json);
            tobj.ClassName = className;
            return tobj;
        }

#if !DEBUG // Serializer benchmark tests - only in release.

		[Test]
		[Category("LongRunning"), Timeout(5 * 60000)] // timeout 5 minutes
		public static void BenchmarkFTJSerializer() {
			int numberOfTimes = 1000000;

			Console.WriteLine("Benchmarking ftj serializer, repeats: " + numberOfTimes);
			Console.WriteLine(AddSpaces("File", 20) + AddSpaces("Type", 16) + AddSpaces("Serialize", 12) + "Deseralize");
			Console.WriteLine("----------------------------------------------------------");

			RunFTJBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, false);
			RunFTJBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, false);
			RunFTJBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, false);
			RunFTJBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, false);
			RunFTJBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, false);
		}

		[Test]
        [Category("LongRunning"), Timeout(5 * 60000)] // timeout 5 minutes
		public static void BenchmarkFTJCodegenSerializer() {
			int numberOfTimes = 1000000;

			Console.WriteLine("Benchmarking ftj serializer, repeats: " + numberOfTimes);
			Console.WriteLine(AddSpaces("File", 20) + AddSpaces("Type", 16) + AddSpaces("Serialize", 12) + "Deseralize");
			Console.WriteLine("----------------------------------------------------------");

			RunFTJBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, true);
			RunFTJBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, true);
			RunFTJBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, true);
			RunFTJBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, true);
			RunFTJBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, true);
		}

		[Test]
        [Category("LongRunning"), Timeout(5 * 60000)] // timeout 5 minutes
		public static void BenchmarkStandardJsonSerializer() {
			int numberOfTimes = 1000000;

			Console.WriteLine("Benchmarking standard serializer, repeats: " + numberOfTimes);
			Console.WriteLine(AddSpaces("File", 20) + AddSpaces("Type", 16) + AddSpaces("Serialize", 12) + "Deseralize");
			Console.WriteLine("----------------------------------------------------------");

			RunStandardJsonBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, false);
		}

		[Test]
        [Category("LongRunning"), Timeout(5 * 60000)] // timeout 5 minutes
		public static void BenchmarkStandardCodegenJsonSerializer() {
			int numberOfTimes = 1000000;

			Console.WriteLine("Benchmarking standard serializer, repeats: " + numberOfTimes);
			Console.WriteLine(AddSpaces("File", 20) + AddSpaces("Type", 16) + AddSpaces("Serialize", 12) + "Deseralize");
			Console.WriteLine("----------------------------------------------------------");

			RunStandardJsonBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, true);
		}

		[Test]
        [Category("LongRunning"), Timeout(5 * 60000)] // timeout 5 minutes
		public static void BenchmarkAllSerializers() {
			int numberOfTimes = 1000000;

			Console.WriteLine("Benchmarking serializers, repeats: " + numberOfTimes);
			Console.WriteLine(AddSpaces("File", 20) + AddSpaces("Type", 16) + AddSpaces("Serialize", 12) + "Deseralize");
			Console.WriteLine("----------------------------------------------------------");

			RunFTJBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, false);
			RunFTJBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("jsstyle.json", File.ReadAllText("jsstyle.json"), numberOfTimes, true);

			Console.WriteLine();

			RunFTJBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, false);
			RunFTJBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("person.json", File.ReadAllText("person.json"), numberOfTimes, true);

			Console.WriteLine();

			RunFTJBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, false);
			RunFTJBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("supersimple.json", File.ReadAllText("supersimple.json"), numberOfTimes, true);

			Console.WriteLine();

			RunFTJBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, false);
			RunFTJBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("simple.json", File.ReadAllText("simple.json"), numberOfTimes, true);

			Console.WriteLine();

			RunFTJBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, false);
			RunFTJBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, true);
			RunStandardJsonBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, false);
			RunStandardJsonBenchmark("TestMessage.json", File.ReadAllText("TestMessage.json"), numberOfTimes, true);
		}

		private static void RunFTJBenchmark(string name, string json, int numberOfTimes, bool useCodegen) {
			byte[] ftj = null;
			int size = 0;
			TObject tObj;
			Json jsonInst;
			DateTime start;
			DateTime stop;

			TJson.UseCodegeneratedSerializer = false;

			tObj = CreateJsonTemplate(Path.GetFileNameWithoutExtension(name), json);
			jsonInst = (Json)tObj.CreateInstance();

			// using standard json serializer to populate object with values.
			jsonInst.PopulateFromJson(json);

			TJson.UseCodegeneratedSerializer = useCodegen;
			TJson.DontCreateSerializerInBackground = true;

			if (useCodegen) {
				Console.Write(AddSpaces(name, 20) + AddSpaces("FTJ-Codegen", 16));

				// Call serialize once to make sure that the codegenerated serializer is created.
				size = tObj.ToFasterThanJson(jsonInst, out ftj);
			} else {
				Console.Write(AddSpaces(name, 20) + AddSpaces("FTJ", 16));
			}

			// Serializing to FTJ.
			start = DateTime.Now;
			for (int i = 0; i < numberOfTimes; i++) {
				size = tObj.ToFasterThanJson(jsonInst, out ftj);
			}
			stop = DateTime.Now;

			PrintResult(stop, start, numberOfTimes, 12);

			// Deserializing from FTJ.
			unsafe {
				fixed (byte* p = ftj) {
					start = DateTime.Now;
					for (int i = 0; i < numberOfTimes; i++) {
						jsonInst = (Json)tObj.CreateInstance();
						size = tObj.PopulateFromFasterThanJson(jsonInst, (IntPtr)p, size);
					}
				}
			}
			stop = DateTime.Now;

			PrintResult(stop, start, numberOfTimes, 0);
			Console.Write("\n");
		}

        private static void RunStandardJsonBenchmark(string name, string json, int numberOfTimes, bool useCodegen) {
			byte[] jsonArr = null;
			int size = 0;
			TObject tObj;
			Json jsonInst;
			DateTime start;
			DateTime stop;

			TJson.UseCodegeneratedSerializer = false;
			
			tObj = CreateJsonTemplate(Path.GetFileNameWithoutExtension(name), json);
			jsonInst = (Json)tObj.CreateInstance();

			// using standard json serializer to populate object with values.
			jsonInst.PopulateFromJson(json);

			TJson.UseCodegeneratedSerializer = useCodegen;
			TJson.DontCreateSerializerInBackground = true;

			if (useCodegen) {
				Console.Write(AddSpaces(name, 20) + AddSpaces("STD-Codegen", 16));

				// Call serialize once to make sure that the codegenerated serializer is created.
				jsonArr = jsonInst.ToJsonUtf8();
			} else {
				Console.Write(AddSpaces(name, 20) + AddSpaces("STD", 16));
			}

			// Serializing to standard json.
			start = DateTime.Now;
			for (int i = 0; i < numberOfTimes; i++) {
				size = tObj.ToJsonUtf8(jsonInst, out jsonArr);
			}
			stop = DateTime.Now;
			PrintResult(stop, start, numberOfTimes, 12);

			// Deserializing from standard json.
			unsafe {
				fixed (byte* p = jsonArr) {
					start = DateTime.Now;
					for (int i = 0; i < numberOfTimes; i++) {
						size = tObj.PopulateFromJson(jsonInst, (IntPtr)p, size);
					}
				}
			}
			stop = DateTime.Now;
			PrintResult(stop, start, numberOfTimes, 0);
			Console.Write("\n");
        }

#endif // Serializer benchmark tests - only in release.

		private static string AddSpaces(string str, int totalLength) {
			string after = str;
			for (int i = 0; i < (totalLength - str.Length); i++) {
				after += " ";
			}
			return after;
		}

		private static void PrintResult(DateTime stop, DateTime start, int numberOfTimes, int space) {
			var tms = (stop - start).TotalMilliseconds;
			var kps = numberOfTimes / tms;

			string str = AddSpaces(kps.ToString(".00") + " k/s", space);

			Console.Write(AddSpaces(kps.ToString(".00") + " k/s", space));
		}

        [Test]
        public static void EncodeAndDecodeJsonStrings() {
            // "standard" special characters.
            EncodeDecodeString("1\b2\f3\n4\r5\t6\"7\\");

            // Low unicode characters.
            EncodeDecodeString("UnicodeChars:\u001a\u0006");

            // High unicode characters.
            EncodeDecodeString("UnicodeChars:\u2031");
        }

        private static void EncodeDecodeString(string value) {
            byte[] buffer = new byte[1024];
            byte[] expected;
            int count;
            int used;
            string decodedString;

            unsafe {
                fixed (byte* p = buffer) {
                    count = JsonHelper.WriteString((IntPtr)p, buffer.Length, value);
                    JsonHelper.ParseString((IntPtr)p, buffer.Length, out decodedString, out used);
                }
            }
            expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
            AssertAreEqual(expected, buffer, count);

            Assert.AreEqual(count, used);
            Assert.AreEqual(value, decodedString);
        }

        private static void AssertAreEqual(byte[] expected, byte[] actual, int count) {
            Assert.AreEqual(expected.Length, count);
            for (int i = 0; i < count; i++) {
                if (expected[i] != actual[i])
                    throw new AssertionException("Expected '" + expected[i] + "' but found '" + actual[i] + "' at position " + i + ".");
            }
        }

        [Test]
        public static void GenerateStdSerializationParseTreeOverview() {
            TJson objTemplate;
            objTemplate = CreateJsonTemplateFromFile("person.json");
            ParseNode parseTree = ParseTreeGenerator.BuildParseTree(objTemplate);
            Console.WriteLine(parseTree.ToString());
        }

        [Test]
        public static void GenerateStdSerializationAstTreeOverview() {
            TJson objTemplate;
            objTemplate = CreateJsonTemplateFromFile("person.json");

			StdDomGenerator domGenerator = new StdDomGenerator(objTemplate);
            Console.WriteLine(domGenerator.GenerateDomTree().ToString(true));
        }

		[Test]
		public static void GenerateFTJSerializationAstTreeOverview() {
			TJson objTemplate;
			objTemplate = CreateJsonTemplateFromFile("person.json");

			FTJDomGenerator domGenerator = new FTJDomGenerator(objTemplate);
			Console.WriteLine(domGenerator.GenerateDomTree().ToString(true));
		}

		[Test]
		public static void GenerateStdSerializationCsCode() {
			TJson objTemplate;

			objTemplate = CreateJsonTemplateFromFile("supersimple.json");
			objTemplate.ClassName = "PreGenerated";

			StdCSharpGenerator generator = new StdCSharpGenerator(new StdDomGenerator(objTemplate));
			Console.WriteLine(generator.GenerateCode());
		}

		[Test]
		public static void GenerateFTJSerializationCsCode() {
			TJson objTemplate;

			objTemplate = CreateJsonTemplateFromFile("supersimple.json");
			objTemplate.ClassName = "PreGenerated";

			FTJCSharpGenerator generator = new FTJCSharpGenerator(new FTJDomGenerator(objTemplate));
			Console.WriteLine(generator.GenerateCode());
		}

        /// <summary>
        /// Creates a template from a JSON-by-example file
        /// </summary>
        /// <param name="filePath">The file to load</param>
        /// <returns>The newly created template</returns>
        private static TJson CreateJsonTemplateFromFile( string filePath ) {
            string json = File.ReadAllText(filePath);
            string className = Path.GetFileNameWithoutExtension(filePath);
            var tobj = TJson.CreateFromMarkup<Json, TJson>("json", json, className);
            tobj.ClassName = className;
            return tobj;
        }

		//[Test]
		//public static void DebugPregeneratedSerializationCode() {
		//	byte[] jsonArr;
		//	int size;
		//	int sizeAfterPopulate;
		//	string correctJson;
		//	string codegenJson;
		//	TJson tPerson;

		//	tPerson = CreateJsonTemplateFromFile("supersimple.json");
		//	var person = (Json)tPerson.CreateInstance();
		//	//SetDefaultPersonValues(person);

		//	TypedJsonSerializer serializer = new __starcountergenerated__.PreGeneratedSerializer();

		//	// First use fallback serializer to create a correct json string.
		//	TJson.UseCodegeneratedSerializer = false;
		//	TJson.FallbackSerializer = new NewtonsoftSerializer();
		//	person.PopulateFromJson(File.ReadAllText("supersimple.json"));
		//	correctJson = person.ToJson();

		//	// Then we do the same but use codegeneration. We use the pregenerated serializer here
		//	// to be able to debug it, but we will get the same result by enabling codegenerated serializer 
		//	// on the template.
		//	TJson.UseCodegeneratedSerializer = true;
		//	TJson.FallbackSerializer = DefaultSerializer.Instance;

		//	size = serializer.ToJsonUtf8(person, out jsonArr);
		//	codegenJson = Encoding.UTF8.GetString(jsonArr, 0, size);

		//	Console.WriteLine("Count: " + size);
		//	Console.WriteLine(codegenJson);

		//	AssertAreEqual(Encoding.UTF8.GetBytes(correctJson), jsonArr, size);
		//	Assert.AreEqual(correctJson, codegenJson);

		//	// Now we populate a new person instance with values from the serializer json.
		//	// And compare it to the original. All values should be identical.
		//	var person2 = (Json)tPerson.CreateInstance();
		//	sizeAfterPopulate = serializer.PopulateFromJson(person2, jsonArr, size);

		//	Assert.AreEqual(size, sizeAfterPopulate);
		//	AssertAreEqual(person, person2);
		//}

        private static void AssertAreEqual(Json expected, Json actual) {
            TJson tExpected = (TJson)expected.Template;
            TJson tActual = (TJson)actual.Template;

            // We assume that the instances used the same Template.
            Assert.AreEqual(tExpected, tActual);
            foreach (Template child in tExpected.Properties) {
                if (child is TBool)
                    Assert.AreEqual(((TBool)child).Getter(expected), ((TBool)child).Getter(actual));
                else if (child is TDecimal)
                    Assert.AreEqual(((TDecimal)child).Getter(expected), ((TDecimal)child).Getter(actual));
                else if (child is TDouble)
                    Assert.AreEqual(((TDouble)child).Getter(expected), ((TDouble)child).Getter(actual));
                else if (child is TLong)
                    Assert.AreEqual(((TLong)child).Getter(expected), ((TLong)child).Getter(actual));
                else if (child is TString)
                    Assert.AreEqual(((TString)child).Getter(expected), ((TString)child).Getter(actual));
                else if (child is TJson)
                    AssertAreEqual(((TJson)child).Getter(expected), ((TJson)child).Getter(actual));
                else if (child is TObjArr) {
                    var arr1 = ((TObjArr)child).Getter(expected);
                    var arr2 = ((TObjArr)child).Getter(actual);
                    Assert.AreEqual(arr1.Count, arr2.Count);
                    for (int i = 0; i < arr1.Count; i++) {
                        AssertAreEqual((Json)arr1._GetAt(i), (Json)arr2._GetAt(i));
                    }
                } else
                    throw new NotSupportedException();
            }
        }

        private static void SetDefaultPersonValues(dynamic person) {
            person.FirstName = "Arne";
            person.LastName = "Anka";
            person.Age = 19;
            person.Stats = 39.4567m;
            person.ExtraInfo.Text = "hi ha ho he";

            var field = person.Fields.Add();
            field.Type = "Phone";
            field.Info.Text = "123-555-7890";

            field = person.Fields.Add();
            field.Type = "Email";
            field.Info.Text = "arneanka@gmail.com";
        }
    }
}
