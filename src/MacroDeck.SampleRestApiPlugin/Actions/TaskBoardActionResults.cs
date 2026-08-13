using MacroDeck.SampleRestApiPlugin.Api;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleRestApiPlugin.Actions;

/// <summary>
/// One place mapping an API failure onto a truthful <see cref="ActionResult"/>. The distinction
/// matters to the user: "not configured" points at the config flow, "unauthorized" at re-authenticating
/// and a timeout at retrying, and a generic failure would hide all three.
/// </summary>
internal static class TaskBoardActionResults
{
	internal static ActionResult From(TaskBoardException exception) => ActionResult.Failed(exception.Reason switch
	{
		TaskBoardFailure.NotConfigured => ActionErrorCodes.NotConfigured,
		TaskBoardFailure.Unreachable => ActionErrorCodes.NotConnected,
		TaskBoardFailure.Timeout => ActionErrorCodes.Timeout,
		TaskBoardFailure.Unauthorized => ActionErrorCodes.PermissionDenied,
		TaskBoardFailure.NotFound => ActionErrorCodes.NotFound,
		_ => ActionErrorCodes.ProviderError
	}, exception.Message);
}
