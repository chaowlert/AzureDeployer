using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using Microsoft.Web.Administration;

namespace AzureDeployer
{
    public static class IisHelper
    {
        public static void SetMaxConnection(int maxConnection, int siteIndex = 0)
        {
            using (var mgr = new ServerManager())
            {
                mgr.Sites[siteIndex].Limits.MaxConnections = maxConnection;
                mgr.CommitChanges();
            }
        }

        public static void RemoveXPowerBy()
        {
            using (var serverManager = new ServerManager())
            {
                var config = serverManager.GetApplicationHostConfiguration();
                var httpProtocolSection = config.GetSection("system.webServer/httpProtocol");
                var customHeaderCollection = httpProtocolSection.GetCollection("customHeaders");

                var xPowerByHeader = customHeaderCollection.FirstOrDefault(h => h.ElementTagName == "add" && (string)h.GetAttributeValue("name") == "X-Powered-By");
                if (xPowerByHeader != null)
                {
                    customHeaderCollection.Remove(xPowerByHeader);
                    serverManager.CommitChanges();
                }
            }
        }

        public static void SetDynamicCompression(params string[] mediaTypes)
        {
            using (var serverManager = new ServerManager())
            {
                var config = serverManager.GetApplicationHostConfiguration();
                var httpCompressionSection = config.GetSection("system.webServer/httpCompression");
                var dynamicTypesCollection = httpCompressionSection.GetCollection("dynamicTypes");

                foreach (var mediaType in mediaTypes)
                {
                    if (dynamicTypesCollection.Any(x => (string)x.GetAttributeValue("mimeType") == mediaType))
                        continue;
                    var addElement = dynamicTypesCollection.CreateElement("add");
                    addElement["mimeType"] = mediaType;
                    addElement["enabled"] = true;
                    dynamicTypesCollection.AddAt(0, addElement);
                }

                serverManager.CommitChanges();
            }
        }

        public static void CreateApplication(string appName, string appPath, string virtualAppPath, IisConfig config = null, int siteIndex = 0)
        {
            //create app
            using (var mgr = new ServerManager())
            {
                var iisAppPool = mgr.ApplicationPools[appName];
                var hasChange = false;
                if (iisAppPool == null)
                {
                    //directory permission
                    var dirInfo = new DirectoryInfo(appPath);
                    var security = dirInfo.GetAccessControl(AccessControlSections.Access);
                    const FileSystemRights right = FileSystemRights.FullControl ^ FileSystemRights.ReadAndExecute;
                    var acl = new FileSystemAccessRule("NETWORK SERVICE", right, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Deny);
                    security.AddAccessRule(acl);
                    dirInfo.SetAccessControl(security);

                    //apppool permission
                    iisAppPool = mgr.ApplicationPools.Add(appName);
                    iisAppPool.ManagedRuntimeVersion = "v4.0";
                    iisAppPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                    hasChange = true;
                }
                config = config ?? IisConfig.Default;
                var startMode = iisAppPool["startMode"] as string;
                var target = config.AutoStart ? "AlwaysRunning" : "OnDemand";
                if (startMode != target)
                {
                    iisAppPool["startMode"] = target;
                    hasChange = true;
                }
                var target2 = TimeSpan.FromMinutes(config.IdleTimeout);
                if (iisAppPool.ProcessModel.IdleTimeout != target2)
                {
                    iisAppPool.ProcessModel.IdleTimeout = target2;
                    hasChange = true;
                }
                if (iisAppPool.Enable32BitAppOnWin64 != config.Enable32Bit)
                {
                    iisAppPool.Enable32BitAppOnWin64 = config.Enable32Bit;
                    hasChange = true;
                }
                if (hasChange)
                    mgr.CommitChanges();

                hasChange = false;
                var iisApp = mgr.Sites[siteIndex].Applications[virtualAppPath];
                if (iisApp == null)
                {
                    iisApp = mgr.Sites[siteIndex].Applications.Add(virtualAppPath, appPath);
                    iisApp.ApplicationPoolName = appName;
                    iisApp.EnabledProtocols = "https,http";
                    hasChange = true;
                }
                var preloadEnabled = Equals(iisApp["preloadEnabled"], true);
                if (preloadEnabled != config.AutoStart)
                {
                    iisApp["preloadEnabled"] = config.AutoStart;
                    hasChange = true;
                }
                if (hasChange)
                    mgr.CommitChanges();
            }
        }
    }
}
