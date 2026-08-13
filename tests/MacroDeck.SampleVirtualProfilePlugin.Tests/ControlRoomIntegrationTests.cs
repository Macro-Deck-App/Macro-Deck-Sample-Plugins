using MacroDeck.Plugin.Protocol.Capabilities.Actions;
using MacroDeck.Plugin.Protocol.Capabilities.Variables;
using MacroDeck.Plugin.Protocol.Capabilities.VirtualProfiles;
using MacroDeck.Plugin.Testing;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Sdk.Decks;
using MacroDeck.Sdk.Scripts;
using MacroDeck.Sdk.Widgets;
using NUnit.Framework;

namespace MacroDeck.SampleVirtualProfilePlugin.Tests;

[TestFixture]
public sealed class ControlRoomIntegrationTests
{
	private static readonly string[] _sceneWidgetIds = ["scene-live", "scene-standby", "scene-break", "scene-offline"];
	private static readonly string[] _breakOnly = ["scene-break"];
	private static readonly string[] _scriptNames = ["Start stream", "Broken"];

	[Test]
	public async Task The_profile_offers_one_button_per_scene()
	{
		await using var harness = await CreateAsync();

		var profiles = (await harness.VirtualProfiles.GetProfilesAsync()).DataAs<VirtualProfilesResult>();
		var scenes = profiles!.Profiles.Single().Folders.Single(folder => folder.Id == "scenes");

		Assert.That(scenes.Widgets.Select(widget => widget.Id), Is.EquivalentTo(_sceneWidgetIds));
		Assert.That(scenes.Widgets.Select(widget => widget.Type), Is.All.EqualTo("ActionButton"));
	}

	[Test]
	public async Task The_layout_is_locked_because_the_plugin_owns_it()
	{
		await using var harness = await CreateAsync();

		var layout = (await harness.VirtualProfiles.GetProfilesAsync())
			.DataAs<VirtualProfilesResult>()!.Profiles.Single().Layout;

		Assert.That(layout.RowsLocked, Is.True);
		Assert.That(layout.ColumnsLocked, Is.True);
	}

	[Test]
	public async Task Pressing_a_scene_button_switches_the_scene_everywhere()
	{
		await using var harness = await CreateAsync();

		await harness.VirtualProfiles.SendWidgetInteractionAsync(Press("scene-live"));

		var active = (await harness.Variables.GetAsync("active-scene")).DataAs<VariableValueDto>();
		var isLive = (await harness.Variables.GetAsync("is-live")).DataAs<VariableValueDto>();
		var published = harness.Context.Events.Published[^1];

		Assert.That(active!.Text, Is.EqualTo("Live"));
		Assert.That(isLive!.Boolean, Is.True);
		Assert.That(published.EventId, Is.EqualTo("scene-changed"));
		Assert.That(published.Parameters!.Value.GetProperty("source").GetString(), Is.EqualTo("widget"));
	}

	[Test]
	public async Task The_pressed_scene_is_the_one_the_profile_marks_as_active()
	{
		await using var harness = await CreateAsync();

		await harness.VirtualProfiles.SendWidgetInteractionAsync(Press("scene-break"));

		var scenes = (await harness.VirtualProfiles.GetProfilesAsync())
			.DataAs<VirtualProfilesResult>()!.Profiles.Single().Folders.Single(folder => folder.Id == "scenes");

		// The active scene is the one drawn in its own colour; the rest share the inactive one.
		var highlighted = scenes.Widgets
			.Where(widget => !widget.Data!.Contains("#20242B", StringComparison.Ordinal))
			.Select(widget => widget.Id)
			.ToArray();

		Assert.That(highlighted, Is.EqualTo(_breakOnly));
	}

	[Test]
	public async Task The_scene_is_written_into_a_host_variable_the_plugin_owns()
	{
		await using var harness = await CreateAsync();

		await harness.VirtualProfiles.SendWidgetInteractionAsync(Press("scene-standby"));

		var created = harness.Context.Variables.Created.Single();
		var current = await harness.Context.Variables.GetByNameAsync("sample_control_room_scene");

		Assert.That(created.Name, Is.EqualTo("sample_control_room_scene"));
		Assert.That(current!.Value, Is.EqualTo("Standby"));
	}

