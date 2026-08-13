using MacroDeck.Plugin.Hosting;
using MacroDeck.SampleWeatherPlugin;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
var plugin = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.RegisterIntegration<WeatherIntegration>()
	.Build();

await plugin.RunAsync();
