using System;

namespace WebCaching
{
    /// <summary>
    /// Represents a cache item.
    /// </summary>
    public class CacheItem
    {
        #region Properties

        private long _updated = 0;
        private object _value = null;

        /// <summary>
        /// Gets or sets the item value.
        /// </summary>
        public object Value
        {
            get { return _value; }
            set
            {
                _value = value;
                _updated = DateTime.UtcNow.Ticks;
            }
        }

        /// <summary>
        /// Gets or sets the number of milliseconds for which this item is considered "up-to-date". 
        /// </summary>
        /// <remarks>The value less or equal to 0 means that the cache item never expires.</remarks>
        public long TimeToLive { get; set; }

        /// <summary>
        /// Gets value indicating whether this cache item is expired.
        /// </summary>
        public bool Expired
        {
            get { return TimeToLive > 0 && TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _updated).TotalMilliseconds > TimeToLive; }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of an object.
        /// </summary>
        public CacheItem() : this(null) { }

        /// <summary>
        /// Initializes a new instance of an object.
        /// </summary>
        /// <param name="value">Item value.</param>
        public CacheItem(object value)
        {
            this.Value = value;
        }
    }
}
