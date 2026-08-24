using MacroDeck.Localization;
using MacroDeck.SampleVirtualProfilePlugin.Scenes;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleVirtualProfilePlugin.Actions;

/// <summary>
/// The action counterpart to pressing a scene button in the virtual profile: both end in
/// <see cref="ControlRoomIntegration.ApplySceneAsync"/>, so a deck button and the profile cannot drift
/// apart.
/// </summary>
internal sealed class SetSceneAction(ControlRoomIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "set-scene";

	public LocalizedText Name => Strings.Actions.SetScene.Name();

	public LocalizedText Description => Strings.Actions.SetScene.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("scene", label: Strings.Actions.SetScene.Scene.Label(), required: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(new DynamicOptionsResult
		{
			Options =
			[
				.. ControlRoomScenes.All.Select(scene => new ActionParameterOption
				{
					Value = scene.Id,
					Label = ControlRoomScenes.DisplayName(scene)
				})
			]
		});

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("scene") is not string sceneId ||
				ControlRoomScenes.Find(sceneId) is not { } scene)
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.InvalidValue(Strings.Actions.SetScene.Scene.Label()));
			}

			await integration.ApplySceneAsync(scene, source: "action", context.CancellationToken);
			return ActionResult.Success();
		}
	}
}
