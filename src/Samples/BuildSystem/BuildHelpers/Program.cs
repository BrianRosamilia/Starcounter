using Starcounter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildHelpers
{
    enum ModeType
    {
        None,
        UploadBigFile
    };

    enum ReturnCodes
    {
        Success,
        WrongPutResponse,
        CantSendFile,
        ExceptionOccurred,
        WrongChecksum
    }

    class Program
    {
        static Int32 Main(string[] args)
        {
            try
            {
                ModeType mode = new ModeType();

                foreach (String arg in args)
                {
                    if (arg.StartsWith("-Mode="))
                    {
                        String modeString = arg.Substring("-Mode=".Length);

                        if (modeString == "UploadBigFile")
                            mode = ModeType.UploadBigFile;
                    }
                }

                if (mode == ModeType.UploadBigFile)
                {
                    String uploadUri = null, pathToFile = null;

                    for (Int32 i = 0; i < args.Length; i++)
                    {
                        if (args[i].StartsWith("-UploadUri="))
                        {
                            uploadUri = args[i].Substring("-UploadUri=".Length);

                            if (uploadUri.StartsWith("\""))
                                uploadUri = uploadUri.Substring(1, uploadUri.Length - 2);
                        }
                        else if (args[i].StartsWith("-PathToFile="))
                        {
                            pathToFile = args[i].Substring("-PathToFile=".Length);
                            if (pathToFile.StartsWith("\""))
                                pathToFile = pathToFile.Substring(1, pathToFile.Length - 2);
                        }
                    }

                    Response resp = X.POST(uploadUri, (String) null, null);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Problems obtaining resource from \"" + uploadUri + "\" Status description: " + resp.Body);
                        return (Int32) ReturnCodes.WrongPutResponse;
                    }

                    String resId = resp.Body;
               
                    using (FileStream fs = File.OpenRead(pathToFile))
                    {
                        Int32 readBytes;
                        Byte[] buf = new Byte[64 * 1024];

                        while ((readBytes = fs.Read(buf, 0, buf.Length)) > 0)
                        {
                            UInt64 checkSum = 0;

                            if (readBytes < buf.Length)
                            {
                                Byte[] truncatedBuf = new Byte[readBytes];
                                Array.Copy(buf, truncatedBuf, readBytes);

                                for (Int32 i = 0; i < truncatedBuf.Length; i++)
                                    checkSum += truncatedBuf[i];

                                resp = X.PUT(uploadUri + "/" + resId, truncatedBuf, "UploadSettings: Final\r\n");
                            }
                            else
                            {
                                for (Int32 i = 0; i < buf.Length; i++)
                                    checkSum += buf[i];

                                resp = X.PUT(uploadUri + "/" + resId, buf, null);
                            }

                            // Checking response status code.
                            if (!resp.IsSuccessStatusCode)
                            {
                                Console.WriteLine("Error in response: " + resp.Body);
                                return (Int32)ReturnCodes.CantSendFile;
                            }

                            // Checking checksum.
                            if (checkSum != UInt64.Parse(resp.Body))
                            {
                                Console.WriteLine("Wrong response checksum: " + resp.Body);
                                return (Int32)ReturnCodes.WrongChecksum;
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("Application threw an exception: " + exc.ToString());
                return (Int32)ReturnCodes.ExceptionOccurred;
            }

            Console.WriteLine("Operation completed successfully!");
            return (Int32)ReturnCodes.Success;
        }
    }
}
