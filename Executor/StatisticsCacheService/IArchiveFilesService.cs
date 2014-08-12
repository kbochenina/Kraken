using System;
using System.IO;
using System.ServiceModel;
using CommonDataTypes;

namespace StatisticsCacheService
{
    [ServiceContract]
    public interface IArchiveFilesService
    {
        [OperationContract]
        ArchiveFilesTicket GetAllResourcesInfoBetween(DateTime startDate, DateTime endDate);

        [OperationContract]
        ArchiveFilesTicket GetAllTasksInfoBetween(DateTime startDate, DateTime endDate);

        [OperationContract]
        Stream GetArchiveFile(string fileName);
    }
}
