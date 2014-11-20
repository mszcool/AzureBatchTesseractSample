// 
// MIT License 
// Copyright (c) 2014 Microsoft. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person 
// obtaining a copy of this software and associated documentation 
// files (the "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, distribute, 
// sublicense, and/or sell copies of the Software, and to permit persons to 
// whom the Software is furnished to do so, subject to the following conditions: 
// 
// The above copyright notice and this permission notice shall be included 
// in all copies or substantial portions of the Software. 
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
//
using Microsoft.WindowsAzure.Storage;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace BatchTesseractWrapper
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Starting BatchTesseractWrapper.exe...");


            try
            {
                #region Reading Configuration Data and determining Tesseract Base Path

                var tesseractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"tesseract\tesseract.exe");

                var storageAccountName = ConfigurationManager.AppSettings["BatchDemoStorageAccount"];
                var storageAccountKey = ConfigurationManager.AppSettings["BatchDemoStorageAccountKey"];
                var storageAccount = CloudStorageAccount.Parse(string.Format(
                                        "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", 
                                        storageAccountName,
                                        storageAccountKey)); 
                
                var blobClient = storageAccount.CreateCloudBlobClient();
                var blobOcrResultsContainer = blobClient.GetContainerReference("ocr-results");
                blobOcrResultsContainer.CreateIfNotExists();

                #endregion

                Console.WriteLine("Wrapping tesseract OCR to upload results back to Azure BLOB storage!");
                Console.WriteLine("- Input file: {0}", args[0]);
                Console.WriteLine("- Output file: {0}", args[1]);

                var inFile = string.Format("{0}_in.png", args[1]);
                try
                {
                    Console.WriteLine("Writing blob content to local file...");
                    var httpClient = new HttpClient();
                    var httpGetTask = httpClient.GetAsync(args[0]);
                    httpGetTask.Wait();
                    httpGetTask.Result.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(inFile, FileMode.Create))
                    {
                        var writeTask = httpGetTask.Result.Content.CopyToAsync(fs);
                        writeTask.Wait();
                    }
                    Console.WriteLine("Local file written!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error downloading file: {0}", ex.Message);
                    return -7777;
                }

                Console.WriteLine("Launching tesseract...");
                Console.WriteLine(tesseractPath);
                var tesseractProc = Process.Start
                    (
                        tesseractPath, 
                        string.Format("\"{0}\" \"{1}\"", inFile, args[1])
                    );
                if (tesseractProc != null)
                {
                    tesseractProc.WaitForExit();
                    Console.WriteLine("Tesseract Exit Code: {0}", tesseractProc.ExitCode);
                    Console.WriteLine();
                    if (tesseractProc.ExitCode == 0)
                    {
                        try
                        {
                            // Upload the resulting file (usually <filename>.txt in case of tesseract)
                            Console.WriteLine("Uploading result to blob...");
                            var newBlob = blobOcrResultsContainer.GetBlockBlobReference(args[1]);
                            newBlob.UploadFromFile(string.Concat(args[1], ".txt"), FileMode.Open);
                            Console.WriteLine("Upload completed!");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Unable to upload to blob: {0}", ex.Message);
                            return -8888;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Tesseract Exit Code is not 0!");
                        return tesseractProc.ExitCode;
                    }
                }
                else
                {
                    Console.WriteLine("Unable to launch tesseract.exe!");
                    return -9999;
                }

                Console.WriteLine("Completed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed executing BatchTesseractWrapper with Exception:");
                Console.WriteLine(ex);
                return (-1) * ex.HResult;
            }
        }
    }
}
