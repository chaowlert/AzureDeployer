AzureDeployer
=============

This is to deploy files or Web Application to WebRole. This is useful for deploying NLog.config, asset files, or additional web applications.

####How to use
1. This library use [Azure Storage Extension](https://github.com/chaowlert/AzureStorageExtensions) to connect to blob.  You need to setup connection string and upload files to `modules` folder.
2. If blob path starts with `approot/`, it will be deployed to RoleEntry.  
If blob path starts with `sitesroot/{site_number}`, it will be deployed to specific web site.  
If blob path starts with `*/`, it will be deployed to all applications.  
If blob path is anything else, it will be deployed to local resources.
3. If blob file end with `.zip`, it will be unzipped, and deployed as folder. And if zip content contain `Global.asax`, it will create web application.
4. To deploy, call to `ModuleDeployer.Deploy`.
5. There is class `IisHelper` to do simple configuration.
6. Also `FileWatcher` class, if you would like to monitor files after deployed. It is good for configuration file.
