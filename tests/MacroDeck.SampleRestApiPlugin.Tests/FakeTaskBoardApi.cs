using System.Net;
using System.Net.Http.Json;
using MacroDeck.SampleRestApiPlugin.Api;

namespace MacroDeck.SampleRestApiPlugin.Tests;

/// <summary>
/// A deterministic stand-in for the imaginary Task Board service, plugged in as the typed client's
/// primary handler. The plugin's own code is unchanged by this: it still builds real requests and
/// parses real JSON, which is what makes these tests worth more than mocking the client away.
/// </summary>
internal sealed class FakeTaskBoardApi : HttpMessageHandler
{
	internal const string ValidToken = "valid-token";
	internal static readonly Uri BaseAddress = new("https://task-board.test/api/");

	private readonly List<TaskBoardCard> _cards =
	[
		new("card-1", "Write the release notes", "list-inbox", "normal", false, null),
		new("card-2", "Renew the certificate", "list-ops", "high", false, null),
		new("card-3", "Archive last season", "list-inbox", "low", true, null)
	];

	/// <summary>Set to make every request fail the way an unreachable server does.</summary>
	internal bool IsOffline { get; set; }

	internal IReadOnlyList<TaskBoardCard> Cards => _cards;

	internal IReadOnlyList<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		((List<HttpRequestMessage>)Requests).Add(request);

		if (IsOffline)
		{
			throw new HttpRequestException("Connection refused.");
		}

		if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: ValidToken })
		{
			return new HttpResponseMessage(HttpStatusCode.Unauthorized);
		}

		var path = request.RequestUri!.AbsolutePath;

		if (request.Method == HttpMethod.Get && path.EndsWith("/lists", StringComparison.Ordinal))
		{
			return Json(new[] { new TaskBoardList("list-inbox", "Inbox"), new TaskBoardList("list-ops", "Operations") });
		}

		if (request.Method == HttpMethod.Get && path.EndsWith("/cards", StringComparison.Ordinal))
		{
			var openOnly = request.RequestUri.Query.Contains("open=true", StringComparison.Ordinal);
			return Json(_cards.Where(card => !openOnly || !card.Done).ToArray());
		}

		if (request.Method == HttpMethod.Post && path.EndsWith("/cards", StringComparison.Ordinal))
		{
			var body = (await request.Content!.ReadFromJsonAsync<CreateCardRequest>(cancellationToken))!;
			var created = new TaskBoardCard($"card-{_cards.Count + 1}", body.Title, body.ListId, body.Priority, false, body.DueAt);
			_cards.Add(created);
			return Json(created);
		}

		if (request.Method == HttpMethod.Post && path.EndsWith("/complete", StringComparison.Ordinal))
		{
			var id = path.Split('/')[^2];
			var index = _cards.FindIndex(card => card.Id == id);
			if (index < 0)
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			_cards[index] = _cards[index] with { Done = true };
			return Json(_cards[index]);
		}

		return new HttpResponseMessage(HttpStatusCode.NotFound);
	}

	private static HttpResponseMessage Json<T>(T payload)
		=> new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
}
