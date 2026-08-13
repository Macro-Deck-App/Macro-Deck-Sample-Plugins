using MacroDeck.Sdk.MusicPlayer;

namespace MacroDeck.SampleMusicPlayerPlugin.Player;

/// <summary>
/// A transport-only instance: no catalogue, no devices. It implements <see cref="IMusicPlayer"/> and
/// nothing else, so the host reports <c>HasCatalog</c>/<c>HasDevices</c> as false for it and never
/// offers the corresponding operations - the same way a real service supports browsing on some
/// accounts but not others.
/// </summary>
internal sealed class SpeakerMusicPlayer(PlaybackEngine engine) : IMusicPlayer
{
	internal PlaybackEngine Engine { get; } = engine;

	public Task<MusicPlayerState> GetStateAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult(PlayerState.From(Engine, device: null));

	public Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken = default)
		=> Task.FromResult(MusicLibrary.Artwork(artworkId));

	public Task PlayAsync(CancellationToken cancellationToken = default)
	{
		Engine.Play();
		return Task.CompletedTask;
	}

	/// <summary>Reachable even without a catalogue: the host can still hand an item another instance
	/// browsed, so the operation stays supported.</summary>
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
}
