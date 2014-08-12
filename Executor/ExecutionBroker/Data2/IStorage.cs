/* (с) НИУ ИТМО, 2011-2012 */
using System;
using System.Collections.Generic;
using Easis.Monitoring.DataLayer;
using MongoDB.Bson;

namespace Easis.Monitoring
{



    public interface IStorage
    {






        T GetDataObject<T>(StoragePath storagePath);







        void SetDataObject<T>(StoragePath storagePath, T obj, TimeSpan actualFor);







        void SetFields<T>(StoragePath storagePath, IDictionary<string, T> fields, TimeSpan actualFor);



        bool SupportHistory { get; set; }





        string GetDataInJson(StoragePath storagePath);




        string[] GetCollectionNames();






        string GetActualEntityInJson(StoragePath storagePath);





        BsonDocument GetActualEntityInBson(StoragePath storagePath);






        int CleanOldData(StoragePath from, TimeSpan savePeriod);

        bool ContainsEntity(StoragePath storagePath);
    }
}