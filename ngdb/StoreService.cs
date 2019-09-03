using Microsoft.Extensions.Options;
using ngdb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ngdb
{
    /// <summary>
    /// The <see cref="StoreService"/> is meant to be instantiated once and then reused (i.e. a singleton).
    /// </summary>
    public class StoreService
    {
        private Mutex collectionAddMutex;
        private NgDbConfig ngDbConfig;
        private readonly Dictionary<string, (Collection Collection, Dictionary<string, (long Cas, Mutex Mutex, object Value)> Items)> collections;

        public StoreService(IOptionsMonitor<NgDbConfig> options)
        {
            ngDbConfig = options.CurrentValue;
            this.collectionAddMutex = new Mutex();
            this.collections = new Dictionary<string, (Collection Collection, Dictionary<string, (long Cas, Mutex Mutex, object Value)> Items)>();
        }

        /// <summary>
        /// Adds a <see cref="Collection"/> to the internal list of collections. 
        /// This method never fails, it either returns true if the collection was created and added to the list. 
        /// Or false if the collection name exists already in the collection list or is more than 10 chars.
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="persistenceEnabled"></param>
        /// <returns></returns>
        public bool CreateCollection(string collectionName, bool persistenceEnabled)
        {
            collectionAddMutex.WaitOne(millisecondsTimeout: 50);
            var collection = new Collection()
            {
                Name = collectionName,
                PersistenceEnabled = persistenceEnabled
            };
            if (collections.ContainsKey(collectionName)) return false;
            collections.Add(collectionName, (collection, new Dictionary<string, (long Cas, Mutex Mutex, object Value)>()));
            collectionAddMutex.ReleaseMutex();
            return true;
        }

        /// <summary>
        /// Returns the internal list of <see cref="Collection"/>.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Collection> GetCollections() => collections.Values.Select(c => c.Collection);

        /// <summary>
        /// Returns the <see cref="Collection"/> for the given <paramref name="collectionName"/>.
        /// </summary>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        public Collection GetCollection(string collectionName) => collections.GetValueOrDefault(collectionName).Collection;

        public OperationResult<T> Get<T>(string collectionName, string key)
        {
            var result = new OperationResult<T>() { Collection = collectionName, Key = key, Success = false };
            if (collections.TryGetValue(collectionName, out var db))
            {
                if (!string.IsNullOrWhiteSpace(key) && db.Items.TryGetValue(key, out var meta))
                {
                    try
                    {
                        result.Document = (T)meta.Value;
                        result.Success = true;
                        result.Message = nameof(result.Success);
                        result.Status = NgDbStoreStatus.Succeeded;
                        result.Cas = meta.Cas;
                    }
                    catch (InvalidCastException)
                    {
                        result.Status = NgDbStoreStatus.InvalidCast;
                        result.Message = $"The element with key '{key}' from collection '{collectionName}' could not be cast into the requested type '{typeof(T).Name}'";
                    }
                }
                else
                {
                    result.Status = NgDbStoreStatus.KeyNotFound;
                    result.Message = $"The element with key '{key}' from collection '{collectionName}' was not found";
                }
            }
            else
            {
                result.Status = NgDbStoreStatus.CollectionNotFound;
                result.Message = $"The collection '{collectionName}' was not found";
            }
            return result;
        }

        /// <summary>
        /// Performs a create-or-update operation on the given collectionName and key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="cas"></param>
        /// <returns></returns>
        public OperationResult<T> Set<T>(string collectionName, string key, T value, long cas)
        {
            var result = new OperationResult<T>() { Collection = collectionName, Key = key, Success = false };
            if (collections.TryGetValue(collectionName, out var db))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    if (db.Items.TryGetValue(key, out var meta))
                    {
                        if (meta.Mutex.WaitOne(millisecondsTimeout: ngDbConfig.SetTimeoutInMilliseconds))
                        {
                            if (cas == meta.Cas)
                            {
                                meta.Value = value;
                                meta.Cas = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                result.Success = true;
                                result.Message = nameof(result.Success);
                                result.Status = NgDbStoreStatus.Succeeded;
                                db.Collection.ItemCount++;
                            }
                            else
                            {
                                result.Status = NgDbStoreStatus.CasMismatch;
                                result.Message = $"Updating the item with key '{key}' into collection '{collectionName}' failed because the lock could not be acquired within the defined timeout ({ngDbConfig.SetTimeoutInMilliseconds}ms)";
                            }
                            meta.Mutex.ReleaseMutex();
                        }
                        else
                        {
                            result.Status = NgDbStoreStatus.SetTimeout;
                            result.Message = $"Updating the item with key '{key}' into collection '{collectionName}' failed because the lock could not be acquired within the defined timeout ({ngDbConfig.SetTimeoutInMilliseconds}ms)";
                        }
                    }
                    else
                    {
                        meta = (DateTimeOffset.UtcNow.ToUnixTimeSeconds(), new Mutex(), value);
                        if (db.Items.TryAdd(key, meta))
                        {
                            result.Success = true;
                            result.Message = nameof(result.Success);
                            result.Status = NgDbStoreStatus.Succeeded;
                        }
                        else
                        {
                            result.Status = NgDbStoreStatus.ConcurrentCreate;
                            result.Message = $"Creating the item with key '{key}' into collection '{collectionName}' failed most likely because of a race condition";
                        }
                    }
                }
            }
            else
            {
                result.Status = NgDbStoreStatus.CollectionNotFound;
                result.Message = $"The collection '{collectionName}' was not found";
            }
            return result;
        }
    }

    public class OperationResult<T>
    {
        public string Collection { get; set; }
        public string Key { get; set; }
        public long Cas { get; set; }
        public T Document { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public NgDbStoreStatus Status { get; set; }

    }

    public enum NgDbStoreStatus
    {
        Succeeded,
        CollectionNotFound,
        KeyNotFound,
        CasMismatch,
        ConcurrentCreate,
        SetTimeout,
        InvalidCast
    }
}
