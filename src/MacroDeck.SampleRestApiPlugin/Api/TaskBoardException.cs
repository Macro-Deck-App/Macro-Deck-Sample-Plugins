using System.Net;
using MacroDeck.Localization;

namespace MacroDeck.SampleRestApiPlugin.Api;

/// <summary>
/// One failure type for the whole client, carrying the reason callers actually branch on. Actions map
/// <see cref="Reason"/> onto an <c>ActionResult</c> error code and the issue provider maps it onto an
/// integration issue, so neither has to inspect HTTP status codes itself.
/// <para>
/// <see cref="Exception.Message"/> and <see cref="UserMessage"/> say the same thing twice on purpose:
/// the first is diagnostic text for the log, in one fixed language, and the second is the reference the
/// host resolves for whoever is reading the deck. Anything a user sees uses the second.
/// </para>
/// </summary>
public sealed class TaskBoardException : Exception
{
	public TaskBoardException(
		TaskBoardFailure reason,
		string message,
		LocalizedText userMessage,
		Exception? innerException = null)
		: base(message, innerException)
	{
		Reason = reason;
		UserMessage = userMessage;
	}

	public TaskBoardFailure Reason { get; }

	/// <summary>The same failure as text a client resolves in its own language.</summary>
	public LocalizedText UserMessage { get; }

	public static TaskBoardException NotConfigured() => new(
		TaskBoardFailure.NotConfigured,
		"The Task Board integration is not configured yet.",
		Strings.Failures.NotConfigured());

	public static TaskBoardException Unreachable(Exception innerException) => new(
		TaskBoardFailure.Unreachable,
		"The Task Board API is unreachable.",
		Strings.Failures.Unreachable(),
		innerException);

	public static TaskBoardException Timeout(Exception innerException) => new(
		TaskBoardFailure.Timeout,
		"The Task Board API did not answer in time.",
		Strings.Failures.Timeout(),
		innerException);

	public static TaskBoardException EmptyBody() => new(
		TaskBoardFailure.ServerError,
		"The Task Board API answered with an empty body.",
		Strings.Failures.EmptyBody());

	public static TaskBoardException FromStatus(HttpStatusCode status) => status switch
	{
		HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new TaskBoardException(
			TaskBoardFailure.Unauthorized,
			"The Task Board API rejected the token.",
			Strings.Failures.Unauthorized()),
		HttpStatusCode.NotFound => new TaskBoardException(
			TaskBoardFailure.NotFound,
			"The Task Board API does not know that item.",
			Strings.Failures.NotFound()),
		_ => new TaskBoardException(
			TaskBoardFailure.ServerError,
			$"The Task Board API answered {(int)status}.",
			Strings.Failures.ServerError((int)status))
	};
}

public enum TaskBoardFailure
{
	NotConfigured,
	Unreachable,
	Timeout,
	Unauthorized,
	NotFound,
	ServerError
}
