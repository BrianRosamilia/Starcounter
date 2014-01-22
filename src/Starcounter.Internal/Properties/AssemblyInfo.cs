using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Starcounter.Internal")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Starcounter AB")]
[assembly: AssemblyProduct("Starcounter.Internal")]
[assembly: AssemblyCopyright("Copyright © Starcounter AB 2012")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("eba56304-3b70-4c9c-ac5c-1d7dad5995ec")]

// Have internals visible to the friend test assembly
[assembly: InternalsVisibleTo("Starcounter.Rest,                       PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Internal.Tests,             PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.XSON,                       PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.XSON.PartialClassGenerator, PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.XSON.Tests,                 PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.XSON.CodeGeneration.Tests,  PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Bootstrap,                  PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter,                            PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("NetworkIoTest,                          PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Apps.JsonPatch,             PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Apps.Server,                PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Node,                       PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Web.Tests,                  PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("HttpParser.Tests,                       PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Bootstrap,                  PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]
[assembly: InternalsVisibleTo("Starcounter.Hypermedia,                 PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
