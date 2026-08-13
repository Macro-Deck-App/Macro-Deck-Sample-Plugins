using MacroDeck.Plugin.Hosting.Integrations;
using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Plugin.Protocol.Handshake;
using MacroDeck.SampleVirtualProfilePlugin.Actions;
using MacroDeck.SampleVirtualProfilePlugin.Profiles;
using MacroDeck.SampleVirtualProfilePlugin.Scenes;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Events;
using MacroDeck.Sdk.Notifications;
using MacroDeck.Sdk.Profiles;
using MacroDeck.Sdk.Variables;
using Serilog;

namespace MacroDeck.SampleVirtualProfilePlugin;

/// <summary>
/// A virtual profile the plugin owns, plus the callbacks that go the other way: a plugin does not only
/// answer the host, it drives the deck, styles widgets, runs scripts, writes variables and notifies the
/// user. Pressing a scene button in the profile and running the "set scene" action end in the same
/// place, so the two directions stay visibly consistent.
/// </summary>
public sealed class ControlRoomIntegration : IPluginIntegration, IProfileProvider, IVariableProvider, IEventProvider
{
	internal const string SceneChangedEventId = "scene-changed";

	private const string SceneVariableName = "sample_control_room_scene";

	private readonly IPluginCatalogNotifier _catalogNotifier;
	private readonly ILogger _logger;

	private IIntegrationContext? _context;

	public ControlRoomIntegration(IPluginCatalogNotifier catalogNotifier, ILogger logger)
	{
		_catalogNotifier = catalogNotifier;
		_logger = logger.ForContext<ControlRoomIntegration>();
		Actions =
		[
			new SetSceneAction(this),
			new StyleWidgetAction(this),
			new AnnounceAction(this),
			new RunScriptAction(this),
			new NavigateDeckAction(this)
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	internal ControlRoomScene ActiveScene { get; private set; } = ControlRoomScenes.Default;

	/// <summary>Null before <see cref="InitializeAsync"/> and after <see cref="ShutdownAsync"/>. Actions
	/// treat that as "not ready" rather than dereferencing it.</summary>
	internal IIntegrationContext? Context => _context;

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

	public IReadOnlyList<VirtualProfileDescriptor> GetProfiles() => [ControlRoomProfile.Build(ActiveScene)];

	/// <summary>
	/// A press on one of the profile's own widgets. Fire-and-forget by contract - there is nothing to
	/// reply with - so it does its work and swallows nothing else.
	/// </summary>
	public async Task HandleWidgetInteractionAsync(
		string profileId,
		string folderId,
		string widgetId,
		WidgetInteraction interaction)
	{
		if (ControlRoomProfile.SceneOf(widgetId) is not { } scene)
		{
			return;
		}

		_logger.Information("Widget {WidgetId} requested scene {Scene} ({Trigger}).", widgetId, scene.Id, interaction.TriggerType);
		await ApplySceneAsync(scene, source: "widget", CancellationToken.None);
	}

	/// <summary>
	/// The one path that changes the scene, whether a widget or an action asked for it: it updates the
	/// state, tells the host the profile catalogue is stale so the buttons redraw, pushes the new value
	/// into a host variable and publishes the event.
	/// </summary>
	internal async Task ApplySceneAsync(ControlRoomScene scene, string source, CancellationToken cancellationToken)
	{
		ActiveScene = scene;

		// A virtual profile is a synchronous catalogue, so the host is serving a cached describe until
		// it is told otherwise - without this, the buttons keep the previous scene's colours.
		_catalogNotifier.CatalogChanged(CapabilityKinds.VirtualProfiles, reason: $"scene changed to {scene.Id}");

		if (_context is not { } context)
		{
			return;
		}

		context.Events.Publish(SceneChangedEventId, new Dictionary<string, object?>
		{
			["scene"] = scene.Id,
			["isLive"] = scene.IsLive,
			["source"] = source
		});

		// A pushed variable update, as opposed to the polled ProvidedVariables below: the host owns this
		// variable, and the plugin writes it when something happens rather than waiting to be asked.
		try
		{
			var existing = await context.Variables.GetByNameAsync(SceneVariableName);
			if (existing is null)
			{
				await context.Variables.CreateAsync(SceneVariableName, VariableType.Text, scene.Name);
			}
			else
			{
				await context.Variables.SetValueAsync(existing.Id, scene.Name);
			}
		}
		catch (HostInvocationException exception)
		{
			// Every round-trip callback can fail on the wire - rate limited, timed out, or no live
			// connection - which an in-process integration never has to handle.
			_logger.Warning(exception, "Could not write the scene variable.");
		}

		context.Notifications.Notify(new UserNotificationRequest
		{
			Title = $"Scene: {scene.Name}",
			Level = scene.IsLive ? UserNotificationLevel.Warning : UserNotificationLevel.Info,
			Key = "control-room-scene"
		});
	}

	public IReadOnlyList<ProvidedVariable> ProvidedVariables { get; } =
	[
		new ProvidedVariable("sample_control_room_active_scene", VariableType.Text) { DefinitionId = "active-scene" },
		new ProvidedVariable("sample_control_room_is_live", VariableType.Boolean) { DefinitionId = "is-live" }
	];

	public Task<object?> GetValueAsync(string name, CancellationToken cancellationToken)
		=> Task.FromResult(name switch
		{
			"sample_control_room_active_scene" => (object?)ActiveScene.Name,
			"sample_control_room_is_live" => ActiveScene.IsLive,
			_ => null
		});

	public IReadOnlyList<EventDefinition> EventDefinitions { get; } =
	[
		new EventDefinition
		{
			Id = SceneChangedEventId,
			Name = "Scene changed",
			Description = "Raised when the control room switches to another scene.",
			PayloadParameters =
			[
				ActionParameter.Text("scene", "Scene"),
				ActionParameter.Toggle("isLive", "On air"),
				ActionParameter.Text("source", "Triggered by")
			]
		}
	];
}
