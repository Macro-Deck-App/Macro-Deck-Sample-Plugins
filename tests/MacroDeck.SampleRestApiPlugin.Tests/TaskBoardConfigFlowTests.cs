using MacroDeck.Plugin.Protocol.Capabilities.ConfigFlow;
using MacroDeck.Plugin.Testing;
using MacroDeck.SampleRestApiPlugin.Api;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MacroDeck.SampleRestApiPlugin.Tests;

/// <summary>
/// The config flow driven step by step, the way the host drives it. Session state lives in the plugin
/// and is keyed by the session id the host mints, so every call here carries the same one.
/// </summary>
[TestFixture]
public sealed class TaskBoardConfigFlowTests
{
	private const string SessionId = "session-1";

	private static readonly Dictionary<string, object?> _noInput = [];

	[Test]
	public async Task The_flow_starts_by_asking_where_the_server_is()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());

		var result = (await harness.ConfigFlow.StartAsync(Start())).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Step"));
		Assert.That(result.NextStep!.StepId, Is.EqualTo("server"));
		Assert.That(result.NextStep.Fields.Select(field => field.Name), Does.Contain("serverUrl"));
	}

	[Test]
	public async Task A_url_that_is_not_a_url_is_rejected_with_a_field_error()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());

		var result = (await harness.ConfigFlow.SubmitAsync(Submit("server", new Dictionary<string, object?>
		{
			["serverUrl"] = "not a url",
			["authMethod"] = "token"
		}))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Error"));
		Assert.That(result.FieldErrors, Does.ContainKey("serverUrl"));
	}

	[Test]
	public async Task A_valid_server_leads_to_the_token_step()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());

		var result = (await harness.ConfigFlow.SubmitAsync(Submit("server", ServerInput()))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Step"));
		Assert.That(result.NextStep!.StepId, Is.EqualTo("token"));
		Assert.That(result.NextStep.Fields.Single().Type, Is.EqualTo("Secret"));
	}

	[Test]
	public async Task A_token_the_server_rejects_never_completes_the_entry()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());
		await harness.ConfigFlow.SubmitAsync(Submit("server", ServerInput()));

		var result = (await harness.ConfigFlow.SubmitAsync(Submit("token",
			new Dictionary<string, object?> { ["token"] = "nonsense" }))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Error"));
		Assert.That(result.ErrorMessage, Does.Contain("rejected the token"));
	}

	[Test]
	public async Task A_verified_token_completes_and_is_stored_as_a_secret()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());
		await harness.ConfigFlow.SubmitAsync(Submit("server", ServerInput()));

		var result = (await harness.ConfigFlow.SubmitAsync(Submit("token",
			new Dictionary<string, object?> { ["token"] = FakeTaskBoardApi.ValidToken }))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Complete"));
		Assert.That(result.EntryTitle, Does.Contain("task-board.test"));
		Assert.That(result.Values!["serverUrl"].IsSecret, Is.False);
		Assert.That(result.Values["token"].IsSecret, Is.True);
		Assert.That(result.Values["token"].Value, Is.EqualTo(FakeTaskBoardApi.ValidToken));
	}

	[Test]
	public async Task Choosing_to_sign_in_sends_the_user_to_the_authorization_page()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());

		var input = ServerInput();
		input["authMethod"] = "oauth";
		var result = (await harness.ConfigFlow.SubmitAsync(Submit("server", input))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("External"));
		Assert.That(result.ResumeStepId, Is.EqualTo("oauth-callback"));
		Assert.That(result.ExternalUrl, Does.Contain("redirect_uri=http%3A%2F%2Flocalhost%2Fcallback"));
		Assert.That(result.ExternalUrl, Does.Contain("state=state-1"));
	}

	[Test]
	public async Task Coming_back_without_a_code_starts_over_instead_of_completing()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());

		var input = ServerInput();
		input["authMethod"] = "oauth";
		await harness.ConfigFlow.SubmitAsync(Submit("server", input));

		var result = (await harness.ConfigFlow.SubmitAsync(Submit("oauth-callback", _noInput)))
			.DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Error"));
		Assert.That(result.NextStep!.StepId, Is.EqualTo("server"));
	}

	[Test]
	public async Task Coming_back_with_a_code_completes_the_entry()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());

		var input = ServerInput();
		input["authMethod"] = "oauth";
		await harness.ConfigFlow.SubmitAsync(Submit("server", input));

		var result = (await harness.ConfigFlow.SubmitAsync(
			Submit("oauth-callback", _noInput, authorizationCode: FakeTaskBoardApi.ValidToken))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Complete"));
		Assert.That(result.Values!["token"].IsSecret, Is.True);
	}

	[Test]
	public async Task An_unknown_step_is_refused_rather_than_treated_as_the_current_one()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());
		await harness.ConfigFlow.StartAsync(Start());

		var result = (await harness.ConfigFlow.SubmitAsync(Submit("who-knows", _noInput))).DataAs<ConfigFlowResultDto>();

		Assert.That(result!.Kind, Is.EqualTo("Error"));
	}

	private static Dictionary<string, object?> ServerInput() => new()
	{
		["serverUrl"] = FakeTaskBoardApi.BaseAddress.ToString(),
		["authMethod"] = "token"
	};

	private static FlowStartArguments Start() => new() { SessionId = SessionId, OAuth = OAuth() };

	private static FlowSubmitArguments Submit(
		string stepId,
		IReadOnlyDictionary<string, object?> input,
		string? authorizationCode = null)
		=> new()
		{
			SessionId = SessionId,
			StepId = stepId,
			Input = input.ToDictionary(pair => pair.Key,
				pair => System.Text.Json.JsonSerializer.SerializeToElement(pair.Value),
				StringComparer.Ordinal),
			OAuth = OAuth(authorizationCode)
		};

	private static ConfigFlowOAuthContextDto OAuth(string? authorizationCode = null) => new()
	{
		RedirectUri = "http://localhost/callback",
		State = "state-1",
		AuthorizationCode = authorizationCode
	};

	private static async Task<PluginTestHarness> CreateAsync(FakeTaskBoardApi api)
	{
		var harness = PluginTestHarness.Create(builder =>
		{
			builder.RegisterIntegration<RestApiIntegration>();
			builder.Services.AddTaskBoardApi().ConfigurePrimaryHttpMessageHandler(() => api);
		});

		await harness.InitializeIntegrationsAsync();
		return harness;
	}
}
