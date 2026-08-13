using System.Text.Json.Serialization;

namespace MacroDeck.SampleRestApiPlugin.Api;

/// <summary>The request and response bodies of the imaginary Task Board API this sample talks to.</summary>
public sealed record TaskBoardList(
	[property: JsonPropertyName("id")] string Id,
	[property: JsonPropertyName("name")] string Name);

public sealed record TaskBoardCard(
	[property: JsonPropertyName("id")] string Id,
	[property: JsonPropertyName("title")] string Title,
	[property: JsonPropertyName("listId")] string ListId,
	[property: JsonPropertyName("priority")] string Priority,
	[property: JsonPropertyName("done")] bool Done,
	[property: JsonPropertyName("dueAt")] DateTimeOffset? DueAt);

public sealed record CreateCardRequest(
	[property: JsonPropertyName("title")] string Title,
	[property: JsonPropertyName("listId")] string ListId,
	[property: JsonPropertyName("priority")] string Priority,
	[property: JsonPropertyName("notes")] string? Notes,
	[property: JsonPropertyName("dueAt")] DateTimeOffset? DueAt);
