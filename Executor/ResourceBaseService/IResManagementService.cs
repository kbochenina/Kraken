using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace MITP
{
    [ServiceContract]
    public interface IResManagementService
    {

        [OperationContract]
        List<String> GetResourceList();

        [OperationContract]
        String GetResource(String resourceId);

        [OperationContract]
        bool SaveResource(string resourceDesc, string resourceName);

        [OperationContract]
        bool IsResourceAlreadyExisted(string resourceName);

        [OperationContract]
        bool DeleteResource(string resourceName);

        [OperationContract]
        List<String> GetPResourceList(string userid);

        [OperationContract]
        String GetPResource(String resourceId, String userId);

        [OperationContract]
        bool SavePResource(string resourceDesc, string resourceName, String userId);

        [OperationContract]
        bool DeletePResource(string resourceName, String userId);

        [OperationContract]
        bool IsPResourceAlreadyExisted(string resourceName, String userId);

    }
}
