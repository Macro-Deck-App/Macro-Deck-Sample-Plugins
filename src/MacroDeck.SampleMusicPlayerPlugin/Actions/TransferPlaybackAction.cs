using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleMusicPlayerPlugin.Actions;

/// <summary>
/// Moves playback to another output device, with the device picker as the fallback when none was
/// configured - the device-shaped counterpart to <see cref="PlayCatalogItemAction"/>.
/// </summary>
internal sealed class TransferPlaybackAction(MusicPlayerIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "transfer-playback";

	public string Name => "Transfer playback";

	public string Description => "Moves playback of the sample library to another device.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("device",
			label: "Device",
			description: "Leave empty to pick one on the client that pressed the button."),
		ActionParameter.Toggle("startPlayback", label: "Start playing after the transfer", defaultValue: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public async Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var devices = await integration.Library.GetDevicesAsync(cancellationToken);
		return new DynamicOptionsResult
		{
			Options = [.. devices.Select(device => new ActionParameterOption { Value = device.Id, Label = device.Name })]
		};
	}

	private sealed class Executor(MusicPlayerIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var startPlayback = context.Parameters.GetValueOrDefault("startPlayback") is not false;

			if (context.Parameters.GetValueOrDefault("device") is not string { Length: > 0 } deviceId)
			{
				context.Interactions?.RequestDevicePicker(context.OriginClientId,
					MusicPlayerIntegration.LibraryInstanceId,
					startPlayback,
					prompt: "Pick an output device");

				return ActionResult.Accepted("Asked the client to pick a device.");
			}

			var devices = await integration.Library.GetDevicesAsync(context.CancellationToken);
			if (!devices.Any(device => string.Equals(device.Id, deviceId, StringComparison.Ordinal)))
			{
				return ActionResult.Failed(ActionErrorCodes.NotFound, $"No device with id '{deviceId}'.");
			}

			await integration.Library.TransferPlaybackAsync(deviceId, startPlayback, context.CancellationToken);
			return ActionResult.Success();
		}
	}
}
