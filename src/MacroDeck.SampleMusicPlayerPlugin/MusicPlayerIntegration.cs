using System.Globalization;
using MacroDeck.SampleMusicPlayerPlugin.Actions;
using MacroDeck.SampleMusicPlayerPlugin.Player;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Events;
using MacroDeck.Sdk.MusicPlayer;
using MacroDeck.Sdk.Variables;

namespace MacroDeck.SampleMusicPlayerPlugin;

/// <summary>
/// Two synthetic players over one shared library: a full-surface one with catalogue and devices, and a
/// transport-only one. Actions, variables and the track-changed event all read the same playback
/// state, so what a widget shows and what a variable says cannot disagree.
/// </summary>
public sealed class MusicPlayerIntegration : IPluginIntegration, IMusicPlayerProvider, IVariableProvider,
	IEventProvider, IDynamicEventOptionsProvider
{
	internal const string LibraryInstanceId = "library";
	internal const string SpeakerInstanceId = "speaker";

	internal const string TrackChangedEventId = "track-changed";

	private readonly LibraryMusicPlayer _library;
	private readonly SpeakerMusicPlayer _speaker;

	private IIntegrationContext? _context;

	public MusicPlayerIntegration(TimeProvider timeProvider)
	{
		_library = new LibraryMusicPlayer(new PlaybackEngine(timeProvider, engine => PublishTrackChanged(LibraryInstanceId, engine)));
		_speaker = new SpeakerMusicPlayer(new PlaybackEngine(timeProvider, engine => PublishTrackChanged(SpeakerInstanceId, engine)));

		Actions =
		[
			new TogglePlaybackAction(this),
			new SetVolumeAction(this),
			new PlayCatalogItemAction(this),
			new TransferPlaybackAction(this)
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public Task InitializeAsync(IIntegrationContext context)
	{
		_context = context;
		return Task.CompletedTask;
	}

	public Task ShutdownAsync()
	{
		_context = null;
		return Task.CompletedTask;
	}

	public IReadOnlyList<MusicPlayerInstance> GetInstances() =>
	[
		new(LibraryInstanceId, "Sample library"),
		new(SpeakerInstanceId, "Sample speaker")
	];

	public IMusicPlayer? GetPlayer(string instanceId) => instanceId switch
	{
		LibraryInstanceId => _library,
		SpeakerInstanceId => _speaker,
		_ => null
	};

	internal LibraryMusicPlayer Library => _library;

	internal PlaybackEngine? EngineOf(string instanceId) => instanceId switch
	{
		LibraryInstanceId => _library.Engine,
		SpeakerInstanceId => _speaker.Engine,
		_ => null
	};

	public IReadOnlyList<VariableDefinition> Variables { get; } =
	[
		VariableDefinition.Eager("sample_music_track", VariableType.Text) with { Id = "track" },
		VariableDefinition.Eager("sample_music_artist", VariableType.Text) with { Id = "artist" },
		VariableDefinition.Eager("sample_music_is_playing", VariableType.Boolean) with { Id = "is-playing" },
		// Writable, so a Slider widget bound to it drives the volume and reads the real one back. Volume is
		// cheap to apply continuously, so the drag is not deferred to release.
		VariableDefinition.Eager("sample_music_volume", VariableType.Numeric) with
		{
			Id = "volume",
			Unit = "%",
			SemanticKind = VariableSemanticKinds.Percentage,
			Write = new VariableWriteCapability()
		}
	];

	/// <summary>Reports the library instance, addressed by local id. An id this provider does not know
	/// reads as unavailable, which the host renders as an empty value rather than an error.</summary>
	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		var engine = _library.Engine;
		return ValueTask.FromResult(localId switch
		{
			"track" => VariableReading.Of(engine.CurrentTrack.Title),
			"artist" => VariableReading.Of(engine.CurrentTrack.Artist),
			"is-playing" => VariableReading.Of(engine.IsPlaying),
			"volume" => VariableReading.Of(engine.VolumePercent, 0, 100, 1),
			_ => VariableReading.Unavailable
		});
	}

	/// <summary>Only called for "volume", the one variable declaring a write. The engine clamps, and the
	/// clamped value arrives on the next read.</summary>
	public ValueTask<VariableWriteResult> SetValueAsync(string localId, object? value, CancellationToken cancellationToken = default)
	{
		if (value is not (double or int or long))
		{
			return ValueTask.FromResult(VariableWriteResult.InvalidValue());
		}

		_library.Engine.SetVolume((int)Convert.ToDouble(value, CultureInfo.InvariantCulture));
		return ValueTask.FromResult(VariableWriteResult.Applied());
	}

	public IReadOnlyList<EventDefinition> EventDefinitions { get; } =
	[
		new EventDefinition
		{
			Id = TrackChangedEventId,
			Name = Strings.Events.TrackChanged.Name(),
			Description = Strings.Events.TrackChanged.Description(),
			// A configuration parameter narrows what the user subscribes to; its options come from
			// GetEventOptionsAsync below rather than being fixed at declaration time.
			ConfigurationParameters =
			[
				ActionParameter.DynamicChoice("player", label: Strings.Fields.Player.Label(), required: true)
			],
			PayloadParameters =
			[
				ActionParameter.Text("player", Strings.Fields.Player.Label()),
				ActionParameter.Text("track", Strings.Fields.Track.Label()),
				ActionParameter.Text("artist", Strings.Fields.Artist.Label())
			]
		}
	];

	public Task<DynamicOptionsResult> GetEventOptionsAsync(EventOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(new DynamicOptionsResult { Options = InstanceOptions() });

	/// <summary>An instance's display name is a plain string on the provider contract - it names a
	/// configured account, not UI text - so it travels into the option as a literal.</summary>
	internal IReadOnlyList<ActionParameterOption> InstanceOptions()
		=> [.. GetInstances().Select(instance => new ActionParameterOption { Value = instance.Id, Label = instance.DisplayName })];

	private void PublishTrackChanged(string instanceId, PlaybackEngine engine)
		=> _context?.Events.Publish(TrackChangedEventId, new Dictionary<string, object?>
		{
			["player"] = instanceId,
			["track"] = engine.CurrentTrack.Title,
			["artist"] = engine.CurrentTrack.Artist
		});
}
