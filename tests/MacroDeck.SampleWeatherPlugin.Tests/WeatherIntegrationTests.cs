using System.Text.Json;
using MacroDeck.Plugin.Protocol.Capabilities.Actions;
using MacroDeck.Plugin.Protocol.Capabilities.Variables;
using MacroDeck.Plugin.Protocol.Capabilities.Weather;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace MacroDeck.SampleWeatherPlugin.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where a plugin author tests what their integration does.
/// </summary>
[TestFixture]
public sealed class WeatherIntegrationTests
{
	private const string StationInstanceId = "primary";

	[Test]
	public async Task The_configured_location_is_what_the_station_and_the_variable_report()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());

		var entryId = harness.Context.Config.AddEntry("Sample");
		harness.Context.Config.SeedString(entryId, "location", "Reykjavík, Iceland");

		await harness.InitializeIntegrationsAsync();

		var instances = (await harness.Weather.GetInstancesAsync()).DataAs<WeatherInstancesResult>();
		var location = (await harness.Variables.GetAsync("location")).DataAs<VariableValueDto>();

		Assert.That(instances!.Instances.Single().DisplayName, Is.EqualTo("Reykjavík, Iceland"));
		Assert.That(location!.Text, Is.EqualTo("Reykjavík, Iceland"));
	}

	[Test]
	public async Task An_unconfigured_plugin_still_reports_a_reading()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		var snapshot = await SnapshotAsync(harness);

		Assert.That(snapshot.IsAvailable, Is.True);
		Assert.That(snapshot.Temperature, Is.Not.Null);
	}

	[Test]
	public async Task Refreshing_changes_the_reading_and_publishes_it()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		var before = await SnapshotAsync(harness);
		var outcome = await harness.Actions.ExecuteAsync("refresh-weather");
		var after = await SnapshotAsync(harness);

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(after.Temperature, Is.Not.EqualTo(before.Temperature));

		var published = harness.Context.Events.Published[^1];
		Assert.That(published.EventId, Is.EqualTo("weather-refreshed"));
		Assert.That(Payload(published.Parameters).GetProperty("temperatureCelsius").GetDouble(), Is.EqualTo(after.Temperature));
		Assert.That(Payload(published.Parameters).GetProperty("condition").GetString(), Is.EqualTo(after.Condition));
	}

	[Test]
	public async Task The_temperature_variable_agrees_with_the_station()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		await harness.Actions.ExecuteAsync("refresh-weather");

		var snapshot = await SnapshotAsync(harness);
		var temperature = (await harness.Variables.GetAsync("temperature-celsius")).DataAs<VariableValueDto>();

		Assert.That(temperature!.Number, Is.EqualTo(snapshot.Temperature));
	}

	[Test]
	public async Task The_slider_reads_back_the_threshold_it_was_dragged_to()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		await harness.Actions.ExecuteAsync("set-alert-threshold",
			new Dictionary<string, object?> { ["thresholdCelsius"] = 12.0 });

		var state = (await harness.Actions.GetSliderStateAsync("set-alert-threshold")).DataAs<SliderStateResult>();

		Assert.That(state!.HasValue, Is.True);
		Assert.That(state.Value, Is.EqualTo(12));
	}

	[Test]
	public async Task A_threshold_that_is_not_a_number_fails_rather_than_being_ignored()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("set-alert-threshold",
			new Dictionary<string, object?> { ["thresholdCelsius"] = "warm" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task The_alert_flag_follows_the_configured_threshold()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		// Below every reading the synthetic station can produce, so the next refresh has to report an alert.
		await harness.Actions.ExecuteAsync("set-alert-threshold",
			new Dictionary<string, object?> { ["thresholdCelsius"] = -50.0 });
		await harness.Actions.ExecuteAsync("refresh-weather");

		var payload = Payload(harness.Context.Events.Published[^1].Parameters);
		Assert.That(payload.GetProperty("isAlert").GetBoolean(), Is.True);
	}

	[Test]
	public async Task A_forced_condition_is_what_the_next_reading_reports()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		var options = (await harness.Actions.GetOptionsAsync("set-condition", "condition")).DataAs<DynamicOptionsResultDto>();
		Assert.That(options!.Options.Select(option => option.Value), Does.Contain("Rain"));

		await harness.Actions.ExecuteAsync("set-condition", new Dictionary<string, object?> { ["condition"] = "Rain" });
		await harness.Actions.ExecuteAsync("refresh-weather");

		Assert.That((await SnapshotAsync(harness)).Condition, Is.EqualTo("Rain"));
	}

	[Test]
	public async Task An_unknown_condition_fails_rather_than_forcing_something_else()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("set-condition",
			new Dictionary<string, object?> { ["condition"] = "Sandstorm" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task An_instance_the_provider_never_declared_has_no_station()
	{
		await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<WeatherIntegration>());
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Weather.GetSnapshotAsync(new WeatherInstanceArguments { InstanceId = "second-station" });

		// The plugin says "no such station"; it is the host's adapter that turns that into an
		// unavailable snapshot rather than an error reaching a widget.
		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error!.Code, Is.EqualTo("CAPABILITY_UNAVAILABLE"));
	}

	private static JsonElement Payload(JsonElement? parameters) => parameters!.Value;

	private static async Task<WeatherSnapshotDto> SnapshotAsync(PluginTestHarness harness)
	{
		var outcome = await harness.Weather.GetSnapshotAsync(new WeatherInstanceArguments { InstanceId = StationInstanceId });
		return outcome.DataAs<WeatherSnapshotDto>()!;
	}
}
