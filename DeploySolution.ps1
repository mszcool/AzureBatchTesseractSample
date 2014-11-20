Param(
    #
    # The region you want to deploy your solution in - all regions, e.g. "West Europe" or "North Europe" are valid
    #
    [Parameter(Mandatory=$true)]
    [string]
    $regionName,

    #
    # The name of the storage account to use for this demo
    #
    [Parameter(Mandatory=$true)]
    [string]
    $storageAccountName,

    #
    # The name of the Azure Batch account to use this demo with
    #
    [Parameter(Mandatory=$true)]
    [string]
    $azureBatchAccountName
)


#
# Print some help in terms of what the script is doing
#
Write-Host ""
Write-Host "This script will setup the Batch Tesseract Client to be ready to go!"
Write-Host "(1) Create a storage account '" $storageAccountName "'"
Write-Host "(2) Create a resource group for the sample called '" $batchSampleResourceGroupName "'"
Write-Host "(3) Create an Azure Batch account '" $azureBatchAccountName "' (your subscription must be enabled for Azure Batch)"
Write-Host "(4) Update Visual Studio xyz.config configurations with your account data"
Write-Host "(5) Builds the solution using msbuild (make sure the path in the script is correct)"
Write-Host "(6) Upload Sample Data and binaries for batch TVMs to the storage account"
Write-Host ""


#
# General Constant stuff
#
$batchSampleResourceGroupName = "AzureBatchTesseractSample"
$storageContainerNameTesseract = "tesseract"
$storageContainerNameSampleData = "ocr-source"
$msBuildPath = "C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe"
$workingDir = (Get-Location).Path

#
# Test if the current working directory is the solution directory
#
if(!(Test-Path -Path ([System.String]::Concat($workingDir, "\AzureBatchTesseract.sln"))))
{
    throw "Solution AzureBatchTesseract.sln not found, make sure you're working in the directory of the sample solution!!"
}

#
# Test if msbuild is present
#
if(!(Test-Path -Path $msBuildPath))
{
    throw "Cannot find msbuild at path '" + $msBuildPath + "'! Please correct the msBuildPath variable in the script to match your msbuild-path!"
}


#
# Testing if the Azure Powershell CmdLets are installed (either resource manager or service management)
#
$azureModule = Get-Module -ListAvailable Azure*
if($azureModule -eq $null) {
    throw 'Need to install and setup the Azure Powershell CmdLets before executing this script!'
}


#
# Check the default subscription
#
$subscr = Get-AzureSubscription -Current
if($subscr -eq $null) {
    throw "Please call Select-AzureSubscription to select a current Azure Subscription"
}


#
# Helper functions for this script
#
function UploadDirectory($baseName, $directoryName, $storageContext, $containerName) 
{
    $filesInDir = Get-ChildItem "$directoryName" -File
    ForEach($file in $filesInDir)
    {
        $fullFileName = $file.FullName
        $resultingBlobName = [System.String]::Concat($baseName, $file.Name)
                Set-AzureStorageBlobContent -File "$fullFileName" `                                    -Container $containerName `                                    -Blob "$resultingBlobName" `                                    -Context $storageContext `
                                    -BlobType Block `
                                    -Force
    }

    $subDirs = Get-ChildItem "$directoryName" -Directory
    ForEach($dir in $subDirs) 
    {
        $dirFullName = $dir.FullName
        $newBaseName = [System.String]::Concat($baseName, $dir.Name, "/")
        UploadDirectory -baseName $newBaseName `                        -directoryName "$dirFullName" `                        -storageContext $storageContext `
                        -containerName $containerName
    }
}


