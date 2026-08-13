using System.Text.Json;
using MacroDeck.SampleVirtualProfilePlugin.Scenes;
using MacroDeck.Sdk.Profiles;

namespace MacroDeck.SampleVirtualProfilePlugin.Profiles;

/// <summary>
/// Builds the profile the plugin owns. A virtual profile is described, never stored: the host has no
/// copy to edit, so the descriptors are rebuilt from the current scene every time they are asked for -
/// which is what makes the active scene's button visibly light up.
/// </summary>
internal static class ControlRoomProfile
{
	internal const string ProfileId = "control-room";
	internal const string ScenesFolderId = "scenes";
	internal const string StatusFolderId = "status";

	private const string WidgetIdPrefix = "scene-";
	private const string InactiveColor = "#20242B";

	internal static VirtualProfileDescriptor Build(ControlRoomScene activeScene) => new(
		ProfileId,
		"Sample Control Room",
		// Locked, because the layout is the plugin's to decide - a user cannot add a row to a folder
		// whose contents this plugin regenerates.
		ProfileLayout.Grid(rows: 2, columns: 4),
		[
			new VirtualFolderDescriptor(ScenesFolderId, "Scenes",
				[.. ControlRoomScenes.All.Select((scene, index) => SceneWidget(scene, index, activeScene))]),
			new VirtualFolderDescriptor(StatusFolderId, "Status",
				[
					new VirtualWidgetDescriptor("status-clock", "Clock", PositionX: 0, PositionY: 0, Width: 2, Height: 1)
				],
				ParentId: ScenesFolderId,
				Order: 1)
		]);

	/// <summary>The scene a widget id stands for, or null for a widget that is not a scene button.</summary>
	internal static ControlRoomScene? SceneOf(string widgetId)
		=> widgetId.StartsWith(WidgetIdPrefix, StringComparison.Ordinal)
			? ControlRoomScenes.Find(widgetId[WidgetIdPrefix.Length..])
			: null;

	private static VirtualWidgetDescriptor SceneWidget(ControlRoomScene scene, int index, ControlRoomScene activeScene)
	{
		var isActive = string.Equals(scene.Id, activeScene.Id, StringComparison.Ordinal);

		// The same JSON payload a stored widget of this type would carry.
		var data = JsonSerializer.Serialize(new
		{
			mode = "single",
			label = scene.Name,
			offState = new
			{
				label = scene.Name,
				backgroundColor = isActive ? scene.Color : InactiveColor,
				labelColor = "#FFFFFF"
			}
		});

		return new VirtualWidgetDescriptor(WidgetIdPrefix + scene.Id, "ActionButton", PositionX: index, PositionY: 0, Data: data);
	}
}
