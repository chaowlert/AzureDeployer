AzureDeployer
=============

This is to deploy files or Web Application to WebRole. This is useful for deploying NLog.config, asset files, or additional web applications.

####How to use
1. This library use [Azure Storage Extension](https://github.com/chaowlert/AzureStorageExtensions) to connect to blob.  You need to setup connection string and upload files to `modules` folder.
2. If blob path starts with `approot/`, it will be deployed to RoleEntry.  
If blob path starts with `sitesroot/{site_number}`, it will be deployed to specific web site.  
If blob path starts with `SysWOW64/` or `System32/`, it will be deployed to respective system path (this might be used for deploying unmanaged dll). 
If blob path starts with `*/`, it will be deployed to all applications.  
If blob path is anything else, it will be deployed to local resources.
3. If blob file end with `.zip`, it will be unzipped, and deployed as folder. And if zip content contain `Global.asax`, it will create web application.
4. To deploy, call `ModuleDeployer.Deploy`.
5. There is class `IisHelper` to do simple configuration.

####Example
In `web.config`, add following to connectionStrings
```
<connectionStrings>
  <add name="DeployerContext" connectionString="DefaultEndpointsProtocol=https;AccountName={name};AccountKey={key}" />
</connectionStrings>
```

In `ServiceDefinition.csdef` add local storage and elevate execution context.
```
<?xml version="1.0" encoding="utf-8"?>
<ServiceDefinition name="MyWeb.Cloud" xmlns="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" schemaVersion="2015-04.2.6">
  <WebRole name="MyWeb" vmsize="Small">
    <Runtime executionContext="elevated" />
    <LocalStorage cleanOnRoleRecycle="false" name="modules" sizeInMB="102400" />
    ...
```

In `WebRole.cs` you might add following in `OnStart` method.
```
IisHelper.SetMaxConnection(5000);                                   //this is to reject call after 5000 connections
IisHelper.SetDynamicCompression("application/json; charset=utf-8"); //this is to compress application/json
IisHelper.RemoveXPowerBy();                                         //this is to remove X-Power-By header

deployer = new ModuleDeployer("modules", allowSystemPath: true);    //this is to create Module Deployer instance
```

And in `Run` method, you might add.
```
while (true)
{
    try
    {
        deployer.Deploy();
    }
    catch (Exception ex)
    {
        logger.Error("Error deploying", ex);
    }
    Thread.Sleep(TimeSpan.FromMinutes(5));
}
```
This will watch `modules` folder in blob storage, and deploy new files, if new files are uploaded.
