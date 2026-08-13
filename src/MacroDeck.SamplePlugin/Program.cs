using MacroDeck.Plugin.Hosting;
using MacroDeck.SamplePlugin;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
var plugin = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.RegisterIntegration<SampleIntegration>()
	.Build();

await plugin.RunAsync();
