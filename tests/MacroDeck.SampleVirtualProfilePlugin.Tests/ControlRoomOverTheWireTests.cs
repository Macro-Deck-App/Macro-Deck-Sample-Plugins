using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Protocol.Capabilities.VirtualProfiles;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace MacroDeck.SampleVirtualProfilePlugin.Tests;

/// <summary>
/// Virtual profiles over a real socket, including the one operation with no reply: a widget
/// interaction is fire-and-forget, so what proves it arrived is the profile that comes back next.
/// </summary>
[TestFixture]
public sealed class ControlRoomOverTheWireTests
{
	[Test]
	public async Task A_widget_interaction_arrives_and_the_next_profile_read_shows_it()
	{
		var builder = MacroDeckPlugin.CreatePlugin()
			.UseMacroDeckLogging()
			.RegisterIntegration<ControlRoomIntegration>();

		await using var host = await MacroDeckTestHost.StartAsync();
		await using var plugin = await host.HostAsync(builder);
		var session = await host.WaitForSessionAsync();

		var outcome = await session.VirtualProfiles.SendWidgetInteractionAsync(new WidgetInteractionArguments
		{
			ProfileId = "control-room",
			FolderId = "scenes",
			WidgetId = "scene-live",
			TriggerType = "onShortPress"
		});
		Assert.That(outcome.Succeeded, Is.True);

		await host.Events.WaitForAsync("scene-changed", TimeSpan.FromSeconds(5));

		var profiles = (await session.VirtualProfiles.GetProfilesAsync()).DataAs<VirtualProfilesResult>();
		var live = profiles!.Profiles.Single()
			.Folders.Single(folder => folder.Id == "scenes")
			.Widgets.Single(widget => widget.Id == "scene-live");

		Assert.That(live.Data, Does.Contain("#D0021B"));
	}
}
