using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using ControllerFarmService.Installation.Impl;
using ControllerFarmService.Installation.Interfaces;
using ControllerFarmService.ResourceBaseService;
using MITP;
using ServiceProxies.PackageInstallationService;

namespace ControllerFarmService.Installation
{
    class InstallationFactory
    {
        public const string USERNAME_FOR_PACKAGE_STORAGE = "UsernameForPackageStorage";
        public const string PASSWORD_FOR_PACKAGE_STORAGE = "PasswordForPackageStorage";
        public const string TEMP_FILE_DIRECTORY = "TempFileDirectory";
        public const string TEMP_FILE_DIRECTORY_CLEAR_INTERVAL = "TempFileDirectoryClearInterval";
        public const string TEMP_FILE_LIFE_INTERVAL = "TempFileLifeInterval";

        private static InstallationFactory _instance;

        private static string userNameForPackageStorage;
        private static string passwordForPackageStorage;

        public static string TempFileDirectory { get; private set; }
        public static int TempFileDirectoryClearInterval { get; private set; }
        public static int TempFileLifeInterval { get; private set; }

        static InstallationFactory()
        {
            userNameForPackageStorage = ConfigurationManager.AppSettings[USERNAME_FOR_PACKAGE_STORAGE];
            passwordForPackageStorage = ConfigurationManager.AppSettings[PASSWORD_FOR_PACKAGE_STORAGE];
            TempFileDirectory = ConfigurationManager.AppSettings[TEMP_FILE_DIRECTORY] ?? Directory.GetCurrentDirectory();

            int result;
            TempFileDirectoryClearInterval = (int.TryParse(ConfigurationManager.AppSettings[TEMP_FILE_DIRECTORY_CLEAR_INTERVAL],out result))? result: 30*60*1000;

            int lifeInterval;
            TempFileLifeInterval = (int.TryParse(ConfigurationManager.AppSettings[TEMP_FILE_LIFE_INTERVAL], out lifeInterval)) ? lifeInterval : 25 * 60 * 1000;
        }

        public static InstallationFactory GetInstance()
        {
            return _instance ?? (_instance = new InstallationFactory());
        }

        private IPackageObtainer packageObtainer;

        public IPackageObtainer GetPackageObtainer()
        {
            var cacher = new PackageCacherImpl(TempFileDirectoryClearInterval, TempFileLifeInterval, TempFileDirectory);
            cacher.Start();
            return packageObtainer ?? (packageObtainer = new PackageObtainerImpl(userNameForPackageStorage, 
                                                                                 passwordForPackageStorage, 
                                                                                 cacher));
        }

        public IInstallationManager GetInstallationManager()
        {
            return new InstallationManager(GetPackageObtainer());
        }
    }
}
