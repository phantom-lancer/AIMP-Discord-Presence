using AIMP.SDK.FileManager.Objects;
using System;

namespace AIMP_Discord_Presence_2.Services
{
	public interface IAlbumArtService : IDisposable
	{
		/// <summary>
		/// Returns the https url of the cover, or an empty string when it is not ready yet.
		/// Must never block: everything that needs the network or the disk happens in the
		/// background, otherwise the presence update is stuck behind a slow http request.
		/// </summary>
		string TryGetImageUrl(IAimpFileInfo fileInfo);
	}

	/// <summary>
	/// Implemented by services that can start loading the cover of a track nobody listens to yet.
	/// </summary>
	public interface IPrefetchingAlbumArtService : IAlbumArtService
	{
		void Prefetch(IAimpFileInfo fileInfo);
	}
}