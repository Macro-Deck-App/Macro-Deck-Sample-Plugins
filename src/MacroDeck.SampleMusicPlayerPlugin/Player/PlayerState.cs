using MacroDeck.Sdk.MusicPlayer;

namespace MacroDeck.SampleMusicPlayerPlugin.Player;

/// <summary>Projects the engine onto the state contract, so both player instances report the same shape.</summary>
internal static class PlayerState
{
	internal static MusicPlayerState From(PlaybackEngine engine, MusicPlayerDevice? device)
	{
		var track = engine.CurrentTrack;
		return new MusicPlayerState
		{
			IsConnected = true,
			PlaybackState = engine.IsPlaying ? PlaybackState.Playing : PlaybackState.Paused,
			TrackName = track.Title,
			Artists = [track.Artist],
			AlbumName = track.Album,
			ArtworkId = track.Id,
			Position = engine.Position,
			Duration = track.Duration,
			VolumePercent = engine.VolumePercent,
			ShuffleEnabled = engine.ShuffleEnabled,
			RepeatMode = engine.RepeatMode,
			DeviceName = device?.Name,
			DeviceType = device?.Type
		};
	}
}
