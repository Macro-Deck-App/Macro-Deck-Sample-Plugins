using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using MacroDeck.SampleRestApiPlugin;
using MacroDeck.SampleRestApiPlugin.Api;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
// Strings is generated from Localization/*.resx, so UseLocalization is what makes every LocalizedText
// this plugin hands the host resolve in the reader's language rather than falling back to its key.
var builder = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.UseLocalization(Strings.LocalizationCatalog)
	.RegisterIntegration<RestApiIntegration>();

// The typed client is an ordinary IHttpClientFactory registration - a plugin is a normal .NET host, so
// nothing about DI changes here.
builder.Services.AddTaskBoardApi();

var plugin = builder.Build();

await plugin.RunAsync();
