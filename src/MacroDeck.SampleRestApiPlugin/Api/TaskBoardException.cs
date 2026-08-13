using System.Net;

namespace MacroDeck.SampleRestApiPlugin.Api;

/// <summary>
/// One failure type for the whole client, carrying the reason callers actually branch on. Actions map
/// <see cref="Reason"/> onto an <c>ActionResult</c> error code and the issue provider maps it onto an
/// integration issue, so neither has to inspect HTTP status codes itself.
/// </summary>
public sealed class TaskBoardException(TaskBoardFailure reason, string message, Exception? innerException = null)
	: Exception(message, innerException)
{
	public TaskBoardFailure Reason { get; } = reason;

	public static TaskBoardException FromStatus(HttpStatusCode status) => status switch
	{
		HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
			new TaskBoardException(TaskBoardFailure.Unauthorized, "The Task Board API rejected the token."),
		HttpStatusCode.NotFound =>
			new TaskBoardException(TaskBoardFailure.NotFound, "The Task Board API does not know that item."),
		_ => new TaskBoardException(TaskBoardFailure.ServerError, $"The Task Board API answered {(int)status}.")
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
