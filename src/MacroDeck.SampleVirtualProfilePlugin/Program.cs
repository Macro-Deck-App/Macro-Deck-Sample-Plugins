using MacroDeck.Plugin.Hosting;
using MacroDeck.SampleVirtualProfilePlugin;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
var plugin = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.RegisterIntegration<ControlRoomIntegration>()
	.Build();

await plugin.RunAsync();