	[Test]
	public async Task Switching_twice_updates_the_variable_instead_of_creating_a_second_one()
	{
		await using var harness = await CreateAsync();

		await harness.VirtualProfiles.SendWidgetInteractionAsync(Press("scene-standby"));
		await harness.VirtualProfiles.SendWidgetInteractionAsync(Press("scene-live"));

		Assert.That(harness.Context.Variables.Created, Has.Count.EqualTo(1));
		Assert.That((await harness.Context.Variables.GetByNameAsync("sample_control_room_scene"))!.Value, Is.EqualTo("Live"));
	}

	[Test]
	public async Task A_press_on_a_widget_that_is_not_a_scene_button_changes_nothing()
	{
		await using var harness = await CreateAsync();
		await harness.VirtualProfiles.SendWidgetInteractionAsync(Press("scene-live"));
		var eventsBefore = harness.Context.Events.Published.Count;

		var outcome = await harness.VirtualProfiles.SendWidgetInteractionAsync(
			Press("status-clock", folderId: "status"));

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(harness.Context.Events.Published, Has.Count.EqualTo(eventsBefore));
		Assert.That((await harness.Variables.GetAsync("active-scene")).DataAs<VariableValueDto>()!.Text, Is.EqualTo("Live"));
	}

	[Test]
	public async Task The_action_and_the_button_reach_the_same_scene()
	{
		await using var harness = await CreateAsync();

		await harness.Actions.ExecuteAsync("set-scene", new Dictionary<string, object?> { ["scene"] = "break" });

		var active = (await harness.Variables.GetAsync("active-scene")).DataAs<VariableValueDto>();
		Assert.That(active!.Text, Is.EqualTo("Break"));
		Assert.That(harness.Context.Events.Published[^1].Parameters!.Value.GetProperty("source").GetString(),
			Is.EqualTo("action"));
	}

