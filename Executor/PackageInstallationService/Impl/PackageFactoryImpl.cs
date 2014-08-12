using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Configuration;
using PackageInstallationService.Impl;
using ServiceProxies.ControllerFarmService;
using ServiceProxies.ResourceBaseService;

namespace PackageInstallationService
{
    class PackageFactoryImpl
    {
        public const string PACKAGE_INSTALLED_CALLBACK_ADDRESS = "PackageInstalledCallbackAddress";
        public const string SVN_USER_NAME = "SVNusername";
        public const string SVN_PASSWORD = "SVNpassword";
        public const string TEMP_FILE_DIRECTORY = "TempFileDirectory";
        public const string REPOSITORY_ADDRESS = "RepositoryAddress";
        public const string REPOSITORY_STRUCTURE_FILE_ADDRESS = "RepositoryStructureFileAddress";

        private const string METADATA_FILE_NAME = "metadata.xml";

        private static PackageFactoryImpl _instance;

        public static string CallbackAddress { get; private set; }

        public static string SVNusername { get; private set; }

        public static string SVNpassword { get; private set; }

        public static string TempFileDirectory { get; private set; }

        public static string RepositoryAddress { get; private set; }

        public static string RepositoryStructureFileAddress { get; private set; }

        static PackageFactoryImpl()
        {
            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                CallbackAddress = Utility.GetConfigSetting(PACKAGE_INSTALLED_CALLBACK_ADDRESS);

                SVNusername = Utility.GetConfigSetting(SVN_USER_NAME);

                SVNpassword = Utility.GetConfigSetting(SVN_PASSWORD);

                TempFileDirectory = Utility.GetConfigSetting(TEMP_FILE_DIRECTORY);

                RepositoryAddress = Utility.GetConfigSetting(REPOSITORY_ADDRESS);

                RepositoryStructureFileAddress = Utility.GetConfigSetting(REPOSITORY_STRUCTURE_FILE_ADDRESS);
            }, "static ctor PackageFactoryImpl failed", "static ctor PackageFactoryImpl succeded");
            
        }

        private long operationCounter;

        private object _lock = new object();

        public static PackageFactoryImpl GetInstance()
        {
            return _instance ?? (_instance = new PackageFactoryImpl());
        }

        public static InstallationServiceClient GetInstallationServiceClient(string url)
        {
            return new InstallationServiceClient("ControllerFarm_Installator", url);
        }

        public IPackageStorageDriver GetPackageStorageDriver()
        {
            var driver = new SVNStorageDriver(RepositoryAddress, RepositoryStructureFileAddress, 
                                                new XmlMetadataProcessor(), new SVNStructureFileProcessor(METADATA_FILE_NAME), 
                                                METADATA_FILE_NAME);
            return driver;
        }

        public IToFarmInstaller GetToFarmInstaller()
        {
            var installer = new ToFarmInstallerImpl();
            return installer;
        }

        public string GetUniqueId()
        {
            long id = 0;
            lock (_lock)
            {
               id = ++operationCounter;
            }

            return id.ToString();
        }
        
    }
}
