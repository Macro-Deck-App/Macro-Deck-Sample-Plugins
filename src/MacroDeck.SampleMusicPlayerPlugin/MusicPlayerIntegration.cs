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

	public IReadOnlyList<ProvidedVariable> ProvidedVariables { get; } =
	[
		new ProvidedVariable("sample_music_track", VariableType.Text) { DefinitionId = "track" },
		new ProvidedVariable("sample_music_artist", VariableType.Text) { DefinitionId = "artist" },
		new ProvidedVariable("sample_music_is_playing", VariableType.Boolean) { DefinitionId = "is-playing" },
		new ProvidedVariable("sample_music_volume", VariableType.Numeric) { DefinitionId = "volume" }
	];

	/// <summary>Reports the library instance. A name this provider does not know returns null, which the
	/// host renders as an unavailable variable rather than an error.</summary>
	public Task<object?> GetValueAsync(string name, CancellationToken cancellationToken)
	{
		var engine = _library.Engine;
		return Task.FromResult(name switch
		{
			"sample_music_track" => (object?)engine.CurrentTrack.Title,
			"sample_music_artist" => engine.CurrentTrack.Artist,
			"sample_music_is_playing" => engine.IsPlaying,
			"sample_music_volume" => engine.VolumePercent,
			_ => null
		});
	}

	public IReadOnlyList<EventDefinition> EventDefinitions { get; } =
	[
		new EventDefinition
		{
			Id = TrackChangedEventId,
			Name = "Track changed",
			Description = "Raised when a player moves to another track.",
			// A configuration parameter narrows what the user subscribes to; its options come from
			// GetEventOptionsAsync below rather than being fixed at declaration time.
			ConfigurationParameters =
			[
				ActionParameter.DynamicChoice("player", label: "Player", required: true)
			],
			PayloadParameters =
			[
				ActionParameter.Text("player", "Player"),
				ActionParameter.Text("track", "Track"),
				ActionParameter.Text("artist", "Artist")
			]
		}
	];

	public Task<DynamicOptionsResult> GetEventOptionsAsync(EventOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(new DynamicOptionsResult { Options = InstanceOptions() });

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
