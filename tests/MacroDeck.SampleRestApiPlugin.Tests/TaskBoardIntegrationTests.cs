using MacroDeck.Plugin.Protocol.Capabilities.Actions;
using MacroDeck.Plugin.Protocol.Capabilities.Issues;
using MacroDeck.Plugin.Protocol.Capabilities.Variables;
using MacroDeck.Plugin.Testing;
using MacroDeck.SampleRestApiPlugin.Api;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MacroDeck.SampleRestApiPlugin.Tests;

[TestFixture]
public sealed class TaskBoardIntegrationTests
{
	[Test]
	public async Task An_unconfigured_integration_reports_an_issue_instead_of_failing_quietly()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi(), configured: false);

		var issues = (await harness.Issues.GetIssuesAsync()).DataAs<IssueListResult>();
		var configured = (await harness.Variables.GetAsync("configured")).DataAs<VariableValueDto>();
		var openCards = (await harness.Variables.GetAsync("open-cards")).DataAs<VariableValueDto>();

		Assert.That(issues!.Issues.Single().Id, Is.EqualTo("not-configured"));
		Assert.That(issues.Issues[0].Severity, Is.EqualTo("Error"));
		Assert.That(configured!.Boolean, Is.False);
		Assert.That(openCards!.Kind, Is.EqualTo("unavailable"), "an unconfigured count is unknown, not zero");
	}

	[Test]
	public async Task An_action_run_before_configuration_says_so()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi(), configured: false);

		var outcome = await harness.Actions.ExecuteAsync("refresh-board");

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error!.Message, Does.Contain("not configured"));
	}

	[Test]
	public async Task A_configured_integration_reads_the_board_and_has_no_issues()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());

		var issues = (await harness.Issues.GetIssuesAsync()).DataAs<IssueListResult>();
		var openCards = (await harness.Variables.GetAsync("open-cards")).DataAs<VariableValueDto>();
		var next = (await harness.Variables.GetAsync("next-card")).DataAs<VariableValueDto>();

		Assert.That(issues!.Issues, Is.Empty);
		Assert.That(openCards!.Number, Is.EqualTo(2), "the third card is already done");
		Assert.That(next!.Text, Is.EqualTo("Write the release notes"));
	}

	[Test]
	public async Task A_rejected_token_becomes_an_issue_that_hands_the_user_back_to_the_config_flow()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi(), token: "expired-token");

		var issues = (await harness.Issues.GetIssuesAsync()).DataAs<IssueListResult>();
		Assert.That(issues!.Issues.Single().Id, Is.EqualTo("unauthorized"));

		var resolution = (await harness.Issues.ResolveAsync(new IssueResolveArguments { IssueId = "unauthorized" }))
			.DataAs<IssueResolveResult>();

		Assert.That(resolution!.Success, Is.True);
		Assert.That(resolution.FollowUp, Is.EqualTo("StartConfigFlow"));
	}

	[Test]
	public async Task An_unreachable_server_is_a_warning_and_retrying_it_reports_the_truth()
	{
		var api = new FakeTaskBoardApi { IsOffline = true };
		await using var harness = await CreateAsync(api);

		var issues = (await harness.Issues.GetIssuesAsync()).DataAs<IssueListResult>();
		Assert.That(issues!.Issues.Single().Id, Is.EqualTo("unreachable"));
		Assert.That(issues.Issues[0].Severity, Is.EqualTo("Warning"));

		// Retrying while the server is still down reports the real reason rather than pretending to fix it.
		var retried = (await harness.Issues.ResolveAsync(new IssueResolveArguments { IssueId = "unreachable" }))
			.DataAs<IssueResolveResult>();
		Assert.That(retried!.Success, Is.False);
		Assert.That(retried.Message, Does.Contain("unreachable"));

		// Once the server answers again the issue is simply gone: the list is live, never cached.
		api.IsOffline = false;
		var afterRecovery = (await harness.Issues.GetIssuesAsync()).DataAs<IssueListResult>();
		Assert.That(afterRecovery!.Issues, Is.Empty);
	}

	[Test]
	public async Task A_failing_read_notifies_the_user_once_and_dismisses_it_on_recovery()
	{
		var api = new FakeTaskBoardApi { IsOffline = true };
		await using var harness = await CreateAsync(api);

		await harness.Actions.ExecuteAsync("refresh-board");
		Assert.That(harness.Context.Notifications.Current, Is.Not.Empty);

		api.IsOffline = false;
		await harness.Actions.ExecuteAsync("refresh-board");
		Assert.That(harness.Context.Notifications.Current, Is.Empty);
	}

	[Test]
	public async Task Creating_a_card_sends_what_was_configured_and_shows_up_in_the_board()
	{
		var api = new FakeTaskBoardApi();
		await using var harness = await CreateAsync(api);

		var outcome = await harness.Actions.ExecuteAsync("create-card", new Dictionary<string, object?>
		{
			["title"] = "Order more coffee",
			["listId"] = "list-ops",
			["priority"] = "high",
			["notes"] = "The good one."
		});

		var created = api.Cards[^1];
		var openCards = (await harness.Variables.GetAsync("open-cards")).DataAs<VariableValueDto>();

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(created.Title, Is.EqualTo("Order more coffee"));
		Assert.That(created.ListId, Is.EqualTo("list-ops"));
		Assert.That(created.Priority, Is.EqualTo("high"));
		Assert.That(openCards!.Number, Is.EqualTo(3));
	}

	[Test]
	public async Task Creating_a_card_without_a_title_never_reaches_the_api()
	{
		var api = new FakeTaskBoardApi();
		await using var harness = await CreateAsync(api);
		var requestsBefore = api.Requests.Count;

		var outcome = await harness.Actions.ExecuteAsync("create-card",
			new Dictionary<string, object?> { ["listId"] = "list-ops" });

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(api.Requests, Has.Count.EqualTo(requestsBefore));
	}

	[Test]
	public async Task Completing_a_card_publishes_it_and_takes_it_off_the_open_list()
	{
		var api = new FakeTaskBoardApi();
		await using var harness = await CreateAsync(api);

		var outcome = await harness.Actions.ExecuteAsync("complete-card",
			new Dictionary<string, object?> { ["cardId"] = "card-1" });

		var published = harness.Context.Events.Published[^1];
		var openCards = (await harness.Variables.GetAsync("open-cards")).DataAs<VariableValueDto>();

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(published.EventId, Is.EqualTo("card-completed"));
		Assert.That(published.Parameters!.Value.GetProperty("title").GetString(), Is.EqualTo("Write the release notes"));
		Assert.That(openCards!.Number, Is.EqualTo(1));
	}

	[Test]
	public async Task A_card_the_server_does_not_know_fails_as_not_found()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());

		var outcome = await harness.Actions.ExecuteAsync("complete-card",
			new Dictionary<string, object?> { ["cardId"] = "card-404" });

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error!.Message, Does.Contain("does not know"));
	}

	[Test]
	public async Task A_rejected_token_fails_the_action_differently_from_an_unreachable_server()
	{
		var api = new FakeTaskBoardApi();
		await using var harness = await CreateAsync(api, token: "expired-token");
		var unauthorized = await harness.Actions.ExecuteAsync("refresh-board");

		await using var offline = await CreateAsync(new FakeTaskBoardApi { IsOffline = true });
		var unreachable = await offline.Actions.ExecuteAsync("refresh-board");

		Assert.That(unauthorized.Error!.Message, Does.Contain("rejected the token"));
		Assert.That(unreachable.Error!.Message, Does.Contain("unreachable"));
	}

	[Test]
	public async Task List_options_come_from_the_api()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());

		var options = (await harness.Actions.GetOptionsAsync("create-card", "listId")).DataAs<DynamicOptionsResultDto>();

		Assert.That(options!.Options.Select(option => option.Label), Is.EquivalentTo(_listNames));
	}

	[Test]
	public async Task Card_options_are_filtered_by_what_the_user_typed()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi());

		var options = (await harness.Actions.GetOptionsAsync("complete-card", "cardId", filter: "certificate"))
			.DataAs<DynamicOptionsResultDto>();

		Assert.That(options!.Options.Single().Value, Is.EqualTo("card-2"));
	}

	[Test]
	public async Task Options_are_empty_rather_than_broken_while_the_server_is_unreachable()
	{
		await using var harness = await CreateAsync(new FakeTaskBoardApi { IsOffline = true });

		var outcome = await harness.Actions.GetOptionsAsync("create-card", "listId");

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(outcome.DataAs<DynamicOptionsResultDto>()!.Options, Is.Empty);
	}

	private static readonly string[] _listNames = ["Inbox", "Operations"];

	private static async Task<PluginTestHarness> CreateAsync(
		FakeTaskBoardApi api,
		bool configured = true,
		string token = FakeTaskBoardApi.ValidToken)
	{
		var harness = PluginTestHarness.Create(builder =>
		{
			builder.RegisterIntegration<RestApiIntegration>();
			builder.Services.AddTaskBoardApi().ConfigurePrimaryHttpMessageHandler(() => api);
		});

		if (configured)
		{
			// What the config flow would have persisted, seeded directly so these tests are about the
			// integration rather than about the wizard.
			var entryId = harness.Context.Config.AddEntry("Task Board");
			harness.Context.Config.SeedString(entryId, "serverUrl", FakeTaskBoardApi.BaseAddress.ToString());
			harness.Context.Config.SeedSecret(entryId, "token", token);
		}

		await harness.InitializeIntegrationsAsync();
		return harness;
	}
}
