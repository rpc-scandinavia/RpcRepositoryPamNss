using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// This cache is build around <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/>, and it adds a lock on the
/// key, so only one process/thread can get/create data for the specific key at one time.
/// This cache also requests new data at certain intervals, but keep returning existing data until new data has been
/// successfully received.
/// </summary>
public class NssCache {
	// Static caches and state that live between provider instances.
	private static readonly ConcurrentDictionary<String, SemaphoreSlim> CacheLocks = new ConcurrentDictionary<String, SemaphoreSlim>();
	private static readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
	public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);
	protected readonly ILogger logger;

	/// <summary>
	/// Instantiates a new NSS cache.
	/// </summary>
	public NssCache(ILogger logger) {
		this.logger = logger;
	} // NssCache

	/// <summary>
	/// Gets the value associated with this key if it exists, or generates a new entry using the provided key and a value
	/// from the given factory if the key is not found.
	/// </summary>
	/// <param name="key">The key.</param>
	/// <param name="fromCache">Whether the returned result is from the cache or the factory method.</param>
	/// <param name="factory">The factory method, that provides the data is missing.</param>
	/// <typeparam name="TItem">The data type stored with that key.</typeparam>
	/// <returns>The value associated with this key.</returns>
	public TItem GetOrCreate<TItem>(String key, out Boolean fromCache, Func<TItem> factory) {
		fromCache = true;

		if (String.IsNullOrWhiteSpace(key) == true) {
			return default(TItem);
		}
		if (factory == null) {
			return default(TItem);
		}

		// Get the lock.
		SemaphoreSlim keyLock = NssCache.CacheLocks.GetOrAdd(key, ((key) => new SemaphoreSlim(1, 1)));
		keyLock.Wait();
		try {
			// Get or create the persistent container that never expires from the cache.
	        NssCacheContainer<TItem> container = Cache.GetOrCreate<NssCacheContainer<TItem>>(key, (entry) => {
				entry.Size = 1;														// Required when SizeLimit is set.
	            return new NssCacheContainer<TItem>();
			});

			// Get data from the factory if no data exist, or it is time to refresh, and we are allowed to retry.
            if (container.WantData == true) {
				try {
					TItem item = factory();
					container.Data = item;
					fromCache = false;
				} catch (Exception exception) {
					// Log.
					if (this.logger.IsEnabled(LogLevel.Debug) == true) {
						this.logger.LogDebug($"Cache factory error: {exception.Message}");
					}

					if (container.RegisterFailure() == true) {
						// Only throw the exception, when the container is empty.
						throw;
					};
				}
			}

			return container.Data;
		} finally {
			keyLock.Release();
		}
	} // GetOrCreate

} // NssCache
