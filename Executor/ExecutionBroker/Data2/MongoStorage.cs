/* (с) НИУ ИТМО, 2011-2012 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace Easis.Monitoring.DataLayer
{
    public class MongoStorage : IStorage
    {
        private MongoServer _server;
        private MongoDatabase _db;
        private const string DEFAULT_DATABASE_NAME = "monitoring";
        private string _dbName;

        public bool SupportHistory { get; set; }
        #region Constructors
        public MongoStorage()
        {

            _dbName = DEFAULT_DATABASE_NAME;
            _server = MongoServer.Create();
            _db = _server.GetDatabase(_dbName);

            SupportHistory = true;
        }

        public MongoStorage(string connString)
        {
            MongoUrlBuilder mb = new MongoUrlBuilder(connString);
            _server = MongoServer.Create(connString);
            _db = _server.GetDatabase(mb.DatabaseName);
            SupportHistory = true;
        }
        #endregion

        private BsonDocument HelperJsonToBson(string json)
        {
            JObject jo = JObject.Parse(json);

            MemoryStream ms = new MemoryStream();
            BsonWriter writer = new BsonWriter(ms);

            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(writer, jo);

            byte[] bytes = new byte[ms.Length];
            ms.Seek(0, SeekOrigin.Begin);
            ms.Read(bytes, 0, (int)ms.Length);

            BsonDocument bdquery = BsonSerializer.Deserialize<BsonDocument>(bytes);
            return bdquery;
        }


        #region Packing timestamps







        private BsonValue HelperPackTimestamps(BsonValue val, DateTime timestamp, DateTime actualTill)
        {
            BsonValue ret = null;
            if (val is IEnumerable<BsonElement>)
            {
                IEnumerable<BsonElement> bes = (IEnumerable<BsonElement>)val;
                foreach (var bsonElement in bes)
                {

                    if (!bsonElement.Name.StartsWith("&") && !bsonElement.Name.StartsWith("_"))
                        bsonElement.Value = HelperPackTimestamps(bsonElement.Value, timestamp, actualTill);
                }

                if (val is BsonDocument)
                {

                    BsonDocument nestedbd = new BsonDocument();
                    nestedbd.Add(new BsonElement("&timestamp", BsonValue.Create(timestamp)));
                    nestedbd.Add(new BsonElement("&actualTill", BsonValue.Create(actualTill)));
                    val.AsBsonDocument.Add(new BsonElement("&timings", nestedbd));
                }
                ret = val;
            }
            else
            {

                BsonDocument nestedbd = new BsonDocument();
                nestedbd.Add(new BsonElement("&value", val));
                nestedbd.Add(new BsonElement("&timestamp", BsonValue.Create(timestamp)));
                nestedbd.Add(new BsonElement("&actualTill", BsonValue.Create(actualTill)));
                ret = nestedbd;
            }
            return ret;
        }







        private BsonValue PackTimestampedDocument(BsonValue bd, DateTime timestamp, DateTime actualTill)
        {
            return HelperPackTimestamps(bd, timestamp, actualTill);
        }


        private BsonValue HelperCleanTimestamps(BsonValue val)
        {
            BsonValue ret = null;
            if (val is BsonDocument)
            {

                if (val.AsBsonDocument.Contains("&timings"))
                {
                    val.AsBsonDocument.Remove("&timings");

                    IEnumerable<BsonElement> bes = (IEnumerable<BsonElement>)val;
                    foreach (var bsonElement in bes)
                    {

                        if (!bsonElement.Name.StartsWith("&") && !bsonElement.Name.StartsWith("_"))
                            bsonElement.Value = HelperCleanTimestamps(bsonElement.Value);
                    }

                    ret = val;

                }
                else
                {

                    ret = val.AsBsonDocument["&value"];
                }
            }
            else
            {
                throw new Exception("CHECKIT");
            }
            
            return ret;
        }


        private BsonDocument UnpackTimestampedDocument(IEnumerable<BsonDocument> bds, DateTime timestamp)
        {




            DateTime last = new DateTime(1000,1,1);
            BsonDocument lastBd = null;
            foreach (BsonDocument bd in bds)
            {
                if(last < bd["&timings"].AsBsonDocument["&timestamp"].AsDateTime)
                {
                    last = bd["&timings"].AsBsonDocument["&timestamp"].AsDateTime;
                    lastBd = bd;
                }
            }

            lastBd = (BsonDocument)HelperCleanTimestamps(lastBd);
            
            return lastBd;
        }

        private BsonDocument UnpackTimestampedDocument(BsonDocument bd, DateTime timestamp)
        {
            return UnpackTimestampedDocument(new List<BsonDocument>() { bd }, timestamp);
        }
        #endregion


        #region Setters

        public void SetDataObject<T>(StoragePath storagePath, T obj, TimeSpan actualFor)
        {
            MongoCollection<T> col = _db.GetCollection<T>(storagePath.CategoryPath);
            
            BsonDocument bd;
            if (obj is BsonDocument)
            {
                bd = obj as BsonDocument;
            }
            else
            {
                bd = obj.ToBsonDocument();
            }

            if(actualFor == TimeSpan.MaxValue)
                bd = (BsonDocument)PackTimestampedDocument(bd, DateTime.Now, DateTime.MaxValue);
            else

            bd = (BsonDocument)PackTimestampedDocument(bd, DateTime.Now, (DateTime.Now + actualFor));


            if (String.IsNullOrEmpty(storagePath.ObjectId) && String.IsNullOrEmpty(storagePath.QueryString))
            {
                col.Insert(obj);
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId))
            {

                BsonDocument bdquery = HelperJsonToBson(storagePath.QueryString);
                QueryDocument query = new QueryDocument(bdquery);

                if (!SupportHistory && col.FindOneAs<BsonDocument>(query) != null)
                {
                    var update = new UpdateDocument
                                     {
                                         {"$set", bd}
                                     };

                    col.Update(query, update);
                }
                else
                {
                    col.Save(bd);
                }
            }
            else if (String.IsNullOrEmpty(storagePath.QueryString))
            {

                Debug.Assert(!SupportHistory);
                BsonValue id = BsonValue.Create(storagePath.ObjectId);
                var query = new QueryDocument {
                    { "_id", id }
                };
                var update = new UpdateDocument {
                    { "$set", bd }
                };
                col.Update(query, update);
            }
            else
            {
                throw new ArgumentException("Storage path can't contain both: query string and object id");
            }
        }

        public void SetFields<T>(StoragePath storagePath, IDictionary<string, T> fields, TimeSpan actualFor)
        {
            MongoCollection col = _db.GetCollection(storagePath.CategoryPath);

            IDictionary<string, object> newDict = new Dictionary<string, object>();
            foreach (var field in fields)
            {
                newDict.Add(field.Key, (object)field.Value);
            }

            BsonDocument bd = (BsonDocument)PackTimestampedDocument(new BsonDocument(newDict), DateTime.Now, (DateTime.Now + new TimeSpan(0, 0, 1, 0)));

            if (String.IsNullOrEmpty(storagePath.ObjectId) && String.IsNullOrEmpty(storagePath.QueryString))
            {
                col.Insert(bd);
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId))
            {

                BsonDocument bdquery = HelperJsonToBson(storagePath.QueryString);
                QueryDocument query = new QueryDocument(bdquery);

                if (!SupportHistory && col.FindOneAs<BsonDocument>(query) != null)
                {
                    var update = new UpdateDocument
                                     {
                                         {"$set", bd}
                                     };

                    col.Update(query, update);

                }
                else
                {
                    BsonDocument bd0 = bd.Merge(bdquery);
                    col.Save(bd0);
                }

            }
            else if (String.IsNullOrEmpty(storagePath.QueryString))
            {

                Debug.Assert(!SupportHistory);
                BsonValue id = BsonValue.Create(storagePath.ObjectId);
                var query = new QueryDocument {
                    { "_id", id }
                };
                var update = new UpdateDocument {
                    { "$set", bd }
                };
                col.Update(query, update);
            }
            else
            {
                throw new ArgumentException("Storage path can't contain both query string and obid");
            }
        }
        #endregion

        #region Getters

        public T GetDataObject<T>(StoragePath storagePath)
        {
            MongoCollection col = _db.GetCollection(storagePath.CategoryPath);
            BsonValue id = BsonValue.Create(storagePath.ObjectId);
            return col.FindOneByIdAs<T>(id);
        }


        public string[] GetCollectionNames()
        {
            return _db.GetCollectionNames().ToArray();
        }

        public BsonDocument GetActualEntityInBson(StoragePath storagePath)
        {
            if (storagePath == null) throw new ArgumentNullException("storagePath");

            if (String.IsNullOrEmpty(storagePath.CategoryPath))
            {
                throw new ArgumentException("Storage path to entity can't be without category");
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId) && String.IsNullOrEmpty(storagePath.QueryString))
            {
                throw new ArgumentException("Storage path to entity can't be without query or objectid");
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId))
            {
                MongoCollection col = _db.GetCollection(storagePath.CategoryPath);

                BsonDocument bdquery = HelperJsonToBson(storagePath.QueryString);


                var q = Query.And(new QueryComplete(bdquery), Query.GTE("&timings.&actualTill", DateTime.Now));

                BsonDocument bd;

                if (SupportHistory)
                {
                    var results = col.FindAs<BsonDocument>(q);
                    if (results.Count() == 0)
                    {
                        throw new ObjectNotFoundException();
                    }
                    bd = UnpackTimestampedDocument(results.AsEnumerable(), DateTime.Now);
                }
                else
                {
                    var results = col.FindOneAs<BsonDocument>(q);
                    bd = UnpackTimestampedDocument(results, DateTime.Now);
                }

                return bd;
            }
            else if (String.IsNullOrEmpty(storagePath.QueryString))
            {
                MongoCollection col = _db.GetCollection(storagePath.CategoryPath);

                throw new NotImplementedException();
            }
            else
            {
                throw new ArgumentException("Storage path can't contain both query string and obid");
            }

        }






        public int CleanOldData(StoragePath from, TimeSpan savePeriod)
        {
            int ret = 0;

            if(!SupportHistory)
                throw new NotImplementedException();

            if (from == null || String.IsNullOrEmpty(from.CategoryPath))
            {

                foreach (var colname in _db.GetCollectionNames())
                {
                    MongoCollection col = _db.GetCollection(colname);

                    var q = Query.And(Query.LT("&timings.&actualTill", DateTime.Now - savePeriod));

                    SafeModeResult smr = col.Remove(q,SafeMode.True);

                    if (smr != null)
                    {
                        if (smr.Ok)
                        {
                            ret = (int) smr.DocumentsAffected;
                        }
                        else
                        {
                            throw new Exception(smr.ErrorMessage);
                        }
                    }
                    else
                    {
                        ret = 1;
                    }
               
                }

            }
            else if (String.IsNullOrEmpty(from.ObjectId) && String.IsNullOrEmpty(from.QueryString))
            {

                MongoCollection col = _db.GetCollection(from.CategoryPath);
                var q = Query.And(Query.LT("&timings.&actualTill", DateTime.Now - savePeriod));

                SafeModeResult smr = col.Remove(q, SafeMode.True);
                if (smr != null)
                {
                    if (smr.Ok)
                    {
                        ret = (int ) smr.DocumentsAffected;
                    }
                    else
                    {
                        throw new Exception(smr.ErrorMessage);
                    }
                }
                else
                {
                    ret = 1;
                }
            }
            else if (String.IsNullOrEmpty(from.ObjectId))
            {

                MongoCollection col = _db.GetCollection(from.CategoryPath);
                BsonDocument bdquery = HelperJsonToBson(from.QueryString);

                var q = Query.And(new QueryComplete(bdquery), Query.LT("&timings.&actualTill", DateTime.Now - savePeriod));

                SafeModeResult smr = col.Remove(q, SafeMode.True);

                if (smr != null)
                {
                    if (smr.Ok)
                    {
                        ret = (int)smr.DocumentsAffected;
                    }
                    else
                    {
                        throw new Exception(smr.ErrorMessage);
                    }
                }
                else
                {
                    ret = 1;
                }
            }
            else if (String.IsNullOrEmpty(from.QueryString))
            {
                throw new InvalidOperationException();
            }
            else
            {
                throw new ArgumentException("Storage path can't contain both query string and obid");
            }
            return ret;
        }






        public string GetActualEntityInJson(StoragePath storagePath)
        {
            return GetActualEntityInBson(storagePath).ToJson();
        }

        public bool ContainsEntity(StoragePath storagePath)
        {
            bool ret = false;
            if (storagePath == null) throw new ArgumentNullException("storagePath");

            if (String.IsNullOrEmpty(storagePath.CategoryPath))
            {
                throw new ArgumentException("Storage path to entity can't be without category");
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId) && String.IsNullOrEmpty(storagePath.QueryString))
            {
                throw new ArgumentException("Storage path to entity can't be without query or objectid");
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId))
            {
                MongoCollection col = _db.GetCollection(storagePath.CategoryPath);
                BsonDocument bdquery = HelperJsonToBson(storagePath.QueryString);

                var q = Query.And(new QueryComplete(bdquery), Query.GTE("&timings.&actualTill", DateTime.Now));
                BsonDocument bd;
                if (SupportHistory)
                {
                    var results = col.FindAs<BsonDocument>(q);
                    if (results.Count() == 0)
                    {
                        ret = false;
                    }
                    else
                    {
                        ret = true;
                    }
                }
                else
                {
                    var results = col.FindOneAs<BsonDocument>(q);
                    if (results == null)
                    {
                        ret = false;
                    }
                    else
                    {
                        ret = true;
                    }
                }

                return ret;
            }
            else if (String.IsNullOrEmpty(storagePath.QueryString))
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new ArgumentException("Storage path can't contain both query string and obid");
            }

        }

        public string GetDataInJson(StoragePath storagePath)
        {
            string ret = "";
            if (String.IsNullOrEmpty(storagePath.CategoryPath))
            {
                ret += "{";
                ret += String.Format("'path':'{0}',", storagePath.ToString());

                ret += "'result': [";

                foreach (var colname in _db.GetCollectionNames())
                {
                    ret += "{";
                    ret += String.Format("'_name':'{0}',", colname);
                    ret += String.Format("'_type':'{0}'", "collection");
                    ret += "},";
                }
                ret += "],";
                ret += String.Format("'status':'{0}'", "ok");
                ret += "}";
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId) && String.IsNullOrEmpty(storagePath.QueryString))
            {
                MongoCollection col = _db.GetCollection(storagePath.CategoryPath);

                MongoCursor<BsonDocument> mc = col.FindAllAs<BsonDocument>();

                ret += "{";

                ret += String.Format("'path':'{0}',", storagePath.ToString());

                ret += "'result': [";

                foreach (var d in mc)
                {
                    ret += "{";
                    ret += String.Format("'_name':'{0}',", d.GetValue("_id"));
                    ret += String.Format("'_type':'{0}'", "object");
                    ret += "}";

                }
                ret += "],";
                ret += String.Format("'status':'{0}'", "ok");
                ret += "}";
            }
            else if (String.IsNullOrEmpty(storagePath.ObjectId))
            {
                MongoCollection col = _db.GetCollection(storagePath.CategoryPath);
                throw new NotImplementedException();
            }
            else if (String.IsNullOrEmpty(storagePath.QueryString))
            {
                MongoCollection col = _db.GetCollection(storagePath.CategoryPath);
                throw new NotImplementedException();
            }
            else
            {
                throw new ArgumentException("Storage path can't contain both query string and obid");
            }
            return ret;
        }
        #endregion

    }

    public class ObjectNotFoundException : Exception
    {
    }
}
