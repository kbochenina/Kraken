using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Web.Configuration;
using MITP;
using NLog;
using Newtonsoft.Json.Linq;
using ResourceBaseService.ControllerFarmService;
using ResourceBaseService.UmService;
using PFX = System.Threading.Tasks;

namespace MITP
{
    public class ResourceBaseImpl:IResourceBase
    {
        public const string DEBUG_MODE = "UmDebugMode";

        private static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromSeconds(1);
        private static DateTime _lastUpdateTime = DateTime.Now - UPDATE_INTERVAL - UPDATE_INTERVAL;

        private static Resource[] _cachedResources = Resource.Load().ToArray();
        private static Resource[] _futureResources = null;

        //private static object _resourcesLock = new object();
        //private static object _dumpingLock   = new object();
        //private static bool _updateFinished = true;

        private static object _cacheLock = new object();

        private static object _updateLock = new object();
        private static bool _updating = false;

        private static HashSet<string> _updateKeys = new HashSet<string>();

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
 
        public static ResourceBaseImpl GetInstance()
        {
            return new ResourceBaseImpl();
        }

        private readonly IUmClient _umClient = new UmClientClient();
        private readonly IUmManager _umManager = new UmManagerClient();

        private const String ResourcePrefixUri = "clvr://platform/" + ResourceType + "/";
        private const String ResourceType = "resources";
        private const String ResourcePrefixUriNS = "clvr://platform/" + ResourceType;
        private const String ResourceRightPrefixUri = "clvr://platform/right/" + ResourceType + "/";

        private ResourceBaseImpl()
        {
            PFX.Task.Factory.StartNew(() =>
            {
                CheckForResourceDescriptionsUpdate();
            });
        }

        private static bool ItsTimeToUpdate()
        {
            return (!_updating && (DateTime.Now > _lastUpdateTime + UPDATE_INTERVAL || _lastUpdateTime > DateTime.Now));
        }

        private static void CheckForResourceDescriptionsUpdate()
        {
            if (ItsTimeToUpdate())
            {
                lock (_updateLock)
                {
                    if (ItsTimeToUpdate())
                    {
                        _updating = true;

                        var futureResources = Resource.Load().ToArray();

                        Resource[] currentResources = null;
                        lock (_cacheLock)
                        {
                            currentResources = _cachedResources;
                            _futureResources = futureResources;
                        }

                        //var unchangedResourceNames = futureResources.Intersect(currentResources).Select(r => r.ResourceName);
                        //var affectedControllerUrls = futureResources.Where(r => uncha

                        var affectedControllerUrls =
                            futureResources
                                .Select(r => r.Controller.Url).Distinct()
                            .Union(currentResources
                                .Select(r => r.Controller.Url).Distinct());

                        if (String.Join("\r\n", futureResources.OrderBy(r => r.ResourceName).Select(r => r.Json)) ==
                            String.Join("\r\n", currentResources.OrderBy(r => r.ResourceName).Select(r => r.Json)))
                            affectedControllerUrls = new string[0];

                        if (_updateKeys.Any())
                        {
                            // todo : Log.Warn
                            _updateKeys = new HashSet<string>();
                        }

                        foreach (string url in affectedControllerUrls) // _updateKeys not threadsafe => not in parallel 
                        {
                            string updateKey = url; // todo : generate dumping key

                            _updateKeys.Add(updateKey);

                            var farm = new ControllerFarmServiceClient();
                            farm.Endpoint.Address = new EndpointAddress(url);
                            farm.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(5);

                            try
                            {
                                farm.ReloadAllResources(updateKey);
                                farm.Close();
                            }
                            catch (Exception e)
                            {
                                farm.Abort();
                                // todo: log exception

                                _updateKeys.Remove(updateKey);
                            }
                        }

                        if (!_updateKeys.Any())
                        {
                            _updating = false;
                            _lastUpdateTime = DateTime.Now;
                        }
                    }
                }
            }
        }

        private static string[] GetAllResourceRights()
        {
            var rights = new string[5];
            rights[0] = ResourceRightPrefixUri + "READ";
            rights[1] = ResourceRightPrefixUri + "ADD_PACKAGE";
            rights[2] = ResourceRightPrefixUri + "WRITE_RES_CFG";
            rights[3] = ResourceRightPrefixUri + "WRITE_PACK_CFG";
            rights[4] = ResourceRightPrefixUri + "EXECUTE";
            return rights;
        }

