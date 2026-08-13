using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace MacroDeck.SampleWeatherPlugin.Tests;

/// <summary>
/// The end-to-end level: a real child process, launched the way the supervisor launches an installed
/// plugin. Worth one test rather than a suite - what it proves is that the built artifact starts,
/// registers and shuts down cleanly, which no in-process subject can show.
/// </summary>
[TestFixture]
public sealed class WeatherProcessTests
{
	[Test]
	public async Task The_built_plugin_starts_registers_and_stops_cleanly()
	{
		// The plugin project is a ProjectReference, so its executable and its manifest.json sit next to
		// the test assembly - the content root the SDK reads its identity from.
		var executable = Path.Combine(AppContext.BaseDirectory,
			OperatingSystem.IsWindows() ? "MacroDeck.SampleWeatherPlugin.exe" : "MacroDeck.SampleWeatherPlugin");

		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.LaunchAsync(PluginLaunchSpec.ForExecutable(executable));

		var session = await host.WaitForSessionAsync(TimeSpan.FromSeconds(30));
		var outcome = await session.Actions.ExecuteAsync("refresh-weather");
		Assert.That(outcome.Succeeded, Is.True);

		var report = await plugin.StopGracefullyAsync(TimeSpan.FromSeconds(10));
		Assert.That(report.ExitedWithinGrace, Is.True);
		Assert.That(report.Killed, Is.False);
	}
}
