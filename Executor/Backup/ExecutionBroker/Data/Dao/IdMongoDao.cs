using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Config = System.Configuration.ConfigurationManager;

namespace ExecutionBroker.Data.Dao
{
    public class IdMongoDao : AMongoBaseDao
    {

        private const string MetaTaskCollectionName = "seq";
        private const string Id = "task_id";

        public IdMongoDao()
        {
            Init();
        }

        public long GetNewId()
        {
            try
            {
                var jobs = db.GetCollection(MetaTaskCollectionName);
                var query = Query.GTE(Id, (long) 0);
                var update = Update.Inc(Id, (long) 1);
                var sortBy = SortBy.Descending(Id);
                var result = jobs.FindAndModify(query, sortBy, update, returnNew: true, upsert: true);
                /*
                var chosenJob = result.Response;
                BsonElement bs;
                chosenJob[1].AsBsonDocument.TryGetElement(Id, out bs);
                return bs.Value.AsInt64;
                /**/
                long generatedId = result.ModifiedDocument[Id].AsInt64;
                return generatedId;
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }
}