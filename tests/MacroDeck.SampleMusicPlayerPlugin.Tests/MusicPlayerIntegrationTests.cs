using System.Text.Json;
using MacroDeck.Plugin.Protocol.Capabilities.Actions;
using MacroDeck.Plugin.Protocol.Capabilities.Events;
using MacroDeck.Plugin.Protocol.Capabilities.MusicPlayer;
using MacroDeck.Plugin.Protocol.Capabilities.Variables;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace MacroDeck.SampleMusicPlayerPlugin.Tests;

[TestFixture]
public sealed class MusicPlayerIntegrationTests
{
	private const string LibraryId = "library";
	private const string SpeakerId = "speaker";

	private static readonly string[] _eveningOnly = ["Evening"];
	private static readonly string[] _playlistIds = ["playlist-focus", "playlist-evening"];
	private static readonly string[] _instanceIds = [LibraryId, SpeakerId];

	[Test]
	public async Task Only_the_instance_that_implements_them_advertises_catalog_and_devices()
	{
		await using var harness = await CreateAsync();

		var instances = (await harness.MusicPlayer.GetInstancesAsync()).DataAs<MusicPlayerInstancesResult>();

		var library = instances!.Instances.Single(instance => instance.Id == LibraryId);
		var speaker = instances.Instances.Single(instance => instance.Id == SpeakerId);

		Assert.That(library.HasCatalog, Is.True);
		Assert.That(library.HasDevices, Is.True);
		Assert.That(speaker.HasCatalog, Is.False);
		Assert.That(speaker.HasDevices, Is.False);
	}

	[Test]
	public async Task Toggling_starts_playback_and_toggling_again_stops_it()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.ToggleAsync(Instance(LibraryId));
		Assert.That((await StateAsync(harness, LibraryId)).PlaybackState, Is.EqualTo("Playing"));

