# Sample Music Player plugin

The complete music-player surface, on a made-up library rather than a real service: transport
controls, artwork, catalogue browsing, output devices and two instances with deliberately different
capabilities. Read it when your plugin drives something that plays media.

## Read these first

- **`MusicPlayerIntegration.cs`** - the provider: two instances over one library, plus the variables and
  the `track-changed` event, and `IDynamicEventOptionsProvider` so a subscription can name a player.
- **`Player/PlaybackEngine.cs`** - the state machine both instances run on. Position is derived from
  `TimeProvider` rather than a timer, so a paused player reports where it stopped and a test with a
  manual clock sees the same progression a real one would.
- **`Player/LibraryMusicPlayer.cs`** - the full instance: `IMusicPlayer` plus `IMusicPlayerCatalogProvider`
  and `IMusicPlayerDeviceProvider`. The host reads those two off the player object itself, which is why
  they live here and not on the provider.
- **`Player/SpeakerMusicPlayer.cs`** - the transport-only instance. It implements `IMusicPlayer` and
  nothing else, so the host reports `HasCatalog`/`HasDevices` as false for it and never offers those
  operations - the same way a real service supports browsing on some accounts but not others.
- **`Actions/PlayCatalogItemAction.cs`** - dynamic options that depend on another parameter, plus the
  item picker: leaving the item empty asks the client that pressed the button to choose one.
- **`Actions/TransferPlaybackAction.cs`** - the device-shaped counterpart, with the device picker.
- **`Actions/SetVolumeAction.cs`** - a slider bound to the selected player's actual volume.

Catalogue and device reads are the one place a failure must *throw* rather than degrade to an empty
result: "nothing found" and "could not load" have to look different in the UI. Everything else here
degrades - see the parity matrix.

## Running it against a local host

Use this project's **Macro Deck - Real Host** launch profile as described in the repository's
[run and debug guide](../../README.md#run-and-debug-against-macro-deck). Then add the "Sample library"
instance to a Music Player widget. Playback, artwork, the catalogue and the device list all work
without any account.

## Testing it

```bash
dotnet test tests/MacroDeck.SampleMusicPlayerPlugin.Tests
```

`MusicPlayerIntegrationTests` drives the capability directly and advances `harness.Clock` to prove the
position tracks the clock; `MusicPlayerOverTheWireTests` covers what changes shape on the wire - the
position, the enums and the artwork bytes.
