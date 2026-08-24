using MacroDeck.Localization;

namespace MacroDeck.SampleVirtualProfilePlugin.Scenes;

/// <summary>The scenes the sample control room can be in. Everything else - the virtual profile, the
/// variables, the event - is derived from whichever one is active.</summary>
internal static class ControlRoomScenes
{
	internal static IReadOnlyList<ControlRoomScene> All { get; } =
	[
		new("live", "Live", "#D0021B", IsLive: true),
		new("standby", "Standby", "#F5A623", IsLive: false),
		new("break", "Break", "#4A90D9", IsLive: false),
		new("offline", "Offline", "#4A4A4A", IsLive: false)
	];

	internal static ControlRoomScene Default => All[3];

	internal static ControlRoomScene? Find(string id)
		=> All.FirstOrDefault(scene => string.Equals(scene.Id, id, StringComparison.Ordinal));

	/// <summary>
	/// The scene's name for a surface that resolves a reference in the reader's language - an action's
	/// option label, an event's payload. <see cref="ControlRoomScene.Name"/> stays beside it for the
	/// surfaces that cannot take one: a virtual widget's JSON payload, a host variable's value and a
	/// notification title are all plain strings on their contracts.
	/// </summary>
	internal static LocalizedText DisplayName(ControlRoomScene scene) => scene.Id switch
	{
		"live" => Strings.Scenes.Live(),
		"standby" => Strings.Scenes.Standby(),
		"break" => Strings.Scenes.Break(),
		"offline" => Strings.Scenes.Offline(),
		_ => scene.Name
	};
}

internal sealed record ControlRoomScene(string Id, string Name, string Color, bool IsLive);
