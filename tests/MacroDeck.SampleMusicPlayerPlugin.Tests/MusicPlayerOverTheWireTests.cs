using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Protocol.Capabilities.MusicPlayer;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace MacroDeck.SampleMusicPlayerPlugin.Tests;

/// <summary>
/// The music player over a real socket. State, catalogue items and artwork are the three shapes whose
/// serialization is worth proving - a <c>TimeSpan</c> position, an enum and a byte payload all change
/// form on the way across.
/// </summary>
[TestFixture]
public sealed class MusicPlayerOverTheWireTests
{
	[Test]
	public async Task Playback_state_survives_the_trip()
	{
		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.HostAsync(Builder());
		var session = await host.WaitForSessionAsync();

		await session.MusicPlayer.PlayAsync(new MusicPlayerInstanceArguments { InstanceId = "library" });
		var state = (await session.MusicPlayer.GetStateAsync(new MusicPlayerInstanceArguments { InstanceId = "library" }))
			.DataAs<MusicPlayerStateDto>();

		Assert.That(state!.PlaybackState, Is.EqualTo("Playing"));
		Assert.That(state.RepeatMode, Is.EqualTo("Off"));
		Assert.That(state.DurationSeconds, Is.GreaterThan(0));
	}

	[Test]
	public async Task The_catalog_and_its_artwork_survive_the_trip()
	{
		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.HostAsync(Builder());
		var session = await host.WaitForSessionAsync();

		var catalog = (await session.MusicPlayer.GetCatalogAsync(new MusicPlayerCatalogArguments
		{
			InstanceId = "library",
			Kind = "Track"
		})).DataAs<MusicPlayerCatalogResult>();

		var first = catalog!.Items[0];
		var artwork = (await session.MusicPlayer.GetArtworkAsync(new MusicPlayerArtworkArguments
		{
			InstanceId = "library",
			ArtworkId = first.ArtworkId!
		})).DataAs<MusicPlayerArtworkResult>();

		Assert.That(first.DurationSeconds, Is.GreaterThan(0));
		// Small artwork travels inline as base64 rather than through the asset pipeline.
		Assert.That(artwork!.Data, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public async Task Browsing_an_instance_without_a_catalog_is_refused_rather_than_answered_empty()
	{
		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.HostAsync(Builder());
		var session = await host.WaitForSessionAsync();

		var outcome = await session.MusicPlayer.GetCatalogAsync(new MusicPlayerCatalogArguments
		{
			InstanceId = "speaker",
			Kind = "Track"
		});

		// "Empty" has to mean genuinely empty for a catalogue, so an instance that cannot browse fails
		// instead of answering with no items.
		Assert.That(outcome.Succeeded, Is.False);
	}

	private static PluginHostBuilder Builder()
		=> MacroDeckPlugin.CreatePlugin()
			.UseMacroDeckLogging()
			.RegisterIntegration<MusicPlayerIntegration>();
}
