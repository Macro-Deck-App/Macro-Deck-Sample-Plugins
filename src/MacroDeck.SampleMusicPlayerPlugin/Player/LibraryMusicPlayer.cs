using MacroDeck.Sdk.MusicPlayer;

namespace MacroDeck.SampleMusicPlayerPlugin.Player;

/// <summary>
/// The full-surface instance: transport, catalogue browsing and output devices. The host reads the
/// last two off the player object itself (<c>HasCatalog</c>/<c>HasDevices</c> on the instance
/// descriptor), which is why they are implemented here rather than on the provider - see
/// <see cref="SpeakerMusicPlayer"/> for an instance that only supports transport.
/// </summary>
internal sealed class LibraryMusicPlayer(PlaybackEngine engine)
	: IMusicPlayer, IMusicPlayerCatalogProvider, IMusicPlayerDeviceProvider
{
	private readonly List<MusicPlayerDevice> _devices =
	[
		new("device-living-room", "Living room", "speaker", IsActive: true, VolumePercent: 60),
		new("device-kitchen", "Kitchen", "speaker"),
		new("device-headphones", "Headphones", "headphones")
	];

	internal PlaybackEngine Engine { get; } = engine;

	public Task<MusicPlayerState> GetStateAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult(PlayerState.From(Engine, ActiveDevice));

	public Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken = default)
		=> Task.FromResult(MusicLibrary.Artwork(artworkId));

	public Task PlayAsync(CancellationToken cancellationToken = default)
	{
		Engine.Play();
		return Task.CompletedTask;
	}

	public Task PlayItemAsync(MusicPlayerCatalogItem item, CancellationToken cancellationToken = default)
	{
		Engine.PlayItem(item);
		return Task.CompletedTask;
	}

	public Task PauseAsync(CancellationToken cancellationToken = default)
	{
		Engine.Pause();
		return Task.CompletedTask;
	}

	public Task TogglePlayPauseAsync(CancellationToken cancellationToken = default)
	{
		Engine.Toggle();
		return Task.CompletedTask;
	}

	public Task NextAsync(CancellationToken cancellationToken = default)
	{
		Engine.Next();
		return Task.CompletedTask;
	}

	public Task PreviousAsync(CancellationToken cancellationToken = default)
	{
		Engine.Previous();
		return Task.CompletedTask;
	}

	public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
	{
		Engine.Seek(position);
		return Task.CompletedTask;
	}

	public Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default)
	{
		Engine.SetVolume(volumePercent);
		return Task.CompletedTask;
	}

	public Task SetShuffleAsync(bool enabled, CancellationToken cancellationToken = default)
	{
		Engine.SetShuffle(enabled);
		return Task.CompletedTask;
	}

	public Task SetRepeatModeAsync(RepeatMode mode, CancellationToken cancellationToken = default)
	{
		Engine.SetRepeatMode(mode);
		return Task.CompletedTask;
	}

	/// <summary>An empty result here means the catalogue really is empty. A read that fails has to throw,
	/// so the UI can offer a retry instead of showing "nothing found" - see capability-parity.md.</summary>
	public Task<IReadOnlyList<MusicPlayerCatalogItem>> GetCatalogAsync(
		string instanceId,
		MusicPlayerCatalogItemKind kind,
		string? filter,
		CancellationToken cancellationToken)
		=> Task.FromResult(MusicLibrary.CatalogItems(kind, filter));

	public Task<IReadOnlyList<MusicPlayerDevice>> GetDevicesAsync(CancellationToken cancellationToken)
		=> Task.FromResult<IReadOnlyList<MusicPlayerDevice>>([.. _devices]);

	public Task TransferPlaybackAsync(string deviceId, bool startPlayback, CancellationToken cancellationToken)
	{
		var target = _devices.FindIndex(device => string.Equals(device.Id, deviceId, StringComparison.Ordinal));
		if (target < 0)
		{
			return Task.CompletedTask;
		}

		for (var i = 0; i < _devices.Count; i++)
		{
			_devices[i] = _devices[i] with { IsActive = i == target };
		}

		if (startPlayback)
		{
			Engine.Play();
		}

		return Task.CompletedTask;
	}

	internal MusicPlayerDevice? ActiveDevice => _devices.FirstOrDefault(device => device.IsActive);
}