#
# Create the storage account and get the access keys
#
Write-Host ""
Write-Host "(1) Creating an Azure Storage Account..." -ForegroundColor Yellow
Switch-AzureMode -Name AzureServiceManagement
$storageAccount = Get-AzureStorageAccount -StorageAccountName $storageAccountName -ErrorAction SilentlyContinue
if($storageAccount -eq $null) 
{
    Write-Host "- Storage account does not exist, yet - creating new one..."
    New-AzureStorageAccount -StorageAccountName $storageAccountName `                            -Label $storageAccountName `                            -Location $regionName 
    $storageAccount = Get-AzureStorageAccount -StorageAccountName $storageAccountName
    if($storageAccount -eq $null) 
    {
        throw "Unable to create storage account with name '" + $storageAccountName + "'!"
    }
} 
else 
{
    Write-Host "- Storage Account exists, already. Skipping creation!"
}
Write-Host "- Storage account created, getting access keys!"
$storageKeys = Get-AzureStorageKey -StorageAccountName $storageAccountName
if($storageKeys -eq $null) 
{
    throw "Unable to retrieve storage access keys for storage account '" + $storageAccountName + "'!"
}
$storagePrimaryKey = $storageKeys.Primary


#
# Switch to resource group manager mode to be able to create batch accounts and a resource group for the demo
#
Write-Host ""
Write-Host "(2) Switching to Azure Resource Manager Mode and creating resource group if needed!!" -ForegroundColor Yellow
Switch-AzureMode -Name AzureResourceManager
Write-Host "- Creating Resource Group '" $batchSampleResourceGroupName "'..."
$resGroup = Get-AzureResourceGroup -Name $batchSampleResourceGroupName
if($resGroup -eq $null) 
{
    Write-Host "- Resource group does not exist, yet... creating..."
    New-AzureResourceGroup -Name $batchSampleResourceGroupName -Location $regionName
    if($resGroup -eq $null) {
        throw "Unable to create and/or find the Azure Resource group '" + $batchSampleResourceGroupName + "'!!"
    }
}
else 
{
    Write-Host "- Resource group exists, already... skipping..."
}


#
# Creating azure atch account
#
Write-Host ""
Write-Host "(3) Creating Azure Batch Account if it does not exist..." -ForegroundColor Yellow
$azureBatchAccount = Get-AzureBatchAccount -AccountName $azureBatchAccountName -ResourceGroupName $batchSampleResourceGroupName -ErrorAction SilentlyContinue
if($azureBatchAccount -eq $null) 
{
    Write-Host "- Batch account " $azureBatchAccountName " does not exist, creating one..."
    New-AzureBatchAccount -AccountName $azureBatchAccountName `                          -ResourceGroupName $batchSampleResourceGroupName `                          -Location $regionName
    $azureBatchAccount = Get-AzureBatchAccount -AccountName $azureBatchAccountName -ResourceGroupName $batchSampleResourceGroupName
    if($azureBatchAccount -eq $null) 
    {
        throw "Creation of Batch account failed, unable to load batch account!"
    }
}
else 
{
    Write-Host "- Batch account exists, skipping creation..."
}
Write-Host "- Getting Batch Account Primary Key..."
$azureBatchAccountKeys = Get-AzureBatchAccountKeys -AccountName $azureBatchAccountName
$azureBatchPrimaryAccessKey = $azureBatchAccountKeys.PrimaryAccountKey


#
# Update app.config configuration files
#
Write-Host ""
Write-Host "(4) Updating app.config files from the projects before building them..." -ForegroundColor Yellow
$doc = New-Object System.Xml.XmlDocument

Write-Host "- Updating .\BatchTesseractClient\app.config"
$doc.Load($workingDir + "\BatchTesseractClient\app.config")
$batchAccountNameXmlAttr = $doc.SelectSingleNode("//add[@key='BatchAccountName']/@value")
$batchAccountNameXmlAttr.Value = $azureBatchAccountName
$batchAccountKeyXmlAttr = $doc.SelectSingleNode("//add[@key='BatchAccountKey']/@value")
$batchAccountKeyXmlAttr.Value = $azureBatchPrimaryAccessKey
$batchStorageAccountNameXmlAttr = $doc.SelectSingleNode("//add[@key='BatchDemoStorageAccount']/@value")
$batchStorageAccountNameXmlAttr.Value = $storageAccountName
$batchStorageAccountKeyXmlAttr = $doc.SelectSingleNode("//add[@key='BatchDemoStorageAccountKey']/@value")
$batchStorageAccountKeyXmlAttr.value = $storagePrimaryKey
$doc.Save($workingDir + "\BatchTesseractClient\app.config")

