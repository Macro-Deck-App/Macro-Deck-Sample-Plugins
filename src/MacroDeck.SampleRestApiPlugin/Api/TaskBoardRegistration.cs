using Microsoft.Extensions.DependencyInjection;

namespace MacroDeck.SampleRestApiPlugin.Api;

/// <summary>
/// The client's registration, in one place so a test can start from the same wiring the plugin runs
/// with and only replace the primary handler.
/// </summary>
public static class TaskBoardRegistration
{
	public static IHttpClientBuilder AddTaskBoardApi(this IServiceCollection services)
	{
		services.AddSingleton<TaskBoardCredentials>();

		// No base address here: it is only known once the config flow has run, so every request builds
		// its own absolute URI from TaskBoardCredentials.
		return services.AddHttpClient<TaskBoardClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
	}
}
