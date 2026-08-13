using MacroDeck.Sdk.MusicPlayer;

namespace MacroDeck.SampleMusicPlayerPlugin.Player;

/// <summary>
/// The playback state machine both player instances run on. It owns no SDK contract - the players
/// adapt it - so the transport rules stay readable in one place.
///
/// Position is derived from <see cref="TimeProvider"/> rather than a timer: a paused player reports the
/// position it stopped at, a playing one reports elapsed time since it started, and a test advancing a
/// <c>ManualTimeProvider</c> sees the same progression a real clock would produce.
/// </summary>
internal sealed class PlaybackEngine(TimeProvider timeProvider, Action<PlaybackEngine> trackChanged)
{
	private readonly Lock _gate = new();

	private IReadOnlyList<LibraryTrack> _queue = MusicLibrary.Tracks;
	private int _index;
	private TimeSpan _offset;
	private DateTimeOffset? _playingSince;

	internal LibraryTrack CurrentTrack
	{
		get
		{
			lock (_gate)
			{
				return _queue[_index];
			}
		}
	}

	internal bool IsPlaying
	{
		get
		{
			lock (_gate)
			{
				return _playingSince is not null;
			}
		}
	}

	internal int VolumePercent { get; private set; } = 60;

	internal bool ShuffleEnabled { get; private set; }

	internal RepeatMode RepeatMode { get; private set; } = RepeatMode.Off;

	internal TimeSpan Position
	{
		get
		{
			lock (_gate)
			{
				var position = _playingSince is { } since
					? _offset + (timeProvider.GetUtcNow() - since)
					: _offset;
				var duration = _queue[_index].Duration;
				return position > duration ? duration : position;
			}
		}
	}

	internal void Play()
	{
		lock (_gate)
		{
			_playingSince ??= timeProvider.GetUtcNow();
		}
	}

	internal void Pause()
	{
		lock (_gate)
		{
			if (_playingSince is not { } since)
			{
				return;
			}

			_offset += timeProvider.GetUtcNow() - since;
			_playingSince = null;
		}
	}

	internal void Toggle()
	{
		if (IsPlaying)
		{
			Pause();
		}
		else
		{
			Play();
		}
	}

	internal void Next() => Move(1);

	internal void Previous() => Move(-1);

	internal void Seek(TimeSpan position)
	{
		lock (_gate)
		{
			var duration = _queue[_index].Duration;
			_offset = position < TimeSpan.Zero ? TimeSpan.Zero : position > duration ? duration : position;
			if (_playingSince is not null)
			{
				_playingSince = timeProvider.GetUtcNow();
			}
		}
	}

	internal void SetVolume(int volumePercent) => VolumePercent = Math.Clamp(volumePercent, 0, 100);

	internal void SetShuffle(bool enabled) => ShuffleEnabled = enabled;

	internal void SetRepeatMode(RepeatMode mode) => RepeatMode = mode;

	/// <summary>Replaces the queue with one item's tracks and starts at its first track.</summary>
	internal void PlayItem(MusicPlayerCatalogItem item)
	{
		var tracks = item.Kind == MusicPlayerCatalogItemKind.Playlist
			? MusicLibrary.FindPlaylist(item.Id) is { } playlist ? MusicLibrary.TracksOf(playlist) : []
			: MusicLibrary.FindTrack(item.Id) is { } track ? [track] : [];

		if (tracks.Count == 0)
		{
			return;
		}

		lock (_gate)
		{
			_queue = tracks;
			_index = 0;
			_offset = TimeSpan.Zero;
			_playingSince = timeProvider.GetUtcNow();
		}

		trackChanged(this);
	}

	private void Move(int direction)
	{
		lock (_gate)
		{
			_index = (_index + direction + _queue.Count) % _queue.Count;
			_offset = TimeSpan.Zero;
			if (_playingSince is not null)
			{
				_playingSince = timeProvider.GetUtcNow();
			}
		}

		trackChanged(this);
	}
}
