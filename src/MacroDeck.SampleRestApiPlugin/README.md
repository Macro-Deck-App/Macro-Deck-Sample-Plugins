# Sample Task Board plugin

How to build an integration for a third-party service: a typed HTTP client behind
`IHttpClientFactory`, credentials that arrive from a config flow, and honest failure reporting. The
service itself - a "Task Board" with lists and cards - is imaginary, but the plugin is shaped exactly
like one talking to a real API.

This is deliberately not another generic HTTP integration. It shows how *a specific service* is
integrated, not how to send arbitrary requests.

## Read these first

- **`Api/TaskBoardClient.cs`** - the typed client, registered with `AddHttpClient<TaskBoardClient>()`.
  Everything the plugin can do to the service goes through here; nothing else sees `HttpClient`.
- **`Api/TaskBoardException.cs`** - one failure type carrying the reason callers branch on, so neither
  the actions nor the issue provider inspect status codes themselves.
- **`Api/TaskBoardCredentials.cs`** - what the config flow produced. A plugin is configured *after* it
  starts, so the base address and the token cannot be baked into the registration.
- **`ConfigFlow/TaskBoardConfigFlow.cs`** - several steps, a branch, a value stored as a secret and an
  external OAuth round trip. What a step collects is verified against the API before the entry is
  completed, so a wrong token fails in the wizard rather than as a broken integration afterwards.
- **`RestApiIntegration.cs`** - reads the entry back in `InitializeAsync` (which the host re-runs when
  the configuration changes), keeps the variables in sync, and reports integration issues.
- **`Actions/TaskBoardActionResults.cs`** - the failure mapping. "Not configured", "token rejected" and
  "server unreachable" point the user at three different fixes, and a generic failure would hide all
  three.

## Integration issues

`GetIssuesAsync` is a live round trip, never a cached list - a stale issue is worse than none. The
three it can report are the three a user can actually act on, and two of them hand the user back to
the config flow through `IssueResolutionFollowUp.StartConfigFlow`.

## Running it against a local host

Use this project's **Macro Deck - Real Host** launch profile as described in the repository's
[run and debug guide](../../README.md#run-and-debug-against-macro-deck). There is no server to point it
at, which is itself the interesting part: without configuration the plugin reports the "not
configured" issue, and with a URL that does not answer it reports the unreachable one. To see the
happy path, run the tests - they include a fake server.

## Testing it

```bash
dotnet test tests/MacroDeck.SampleRestApiPlugin.Tests
```

`FakeTaskBoardApi.cs` is a deterministic stand-in plugged in as the client's primary handler, so the
plugin's own request building, JSON parsing and error mapping all still run for real.
`TaskBoardConfigFlowTests` drives the wizard step by step, including the OAuth branch.
