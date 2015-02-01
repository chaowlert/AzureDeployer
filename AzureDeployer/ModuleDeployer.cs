using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Common.Logging;
using Ionic.Zip;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace AzureDeployer
{
    public class ModuleDeployer
    {
        static readonly ILog _logger = LogManager.GetLogger(RoleEnvironment.CurrentRoleInstance.Id);
        readonly string _appRootPath;
        readonly string _localResourcePath;
        readonly string _systemPath;
        public ModuleDeployer(string localResource, string appRootPath = null, bool allowSystemPath = false)
        {
            _localResourcePath = RoleEnvironment.GetLocalResource(localResource).RootPath;
            if (!string.IsNullOrEmpty(appRootPath) && appRootPath[0] != '/')
                appRootPath = '/' + appRootPath;
            _appRootPath = appRootPath;
            if (allowSystemPath)
                _systemPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }

        readonly Dictionary<string, DateTime> _moduleVersions = new Dictionary<string, DateTime>();

        public void Deploy()
        {
            var context = new DeployerContext();
            var dict = context.Modules
                              .ListBlobs(null, true)
                              .OfType<CloudBlockBlob>()
                              .ToDictionary(b => b.Name, b => b.Properties.LastModified.GetValueOrDefault().DateTime);
            foreach (var kvp in dict)
            {
                DateTime version;
                if (_moduleVersions.TryGetValue(kvp.Key, out version) && version == kvp.Value)
                    continue;
                try
                {
                    var blob = context.Modules.GetBlockBlobReference(kvp.Key);
                    if (kvp.Key.StartsWith("*/"))
                    {
                        var path = kvp.Key.Substring(1);
                        deploy(blob, "approot/bin" + path);
                        foreach (var dir in Directory.GetDirectories(_rootPath + @"sitesroot\"))
                        {
                            var name = Path.GetFileName(dir);
                            deploy(blob, "sitesroot/" + name + path);
                        }
                        foreach (var dir in Directory.GetDirectories(_localResourcePath))
                        {
                            var name = Path.GetFileName(dir);
                            deploy(blob, name + path);
                        }
                    }
                    else
                        deploy(blob, kvp.Key);
                }
                catch (Exception ex)
                {
                    _logger.Error("error deploying " + kvp.Key, ex);
                }
                _moduleVersions[kvp.Key] = kvp.Value;
            }
        }
        static readonly string _rootPath = Path.GetPathRoot(Assembly.GetExecutingAssembly().Location);

        void deploy(CloudBlockBlob blob, string name)
        {
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                deployZip(blob, name);
            else
                deploySingleFile(blob, name);
        }

        string getRootPath(string path)
        {
            if (path.StartsWith("approot/") || path.StartsWith("sitesroot/"))
                return _rootPath;
            else if (_systemPath != null &&
                (path.StartsWith("SysWOW64/") || path.StartsWith("System32/")))
                return _systemPath;
            else
                return _localResourcePath;
        }
        static bool isWebPath(string path)
        {
            return File.Exists(Path.Combine(path, "global.asax"));
        }
        void deployZip(CloudBlockBlob blob, string name)
        {
            //set directory
            var app = name.Substring(0, name.Length - 4);
            var rootPath = getRootPath(app + '/');
            var appPath = Path.Combine(rootPath, app.Replace('/', '\\'));
            var path = Path.GetDirectoryName(appPath);
            _logger.Info("deploying " + name + " to " + path);
            Directory.CreateDirectory(appPath);

            //deploy files
            using (var mem = new MemoryStream())
            {
                blob.DownloadToStream(mem);
                mem.Position = 0;
                using (var zip = ZipFile.Read(mem))
                {
                    zip.ExtractAll(path, ExtractExistingFileAction.OverwriteSilently);
                }
            }
            
            //deploy to approot, siteroot
            if (rootPath == _rootPath)
                return;
            if (!isWebPath(appPath))
                return;

            var configFile = Path.Combine(appPath, "deploy.json");
            IisConfig config = null;
            if (File.Exists(configFile))
            {
                var configJson = File.ReadAllText(configFile);
                config = JsonConvert.DeserializeObject<IisConfig>(configJson);
            }

            var appName = Path.GetFileName(appPath);
            IisHelper.CreateApplication(appName, appPath, _appRootPath + '/' + app, config);

            var wildcards = blob.Container
                                .ListBlobs("*", true)
                                .OfType<CloudBlockBlob>()
                                .Select(item => item.Name);
            foreach (var wildcard in wildcards)
            {
                var wBlob = blob.Container.GetBlockBlobReference(wildcard);
                var wPath = wildcard.Substring(1);
                deploy(wBlob, app + wPath);
            }
        }

        void deploySingleFile(CloudBlockBlob blob, string name)
        {
            var rootPath = getRootPath(name);            
            var filepath = Path.Combine(rootPath, name.Replace('/', '\\'));
            var dir = Path.GetDirectoryName(filepath);
            _logger.Info("deploying " + name + " to " + filepath);
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(dir);

            //deploy files
            using (var file = File.Open(filepath, FileMode.Create))
            {
                blob.DownloadToStream(file);
            }
        }
    }
}
