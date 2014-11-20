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
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.FileStaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BatchTesseractClient
{
    internal class Program
    {
        private const string PoolName = "batchtesseractsamplepool";

        private static void Main(string[] args)
        {
            Console.WriteLine("What to do?\r\n(c)reatepool\r\n(s)cheduletasks\r\n(d)elete pool");
            var actionToDo = Console.ReadLine();

            #region Reading configuration Data

            var accountName = ConfigurationManager.AppSettings["BatchAccountName"];
            var accountKey = ConfigurationManager.AppSettings["BatchAccountKey"];

            var storageAccountName = ConfigurationManager.AppSettings["BatchDemoStorageAccount"];
            var storageAccountKey = ConfigurationManager.AppSettings["BatchDemoStorageAccountKey"];
            var storageAccount = CloudStorageAccount.Parse(string.Format(
                                    "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", 
                                    storageAccountName,
                                    storageAccountKey));

            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobOcrSourceContainer = blobClient.GetContainerReference("ocr-source");
            var blobTesseractContainer = blobClient.GetContainerReference("tesseract");

            var stagingStorageCred = new StagingStorageAccount(
                storageAccountName,
                storageAccountKey,
                string.Format("https://{0}.blob.core.windows.net", storageAccountName));

            #endregion

            Console.WriteLine("Creating batch client to access Azure Batch Service...");
            var credentials = new BatchCredentials(accountName, accountKey);
            var batchClient = BatchClient.Connect(
                "https://batch.core.windows.net",
                credentials
                );
            Console.WriteLine("Batch client created successfully!");

            #region Setup Compute Pool or delete compute pool

            if (actionToDo == "c" || actionToDo == "d")
            {
                #region Get Resource Files and files to process from BLOB storage

                Console.WriteLine();

                var binaryResourceFiles = new List<IResourceFile>();
                Console.WriteLine("Get list of 'resource files' required for execution from BLOB storage...");
                foreach (var resFile in blobTesseractContainer.ListBlobs(useFlatBlobListing: true))
                {
                    var sharedAccessSig = CreateSharedAccessSignature(blobTesseractContainer, resFile);
                    var fullUriString = resFile.Uri.ToString();
                    var relativeUriString = fullUriString.Replace(blobTesseractContainer.Uri + "/", "");

                    Console.WriteLine("- {0} ", relativeUriString);

                    binaryResourceFiles.Add(
                        new ResourceFile
                            (
                            fullUriString + sharedAccessSig,
                            relativeUriString.Replace("/", @"\")
                            )
                        );
                }
                Console.WriteLine();

                #endregion

                Console.WriteLine();
                Console.WriteLine("Creating pool if needed...");
                using (var pm = batchClient.OpenPoolManager())
                {
                    var pools = pm.ListPools().ToList();
                    var poolExists = (pools.Select(p => p.Name).Contains(PoolName));
                    if ((actionToDo == "c") && !poolExists)
                    {
                        Console.WriteLine("Creating the pool...");
                        var newPool = pm.CreatePool
                            (PoolName, "3", "small", 5
                            );
                        newPool.StartTask = new StartTask
                        {
                            ResourceFiles = binaryResourceFiles,
                            CommandLine = "cmd /c CopyFiles.cmd",
                            WaitForSuccess = true
                        };
                        newPool.CommitAsync().Wait();
                        Console.WriteLine("Pool {0} created!", PoolName);
                    }
                    else if ((actionToDo == "d") && poolExists)
                    {
                        Console.WriteLine("Deleting the pool...");
                        pm.DeletePoolAsync(PoolName).Wait();
                        Console.WriteLine("Pool {0} deleted!", PoolName);
                    }
                    else
                    {
                        Console.WriteLine("Action {0} not executed since pool does {1}!",
                            actionToDo == "c" ? "'Create Pool'" : "'Delete Pool'",
                            poolExists ? "exist, already" : "not exist, anyway");
                    }
                }
            }

            #endregion

            #region Scheduling and running jobs

            if (actionToDo == "s")
            {
                #region Get the Task Files

                Console.WriteLine();
                var filesToProcess = new List<IResourceFile>();
                Console.WriteLine("Get list of 'files' to be processed in tasks...");
                foreach (var fileToProc in blobOcrSourceContainer.ListBlobs(useFlatBlobListing: true))
                {
                    var sharedAccessSig = CreateSharedAccessSignature(blobOcrSourceContainer, fileToProc);
                    var fullUriString = fileToProc.Uri.ToString();
                    var relativeUriString = fullUriString.Replace(blobOcrSourceContainer.Uri + "/", "");

                    Console.WriteLine("- {0}", relativeUriString);

                    filesToProcess.Add(
                        new ResourceFile(
                            fullUriString + sharedAccessSig,
                            relativeUriString.Replace("/", @"\")
                            )
                        );
                }

                #endregion

                Console.WriteLine();
                Console.WriteLine("Creating a job with its tasks...");
                using (var wiMgr = batchClient.OpenWorkItemManager())
                {
                    var workItemName = string.Format("ocr-{0}", DateTime.UtcNow.Ticks);
                    Console.WriteLine("- Creating a work item {0}...", workItemName);
                    var ocrWorkItem = wiMgr.CreateWorkItem(workItemName);
                    ocrWorkItem.JobExecutionEnvironment =
                        new JobExecutionEnvironment
                        {
                            PoolName = PoolName
                        };
                    ocrWorkItem.CommitAsync().Wait();

                    Console.WriteLine("- Adding tasks to the job of the work item.");
                    var taskNr = 0;
                    const string defaultJobName = "job-0000000001";
                    var job = wiMgr.GetJob(workItemName, defaultJobName);

                    foreach (var ocrFile in filesToProcess)
                    {
                        var taskName = string.Format("task_no_{0}", taskNr++);

                        Console.WriteLine("  - {0} for file {1}", taskName, ocrFile.FilePath);

                        var taskCmd =
                            string.Format(
                                "cmd /c %WATASK_TVM_ROOT_DIR%\\shared\\BatchTesseractWrapper.exe \"{0}\" \"{1}\"",
                                ocrFile.BlobSource,
                                Path.GetFileNameWithoutExtension(ocrFile.FilePath));

                        ICloudTask cloudTask = new CloudTask(taskName, taskCmd);

                        job.AddTask(cloudTask);
                    }
                    Console.WriteLine("- All tasks created, committing job!");
                    job.Commit();

                    Console.WriteLine();
                    Console.WriteLine("Waiting for job to be completed...");
                    var toolBox = batchClient.OpenToolbox();
                    var stateMonitor = toolBox.CreateTaskStateMonitor();
                    var runningTasks = wiMgr.ListTasks(workItemName, defaultJobName);
                    stateMonitor.WaitAll(runningTasks, TaskState.Completed, TimeSpan.FromMinutes(10));
                    Console.WriteLine("All tasks completed!");

                    var tasksFinalResult = wiMgr.ListTasks(workItemName, defaultJobName);
                    foreach (var t in tasksFinalResult)
                    {
                        Console.WriteLine("- Task {0}: {1}, exit code {2}", t.Name, t.State,
                            t.ExecutionInformation.ExitCode);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press ENTER to quit!");
            Console.ReadLine();

            #endregion
        }

        private static string CreateSharedAccessSignature(CloudBlobContainer blobTesseractContainer,
            IListBlobItem resFile)
        {
            var resBlob = blobTesseractContainer.GetBlobReferenceFromServer(resFile.Uri.ToString());
            var sharedAccessPolicy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List,
                SharedAccessStartTime = DateTime.UtcNow,
                SharedAccessExpiryTime = DateTime.UtcNow.AddYears(1)
            };
            var sharedAccessSig = resBlob.GetSharedAccessSignature(sharedAccessPolicy);
            return sharedAccessSig;
        }
    }
}