		await harness.MusicPlayer.ToggleAsync(Instance(LibraryId));
		Assert.That((await StateAsync(harness, LibraryId)).PlaybackState, Is.EqualTo("Paused"));
	}

	[Test]
	public async Task Position_follows_the_clock_while_playing_and_holds_while_paused()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.PlayAsync(Instance(LibraryId));
		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.That((await StateAsync(harness, LibraryId)).PositionSeconds, Is.EqualTo(30).Within(0.5));

		await harness.MusicPlayer.PauseAsync(Instance(LibraryId));
		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.That((await StateAsync(harness, LibraryId)).PositionSeconds, Is.EqualTo(30).Within(0.5));
	}

	[Test]
	public async Task Seeking_past_the_end_stops_at_the_track_length()
	{
		await using var harness = await CreateAsync();

		var duration = (await StateAsync(harness, LibraryId)).DurationSeconds;
		await harness.MusicPlayer.SeekAsync(new MusicPlayerSeekArguments
		{
			InstanceId = LibraryId,
			PositionSeconds = duration!.Value + 600
		});

		Assert.That((await StateAsync(harness, LibraryId)).PositionSeconds, Is.EqualTo(duration));
	}

	[Test]
	public async Task Skipping_moves_to_another_track_and_publishes_it()
	{
		await using var harness = await CreateAsync();

		var before = await StateAsync(harness, LibraryId);
		await harness.MusicPlayer.NextAsync(Instance(LibraryId));
		var after = await StateAsync(harness, LibraryId);

		Assert.That(after.TrackName, Is.Not.EqualTo(before.TrackName));

		var published = harness.Context.Events.Published[^1];
		Assert.That(published.EventId, Is.EqualTo("track-changed"));
		Assert.That(published.Parameters!.Value.GetProperty("player").GetString(), Is.EqualTo(LibraryId));
		Assert.That(published.Parameters.Value.GetProperty("track").GetString(), Is.EqualTo(after.TrackName));
	}

	[Test]
	public async Task The_two_instances_play_independently()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.PlayAsync(Instance(LibraryId));

		Assert.That((await StateAsync(harness, LibraryId)).PlaybackState, Is.EqualTo("Playing"));
		Assert.That((await StateAsync(harness, SpeakerId)).PlaybackState, Is.EqualTo("Paused"));
	}

	[Test]
	public async Task The_variables_report_what_the_player_is_playing()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.SetVolumeAsync(new MusicPlayerVolumeArguments { InstanceId = LibraryId, VolumePercent = 35 });
		await harness.MusicPlayer.PlayAsync(Instance(LibraryId));

		var state = await StateAsync(harness, LibraryId);
		var track = (await harness.Variables.GetAsync("track")).DataAs<VariableValueDto>();
		var playing = (await harness.Variables.GetAsync("is-playing")).DataAs<VariableValueDto>();
		var volume = (await harness.Variables.GetAsync("volume")).DataAs<VariableValueDto>();

		Assert.That(track!.Text, Is.EqualTo(state.TrackName));
		Assert.That(playing!.Boolean, Is.True);
		Assert.That(volume!.Number, Is.EqualTo(35));
	}

	[Test]
	public async Task A_volume_outside_the_range_is_clamped_rather_than_rejected()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.SetVolumeAsync(new MusicPlayerVolumeArguments { InstanceId = LibraryId, VolumePercent = 140 });

		Assert.That((await StateAsync(harness, LibraryId)).VolumePercent, Is.EqualTo(100));
	}

	[Test]
	public async Task Browsing_the_catalog_filters_by_kind_and_text()
	{
		await using var harness = await CreateAsync();

		var tracks = (await harness.MusicPlayer.GetCatalogAsync(new MusicPlayerCatalogArguments
		{
			InstanceId = LibraryId,
			Kind = "Track"
		})).DataAs<MusicPlayerCatalogResult>();

		var playlists = (await harness.MusicPlayer.GetCatalogAsync(new MusicPlayerCatalogArguments
		{
			InstanceId = LibraryId,
			Kind = "Playlist",
			Filter = "even"
		})).DataAs<MusicPlayerCatalogResult>();

		Assert.That(tracks!.Items, Is.Not.Empty);
		Assert.That(tracks.Items.Select(item => item.Kind), Is.All.EqualTo("Track"));
		Assert.That(playlists!.Items.Select(item => item.Title), Is.EqualTo(_eveningOnly));
	}

	[Test]
	public async Task Playing_a_playlist_starts_its_first_track()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.PlayItemAsync(new MusicPlayerPlayItemArguments
		{
			InstanceId = LibraryId,
			Item = new MusicPlayerCatalogItemDto { Id = "playlist-evening", Title = "Evening", Kind = "Playlist" }
		});

		var state = await StateAsync(harness, LibraryId);
		Assert.That(state.TrackName, Is.EqualTo("Night Transit"));
		Assert.That(state.PlaybackState, Is.EqualTo("Playing"));
	}

	[Test]
	public async Task Artwork_is_served_for_the_id_the_state_reported()
	{
		await using var harness = await CreateAsync();

		var state = await StateAsync(harness, LibraryId);
		var artwork = (await harness.MusicPlayer.GetArtworkAsync(new MusicPlayerArtworkArguments
		{
			InstanceId = LibraryId,
			ArtworkId = state.ArtworkId!
		})).DataAs<MusicPlayerArtworkResult>();

		Assert.That(artwork!.MimeType, Is.EqualTo("image/svg+xml"));
		Assert.That(artwork.Data, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task Transferring_playback_makes_the_target_device_the_active_one()
	{
		await using var harness = await CreateAsync();

		await harness.MusicPlayer.TransferAsync(new MusicPlayerTransferArguments
		{
			InstanceId = LibraryId,
			DeviceId = "device-kitchen",
			StartPlayback = true
		});

		var devices = (await harness.MusicPlayer.GetDevicesAsync(Instance(LibraryId))).DataAs<MusicPlayerDevicesResult>();
		var state = await StateAsync(harness, LibraryId);

		Assert.That(devices!.Devices.Single(device => device.IsActive).Id, Is.EqualTo("device-kitchen"));
		Assert.That(state.DeviceName, Is.EqualTo("Kitchen"));
		Assert.That(state.PlaybackState, Is.EqualTo("Playing"));
	}

	[Test]
	public async Task The_transport_action_reaches_the_same_state_the_capability_does()
	{
		await using var harness = await CreateAsync();

		await harness.Actions.ExecuteAsync("toggle-playback", new Dictionary<string, object?> { ["player"] = LibraryId });

		Assert.That((await StateAsync(harness, LibraryId)).PlaybackState, Is.EqualTo("Playing"));
	}

	[Test]
	public async Task An_action_naming_an_unknown_player_fails()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("toggle-playback",
			new Dictionary<string, object?> { ["player"] = "kitchen-radio" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task The_volume_slider_reads_back_the_selected_players_volume()
	{
		await using var harness = await CreateAsync();

		await harness.Actions.ExecuteAsync("set-volume",
			new Dictionary<string, object?> { ["player"] = SpeakerId, ["volume"] = 25.0 });

		var state = (await harness.Actions.GetSliderStateAsync("set-volume",
			new Dictionary<string, object?> { ["player"] = SpeakerId })).DataAs<SliderStateResult>();

		Assert.That(state!.Value, Is.EqualTo(25));
		Assert.That((await StateAsync(harness, LibraryId)).VolumePercent, Is.EqualTo(60), "the other player is untouched");
	}

	[Test]
	public async Task The_slider_has_no_state_when_no_player_is_chosen_yet()
	{
		await using var harness = await CreateAsync();

		var state = (await harness.Actions.GetSliderStateAsync("set-volume")).DataAs<SliderStateResult>();

		Assert.That(state!.HasValue, Is.False);
	}

	[Test]
	public async Task Item_options_follow_the_kind_that_is_already_selected()
	{
		await using var harness = await CreateAsync();

		var options = (await harness.Actions.GetOptionsAsync("play-catalog-item",
			"item",
			currentParameters: new Dictionary<string, object?> { ["kind"] = "Playlist" })).DataAs<DynamicOptionsResultDto>();

		Assert.That(options!.Options.Select(option => option.Value), Is.EquivalentTo(_playlistIds));
	}

	// A picker request is fire-and-forget and leaves no result to read, so what these two assert is the
	// half a caller can observe: the run is accepted rather than failed, and nothing was played or
	// transferred on a guess.
	[Test]
	public async Task Leaving_the_item_empty_asks_the_client_to_pick_one()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("play-catalog-item",
			new Dictionary<string, object?> { ["kind"] = "Track" },
			originClientId: "client-7");

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(outcome.DataAs<ActionExecuteResult>()!.Accepted, Is.True);
		Assert.That((await StateAsync(harness, LibraryId)).PlaybackState, Is.EqualTo("Paused"));
	}

	[Test]
	public async Task Leaving_the_device_empty_asks_the_client_to_pick_one()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("transfer-playback",
			new Dictionary<string, object?> { ["startPlayback"] = false },
			originClientId: "client-7");

		var devices = (await harness.MusicPlayer.GetDevicesAsync(Instance(LibraryId))).DataAs<MusicPlayerDevicesResult>();

		Assert.That(outcome.DataAs<ActionExecuteResult>()!.Accepted, Is.True);
		Assert.That(devices!.Devices.Single(device => device.IsActive).Id, Is.EqualTo("device-living-room"));
	}

	[Test]
	public async Task Transferring_to_an_unknown_device_fails_rather_than_doing_nothing_quietly()
	{
		await using var harness = await CreateAsync();

		var outcome = await harness.Actions.ExecuteAsync("transfer-playback",
			new Dictionary<string, object?> { ["device"] = "device-garden" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Event_options_offer_the_players_a_subscription_can_name()
	{
		await using var harness = await CreateAsync();

		var options = (await harness.Events.GetOptionsAsync(new EventOptionsArguments
		{
			EventId = "track-changed",
			ParameterName = "player"
		})).DataAs<DynamicOptionsResultDto>();

		Assert.That(options!.Options.Select(option => option.Value), Is.EquivalentTo(_instanceIds));
	}

	private static async Task<PluginTestHarness> CreateAsync()
	{
		var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<MusicPlayerIntegration>());
		await harness.InitializeIntegrationsAsync();
		return harness;
	}

	private static MusicPlayerInstanceArguments Instance(string instanceId) => new() { InstanceId = instanceId };

	private static async Task<MusicPlayerStateDto> StateAsync(PluginTestHarness harness, string instanceId)
		=> (await harness.MusicPlayer.GetStateAsync(Instance(instanceId))).DataAs<MusicPlayerStateDto>()!;
}
