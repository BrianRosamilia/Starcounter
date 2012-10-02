﻿
using NUnit.Framework;
using System;
using System.Text;

namespace Starcounter.Internal
{
   /// <summary>
   /// Tests that reading and writing Base32, Base64 and Base256 values works as expected
   /// </summary>
   /// <remarks>
   /// Type       Range  
   /// =========================
   /// Base64x11    				     // Will hold a 64 bit value	(66)
   /// Base64x6   68719476736	     // Will hold a 32 bit value (36)
   /// Base64x5   1073741824 
   /// Base64x4   16777216         // Will hold a 24 bit value (24)
   /// Base64x3   262144			  // Will hold a 16 bit value (18)
   /// Base64x2   4096			     // Will hold a 8 bit value (12)
   /// Base64x1   64
   ///
   /// Type       Range  
   /// =========================
   /// Base32x13    				     // Will hold a 64 bit value	(65)
   /// Base32x7   34359738368	     // Will hold a 32 bit value (35)
   /// Base32x6   1073741824   
   /// Base32x5   33554432         // Will hold a 24 bit value (25)
   /// Base32x4   1048576			  // Will hold a 16 bit value (20)
   /// Base32x3   32768
   /// Base32x2   1024			     // Will hold a 8 bit value (10)
   /// Base32x1   32
   /// </remarks>
   [TestFixture]
   internal class TestBaseXint
   {
      /// <summary>
      /// Tests that the integers written to a buffer are the same when they are read back. Also tests that values outside of the maxValue
      /// fails.
      /// </summary>
      /// <param name="writeFunc">The function used to write an integer in a certain base (for instance WriteBase64x3)</param>
      /// <param name="readFunc">The function used to read an integer in a certain base (for instance ReadBase64x3)</param>
      /// <param name="maxValue">
      /// The maximum maxValue. For instance for Read/Write Base64x1 this value should be 64 and for Read/Write Base64x2 this value should be 4096 (64^2).
      /// </param>
      private unsafe void TestFixedReadWrite(Action<UInt64, IntPtr> writeFunc, Func<IntPtr, UInt64> readFunc, int size, ulong maxValue)
      {
         string str = String.Format("Range 0-{0} using {1} and {2}.", maxValue, writeFunc.Method.ToString(),
                                    readFunc.Method.ToString());
         var buffer = new byte[2048];
         fixed (byte* pbuf = buffer)
         {
            {
               ulong t = 0;
               do
               {
                  if (maxValue > 1000000 && t == 200000)
                  {
                     // Speed the test up as ranges can be very big. We will only test beginning and end of maxValue
                     t = maxValue - 200000;
                  }
                  writeFunc(t, (IntPtr) pbuf);
                  UInt64 read = readFunc((IntPtr) pbuf);
                  if (read != t)
                  {
                     string encoding = Encoding.UTF8.GetString(buffer, 0, size);
                     Assert.AreEqual(t, read, "Encoding \"" + encoding + "\" " + str);
                  }
                  t++;
               } while (t != maxValue && t != 0); // Handle 64 bit roof (increasing t becomes zero again)
            }

            var rnd = new Random();
            ulong cnt = (ulong)Math_Pow(size, 4) + 1000;
            for (ulong i = 0; i <= cnt; i++)
            {
               var t = (UInt64)(rnd.NextDouble() * maxValue);
               writeFunc(t, (IntPtr)pbuf);
               UInt64 read = readFunc((IntPtr)pbuf);
               if (read != t)
               {
                  string encoding = Encoding.UTF8.GetString(buffer, 0, size);
                  Assert.AreEqual(t, read, "Random test. Encoding \"" + encoding + "\" " + str);
               }
            }
            // Test upper boundary
            writeFunc(maxValue, (IntPtr)pbuf);
            Assert.AreEqual(maxValue, readFunc((IntPtr)pbuf), str);
            if (maxValue != 0xFFFFFFFFFFFFFFFF)
            {
               // Its possible to send in a larger integer than is supported by the function. This should fail.
               writeFunc(maxValue + 1, (IntPtr)pbuf);
               Assert.AreNotEqual(maxValue + 1, readFunc((IntPtr)pbuf), str);
            }
         }
         Console.WriteLine( str );
      }
     
