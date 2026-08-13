namespace MacroDeck.SampleRestApiPlugin.Api;

/// <summary>
/// What the config flow produced, held as a singleton so the typed client can be registered before any
/// of it is known. A plugin is configured after it starts, so the base address and the token cannot be
/// baked into <c>AddHttpClient</c> at registration time.
/// </summary>
public sealed class TaskBoardCredentials
{
	private volatile Snapshot _current = new(null, null);

	public bool IsConfigured => _current is { BaseAddress: not null, Token: not null };

	public Uri? BaseAddress => _current.BaseAddress;

	public string? Token => _current.Token;

	public void Apply(Uri? baseAddress, string? token) => _current = new Snapshot(baseAddress, token);

	public void Clear() => _current = new Snapshot(null, null);

	private sealed record Snapshot(Uri? BaseAddress, string? Token);
}
