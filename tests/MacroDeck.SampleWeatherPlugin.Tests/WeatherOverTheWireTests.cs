using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Protocol.Capabilities.Actions;
using MacroDeck.Plugin.Protocol.Capabilities.Weather;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace MacroDeck.SampleWeatherPlugin.Tests;

/// <summary>
/// The same plugin over a real WebSocket against a real host implementation. What is under test here
/// is not the integration's logic - the harness tests cover that - but that it survives the trip:
/// registration, serialization and the event batches.
/// </summary>
[TestFixture]
public sealed class WeatherOverTheWireTests
{
	private static readonly string[] _expectedActionIds = ["refresh-weather", "set-alert-threshold", "set-condition"];

	[Test]
	public async Task The_plugin_registers_its_actions_and_serves_a_snapshot()
	{
		var builder = MacroDeckPlugin.CreatePlugin()
			.UseMacroDeckLogging()
			.RegisterIntegration<WeatherIntegration>();

		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.HostAsync(builder);
		var session = await host.WaitForSessionAsync();

		var actions = (await session.Actions.DescribeAsync()).DataAs<ActionCatalogPayload>();
		Assert.That(actions!.Actions.Select(action => action.LocalId), Is.EquivalentTo(_expectedActionIds));

		var snapshot = (await session.Weather.GetSnapshotAsync(new WeatherInstanceArguments { InstanceId = "primary" }))
			.DataAs<WeatherSnapshotDto>();
		Assert.That(snapshot!.IsAvailable, Is.True);
	}

	[Test]
	public async Task Refreshing_reaches_the_host_as_a_published_event()
	{
		var builder = MacroDeckPlugin.CreatePlugin()
			.UseMacroDeckLogging()
			.RegisterIntegration<WeatherIntegration>();

		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.HostAsync(builder);
		var session = await host.WaitForSessionAsync();

		var outcome = await session.Actions.ExecuteAsync("refresh-weather");
		Assert.That(outcome.Succeeded, Is.True);

		// Publishing crosses the socket asynchronously, so this is awaited rather than read.
		await host.Events.WaitForAsync("weather-refreshed", TimeSpan.FromSeconds(5));
	}
}
