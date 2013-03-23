using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace WebCaching
{
    /// <summary>
    /// Represents a LRU dictionary.
    /// </summary>
    public class RecencyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        #region Properties

        /// <summary>
        /// Gets an object used to synchronize access to the dictionary methods from multiple threads.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or sets the recency.
        /// </summary>
        private List<TKey> Recency { get; set; }

        /// <summary>
        /// Gets or sets the actual dictionary data.
        /// </summary>
        private ConcurrentDictionary<TKey, TValue> Data { get; set; }

        /// <summary>
        /// Gets the maximum capacity of the dictionary.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Gets the collection of all dictionary keys.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get { return Data.Keys; }
        }

        /// <summary>
        /// Gets the collection of all dictionary values.
        /// </summary>
        public ICollection<TValue> Values
        {
            get { return Data.Values; }
        }

        /// <summary>
        /// Gets the current number of items in the dictionary.
        /// </summary>
        public int Count
        {
            get { return Data.Count; }
        }

        /// <summary>
        /// Gets value indicating whether the dictionary is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>The value with the specified key.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="key">key</paramref> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="key">key</paramref> doesn't exist in the dictionary.</exception>
        public TValue this[TKey key]
        {
            get
            {
                TValue ret = default(TValue);

                if (key == null)
                    throw new ArgumentNullException("key");
                else if (!ContainsKey(key))
                    throw new ArgumentException("An item with the given key doesn't exists in a dictionary.", "key");
                else
                    ret = GetInternal(key);

                return ret;
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                else if (!ContainsKey(key))
                    throw new ArgumentException("An item with the given key doesn't exists in a dictionary.", "key");
                else
                    SetInternal(key, value);
            }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of an object.
        /// </summary>
        /// <param name="capacity">The maximum capacity of the dictionary.</param>
        /// <exception cref="System.ArgumentException"><paramref name="capacity">capacity</paramref> is less or equal to zero.</exception>
        public RecencyDictionary(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero.", "capacity");
            else
            {
                this.Capacity = capacity;

                this.Recency = new List<TKey>();
                this.Data = new ConcurrentDictionary<TKey, TValue>();
            }
        }

        /// <summary>
        /// Adds new item to the dictionary.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <param name="value">Item value.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="key">key</paramref> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="key">key</paramref> already exist in the dictionary.</exception>
        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            else if (ContainsKey(key))
                throw new ArgumentException("An item with the given key already exists in a dictionary.", "key");
            else
                SetInternal(key, value);
        }

        /// <summary>
        /// Returns value indicating whether the item with the given key is present in the dictionary.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>value indicating whether the item with the given key is present in the dictionary.</returns>
        public bool ContainsKey(TKey key)
        {
            return Data.ContainsKey(key);
        }

        /// <summary>
        /// Removes the item with the given key from the dictionary.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Value indicating whether item was successfully removed.</returns>
        public bool Remove(TKey key)
        {
            bool ret = false;

            if (key != null && ContainsKey(key))
            {
                RemoveInternal(key);
                ret = true;
            }

            return ret;
        }

        /// <summary>
        /// Removes all items from the dictionary.
        /// </summary>
        public void Clear()
        {
            Recency.Clear();
            Data.Clear();
        }

        /// <summary>
        /// Returns an enumerator object that can be used to iterate over dictionary items.
        /// </summary>
        /// <returns>An enumerator object that can be used to iterate over dictionary items.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        #region LRU implementation

        /// <summary>
        /// Returns the item with the given key.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Item value.</returns>
        private TValue GetInternal(TKey key)
        {
            TValue ret = default(TValue);

            if (key != null && ContainsKey(key) && Data.TryGetValue(key, out ret))
                OnItemUsed(key);

            return ret;
        }

        /// <summary>
        /// Adds or updates the item with the given key.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <param name="value">Item value.</param>
        private void SetInternal(TKey key, TValue value)
        {
            bool hadItem = false;

            if (key != null)
            {
                hadItem = ContainsKey(key);

                if (!hadItem)
                {
                    // Did we reach the capacity?
                    if (Count >= Capacity)
                    {
                        lock (_lock)
                        {
                            if (Count >= Capacity)
                            {
                                // Removing the oldest item
                                RemoveInternal(Recency[Recency.Count - 1]);
                            }
                        }
                    }
                }

                // Adding or updating the item
                Data.AddOrUpdate(key, value, (k, v) => value);

                if (!hadItem)
                {
                    // Adding to recency
                    Recency.Insert(0, key);
                }
                else
                {
                    // Marking item as most recently used
                    OnItemUsed(key);
                }
            }
        }

        /// <summary>
        /// Removes the item with the given key.
        /// </summary>
        /// <param name="key">item key.</param>
        private void RemoveInternal(TKey key)
        {
            int itemIndex = -1;
            TValue removedValue = default(TValue);

            if (key != null && ContainsKey(key))
            {
                lock (_lock)
                {
                    itemIndex = Recency.IndexOf(key);

                    // Removing from the recency
                    Recency.RemoveAt(itemIndex);
                }

                // Removing from the data
                Data.TryRemove(key, out removedValue);
            }
        }

        /// <summary>
        /// Occurs when the item with the given key is used.
        /// </summary>
        /// <param name="key">Item key.</param>
        private void OnItemUsed(TKey key)
        {
            int itemIndex = -1;

            if (key != null)
            {
                lock (_lock)
                {
                    itemIndex = Recency.IndexOf(key);

                    if (itemIndex != 0)
                    {
                        Recency.RemoveAt(itemIndex);
                        Recency.Insert(0, key);
                    }
                }
            }
        }

        #endregion

        #region Explicit interface implementations

        /// <summary>
        /// Adds the new item to the dictionary.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <exception cref="System.ArgumentNullException">Item key is null.</exception>
        /// <exception cref="System.ArgumentException">An item with the given key already exists in the dictionary.</exception>
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            if (item.Key == null)
                throw new ArgumentNullException("key");
            else if (ContainsKey(item.Key))
                throw new ArgumentException("An item with the given key already exists in the dictionary.", "item");
            else
                SetInternal(item.Key, item.Value);
        }

        /// <summary>
        /// Removes the specified key/value pair from the dictionary.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>Value indicating whether item was successfully removed from the dictionary.</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            bool ret = false;

            if (item.Key != null && ContainsKey(item.Key))
            {
                RemoveInternal(item.Key);
                ret = true;
            }

            return ret;
        }

        /// <summary>
        /// Attempts to get the value associated with the specified key from the dictionary.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the object from the dictionary that has the specified key, or the default value of , if the operation failed.</param>
        /// <returns>Value indicating whether the value with the given key was found in the dictionary.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="key">key</paramref> is null.</exception>
        bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
        {
            bool ret = false;
            value = default(TValue);

            if (key == null)
                throw new ArgumentNullException("key");
            else if (ContainsKey(key))
            {
                value = GetInternal(key);
                ret = true;
            }

            return ret;
        }

        /// <summary>
        /// Returns value indicating whether the dictionary contains an item with the specified key.
        /// </summary>
        /// <param name="item">The item to locate in the dictionary.</param>
        /// <returns>Vlue indicating whether the dictionary contains an item with the specified key.</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            return (Data as ICollection<KeyValuePair<TKey, TValue>>).Contains(item);
        }

        /// <summary>
        /// Copies the items of the dictionary to an array, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the items copied from the dictionary. The array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            (Data as ICollection<KeyValuePair<TKey, TValue>>).CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Returns an enumerator object that can be used to iterate over dictionary items.
        /// </summary>
        /// <returns>An enumerator object that can be used to iterate over dictionary items.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        #endregion
    }
}