      /// <summary>
      /// Math.Pow integer version
      /// </summary>
      /// <param name="x"></param>
      /// <param name="power"></param>
      /// <returns></returns>
      public static long Math_Pow(int x, short power)
      {
         if (power == 0) return 1;
         if (power == 1) return x;
         // ----------------------
         int n = 15;
         while ((power <<= 1) >= 0) n--;

         long tmp = x;
         while (--n > 0)
            tmp = tmp * tmp *
                 (((power <<= 1) < 0) ? x : 1);
         return tmp;
      }

      private unsafe void TestVariableReadWrite(Func<UInt64, IntPtr,ulong> writeFunc, Func<IntPtr, UInt64> readFunc, ulong minValue, ulong maxValue, uint expectedSize )
      {
         var buffer = new byte[2048];
         fixed (byte* pbuf = buffer)
         {
            for (ulong t = minValue; t <= maxValue; t++)
            {
               if ( (maxValue-minValue) > 10000 && t == minValue + 1000)
               {
                  // Speed the test up as ranges can be very big. We will only test beginning and end of maxValue
                  t = maxValue - 1000;
               }
               Assert.AreEqual(expectedSize,writeFunc(t, (IntPtr)pbuf));
               Assert.AreEqual(t, readFunc((IntPtr)pbuf));
            }
            // Test upper boundary
            Assert.AreEqual( expectedSize, writeFunc(maxValue, (IntPtr)pbuf));
            Assert.AreEqual(maxValue, readFunc((IntPtr)pbuf));
            if ( maxValue != 0xFFFFFFFFFFFFFFFF )
            {
               // Its possible to send in a larger integer than is supported by the function. This should fail.
               Assert.AreNotEqual( expectedSize, writeFunc(maxValue+1, (IntPtr)pbuf) );
               Assert.AreNotEqual(maxValue+1, readFunc((IntPtr)pbuf));
            }
         }
         Console.WriteLine(String.Format("Range 0-{0} tested using {1} and {2}.", maxValue - 1, writeFunc.Method.ToString(), readFunc.Method.ToString()));
      }


      /// <summary>
      /// Will test that the fixed size integer read functions return the same value as the write functions.
      /// For instance, that ReadBase32x3 will return the integer supplied in WriteBase32x3.
      /// </summary>
      [Test]
      public unsafe void TestBase32FixedSizes()
      {
         TestFixedReadWrite(Base32Int.WriteBase32x1, Base32Int.ReadBase32x1, 1, 32 - 1);
         TestFixedReadWrite(Base32Int.WriteBase32x2, Base32Int.ReadBase32x2, 2, 32 * 32 - 1 );
         TestFixedReadWrite(Base32Int.WriteBase32x3, Base32Int.ReadBase32x3, 3, 32 * 32 * 32 - 1);
         TestFixedReadWrite(Base32Int.WriteBase32x4, Base32Int.ReadBase32x4, 4, 32 * 32 * 32 * 32 -1 );
         //         TestFixedReadWrite(Base32Int.WriteBase32x5, Base32Int.ReadBase32x5, 32 * 32 * 32 * 32 * 32 -1 );
         TestFixedReadWrite(Base32Int.WriteBase32x6, Base32Int.ReadBase32x6, 6, 32 * 32 * 32 * 32 * 32 * 32 - 1);
         TestFixedReadWrite(Base32Int.WriteBase32x7, Base32Int.ReadBase32x7, 7, 32UL * 32UL * 32UL * 32UL * 32UL * 32UL * 32UL - 1 );
         TestFixedReadWrite(Base32Int.WriteBase32x13, Base32Int.ReadBase32x13, 13, 0xFFFFFFFFFFFFFFFF ); // Test maxed out 64 bit value
      }


