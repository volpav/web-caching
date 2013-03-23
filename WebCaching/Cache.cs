using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace WebCaching
{
    /// <summary>
    /// Represents a cache.
    /// </summary>
    public class Cache
    {
        #region Properties

        /// <summary>
        /// Gets or sets the default TTL (Time To Live) value for all cache items.
        /// </summary>
        private long TimeToLive { get; set; }

        /// <summary>
        /// Gets or sets the cache items.
        /// </summary>
        private RecencyDictionary<string, CacheItem> Items { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of an object.
        /// </summary>
        public Cache() : this(100, 300000) { }

        /// <summary>
        /// Initializes a new instance of an object.
        /// </summary>
        /// <param name="capacity">The maximum number of items that the cache can accept.</param>
        /// <param name="timeToLive">The default TTL (Time To Live) value for all cache items.</param>
        public Cache(int capacity, long timeToLive)
        {
            this.TimeToLive = timeToLive;
            this.Items = new RecencyDictionary<string, CacheItem>(capacity);
        }

        /// <summary>
        /// Returns value indicating whether the item with the given key is currently in the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Value indicating whether the item with the given key is currently in the cache.</returns>
        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && ContainsItem(key);
        }

        /// <summary>
        /// Retrieves the item with the given key from the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Item value.</returns>
        public object Get(string key)
        {
            object ret = null;

            if (!string.IsNullOrEmpty(key))
                ret = GetItem(key);

            return ret;
        }

        /// <summary>
        /// Retrieves the item with the given key from the cache.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="key">Item key.</param>
        /// <returns>Item value.</returns>
        public T Get<T>(string key)
        {
            T ret = default(T);
            object o = Get(key);

            if (o != null && o is T)
                ret = (T)o;

            return ret;
        }

        /// <summary>
        /// Adds or updates the item with the given key.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <param name="value">Item value.</param>
        /// <returns>Value indicating whether the item has been successfully added/updated.</returns>
        public bool Set(string key, object value)
        {
            bool ret = false;

            if (!string.IsNullOrEmpty(key))
            {
                ret = true;
                SetItem(key, value);
            }

            return ret;
        }

        /// <summary>
        /// Removes the item with the given key from the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>The removed item.</returns>
        public object Remove(string key)
        {
            object ret = null;

            if (!string.IsNullOrEmpty(key))
                ret = RemoveItem(key);

            return ret;
        }

        /// <summary>
        /// Removes the item with the given key from the cache.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="key">Item key.</param>
        /// <returns>The removed item.</returns>
        public T Remove<T>(string key)
        {
            T ret = default(T);
            object o = Remove(key);

            if (o != null && o is T)
                ret = (T)o;

            return ret;
        }

        /// <summary>
        /// Removes all items from the cache.
        /// </summary>
        public void Clear()
        {
            ClearItems();
        }

        /// <summary>
        /// Retrieves the item with the given key from the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Item value.</returns>
        protected virtual object GetItem(string key)
        {
            object ret = null;
            CacheItem i = null;

            if (Items.ContainsKey(key))
            {
                i = Items[key];

                if (!i.Expired)
                    ret = i.Value;
                else
                    RemoveItem(key);
            }

            return ret;
        }

        /// <summary>
        /// Adds or updates the item with the given key.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <param name="value">Item value.</param>
        protected virtual void SetItem(string key, object value)
        {
            if (Items.ContainsKey(key))
                Items[key].Value = value;
            else
                Items.Add(key, new CacheItem(value) { TimeToLive = TimeToLive });
        }

        /// <summary>
        /// Removes the item with the given key from the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>The removed item.</returns>
        protected virtual object RemoveItem(string key)
        {
            object ret = null;
            CacheItem i = null;

            if (Items.ContainsKey(key))
            {
                i = Items[key];

                ret = i.Value;
                Items.Remove(key);
            }

            return ret;
        }

        /// <summary>
        /// Removes all items from the cache.
        /// </summary>
        protected virtual void ClearItems()
        {
            Items.Clear();
        }

        /// <summary>
        /// Returns value indicating whether the item with the given key is currently in the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Value indicating whether the item with the given key is currently in the cache.</returns>
        protected virtual bool ContainsItem(string key)
        {
            return Items.ContainsKey(key);
        }
    }
}
