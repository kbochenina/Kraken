using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExecutionBroker.Data.Dao;

namespace MITP
{
	public class Broker
	{
		private static IdMongoDao idDao;
        /*
		public static object Lock = new object();

		// Tasks are shared between threads!
		private static List<Task> _tasksUnsafe = new List<Task>();

		[ThreadStatic]
		private static ListThreadSafeWrapper<Task> _tasks;

		public static ListThreadSafeWrapper<Task> Tasks
		{
			get
			{
				if (_tasks == null)
					_tasks = new ListThreadSafeWrapper<Task>(_tasksUnsafe);

				return _tasks;
			}
		}
		*/

        public static ulong GetNewTaskId()
        {
            /*
             * 
             * 
             * 
             * 
             * var jobs = database.GetCollection("jobs");
var query = Query.And(
    Query.EQ("inprogress", false),
    Query.EQ("name", "Biz report")
);
var sortBy = SortBy.Descending("priority");
var update = Update.
    .Set("inprogress", true)
    .Set("started", DateTime.UtcNow);
var result = jobs.FindAndModify(
    query,
    sortBy,
    update,
    true // return new document
);
var chosenJob = result.ModifiedDocument;
             * 
             * */

/*            var service = EntryPointProxy.GetClustersService();
            ServiceProxies.ClustersService.Code errCode;
            ServiceProxies.ClustersService.TaskInfo taskInfo = null;

            taskInfo = service.CreateTask(out errCode);

            if (errCode != ServiceProxies.ClustersService.Code.OperationSuccess)
                throw new Exception("Cluster exception: " + errCode.ToString());

            return ulong.Parse(taskInfo.TaskID);*/
            if (idDao == null) idDao = new IdMongoDao();
            return Convert.ToUInt64(idDao.GetNewId());
        }

		/*
        [ThreadStatic]
		private static ReflectedList<AbstractAdapter> _adapters;

        public static ReflectedList<AbstractAdapter> Adapters
		{
			get
			{
				if (_adapters == null)
                    _adapters = new ReflectedList<AbstractAdapter>();

				return _adapters;
			}
		}
        */
 
		/*
        [ThreadStatic]
		private static ReflectedList<LaunchModel> _launchModels;

		public static ReflectedList<LaunchModel> LaunchModels
		{
			get
			{
				if (_launchModels == null)
					_launchModels = new ReflectedList<LaunchModel>();

				return _launchModels;
			}
		}
        */
        // todo : Remove LaunchModels

        /*
        [ThreadStatic]
        private static ReflectedList<AbstractResourceProvider> _providers;

        public static ReflectedList<AbstractResourceProvider> Providers
        {
            get
            {
                if (_providers == null)
                    _providers = new ReflectedList<AbstractResourceProvider>();

                return _providers;
            }
        }

        public static AbstractResourceProvider ProviderByName(string providerName)
        {
            return Providers.First(p => p.Name == providerName);
        }

        /*
        public static AbstractResourceProvider ProviderByResource(string resourceName)
        {
            return Providers.First(p => p.GetResourceNames().Any(name => name == resourceName));
        }
        */
    }
}