      [Test]
      public unsafe void TestBase32VariableSizes()
      {
         Assert.AreEqual(1, Base32Int.MeasureNeededSize(0));

         Assert.AreEqual(1, Base32Int.MeasureNeededSize(1));
         Assert.AreEqual(1, Base32Int.MeasureNeededSize(31));
         Assert.AreEqual(2, Base32Int.MeasureNeededSize(32));
         Assert.AreEqual(2, Base32Int.MeasureNeededSize(1023));
         Assert.AreEqual(3, Base32Int.MeasureNeededSize(1024));
         Assert.AreEqual(3, Base32Int.MeasureNeededSize(32767));
         Assert.AreEqual(4, Base32Int.MeasureNeededSize(32768));
         Assert.AreEqual(4, Base32Int.MeasureNeededSize(32 * 32 * 32 * 32 - 1));
         Assert.AreEqual(6, Base32Int.MeasureNeededSize(32 * 32 * 32 * 32)); // Size 5 not available
         Assert.AreEqual(6, Base32Int.MeasureNeededSize(32 * 32 * 32 * 32 * 32 - 1)); // Size 5 not available
         Assert.AreEqual(6, Base32Int.MeasureNeededSize(32 * 32 * 32 * 32 * 32 ));
         Assert.AreEqual(6, Base32Int.MeasureNeededSize(32 * 32 * 32 * 32 * 32 * 32 - 1));
      }



      /// <summary>
      /// Will test that the fixed size integer read functions return the same value as the write functions.
      /// For instance, that ReadBase32x3 will return the integer supplied in WriteBase32x3.
      /// </summary>
      [Test]
      public unsafe void TestBase64FixedSizes()
      {
         TestFixedReadWrite(Base64Int.WriteBase64x1, Base64Int.ReadBase64x1, 1, 64 - 1);
         TestFixedReadWrite(Base64Int.WriteBase64x2, Base64Int.ReadBase64x2, 2, 64 * 64 - 1);
         TestFixedReadWrite(Base64Int.WriteBase64x3, Base64Int.ReadBase64x3, 3, 64 * 64 * 64 - 1);
         TestFixedReadWrite(Base64Int.WriteBase64x4, Base64Int.ReadBase64x4, 4, 64 * 64 * 64 * 64 - 1);
         TestFixedReadWrite(Base64Int.WriteBase64x5, Base64Int.ReadBase64x5, 5, 64 * 64 * 64 * 64 * 64 - 1);
         TestFixedReadWrite(Base64Int.WriteBase64x6, Base64Int.ReadBase64x6, 6, 64UL * 64UL * 64UL * 64UL * 64UL * 64UL - 1);
         TestFixedReadWrite(Base64Int.WriteBase64x11, Base64Int.ReadBase64x11, 11, 0xFFFFFFFFFFFFFFFF); // Test maxed out 64 bit value
      }


      [Test]
      public unsafe void TestBase64VariableSizes()
      {
         Assert.AreEqual(1, Base64Int.MeasureNeededSize(0));
         Assert.AreEqual(1, Base64Int.MeasureNeededSize(1));
         Assert.AreEqual(1, Base64Int.MeasureNeededSize(63));
         Assert.AreEqual(2, Base64Int.MeasureNeededSize(64));
         Assert.AreEqual(2, Base64Int.MeasureNeededSize(64*64-1));
         Assert.AreEqual(3, Base64Int.MeasureNeededSize(64*64));
         Assert.AreEqual(3, Base64Int.MeasureNeededSize(64*64*64-1));
         Assert.AreEqual(4, Base64Int.MeasureNeededSize(64*64*64));
         Assert.AreEqual(4, Base64Int.MeasureNeededSize(64 * 64 * 64 * 64 - 1));
         Assert.AreEqual(5, Base64Int.MeasureNeededSize(64 * 64 * 64 * 64)); // Size 5 not available
         Assert.AreEqual(5, Base64Int.MeasureNeededSize(64 * 64 * 64 * 64 * 64 - 1)); // Size 5 not available
         Assert.AreEqual(6, Base64Int.MeasureNeededSize(64 * 64 * 64 * 64 * 64));
         Assert.AreEqual(6, Base64Int.MeasureNeededSize(64UL * 64UL * 64UL * 64UL * 64UL * 64UL - 1));
      }


   }
}