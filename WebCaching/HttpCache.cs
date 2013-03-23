using System;
using System.Web;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace WebCaching
{
    /// <summary>
    /// Represents a session-based cache whose behavior is dependent on the HTTP cache control headers. This class cannot be inherited.
    /// </summary>
    public sealed class HttpCache : Cache
    {
        #region Properties

        /// <summary>
        /// Gets the cache for a current HTTP session.
        /// </summary>
        public static HttpCache Current
        {
            get
            {
                HttpCache ret = null;
                string key = "HttpCache:Current";

                if (HttpContext.Current.Session[key] == null)
                    HttpContext.Current.Session[key] = new HttpCache();

                ret = HttpContext.Current.Session[key] as HttpCache;

                ret.EnsureFlags();

                return ret;
            }
        }

        /// <summary>
        /// Gets the number of sessions to maintain cache for.
        /// </summary>
        private const int MaxCaches = 200;

        /// <summary>
        /// Gets the all currently available caches.
        /// </summary>
        private static readonly ConcurrentDictionary<string, HttpCache> Caches = new ConcurrentDictionary<string, HttpCache>();

        /// <summary>
        /// Gets the temporary storage.
        /// </summary>
        private Cache TemporaryStorage
        {
            get
            {
                Cache ret = null;
                string key = "HttpCache:TemporaryStorage";

                if (HttpContext.Current.Items[key] == null)
                    HttpContext.Current.Items[key] = new Cache(100, 0);

                ret = HttpContext.Current.Items[key] as Cache;

                return ret;
            }
        }

        /// <summary>
        /// Gets or sets the cache control flags.
        /// </summary>
        private IDictionary<string, string> Flags { get; set; }

        /// <summary>
        /// Gets or sets the timestamp indicating when the cache object was instantiated.
        /// </summary>
        private long Created { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of an object.
        /// </summary>
        private HttpCache()
        {
            Created = DateTime.UtcNow.Ticks;
            Flags = new Dictionary<string, string>();
        }

        /// <summary>
        /// Retrieves the item with the given key from the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Item value.</returns>
        protected override object GetItem(string key)
        {
            object ret = null;

            EnsureExpiration();

            if (CanCache())
                ret = base.GetItem(key);
            else
                ret = TemporaryStorage.Get(key);

            return ret;
        }

        /// <summary>
        /// Adds or updates the item with the given key.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <param name="value">Item value.</param>
        protected override void SetItem(string key, object value)
        {
            if (CanCache())
                base.SetItem(key, value);
            else
                TemporaryStorage.Set(key, value);
        }

        /// <summary>
        /// Returns value indicating whether the item with the given key is currently in the cache.
        /// </summary>
        /// <param name="key">Item key.</param>
        /// <returns>Value indicating whether the item with the given key is currently in the cache.</returns>
        protected override bool ContainsItem(string key)
        {
            EnsureExpiration();

            return base.ContainsItem(key) || TemporaryStorage.Contains(key);
        }

        #region Cache control via HTTP headers

        /// <summary>
        /// Ensures that flags are loaded.
        /// </summary>
        private void EnsureFlags()
        {
            string[] v = null;
            string expires = string.Empty;
            string cacheControl = string.Empty;
            string key = "HttpCache:EnsureFlags";
            DateTime expiresDate = DateTime.MinValue;

            if (!TemporaryStorage.Contains(key))
            {
                expires = HttpContext.Current.Request.Headers["Expires"];
                cacheControl = HttpContext.Current.Request.Headers["Cache-Control"];

                Flags = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(cacheControl))
                {
                    foreach (string pair in cacheControl.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        v = pair.ToLowerInvariant().Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                        if (v.Any())
                        {
                            if (!Flags.ContainsKey(v[0]))
                                Flags.Add(v[0], v.Length > 1 ? v[1] : string.Empty);
                            else
                                Flags[v[0]] = v[1];
                        }
                    }
                }

                if (!Flags.ContainsKey("expires") && !string.IsNullOrEmpty(expires) && DateTime.TryParseExact(expires, "ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture, DateTimeStyles.None, out expiresDate))
                    Flags.Add("expires", expiresDate.Ticks.ToString());

                TemporaryStorage.Set(key, true);
            }
        }

        /// <summary>
        /// Returns value indicating whether items can be added or updated.
        /// </summary>
        /// <returns>Value indicating whether items can be added or updated.</returns>
        private bool CanCache()
        {
            bool ret = true;
            string key = "HttpCache:CanCache";

            if (!TemporaryStorage.Contains(key))
                TemporaryStorage.Set(key, !Flags.ContainsKey("no-cache") && !Flags.ContainsKey("no-store"));

            ret = TemporaryStorage.Get<bool>(key);

            return ret;
        }

        /// <summary>
        /// Ensures that the cache is not expired.
        /// </summary>
        private void EnsureExpiration()
        {
            bool cacheExpired = false;
            long maxAge = -1, expires = -1;
            string key = "HttpCache:EnsureExpiration";

            if (!TemporaryStorage.Contains(key))
            {
                cacheExpired = !CanCache();

                if (!cacheExpired)
                {
                    if (Flags.ContainsKey("max-age") && long.TryParse(Flags["max-age"], out maxAge) && maxAge > 0)
                        cacheExpired = new DateTime(Created + maxAge).Ticks < DateTime.UtcNow.Ticks;
                    else if (Flags.ContainsKey("expires") && long.TryParse(Flags["expires"], out expires))
                        cacheExpired = new DateTime(expires).Ticks < DateTime.UtcNow.Ticks;
                }

                if (cacheExpired)
                    ClearItems();

                TemporaryStorage.Set(key, true);
            }
        }

        #endregion
    }
}
