using MacroDeck.Localization;
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
			_ => ConfigFlowResult.Error(ServerStep(), Strings.ConfigFlow.UnknownStep())
		};

	private ConfigFlowResult SubmitServer(IReadOnlyDictionary<string, object?> input, IConfigFlowContext context)
	{
		if (input.GetValueOrDefault(ServerUrlKey) is not string url ||
			!Uri.TryCreate(EnsureTrailingSlash(url), UriKind.Absolute, out var baseAddress) ||
			baseAddress.Scheme is not ("http" or "https"))
		{
			return ConfigFlowResult.Error(ServerStep(),
				MacroDeckStrings.Validation.InvalidUrl(Strings.ConfigFlow.Server.ServerUrl.Label()),
				new Dictionary<string, LocalizedText>
				{
					[ServerUrlKey] = Strings.ConfigFlow.Server.InvalidUrl()
				});
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
			var required = MacroDeckStrings.Validation.Required(Strings.ConfigFlow.Token.ApiToken.Label());

			return ConfigFlowResult.Error(TokenStep(),
				required,
				new Dictionary<string, LocalizedText> { [TokenKey] = required });
		}

		return await CompleteAsync(token, TokenStep(), cancellationToken);
	}

	private async Task<ConfigFlowResult> SubmitCallbackAsync(IConfigFlowContext context, CancellationToken cancellationToken)
	{
		// Over the wire the authorization code is passed as an argument on the call that carries it
		// rather than being a live value to poll - see capability-parity.md.
		if (context.OAuth.AuthorizationCode is not { Length: > 0 } code)
		{
			return ConfigFlowResult.Error(ServerStep(), Strings.ConfigFlow.OAuth.Cancelled());
		}

		// A real integration posts the code to the service's token endpoint here. The sample's imaginary
		// service accepts the code itself as a bearer token, so there is nothing to exchange.
		return await CompleteAsync(code, ServerStep(), cancellationToken);
	}

	private async Task<ConfigFlowResult> CompleteAsync(string token, ConfigFlowStep retryStep, CancellationToken cancellationToken)
	{
		if (_baseAddress is not { } baseAddress)
		{
			return ConfigFlowResult.Error(ServerStep(), Strings.ConfigFlow.Server.SessionLost());
		}

		try
		{
			await client.VerifyAsync(baseAddress, token, cancellationToken);
		}
		catch (TaskBoardException exception)
		{
			return ConfigFlowResult.Error(retryStep, exception.UserMessage);
		}

		// Values named here are persisted under those keys; the secret one lands in the host's secret
		// store and can only be read back through GetSecretAsync. The entry title is deliberately a plain
		// string: the host stores it as the entry's name and the user renames it from there.
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
		Title = Strings.ConfigFlow.Server.Title(),
		Description = Strings.ConfigFlow.Server.Description(),
		Links =
		[
			new ConfigFlowLink
			{
				Label = Strings.ConfigFlow.Server.ApiDocumentation(),
				Url = "https://example.com/task-board/api"
			}
		],
		Fields =
		[
			// A placeholder showing the shape of a URL is an example, not a sentence: it stays a literal.
			ActionParameter.Url(ServerUrlKey,
				label: Strings.ConfigFlow.Server.ServerUrl.Label(),
				placeholder: "https://task-board.example.com/api/",
				required: true,
				autoPrefixHttps: true),
			ActionParameter.Choice("authMethod",
				[
					new ActionParameterOption { Value = TokenAuth, Label = Strings.AuthMethods.Token() },
					new ActionParameterOption { Value = OAuthAuth, Label = Strings.AuthMethods.OAuth() }
				],
				label: Strings.ConfigFlow.Server.AuthMethod.Label(),
				defaultValue: TokenAuth,
				required: true)
		]
	};

	private static ConfigFlowStep TokenStep() => new()
	{
		StepId = TokenStepId,
		Title = Strings.ConfigFlow.Token.Title(),
		Description = Strings.ConfigFlow.Token.Description(),
		Instructions = [new ConfigFlowInstruction { Text = Strings.ConfigFlow.Token.Instruction() }],
		Fields = [ActionParameter.Secret(TokenKey, label: Strings.ConfigFlow.Token.ApiToken.Label(), required: true)]
	};
}
