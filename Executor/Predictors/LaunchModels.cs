using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

// disable documentation-related warnings
#pragma warning disable 1591

namespace MITP
{
	public abstract class LaunchModel : IComparable
	{
		protected const long SEC_IN_TICKS = (long) 1e7;

        protected abstract bool Matches(TaskDescription task, Resource resource);
        protected abstract Estimation Estimate(TaskDescription task, Resource resource);

        public virtual Estimation EstimateIfMatches(TaskDescription task, Resource resource)
		{
            // todo : EstimateIfMatches => try/catch & nodes != null & nodes.Any() & nodes.First().HavePackage(task.Package)

			if (Matches(task, resource))
                return Estimate(task, resource);

			return null;
		}

		#region IComparable Members

		public virtual int CompareTo(object obj)
		{
			return String.Compare(this.ToString(), obj.ToString());
		}

		#endregion
	}

	public class L1 : LaunchModel
	{
        protected override bool Matches(TaskDescription task, Resource resource)
		{
			return false;
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
			// get all available nodes with all their cores
            int n = resource.Nodes.Sum(node => node.CoresAvailable);
            int[] cores = resource.Nodes.Select(node => node.CoresAvailable).ToArray();

			long sec = (long) 1e7;
			TimeSpan time = new TimeSpan((long) (240.0 * sec / (1.0 * n)));

            return new Estimation(task.TaskId, resource.ProviderName, resource.ResourceName, cores) { ExecutionTime = time };
		}
	}

	public class L2 : LaunchModel
	{
        protected override bool Matches(TaskDescription task, Resource resource)
        {
            /*
            if (nodes.First().ClusterName.ToLower() == "cluster_niinkt_1")
                return true;
            /**/

			return false;
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
			// get 1 core on 1 node, less time estimated

            int n = 0;
            int[] cores = Enumerable.Range(0, resource.Nodes.Count).ToArray();

            cores[0] = 1;

			long sec = (long) 1e7;
			TimeSpan time = new TimeSpan((long) (0.5 * n * 240.0 * sec / (1.0)));

            return new Estimation(task.TaskId, resource.ProviderName, resource.ResourceName, cores) { ExecutionTime = time };
        }
	}

	public class ForceCluster : LaunchModel
	{
        private const string REQUIRED_CLUSTER_NAME = "cluster_niinkt_1";

        protected override bool Matches(TaskDescription task, Resource resource)
        {
            /*
            if (nodes.First().ClusterName.ToLower() == REQUIRED_CLUSTER_NAME.ToLower())
                return true;
            /**/

            return false;
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
			return new Estimation(
                task.TaskId, 
                resource.ProviderName,
                resource.ResourceName,
				new int[1] { 1 }
            ) {
                ExecutionTime = new TimeSpan(0),
            };
		}
	}

	public class SempAndOrcaAccurate : LaunchModel
	{
        protected override bool Matches(TaskDescription task, Resource resource)
        {
			return true;
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
			if (task.Package.ToLower() == "semp" || task.Package.ToLower() == "orca")
			{
                var node = resource.Nodes.First();

                if (node.CoresAvailable > 0 && resource.ResourceName.ToLower() == "cluster_niinkt_1" && node.HasPackage(task.Package))
                    return new Estimation(
                        task.TaskId, 
                        resource.ProviderName,
                        resource.ResourceName,
                        new int[1] { 
							task.Package.ToLower() == "semp"? 1 : 4
						}
                    ) {
                        ExecutionTime = TimeSpan.FromMilliseconds(5)
                    };
			}

			return null;
		}
	}

	public class PlasmonWithGrdOnHP : LaunchModel
	{
        protected override bool Matches(TaskDescription task, Resource resource)
        {
			return false;

			/*
			if (task.Package == Packs.Plasmon && task.Params["SGRAPH2DFILEEXT"] != null &&
				task.Params["SGRAPH2DFILEEXT"].ToLower() == "grd")
				return true;

			return false;
			*/
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
            var node = resource.Nodes.First();

            if (resource.ResourceName.ToLower() == "cluster_niinkt_1" && node.HasPackage(task.Package))
                return new Estimation(
                    task.TaskId, 
                    resource.ProviderName,
                    resource.ResourceName,
                    new int[1] { 1 }
                )
                {
                    ExecutionTime = new TimeSpan(0)
                };

			return null;
		}
	}

	public class OrcaMrciAndTDDFTOnWindows : LaunchModel
	{
        protected override bool Matches(TaskDescription task, Resource resource)
        {
			if (task.Package.ToLower() == "orca" && task.Method.ToUpper() == "MCSCF+MR-CI")
				return true;

            if (task.Package.ToLower() == "orca" && task.Method.ToUpper() == "TDDFT")
				return true;

			return false;
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
            if (resource.Nodes.First().OtherSoftware.Contains("windows") && resource.Nodes.First().HasPackage("ORCA"))
                return new Estimation(
                    task.TaskId, 
                    resource.ProviderName,
                    resource.ResourceName,
                    new int[1] { 1 }
                ) {
                    //ExecutionTime = new TimeSpan(Math.Max(0, 500 - nodes.Length)*SEC_IN_TICKS),
                    ExecutionTime = new TimeSpan(0*SEC_IN_TICKS),
                };

			return null;
		}
	}

