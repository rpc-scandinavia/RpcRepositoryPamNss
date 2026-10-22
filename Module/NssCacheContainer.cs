using System;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// This is a container for storing items in the <see cref="NssCache"/>.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
class NssCacheContainer<TItem> {
	private Boolean isEmpty;
	private TItem data;
	private DateTimeOffset lastSuccessfulFetch;
	private TimeSpan currentBackoff;
	private DateTimeOffset nextRetryAllowed;

	/// <summary>
	/// Instantiates a new empty item container.
	/// </summary>
	public NssCacheContainer() {
		this.isEmpty = true;
		this.data = default(TItem);
		this.lastSuccessfulFetch = DateTimeOffset.MinValue;
		this.currentBackoff = TimeSpan.FromSeconds(5);
		this.nextRetryAllowed = DateTimeOffset.MinValue;
	} // NssCacheContainer

	/// <summary>
	/// Gets whether the container wants data, either because it is empty or it is time to refresh.
	/// </summary>
	public Boolean WantData {
		get {
			return (
				(this.isEmpty == true) || (
					(DateTimeOffset.UtcNow - this.lastSuccessfulFetch > NssCache.RefreshInterval) &&	// Is stale.
					(DateTimeOffset.UtcNow >= this.nextRetryAllowed)									// Can retry.
				)
			);
		}
	} // WantData

	/// <summary>
	/// Gets or sets the data.
	/// </summary>
	public TItem Data {
		get {
			return this.data;
		} set {
			this.data = value;
			this.isEmpty = false;
			this.lastSuccessfulFetch = DateTimeOffset.UtcNow;
			this.currentBackoff = TimeSpan.FromSeconds(5);
			this.nextRetryAllowed = DateTimeOffset.MinValue;
		}
	} // Data

	/// <summary>
	/// Register the failure to fetch data from the data source.
	/// </summary>
	/// <returns>Return true if the container is empty.</returns>
	public Boolean RegisterFailure() {
		if (this.isEmpty == false) {
			// Not critical, the container has stale data, apply exponential backoff until maximum is reached.
			this.currentBackoff = TimeSpan.FromTicks(Math.Min(this.currentBackoff.Ticks * 2, NssCache.MaxBackoff.Ticks));
			this.nextRetryAllowed = DateTimeOffset.UtcNow.Add(this.currentBackoff);
			return false;
		} else {
			// Critical, the container is empty, retry sooner.
			this.nextRetryAllowed = DateTimeOffset.UtcNow.AddSeconds(5);
			return true;
		}
	} // RegisterFailure

} // NssCacheContainer
