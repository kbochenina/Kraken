using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ServiceProxies.ResourceBaseService;

namespace PackageInstallationService
{
    static class Utility
    {
        public static string GetConfigSetting(string name)
        {
            string setting = ConfigurationManager.AppSettings[name];
            if (string.IsNullOrEmpty(setting) || string.IsNullOrWhiteSpace(setting))
            {
                throw new InvalidDataException(name + " config settings is either not present or incorrect");
            }

            return setting;
        }

        public static string DownloadFileFromSVN(string path)
        {
            var localPath = PackageFactoryImpl.TEMP_FILE_DIRECTORY + "\\" + Path.GetFileName(path);
            
            if (!Directory.Exists(PackageFactoryImpl.TEMP_FILE_DIRECTORY))
            {
                Directory.CreateDirectory(PackageFactoryImpl.TEMP_FILE_DIRECTORY);
            }

            int i = 1;
            var finalPath = localPath;
            while (File.Exists(finalPath))
            {
                finalPath = localPath  + "-" + i;
                ++i;
            }

            return Common.InstallationUtility.DownloadFileByPath(path, localPath, PackageFactoryImpl.SVNusername, PackageFactoryImpl.SVNpassword);
        }

        public static void RemoveFile(string path)
        {
            Common.Utility.ExceptionablePlaceWrapper(() =>
            {
                File.Delete(path);
            }, string.Format("Removing file by path:{0} failed", path),
            string.Format("Removing file by path:{0} succeseded", path), false);
        }

        public static ResourceBaseServiceClient GetResourceBaseServiceClient()
        {
            return new ResourceBaseServiceClient("BasicHttpBinding_IResourceBaseService");
        }
    }
}