    public class ForceGrid : LaunchModel
    {
        protected override bool Matches(TaskDescription task, Resource resource)
        {
            return false;

            if (resource.ProviderName == CONST.Providers.GridNns && resource.Nodes.First().HasPackage(task.Package))
                return true;

            return false;
        }

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
            if (resource.ProviderName == CONST.Providers.GridNns && resource.Nodes.First().HasPackage(task.Package) &&
                resource.Nodes.Sum(n => n.CoresAvailable) > 0)
                return new Estimation(
                    task.TaskId,
                    resource.ProviderName,
                    resource.ResourceName,
                    Enumerable.Range(0, resource.Nodes.Count).Zip(resource.Nodes,
                        (zero, n) => (n.CoresAvailable > 0)? 1 : 0
                    ).ToArray()
                )
                {
                    //ExecutionTime = new TimeSpan(Math.Max(0, 500 - nodes.Length)*SEC_IN_TICKS),
                    ExecutionTime = new TimeSpan(0*SEC_IN_TICKS),
                };

            return null;
        }
    }

    public class ForceFirstPC : LaunchModel
    {
        protected override bool Matches(TaskDescription task, Resource resource)
        {
            return false;

            if (resource.ProviderName == CONST.Providers.WinPc && resource.Nodes.First().HasPackage(task.Package))
                return true;

            return false;
        }

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
            if (resource.ProviderName == CONST.Providers.WinPc)
            {
                var node = resource.Nodes.First();

                if (node.HasPackage(task.Package) && node.CoresAvailable > 0)
                {
                    string nodeName = node.NodeName;

                    return new Estimation(
                        task.TaskId,
                        resource.ProviderName,
                        resource.ResourceName,
                        resource.Nodes.Select(n => (n.NodeName == nodeName)? 1 : 0).ToArray()
                    )
                    {
                        //ExecutionTime = new TimeSpan(Math.Max(0, 500 - nodes.Length)*SEC_IN_TICKS),
                        ExecutionTime = TimeSpan.FromSeconds(2),
                    };
                }
            }

            return null;
        }
    }

    public class CommonLauncher : LaunchModel
	{
        protected override bool Matches(TaskDescription task, Resource resource)
        {
			return true;
		}

        protected override Estimation Estimate(TaskDescription task, Resource resource)
        {
			if (task == null || resource == null || !resource.Nodes.Any())
				return null;

			var limits = new[]
			{
				new { pack = "",             otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "GAMESS",       otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "ORCA",         otherSoft = "windows", maxCores = 1,  maxNodes = 1},
				new { pack = "ORCA",         otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "SEMP",         otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "Plasmon",      otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "QDLaser",      otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "JAggregate",   otherSoft = "",        maxCores = 1,  maxNodes = 1},
                //new { pack = "Plasmon",      otherSoft = "",        maxCores = 1,  maxNodes = 1},
                //new { pack = "QDLaser",      otherSoft = "",        maxCores = 8,  maxNodes = 1},
                //new { pack = "JAggregate",   otherSoft = "",        maxCores = 8,  maxNodes = 1},
				//new { pack = "NanoFlow",     otherSoft = "",        maxCores = 16*3, maxNodes = 3},
				new { pack = "NanoFlow",     otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "MD_KMC",       otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "MD_KMC",       otherSoft = "windows", maxCores = 0,  maxNodes = 0},
				new { pack = "NTDMFT",       otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "Dalton",       otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "NAEN",         otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "Upconversion", otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "TestP",        otherSoft = "",        maxCores = 1,  maxNodes = 1},
				new { pack = "Belman",       otherSoft = "",        maxCores = 8,  maxNodes = 1},
				new { pack = "SWAN",         otherSoft = "",        maxCores = 1,  maxNodes = 1},
			};

            var packLimits = limits.Where(l => String.IsNullOrWhiteSpace(l.pack));
            if (limits.Any(limit => limit.pack.ToLower() == task.Package.ToLower()))
                packLimits = limits.Where(limit => limit.pack.ToLower() == task.Package.ToLower());

			// selecting proper limit (maximize software matches):
            
            var properPackLimit = packLimits.First();
			int maxSoftMatches = 0;
            
            foreach (var packLimit in packLimits)
			{
				int softMathcesCount = 0;
				string[] otherSoft = packLimit.otherSoft.Split(new char[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (string soft in otherSoft)
				{
					// todo : [future] packs on different nodes can be different
					if (resource.Nodes.First().OtherSoftware.Contains(soft.ToLower()))
						softMathcesCount++;
				}

				if (softMathcesCount > maxSoftMatches || (packLimit.maxCores > properPackLimit.maxCores && softMathcesCount == maxSoftMatches))
				{
					maxSoftMatches = softMathcesCount;
					properPackLimit = packLimit;
				}
			}

            // Choose number of cores for each node:

            var coresOnNode = new List<int>();
			int coresToDo = properPackLimit.maxCores;
			int nodesToDo = properPackLimit.maxNodes;

			foreach (var node in resource.Nodes)
			{
                if (coresToDo > 0 && nodesToDo > 0)
                {
                    int coresOnCurrentNode = Math.Min(node.CoresAvailable, coresToDo);

                    coresOnNode.Add(coresOnCurrentNode);
                    coresToDo -= coresOnCurrentNode;
                    nodesToDo -= (coresOnCurrentNode == 0) ? 0 : 1;
                }
                else
                    coresOnNode.Add(0);
			}

			int coresFound = coresOnNode.Sum();
			if (coresFound == 0) // haven't found anything
				return null;

            // Estimate (clusters with more nodes are preferable, so subtract it's nodes.Count from time estimation):

			TimeSpan time = new TimeSpan((long) ((Math.Round(18000.0 / coresFound) - resource.Nodes.Count + 60)*SEC_IN_TICKS));
            Estimation estimation = new Estimation(task.TaskId, resource.ProviderName, resource.ResourceName, coresOnNode.ToArray()) 
                { ExecutionTime = time };

			return estimation;
		}
	}
}


