using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MacroDeck.SampleRestApiPlugin.Api;

/// <summary>
/// The typed client, registered with <c>AddHttpClient&lt;TaskBoardClient&gt;()</c> so the handler
/// lifetime and connection pooling are the factory's problem rather than this class's. Everything a
/// caller can do to the remote service goes through here; nothing else in the plugin sees
/// <see cref="HttpClient"/>.
/// </summary>
public sealed class TaskBoardClient(HttpClient httpClient, TaskBoardCredentials credentials)
{
	public Task<IReadOnlyList<TaskBoardList>> GetListsAsync(CancellationToken cancellationToken)
		=> GetAsync<IReadOnlyList<TaskBoardList>>("lists", cancellationToken);

	public Task<IReadOnlyList<TaskBoardCard>> GetCardsAsync(bool openOnly, CancellationToken cancellationToken)
		=> GetAsync<IReadOnlyList<TaskBoardCard>>($"cards?open={(openOnly ? "true" : "false")}", cancellationToken);

	public async Task<TaskBoardCard> CreateCardAsync(CreateCardRequest request, CancellationToken cancellationToken)
	{
		using var message = CreateMessage(HttpMethod.Post, "cards");
		message.Content = JsonContent.Create(request);
		return await SendAsync<TaskBoardCard>(message, cancellationToken);
	}

	public async Task<TaskBoardCard> CompleteCardAsync(string cardId, CancellationToken cancellationToken)
	{
		using var message = CreateMessage(HttpMethod.Post, $"cards/{Uri.EscapeDataString(cardId)}/complete");
		return await SendAsync<TaskBoardCard>(message, cancellationToken);
	}

	/// <summary>A cheap round trip that tells "wrong token" apart from "service down". It takes the values
	/// explicitly so the config flow can check what a user just typed without applying it first.</summary>
	public async Task VerifyAsync(Uri baseAddress, string token, CancellationToken cancellationToken)
	{
		using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, "lists"));
		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		using var response = await SendCoreAsync(message, cancellationToken);
	}

	/// <summary>The same check against the configured values, used by the issue provider.</summary>
	public async Task VerifyAsync(CancellationToken cancellationToken)
	{
		using var message = CreateMessage(HttpMethod.Get, "lists");
		using var response = await SendCoreAsync(message, cancellationToken);
	}

	private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
	{
		using var message = CreateMessage(HttpMethod.Get, path);
		return await SendAsync<T>(message, cancellationToken);
	}

	private async Task<T> SendAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
	{
		using var response = await SendCoreAsync(message, cancellationToken);

		var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
		return payload ?? throw new TaskBoardException(TaskBoardFailure.ServerError,
			"The Task Board API answered with an empty body.");
	}

	private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage message, CancellationToken cancellationToken)
	{
		HttpResponseMessage response;
		try
		{
			response = await httpClient.SendAsync(message, cancellationToken);
		}
		catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
		{
			// A cancelled request the caller did not cancel is the client's own timeout.
			throw new TaskBoardException(TaskBoardFailure.Timeout, "The Task Board API did not answer in time.", exception);
		}
		catch (HttpRequestException exception)
		{
			throw new TaskBoardException(TaskBoardFailure.Unreachable, "The Task Board API is unreachable.", exception);
		}

		if (!response.IsSuccessStatusCode)
		{
			var failure = TaskBoardException.FromStatus(response.StatusCode);
			response.Dispose();
			throw failure;
		}

		return response;
	}

	private HttpRequestMessage CreateMessage(HttpMethod method, string path)
	{
		if (credentials is not { BaseAddress: { } baseAddress, Token: { } token })
		{
			throw new TaskBoardException(TaskBoardFailure.NotConfigured, "The Task Board integration is not configured yet.");
		}

		var message = new HttpRequestMessage(method, new Uri(baseAddress, path));
		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return message;
	}
}
