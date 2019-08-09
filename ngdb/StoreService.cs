using ngdb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ngdb
{
    public class StoreService
    {
        private readonly Dictionary<string, (Collection Collection, Dictionary<string, (long Cas, Mutex Mutex, object Value)> Items)> collections;

        public StoreService()
        {
            this.collections = new Dictionary<string, (Collection Collection, Dictionary<string, (long Cas, Mutex Mutex, object Value)> Items)>();
        }

        public bool CreateCollection(string collectionName, bool persistenceEnabled)
        {
            var collection = new Collection()
            {
                Name = collectionName,
                PersistenceEnabled = persistenceEnabled
            };
            if (collections.ContainsKey(collectionName)) return false;
            collections.Add(collectionName, (collection, new Dictionary<string, (long Cas, Mutex Mutex, object Value)>()));
            return true;
        }

        public IEnumerable<Collection> GetCollections() => collections.Values.Select(c => c.Collection);

        public OperationResult Get(string collectionName, string key)
        {
            var result = new OperationResult() { Key = key, Success = false };
            if (collections.TryGetValue(collectionName, out var db))
            {
                if (!string.IsNullOrWhiteSpace(key) && db.Items.TryGetValue(key, out var meta))
                {
                    result.Success = true;
                    result.Status = NgDbStoreStatus.Succeeded;
                    result.Cas = meta.Cas;
                    result.Value = meta.Value;
                }
                else
                {
                    result.Status = NgDbStoreStatus.KeyNotFound;
                }
            }
            else
            {
                result.Status = NgDbStoreStatus.CollectionNotFound;
            }
            return result;
        }

        public OperationResult Set(string collectionName, string key, object value, long cas)
        {
            var result = new OperationResult() { Key = key, Success = false };
            if (collections.TryGetValue(collectionName, out var db))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    if (db.Items.TryGetValue(key, out var meta))
                    {
                        if (meta.Mutex.WaitOne(millisecondsTimeout: 20))
                        {
                            if (cas == meta.Cas)
                            {
                                meta.Value = value;
                                meta.Cas = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                result.Success = true;
                                result.Status = NgDbStoreStatus.Succeeded;
                                db.Collection.ItemCount++;
                            }
                            else
                            {
                                result.Status = NgDbStoreStatus.CasMismatch;
                            }
                            meta.Mutex.ReleaseMutex();
                        }
                        else
                        {
                            result.Status = NgDbStoreStatus.SetTimeout;
                        }
                    }
                    else
                    {
                        meta = (DateTimeOffset.UtcNow.ToUnixTimeSeconds(), new Mutex(), value);
                        if (db.Items.TryAdd(key, meta))
                        {
                            result.Success = true;
                            result.Status = NgDbStoreStatus.Succeeded;
                        }
                        else
                        {
                            result.Status = NgDbStoreStatus.ConcurrentCreate;
                        }
                    }
                }
            }
            else
            {
                result.Status = NgDbStoreStatus.CollectionNotFound;
            }
            return result;
        }
    }

    public struct OperationResult
    {
        public string Key { get; set; }
        public long Cas { get; set; }
        public object Value { get; set; }
        public bool Success { get; set; }
        public NgDbStoreStatus Status { get; set; }

    }

    public enum NgDbStoreStatus
    {
        Succeeded,
        CollectionNotFound,
        KeyNotFound,
        CasMismatch,
        ConcurrentCreate,
        SetTimeout
    }
}
