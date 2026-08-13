using MacroDeck.Plugin.Hosting;
using MacroDeck.SampleRestApiPlugin;
using MacroDeck.SampleRestApiPlugin.Api;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
var builder = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.RegisterIntegration<RestApiIntegration>();

// The typed client is an ordinary IHttpClientFactory registration - a plugin is a normal .NET host, so
// nothing about DI changes here.
builder.Services.AddTaskBoardApi();

var plugin = builder.Build();

await plugin.RunAsync();