        private bool CheckDebugMode()
        {
            var debugmode = WebConfigurationManager.AppSettings[DEBUG_MODE];

            return debugmode != null && debugmode == "true" ? true : false;
        }

        private ClavireUri[] GetUriesForRead(string userId)
        {
            var clvObj = _umClient.GetAccessible(userId, ResourcePrefixUriNS, ResourceRightPrefixUri + "READ");

            var uniqueList = new Dictionary<string, ClavireUri>();

            foreach (var clvOb in clvObj)
            {
                if (!uniqueList.ContainsKey(clvOb.Name))
                {
                    uniqueList.Add(clvOb.Name, clvOb);
                }
            }

            clvObj = new List<ClavireUri>(uniqueList.Values).ToArray();

            return clvObj;
        }

        private bool CheckWriteAccess(string userId, string resourceName)
        {
            if (CheckDebugMode())
            {
                return true;
            }

            return _umClient.CanAccess(userId, ResourcePrefixUri + resourceName,
                                ResourceRightPrefixUri + "WRITE_RES_CFG");
        }

        private bool CheckDeleteAccess(string userId, string resourceName)
        {
            if (CheckDebugMode())
            {
                return true;
            }

            return _umClient.CanAccess(userId, ResourcePrefixUri + resourceName,
                                                ResourceRightPrefixUri + "WRITE_PACK_CFG");
        }

        private Resource[] GetAllowed(Resource[] resources, string userId)
        {
            if (CheckDebugMode())
            {
                return resources;
            }

            var uris = GetUriesForRead(userId);
            var names = uris.Select(x => x.Name);
            return resources.Where(x => names.Contains(x.ResourceName)).ToArray();
        }

        public Resource[] GetAllResources(string userId)
        {
            lock (_cacheLock)
            {
                var resources = _cachedResources.ToArray();
                resources = GetAllowed(resources, userId);
                return resources;
            }
        }

        public string[] GetResourceNames(string userId)
        {
            var resources = GetAllResources(userId);
            string[] names = resources.Select(r => r.ResourceName).ToArray();
            return names;
        }

        public Resource GetResourceByName(string resourceName, string userId)
        {
            var resources = GetAllResources(userId);
            var res = resources.Single(r => r.ResourceName == resourceName);
            return res;
        }

        public string[] GetNodeNames(string resourceName, string userId)
        {
            var resources = GetAllResources(userId);
            var res = resources.Single(r => r.ResourceName == resourceName);
            var nodeNames = res.Nodes.Select(n => n.NodeName).ToArray();
            return nodeNames;
        }

        public ResourceNode GetResourceNodeByName(string resourceName, string nodeName, string userId)
        {
            var resource = GetResourceByName(resourceName, userId);
            var node = resource.Nodes.Single(n => n.NodeName == nodeName);
            return node;
        }

        public Resource[] GetResourcesForFarm( string userId, string farmId, string updateKey = null)
        {
            if (updateKey != null)      // todo : reset timeout
            {
                lock (_updateLock)
                {
                    _updateKeys.Remove(updateKey);

                    if (!_updateKeys.Any())
                    {
                        lock (_cacheLock)
                        {
                            _cachedResources = _futureResources;
                            _futureResources = null;
                        }

                        _updating = false;
                        _lastUpdateTime = DateTime.Now;
                    }
                }

                while (_updating)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(120)); // todo : random sleep
                }
            }

            var allResources = GetAllResources(userId);
            var resourcesForFarm = allResources.Where(r => r.Controller.FarmId.ToLowerInvariant() == farmId.ToLowerInvariant()).ToArray();

