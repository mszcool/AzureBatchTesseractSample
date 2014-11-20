AzureBatchTesseractSample
=========================

This is an end-2-end sample which I've created as a first step to help developers getting started with Azure Batch. Azure Batch is a service introduced by Microsoft at TechEd EMEA 2014 in November 2014. Essentially it provides you with "Batch Processing-as-a-Service" in a highly scalable and available way.

In the sample I implement OCR recognition of PNG-files using Tesseract. Tesseract is an open source OCR recognition engine. To get more details on Tesseract, please look at http://code.google.com/p/tesseract-ocr/. In my sample I am just using one fully compiled downloaded version from the Tesseract home page without ANY modifications.

To run the sample, you need the following:

- A working Microsoft Azure Subscription; if you don't have one, sign-up for a free trial:
  http://azure.microsoft.com/en-us/pricing/free-trial/
  
- Your Subscription must be activated for the preview of Azure Batch(*)
  http://azure.microsoft.com/en-us/services/preview/
  
- Microsoft Azure PowerShell set-up correctly
  http://azure.microsoft.com/en-us/documentation/articles/install-configure-powershell/

- Visual Studio 2013 Community Edition (or higher)
  http://www.visualstudio.com/products/visual-studio-community-vs

- Git to clone this repository and execute the following command in a PowerShell Window
  .\DeploySolution.ps1 -regionName "North Europe" -storageAccountName yourbatchaccountname -azureBatchAccountName yourbatchaccountname
  
The deployment PowerShell script I included creates a storage account and an Azure Batch account in your Azure Subscription. It then downloads the access keys to those assets and updates the application configuration files. After that it builds the Visual Studio Solution and uploads the required assets as well as sample images for being processed to the storage account. After all of that you simply can open the Visual Studio solution "AzureBatchTesseract.sln" and start the project "BatchTesseractClient". 

For a full description of the sample and some of its code please refer to the corresponding article on my blog:

http://blog.mszcool.com/index.php/2014/11/azure-batch-highly-scalable-batch-processing-with-microsoft-azure-and-a-successor-to-geres2/

Feel free to get in touch with me via Twitter if you have any feedback, comments or questions.

http://twitter.com/mszcool


--- --- --- --- ---


(*) 2014-11-20 - Azure Batch is still in Preview, therefore you need to explicitly activate your Azure subscription