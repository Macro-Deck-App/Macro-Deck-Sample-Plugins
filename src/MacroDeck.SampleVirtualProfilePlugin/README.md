# Sample Control Room plugin

Two things that have no other worked example: a virtual profile the plugin owns, and the callbacks
that go from the plugin to the host. Read it when your plugin wants to *present* a deck of its own, or
to drive the app rather than only answer it.

The plugin models a small control room with four scenes. Pressing one of its own profile buttons and
running its "set scene" action end in the same method, so the two directions stay visibly consistent.

## Read these first

- **`Profiles/ControlRoomProfile.cs`** - the profile is described, never stored: the host has no copy to
  edit, so the descriptors are rebuilt from the current scene every time they are asked for. That is
  what makes the active scene's button change colour.
- **`ControlRoomIntegration.cs`** - `IProfileProvider.HandleWidgetInteractionAsync` for the presses, and
  `ApplySceneAsync` for everything that follows: invalidating the profile catalogue so the buttons
  redraw, publishing the event, writing a host variable and notifying the user.
- **`Actions/StyleWidgetAction.cs`** - `IWidgetApi`: the widget target defaults to `$self`, so the action
  styles the button it was triggered from, and an empty colour clears the override instead of setting
  one.
- **`Actions/NavigateDeckAction.cs`** - `IDeckNavigator`, with the target field only shown for the kinds
  that need one. Its options come from a host-pushed cache, which is empty for a moment right after
  connecting - expected, not an error.
- **`Actions/RunScriptAction.cs`** - `IScriptApi`: the script's own result becomes the action's result,
  so a failing script does not look like a successful button press.
- **`Actions/AnnounceAction.cs`** - `IUserNotifier`, including what a key is for: notifications sharing
  one replace each other, which is also how a plugin expresses progress.

## Callbacks can fail

Every round-trip callback here is wrapped: over the wire a `host.invoke` can be rate limited, time out,
or find no live connection, and throws `HostInvocationException` into the calling integration. An
in-process integration never has to handle that - it is the one genuinely new failure mode a plugin
gets, and ignoring it is the most common way a plugin breaks in the field.

## Running it against a local host

Run the project as the other samples do, then switch to the "Sample Control Room" profile. Its buttons
are the plugin's, not the user's: pressing one changes the scene, recolours the grid and updates the
`sample_control_room_scene` variable.

## Testing it

```bash
dotnet test tests/MacroDeck.SampleVirtualProfilePlugin.Tests
```

The harness tests seed the fake deck, widgets and scripts and then assert on what the plugin asked the
host to do; the wire test proves a widget interaction - the one operation with no reply - actually
arrived, by reading the profile back afterwards.
