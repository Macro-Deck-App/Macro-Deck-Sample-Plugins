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
}

internal sealed record ControlRoomScene(string Id, string Name, string Color, bool IsLive);
