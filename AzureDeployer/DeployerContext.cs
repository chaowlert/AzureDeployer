using AzureStorageExtensions;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureDeployer
{
    public class DeployerContext : BaseCloudContext
    {
        public CloudBlobContainer Modules { get; set; }
    }
}