Write-Host "- Updating .\BatchTesseractWrapper\app.config"
$doc.Load($workingDir + "\BatchTesseractWrapper\app.config")
$batchStorageAccountNameXmlAttr = $doc.SelectSingleNode("//add[@key='BatchDemoStorageAccount']/@value")
$batchStorageAccountNameXmlAttr.Value = $storageAccountName
$batchStorageAccountKeyXmlAttr = $doc.SelectSingleNode("//add[@key='BatchDemoStorageAccountKey']/@value")
$batchStorageAccountKeyXmlAttr.Value = $storagePrimaryKey
$doc.Save($workingDir + "\BatchTesseractWrapper\app.config")

Write-Host "- Updated all config files successfully!"


#
# Build the current solution
# 
Write-Host ""
Write-Host "(5) Building the solution in the currnet directory incl. NuGet Package Restore..." -ForegroundColor Yellow
$buildProc = Start-Process -FilePath $msBuildPath -PassThru
$buildProc.WaitForExit()
if($buildProc.ExitCode -ne 0)
{
    throw  "Build failed with exit code " + $buildProc.ExitCode + ", stopping scrip"
}


#
# Uploading Sample Data to the Storage Accoung
#
Write-Host ""
Write-Host "(6) Switching back to service management and uploading sample data..." -for Yellow
Switch-AzureMode -Name AzureServiceManagement
$storageAccountContext = New-AzureStorageContext -StorageAccountName $storageAccountName `                                                 -StorageAccountKey $storagePrimaryKey `                                                 -Protocol https
$tesseractStorageContainer = Get-AzureStorageContainer -Name $storageContainerNameTesseract -Context $storageAccountContext -ErrorAction SilentlyContinue
if($tesseractStorageContainer -eq $null) 
{
    Write-Host "- Storage Container for tesseract files does not exist, creating..."
    New-AzureStorageContainer -Name $storageContainerNameTesseract -Context $storageAccountContext
    $tesseractStorageContainer = Get-AzureStorageContainer -Name $storageContainerNameTesseract -Context $storageAccountContext
    if($tesseractStorageContainer -eq $null)
    {
        throw "Failed creating tesseract storage container '" + $storageContainerNameTesseract + "'"
    }
}
Write-Host "- Uploading files from .\BatchTesseractWrapper\bin\Debug\*.*"
UploadDirectory -baseName '' `                -directoryName ([String]::Concat($workingDir, "\BatchTesseractWrapper\bin\Debug")) `                -storageContext $storageAccountContext `                -containerName $storageContainerNameTesseractWrite-Host "- Uploading sample data from .\SupportFiles"$sampleDataContainer = Get-AzureStorageContainer -Name $storageContainerNameSampleData -Context $storageAccountContext -ErrorAction SilentlyContinue
if($sampleDataContainer -eq $null) 
{
    Write-Host "- Storage Container for sample data files does not exist, creating..."
    New-AzureStorageContainer -Name $storageContainerNameSampleData -Context $storageAccountContext
    $sampleDataContainer = Get-AzureStorageContainer -Name $storageContainerNameSampleData -Context $storageAccountContext
    if($sampleDataContainer -eq $null)
    {
        throw "Failed creating tesseract storage container '" + $storageContainerNameSampleData + "'"
    }
}UploadDirectory -baseName '' `                -directoryName ([String]::Concat($workingDir, "\SupportFiles")) `                -storageContext $storageAccountContext `                -containerName $storageContainerNameSampleData