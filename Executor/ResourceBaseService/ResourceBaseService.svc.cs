using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using NLog;
using Newtonsoft.Json.Linq;
using PFX = System.Threading.Tasks;

using ResourceBaseService.ControllerFarmService;

namespace MITP
{
    //[ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]    
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]    
    // PerCall to call construstor before every method
    public class ResourceBaseService : IResourceBaseService, IResManagementService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static string GetUserIdFromHeader()
        {
            MessageHeaders messageHeadersElement = OperationContext.Current.IncomingMessageHeaders;
            int i = messageHeadersElement.FindHeader("UserId", "");
            if (i <= -1)
            {
                Log.Error("User not found");
                return null;
            }

            return OperationContext.Current.IncomingMessageHeaders.GetHeader<string>("UserId", "");
        }

        private IResourceBase _resourceBase = ResourceBaseImpl.GetInstance();

        public Resource[] GetAllResources()
        {
            return _resourceBase.GetAllResources(GetUserIdFromHeader());
        }

        public string[] GetResourceNames()
        {
            return _resourceBase.GetResourceNames(GetUserIdFromHeader());
        }

        public Resource GetResourceByName(string resourceName)
        {
            return _resourceBase.GetResourceByName(resourceName, GetUserIdFromHeader());
        }

        public string[] GetNodeNames(string resourceName)
        {
            return _resourceBase.GetNodeNames(resourceName, GetUserIdFromHeader());
        }

        public ResourceNode GetResourceNodeByName(string resourceName, string nodeName)
        {
            return _resourceBase.GetResourceNodeByName(resourceName, nodeName, GetUserIdFromHeader());
        }

        public Resource[] GetResourcesForFarm(string farmId = null, string dumpingKey = null)
        {
            return _resourceBase.GetResourcesForFarm(GetUserIdFromHeader(), farmId, dumpingKey);
        }

        public void AddInstalledPackage(string resourceName, string nodeName, PackageOnNode pack)
        {
            _resourceBase.AddInstalledPackage(resourceName, nodeName, pack, GetUserIdFromHeader());
        }

        public void RemoveInstalledPackage(string resourceName, string nodeName, string packName)
        {
            _resourceBase.RemoveInstalledPackage(resourceName, nodeName, packName, GetUserIdFromHeader());
        }

        public void AddNewNodeToResource(string resourceName, ResourceNode node)
        {
           _resourceBase.AddNewNodeToResource(resourceName, node, GetUserIdFromHeader());
        }

        public void RemoveNodeFromResource(string resourceName, string nodeName)
        {
            _resourceBase.RemoveNodeFromResource(resourceName, nodeName, GetUserIdFromHeader());
        }

        public List<string> GetResourceList()
        {
            return _resourceBase.GetAllResources(GetUserIdFromHeader()).Select(x => x.Json).ToList();
        }

        public string GetResource(string resourceId)
        {
            return _resourceBase.GetResourceByName(resourceId, GetUserIdFromHeader()).Json;
        }

        public bool SaveResource(string resourceDesc, string resourceName)
        {
            return _resourceBase.SaveResource(resourceDesc, resourceName, GetUserIdFromHeader());
        }

        public bool IsResourceAlreadyExisted(string resourceName)
        {
            return _resourceBase.IsResourceAlreadyExisted(resourceName, GetUserIdFromHeader());
        }

        public bool DeleteResource(string resourceName)
        {
           return _resourceBase.DeleteResource(resourceName, GetUserIdFromHeader());
        }

        public List<string> GetPResourceList(string userid)
        {
            return _resourceBase.GetAllResources(userid).Select(x => x.Json).ToList();
        }

        public string GetPResource(string resourceId, string userId)
        {
            return _resourceBase.GetResourceByName(resourceId, userId).Json;
        }

        public bool SavePResource(string resourceDesc, string resourceName, string userId)
        {
            return _resourceBase.SaveResource(resourceDesc, resourceName, userId);
        }

        public bool DeletePResource(string resourceName, string userId)
        {
            return _resourceBase.DeleteResource(resourceName, userId);
        }

        public bool IsPResourceAlreadyExisted(string resourceName, string userId)
        {
            return _resourceBase.IsResourceAlreadyExisted(resourceName, userId);
        }
    }
}
