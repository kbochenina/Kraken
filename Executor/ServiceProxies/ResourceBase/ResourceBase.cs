using System;
using System.Collections.Generic;
using System.Linq;
using MITP;
using ServiceProxies;
using PFX = System.Threading.Tasks;

namespace ServiceProxies.ResourceBaseService
{
    public abstract class ResourceBase
    {
        public static Resource[] GetAllResources(bool updateThem = false)
        {
            var resourceLoadTimer = System.Diagnostics.Stopwatch.StartNew();

            var resourceBase = Discovery.GetResourceBase();
            Resource[] resources = new Resource[0];

            try
            {
                resources = resourceBase.GetAllResources();

                if (updateThem)
                {
                    //foreach (var resource in resources)
                    PFX.Parallel.ForEach(resources, (resource) =>
                    {
                        //var updateStarted = DateTime.Now;
                        var controller = Discovery.GetControllerFarm(resource);

                        try
                        {
                            var nodesStateInfo = controller.GetNodesState(resource.ResourceName);
                            foreach (var nodeState in nodesStateInfo)
                            {
                                var node = resource.Nodes.Single(n => n.NodeName == nodeState.NodeName);
                                node.CoresAvailable = nodeState.CoresAvailable;
                                node.SubmissionsAvailable = (int) Math.Max(0, nodeState.TasksSubmissionLimit - nodeState.TasksSubmitted);
                            }

                            string[] availableNodeNames = nodesStateInfo.Where(n => n.State == ControllerFarmService.NodeState.Available).Select(n => n.NodeName).ToArray();
                            //resource.Nodes = resource.Nodes.Where(n => availableNodeNames.Contains(n.NodeName)).ToArray();

                            controller.Close();
                        }
                        catch (Exception e)
                        {
                            controller.Abort();

                            Log.Warn(String.Format("Exception while updating resource '{0}' state: {1}", resource.ResourceName, e));

                            resource.Nodes = new ResourceNode[0]; // should not count any nodes as 'will be available sometime' 

                            /*
                            foreach (var node in resource.Nodes)
                            {
                                node.CoresAvailable = 0;
                                node.SubmissionsAvailable = 0;
                            }
                            */
                        }
                        finally
                        {
                            //var updateFinished = DateTime.Now;
                            //resourceUpdateTime += updateFinished - updateStarted;
                        }
                    });

                    resourceLoadTimer.Stop();
                    Log.Info(String.Format(
                        "Resources loaded and updated in {0} seconds",
                        resourceLoadTimer.Elapsed.TotalSeconds
                    ));
                }

                resourceBase.Close();
            }
            catch (Exception e)
            {
                resourceBase.Abort();

                if (resourceLoadTimer.IsRunning)
                    resourceLoadTimer.Stop();

                Log.Error("Exception on getting all resources from ResourceBase: " + e.ToString());

                throw;
            }

            if (updateThem)
            {
                resources = resources.Where(r => r.Nodes.Any()).ToArray();
                Log.Info("Available resources: " + String.Join(", ", resources.Select(r => r.ResourceName + "(" + String.Join(", ", r.Nodes.Select(n => n.NodeName)) + ")")));
            }

            return resources;
        }

        public static Resource GetResourceByName(string resourceName)
        {
            var resourceBase = Discovery.GetResourceBase();
            Resource resource;

            try
            {
                resource = resourceBase.GetResourceByName(resourceName);
                resourceBase.Close();
            }
            catch (Exception e)
            {
                resourceBase.Abort();
                Log.Error(String.Format("Exception on getting resource {0} from ResourceBase: {1}", resourceName, e));

                throw;
            }

            return resource;
        }
    }
}