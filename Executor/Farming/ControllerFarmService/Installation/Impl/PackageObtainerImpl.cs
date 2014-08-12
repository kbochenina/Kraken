using System;
using ControllerFarmService.Installation.Interfaces;

namespace ControllerFarmService.Installation.Impl
{
    class PackageObtainerImpl : IPackageObtainer
    {
        private string user;
        private string password;
        private IPackageCacher cacher;

        public PackageObtainerImpl(string user, string password, IPackageCacher cacher)
        {
            this.user = user;
            this.password = password;
            this.cacher = cacher;
        }

        public void ObtainPackage(string address, Action<string, Exception> packageObtainedCallback)
        {
            int revNumber = Common.InstallationUtility.GetCurrentRevisionNumber(address, this.user, this.password);

            cacher.Get(address, response =>
            {
                GetInfoForLogging(response, revNumber);

                if (response.LocalAddress == null || revNumber == -1 || response.RevisionNumber != revNumber)
                {
                    cacher.Remove(address);
                    DownloadPackage(address, revNumber,packageObtainedCallback);
                    return;
                }

                packageObtainedCallback(response.LocalAddress, null);
            });
        }

        private void DownloadPackage(string address, int revNumber,Action<string, Exception> packageObtainedCallback)
        {
            var localAddress = cacher.ReserveAddress(address);
            try
            {
                Common.InstallationUtility.DownloadFileByPathAsync(address, localAddress, this.user, this.password, (ex) =>
                {
                    if (ex == null)
                    {
                        cacher.RegisterDownloaded(address, revNumber);
                    }
                    else
                    {
                        cacher.Remove(localAddress);   
                    }
                    packageObtainedCallback(localAddress, ex);
                });
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(string.Format("Start of Downloading package failed, Address: {0}", address),ex);
                cacher.Remove(localAddress);
                packageObtainedCallback(null, ex);
            }

            Common.Utility.LogDebug(string.Format("Start of downloading package\r\n Address: {0}", address));
        }

        private void GetInfoForLogging(PackageCacherResponse response ,int revNumber)
        {
            Common.Utility.LogDebug(
                string.Format("Info was got from Package Cacher.\r\n LocalAddress:{0}\r\n RevisionNumber: {1}\r\n RevNumber from server: {2}\r\n", 
                response.LocalAddress, response.RevisionNumber, revNumber));
        }
    }
}
