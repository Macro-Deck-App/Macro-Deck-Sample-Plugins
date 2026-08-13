using MacroDeck.SampleRestApiPlugin.Api;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;

namespace MacroDeck.SampleRestApiPlugin.ConfigFlow;

/// <summary>
/// The realistic counterpart to the weather sample's one-field flow: several steps, a branch, a value
/// stored as a secret, and an external authorization round trip. What a step collects is verified
/// against the real API before the entry is completed, so a wrong token fails in the wizard rather
/// than as a broken integration afterwards.
/// </summary>
internal sealed class TaskBoardConfigFlow(TaskBoardClient client) : IConfigFlow
{
	internal const string ServerUrlKey = "serverUrl";
	internal const string TokenKey = "token";

	private const string ServerStepId = "server";
	private const string TokenStepId = "token";
	private const string CallbackStepId = "oauth-callback";

	private const string TokenAuth = "token";
	private const string OAuthAuth = "oauth";

	private Uri? _baseAddress;

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
		=> Task.FromResult(ConfigFlowResult.Step(ServerStep()));

	public async Task<ConfigFlowResult> SubmitAsync(
		string stepId,
		IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context,
		CancellationToken cancellationToken)
		=> stepId switch
		{
			ServerStepId => SubmitServer(input, context),
			TokenStepId => await SubmitTokenAsync(input, cancellationToken),
			CallbackStepId => await SubmitCallbackAsync(context, cancellationToken),
			_ => ConfigFlowResult.Error(ServerStep(), "Unknown step.")
		};

	private ConfigFlowResult SubmitServer(IReadOnlyDictionary<string, object?> input, IConfigFlowContext context)
	{
		if (input.GetValueOrDefault(ServerUrlKey) is not string url ||
			!Uri.TryCreate(EnsureTrailingSlash(url), UriKind.Absolute, out var baseAddress) ||
			baseAddress.Scheme is not ("http" or "https"))
		{
			return ConfigFlowResult.Error(ServerStep(),
				"Enter the server's base URL.",
				new Dictionary<string, string> { [ServerUrlKey] = "Must be an http(s) URL." });
		}

		_baseAddress = baseAddress;

		if (input.GetValueOrDefault("authMethod") as string != OAuthAuth)
		{
			return ConfigFlowResult.Step(TokenStep());
		}

		// The host opens this URL, catches the redirect back to its own callback and resumes the flow at
		// ResumeStepId. RedirectUri and State come from the host, never from the plugin.
		var authorizeUrl = new UriBuilder(new Uri(baseAddress, "oauth/authorize"))
		{
			Query = $"response_type=code&client_id=macro-deck-sample" +
				$"&redirect_uri={Uri.EscapeDataString(context.OAuth.RedirectUri)}" +
				$"&state={Uri.EscapeDataString(context.OAuth.State)}"
		}.Uri;

		return ConfigFlowResult.External(authorizeUrl.ToString(), CallbackStepId);
	}

	private async Task<ConfigFlowResult> SubmitTokenAsync(
		IReadOnlyDictionary<string, object?> input,
		CancellationToken cancellationToken)
	{
		if (input.GetValueOrDefault(TokenKey) is not string { Length: > 0 } token)
		{
			return ConfigFlowResult.Error(TokenStep(),
				"Enter an API token.",
				new Dictionary<string, string> { [TokenKey] = "Required." });
		}

		return await CompleteAsync(token, TokenStep(), cancellationToken);
	}

	private async Task<ConfigFlowResult> SubmitCallbackAsync(IConfigFlowContext context, CancellationToken cancellationToken)
	{
		// Over the wire the authorization code is passed as an argument on the call that carries it
		// rather than being a live value to poll - see capability-parity.md.
		if (context.OAuth.AuthorizationCode is not { Length: > 0 } code)
		{
			return ConfigFlowResult.Error(ServerStep(), "The authorization was cancelled or returned no code.");
		}

		// A real integration posts the code to the service's token endpoint here. The sample's imaginary
		// service accepts the code itself as a bearer token, so there is nothing to exchange.
		return await CompleteAsync(code, ServerStep(), cancellationToken);
	}

	private async Task<ConfigFlowResult> CompleteAsync(string token, ConfigFlowStep retryStep, CancellationToken cancellationToken)
	{
		if (_baseAddress is not { } baseAddress)
		{
			return ConfigFlowResult.Error(ServerStep(), "Start again: the server URL was lost with the session.");
		}

		try
		{
			await client.VerifyAsync(baseAddress, token, cancellationToken);
		}
		catch (TaskBoardException exception)
		{
			return ConfigFlowResult.Error(retryStep, exception.Message);
		}

		// Values named here are persisted under those keys; the secret one lands in the host's secret
		// store and can only be read back through GetSecretAsync.
		return ConfigFlowResult.Complete($"Task Board ({baseAddress.Host})", new Dictionary<string, ConfigFlowValue>
		{
			[ServerUrlKey] = ConfigFlowValue.Plain(baseAddress.ToString()),
			[TokenKey] = ConfigFlowValue.Secret(token)
		});
	}

	private static string EnsureTrailingSlash(string url)
		=> url.EndsWith('/') ? url : url + "/";

	private static ConfigFlowStep ServerStep() => new()
	{
		StepId = ServerStepId,
		Title = "Task Board server",
		Description = "Where the sample's imaginary Task Board API runs, and how to authenticate against it.",
		Links = [new ConfigFlowLink { Label = "API documentation", Url = "https://example.com/task-board/api" }],
		Fields =
		[
			ActionParameter.Url(ServerUrlKey,
				label: "Server URL",
				placeholder: "https://task-board.example.com/api/",
				required: true,
				autoPrefixHttps: true),
			ActionParameter.Choice("authMethod",
				[
					new ActionParameterOption { Value = TokenAuth, Label = "API token" },
					new ActionParameterOption { Value = OAuthAuth, Label = "Sign in (OAuth)" }
				],
				label: "Authentication",
				defaultValue: TokenAuth,
				required: true)
		]
	};

	private static ConfigFlowStep TokenStep() => new()
	{
		StepId = TokenStepId,
		Title = "API token",
		Description = "Paste a personal API token. It is stored in the host's secret store, not in plain configuration.",
		Instructions =
		[
			new ConfigFlowInstruction { Text = "Open Task Board → Settings → API tokens and create a token with board access." }
		],
		Fields = [ActionParameter.Secret(TokenKey, label: "API token", required: true)]
	};
}
