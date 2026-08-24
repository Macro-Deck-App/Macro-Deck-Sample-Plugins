using MacroDeck.Localization;
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

	public LocalizedText Name => Strings.Actions.TransferPlayback.Name();

	public LocalizedText Description => Strings.Actions.TransferPlayback.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("device",
			label: Strings.Fields.Device.Label(),
			description: Strings.Actions.TransferPlayback.Device.Description()),
		ActionParameter.Toggle("startPlayback",
			label: Strings.Actions.TransferPlayback.StartPlayback.Label(),
			defaultValue: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public async Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var devices = await integration.Library.GetDevicesAsync(cancellationToken);

		// A device name comes from the device itself, so it is already in its final form: a literal.
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

				return ActionResult.Accepted(Strings.Actions.TransferPlayback.PickerRequested());
			}

			var devices = await integration.Library.GetDevicesAsync(context.CancellationToken);
			if (!devices.Any(device => string.Equals(device.Id, deviceId, StringComparison.Ordinal)))
			{
				return ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.DeviceNotFound(deviceId));
			}

			await integration.Library.TransferPlaybackAsync(deviceId, startPlayback, context.CancellationToken);
			return ActionResult.Success();
		}
	}
}
