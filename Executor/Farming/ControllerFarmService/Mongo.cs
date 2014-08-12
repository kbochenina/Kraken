using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

using Config = System.Configuration.ConfigurationManager;

namespace MITP
{
    internal static class Mongo
    {
        private const string MONGO_CONNECTIONSTRING_NAME = "MongoConnectionString";
        private const string MONGO_DB_NAME = "MongoDbName";

        private const string NODES_STATE_COLLECTION_NAME = "NodesState";
        private const string TASKS_STATE_COLLECTION_NAME = "TasksState";

        private static string GetCollectionName(Type type)
        {
            if (type == typeof(NodeStateInfo[]))
                return NODES_STATE_COLLECTION_NAME;

            if (type == typeof(TaskCache))
                return TASKS_STATE_COLLECTION_NAME;

            throw new Exception("Unknown collection");
        }

        public static MongoCollection<TCollection> GetCollection<TCollection>()
        {
            string connectionString = Config.AppSettings[MONGO_CONNECTIONSTRING_NAME];
            var server = MongoServer.Create(connectionString);

            string dbName = Config.AppSettings[MONGO_DB_NAME];
            var db = server.GetDatabase(dbName);

            string collectionName = GetCollectionName(typeof(TCollection));
            var collection = db.GetCollection<TCollection>(collectionName);

            return collection;
        }

    }
}