            return resourcesForFarm;
        }

        public void AddInstalledPackage(string resourceName, string nodeName, PackageOnNode pack, string userId)
        {
            lock (_updateLock)
            {
                //throw new NotImplementedException();
                var oldRes = GetResourceByName(resourceName, userId);

                if (oldRes == null)
                {
                    throw new InvalidDataException("There is no resources which can be modified. Check resource name, node name or access rights");
                }

                var json = oldRes.Json;
                var resource = JObject.Parse(json);
                var node = resource["Nodes"].FirstOrDefault(x => (string)x["NodeName"] == nodeName);

                if (node == null)
                {
                    throw new InvalidDataException(string.Format("Resource:{0} doesn't contain node:{1}",resourceName, nodeName));
                }

                var packages = (node["Packages"] != null) ? node["Packages"].ToObject<JArray>() : new JArray();

                var newPack = BuildPackageDescription(pack);
                packages.Add(newPack);

                node["Packages"] = packages;

                var newJson = resource.ToString();

                Resource.SaveResource(newJson, oldRes.ResourceName);
            }
        }

        public void RemoveInstalledPackage(string resourceName, string nodeName, string packName, string userId)
        {
            lock (_updateLock)
            {
                throw new NotImplementedException();
            }
        }

        private JObject BuildPackageDescription(PackageOnNode package)
        {
            //todo make proper fillness later
            var json = new JObject();
            json.Add("Name", package.Name);
            json.Add("Version", package.Version);
            json.Add("AppPath", package.AppPath);
            return json;
        }

        private JObject BuildNodeDescription(ResourceNode node)
        {
            //todo make proper fillness later
            var json = new JObject();
            json.Add("NodeName", node.NodeName);
            json.Add("NodeAddress", node.NodeAddress);

            //Services section
            var jsonServices = new JObject();
            json.Add("Services", jsonServices);
            jsonServices.Add("ExecutionUrl", node.Services.ExecutionUrl);

            //Packages section
            if (node.Packages != null && node.Packages.Count > 0)
            {
                var array = node.Packages.Select(x => BuildPackageDescription(x)).ToArray<object>();
                var jsonPackages = new JArray(array);
                json.Add("Packages", jsonPackages);
            }
            //CoresCount Section
            if (node.CoresCount != 0)
            {
                json.Add("CoresCount", node.CoresCount);   
            }
            //Credentials section 
            if (node.Credentials != null)
            {
                var jsonCredentials = new JObject();

                if (node.Credentials.Username != null)
                {
                    jsonCredentials.Add("Username",node.Credentials.Username);
                }
                if (node.Credentials.Password != null)
                {
                    jsonCredentials.Add("Password", node.Credentials.Password);
                }
                if (node.Credentials.CertFile != null)
                {
                    jsonCredentials.Add("CertFile", node.Credentials.CertFile);
                }

                if (jsonCredentials.Count > 0)
                {
                    json.Add("Credentials", jsonCredentials);
                }
            }
            //SupportedArchitectures section
            if (node.SupportedArchitectures != null && node.SupportedArchitectures.Count() > 0)
            {
                var array = node.SupportedArchitectures.ToArray<object>();
                var jsonSupportedArchitectures = new JArray(array);
                json.Add("SupportedArchitectures", jsonSupportedArchitectures);
            }
            //TasksSubmissionLimit section
            if (node.TasksSubmissionLimit != 0)
            {
                json.Add("TasksSubmissionLimit", node.TasksSubmissionLimit);
            }
            //DataFolders section
            if (node.DataFolders != null)
            {
                var jsonDataFolders = new JObject();
                if (node.DataFolders.ExchangeUrlFromSystem != null)
                {
                    jsonDataFolders.Add("ExchangeUrlFromSystem", node.DataFolders.ExchangeUrlFromSystem);   
                }
                if (node.DataFolders.ExchangeUrlFromResource != null)
                {
                    jsonDataFolders.Add("ExchangeUrlFromResource", node.DataFolders.ExchangeUrlFromResource);
                }
                if (node.DataFolders.LocalFolder != null)
                {
                    jsonDataFolders.Add("LocalFolder", node.DataFolders.LocalFolder);
                }
                jsonDataFolders.Add("CopyLocal", node.DataFolders.CopyLocal);

                json.Add("DataFolders", jsonDataFolders);
            }
            //OtherSoftware section
            if (node.OtherSoftware != null && node.OtherSoftware.Count > 0)
            {
                var arr = node.OtherSoftware.ToArray<object>();
                var jsonOtherSoftware = new JArray(arr);
                json.Add("OtherSoftware", jsonOtherSoftware);
            }
            //HardwareParams section
            if (node.StaticHardwareParams != null && node.StaticHardwareParams.Count > 0)
            {
                var arr = node.StaticHardwareParams.Select(x =>
                    {
                        var obj = new JObject();
                        obj.Add("Key", x.Key);
                        obj.Add("Value", x.Value);
                        return obj;
                    }).ToArray<object>();

                var jsonHardwareParams = new JArray(arr);
                json.Add("HardwareParams", jsonHardwareParams);
            }
            
            return json;
        }

        public bool SaveResource(string resourceDesc, string resourceName, string userId)
        {
            Log.Info("Save operation started for file {0}", resourceName);
            var resources = Resource.Load();
            var resourceExisted = false;
            Log.Info("{0} resources already found", resources.Count());
            foreach (var res in resources)
            {
                if (res.ResourceName.Equals(resourceName))
                {
                    resourceExisted = true;
                }
            }

            Log.Info("Is resource existed {0}", resourceExisted);


            StreamWriter sw = null;
            try
            {
                if (resourceExisted && !CheckWriteAccess(userId,resourceName))
                {
                    Log.Warn(String.Format("User {0} has no right to modify file with ResourceName {1}.", userId,
                                           resourceName));
                    return false;
                }

                Log.Info("Try to save context to file {0}", "\\" + resourceName + ".js");

                Resource.SaveResource(resourceName + "=\r\n" + resourceDesc,resourceName);

                Log.Info("File {0} saved. Rights providing..", resourceName);

                _umClient.InitObject(userId, ResourcePrefixUri + resourceName, GetAllResourceRights());
                Log.Info("File {0} was succesfully added by user {1}", resourceName, userId);
                return true;
            }
            catch (IOException e)
            {
                Log.Error(e);
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Error(e);
                return false;
            }
        }

        public bool DeleteResource(string resourceName, string userId)
        {
            var resources = Resource.Load();

            foreach (var res in resources)
            {
                try
                {
                    if (res.ResourceName.Equals(resourceName))
                    {
                        if (CheckDeleteAccess(userId,resourceName))
                        {
                            Resource.DeleteResource(resourceName);
                            _umManager.DeleteObject(ResourcePrefixUri + resourceName);

                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Couldn't delete resource: " + resourceName);
                    throw new Exception("Couldn't delete resource: " + resourceName);
                }
            }

            return false;
        }

        public bool IsResourceAlreadyExisted(string resourceName, string userId)
        {
           return GetAllResources(userId).Any(x => x.ResourceName == resourceName);
        }

        public void AddNewNodeToResource(string resourceName, ResourceNode node, string userId)
        {
            //throw new NotImplementedException();
            lock (_updateLock)
            {
                var oldRes = GetResourceByName(resourceName, userId);

                if (oldRes == null)
                {
                    throw new InvalidDataException("There is no resources which can be modified. Check resource name, node name or access rights");
                }

                var oldNode = oldRes.Nodes.FirstOrDefault(x => x.NodeName == node.NodeName);
                if (oldNode != null)
                {
                    throw new InvalidDataException("There alredy exists node with NodeName:" + node.NodeName);
                }

                var json = oldRes.Json;
                var resource = JObject.Parse(json);
                var nodes = resource["Nodes"].ToObject<JArray>();

                var newNode = BuildNodeDescription(node);
                nodes.Add(newNode);

                resource["Nodes"] = nodes;

                var newJson = resource.ToString();

                Resource.SaveResource(newJson, oldRes.ResourceName);
            }
        }

        public void RemoveNodeFromResource(string resourceName, string nodeName, string userId)
        {
            lock (_updateLock)
            {
                //throw new NotImplementedException();
                var oldRes = GetResourceByName(resourceName, userId);

                if (oldRes == null)
                {
                    throw new InvalidDataException("There is no resources which can be modified. Check resource name, node name or access rights");
                }

                var oldNode = oldRes.Nodes.FirstOrDefault(x => x.NodeName == nodeName);
                if (oldNode == null)
                {
                    throw new InvalidDataException("There  doesn't exist node with NodeName:" + nodeName);
                }

                var json = oldRes.Json;
                var resource = JObject.Parse(json);
                var nodes = resource["Nodes"].ToObject<JArray>();

                var arr = nodes.Where(x => ((JObject)x)["NodeName"].ToString() != nodeName).ToArray<object>();
                resource["Nodes"] = new JArray(arr);

                var newJson = resource.ToString();

                Resource.SaveResource(newJson, oldRes.ResourceName);
            }
        }


    }
}