	[Test]
	public async Task An_unknown_scene_fails_rather_than_switching_to_a_default()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("set-scene", new Dictionary<string, object?> { ["scene"] = "party" });

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That((await harness.Variables.GetAsync("active-scene")).DataAs<VariableValueDto>()!.Text, Is.EqualTo("Offline"));
	}

	[Test]
	public async Task Styling_falls_back_to_the_widget_that_triggered_the_run()
	{
		await using var harness = await CreateAsync();
		harness.Context.Widgets.Seed(new WidgetTargetInfo
		{
			Id = "widget-1",
			Label = "On air",
			Location = "Main",
			Type = "ActionButton"
		});

		var outcome = await harness.Actions.ExecuteAsync("style-widget",
			new Dictionary<string, object?> { ["label"] = "ON AIR", ["backgroundColor"] = "#D0021B" },
			ownerWidgetId: "widget-1");

		var applied = harness.Context.Widgets.LastApplied["widget-1"];

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(applied.Patch.Label, Is.EqualTo("ON AIR"));
		Assert.That(applied.Patch.BackgroundColor, Is.EqualTo("#D0021B"));
	}

	[Test]
	public async Task Styling_a_widget_the_host_does_not_know_fails()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("style-widget",
			new Dictionary<string, object?> { ["widget"] = "widget-404", ["label"] = "Nope" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Styling_without_a_widget_to_fall_back_on_fails_rather_than_guessing()
	{
		await using var harness = await CreateAsync();

		// No owner widget: the same shape a script or an automation runs an action with.
		var outcome = await harness.Actions.ExecuteAsync("style-widget",
			new Dictionary<string, object?> { ["label"] = "Nope" });

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(harness.Context.Widgets.LastApplied, Is.Empty);
	}

	[Test]
	public async Task The_reset_colour_clears_the_override_instead_of_setting_one()
	{
		await using var harness = await CreateAsync();
		harness.Context.Widgets.Seed(new WidgetTargetInfo
		{
			Id = "widget-1",
			Label = "On air",
			Location = "Main",
			Type = "ActionButton"
		});

		await harness.Actions.ExecuteAsync("style-widget", new Dictionary<string, object?>
		{
			["widget"] = "widget-1",
			["label"] = "Studio",
			["backgroundColor"] = WidgetAppearanceValues.Reset
		});

		var applied = harness.Context.Widgets.LastApplied["widget-1"];

		Assert.That(applied.ClearProperties, Does.Contain(WidgetAppearanceProperty.BackgroundColor));
		Assert.That(applied.Patch.BackgroundColor, Is.Null);
	}

	[Test]
	public async Task Announcing_replaces_the_previous_notification_under_the_same_key()
	{
		await using var harness = await CreateAsync();

		await harness.Actions.ExecuteAsync("announce",
			new Dictionary<string, object?> { ["title"] = "First", ["key"] = "studio" });
		await harness.Actions.ExecuteAsync("announce",
			new Dictionary<string, object?> { ["title"] = "Second", ["key"] = "studio", ["level"] = "Warning" });

		var current = harness.Context.Notifications.Current.Single(notification => notification.Key == "studio");

		Assert.That(current.Title, Is.EqualTo("Second"));
		Assert.That(current.Level, Is.EqualTo(Sdk.Notifications.UserNotificationLevel.Warning));
	}

	[Test]
	public async Task Announcing_without_a_title_fails()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("announce", new Dictionary<string, object?> { ["message"] = "Body only" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Running_a_script_reports_what_the_script_reported()
	{
		await using var harness = await CreateAsync();
		harness.Context.Scripts.Seed(new Script { Id = "script-1", Name = "Start stream" });
		harness.Context.Scripts.Seed(new Script { Id = "script-2", Name = "Broken" },
			() => Sdk.Actions.ActionResult.Failed("PROVIDER_ERROR", "The script threw."));

		var options = (await harness.Actions.GetOptionsAsync("run-script", "scriptId")).DataAs<DynamicOptionsResultDto>();
		var succeeded = await harness.Actions.ExecuteAsync("run-script", new Dictionary<string, object?> { ["scriptId"] = "script-1" });
		var failed = await harness.Actions.ExecuteAsync("run-script", new Dictionary<string, object?> { ["scriptId"] = "script-2" });

		Assert.That(options!.Options.Select(option => option.Label), Is.EquivalentTo(_scriptNames));
		Assert.That(succeeded.Succeeded, Is.True);
		Assert.That(harness.Context.Scripts.Ran, Does.Contain("script-1"));
		Assert.That(failed.Succeeded, Is.False);
	}

	[Test]
	public async Task Navigating_targets_the_client_that_pressed_the_button()
	{
		await using var harness = await CreateAsync();
		harness.Context.Deck.SeedFolders(new DeckFolder { Id = "folder-1", Label = "Studio" });

		var options = (await harness.Actions.GetOptionsAsync("navigate-deck",
			"targetId",
			currentParameters: new Dictionary<string, object?> { ["kind"] = "folder" })).DataAs<DynamicOptionsResultDto>();

		var outcome = await harness.Actions.ExecuteAsync("navigate-deck",
			new Dictionary<string, object?> { ["kind"] = "folder", ["targetId"] = "folder-1" },
			originClientId: "client-3");

		var call = harness.Context.Deck.Calls.Single();

		Assert.That(options!.Options.Single().Label, Is.EqualTo("Studio"));
		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(call.Id, Is.EqualTo("folder-1"));
		Assert.That(call.OriginClientId, Is.EqualTo("client-3"));
	}

	[Test]
	public async Task Navigating_back_needs_no_target()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("navigate-deck", new Dictionary<string, object?> { ["kind"] = "back" });

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(harness.Context.Deck.Calls.Single().Kind, Is.EqualTo(DeckNavigationKind.GoBack));
	}

	[Test]
	public async Task Opening_a_folder_without_naming_one_fails_before_the_host_is_called()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("navigate-deck", new Dictionary<string, object?> { ["kind"] = "folder" });

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(harness.Context.Deck.Calls, Is.Empty);
	}

	private static WidgetInteractionArguments Press(string widgetId, string folderId = "scenes") => new()
	{
		ProfileId = "control-room",
		FolderId = folderId,
		WidgetId = widgetId,
		TriggerType = "onShortPress"
	};

	private static async Task<PluginTestHarness> CreateAsync()
	{
		var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<ControlRoomIntegration>());
		await harness.InitializeIntegrationsAsync();
		return harness;
	}
}
