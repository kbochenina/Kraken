using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MongoDB.Driver;
using Config = System.Configuration.ConfigurationManager;

namespace ExecutionBroker.Data.Dao
{
    public class AMongoBaseDao
    {
        //Const interface parameters
        private const string MONGO_CONNECTIONSTRING_NAME = "MongoConnectionString";
        private const string MONGO_DB_NAME = "MongoDbName";

        private static MongoServer server = null;
        protected static MongoDatabase db = null;

        protected void Init()
        {
            if (server == null) {
                string connectionString = Config.AppSettings[MONGO_CONNECTIONSTRING_NAME];
                server = MongoServer.Create(connectionString);
            }
            if (db == null) {
                string dbName = Config.AppSettings[MONGO_DB_NAME];
                db = server.GetDatabase(dbName);
            }
        }

    }
}