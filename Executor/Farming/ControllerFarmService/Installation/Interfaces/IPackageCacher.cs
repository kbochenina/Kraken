using System;

namespace ControllerFarmService.Installation.Interfaces
{
    interface IPackageCacher
    {
        string ReserveAddress(string key);
        void RegisterDownloaded(string key, int revisionNumber);
        void Remove(string key);
        void Get(string key, Action<PackageCacherResponse> response);
    }

    class PackageCacherResponse
    {
        public string Key { get; private set; }
        public string LocalAddress { get; private set; }
        public Exception Error { get; private set; }
        public int RevisionNumber { get; private set; }

        public PackageCacherResponse(string key, string localAddress, Exception error, int revisionNumber)
        {
            Key = key;
            LocalAddress = localAddress;
            Error = error;
            RevisionNumber = revisionNumber;
        }
    } 
}
