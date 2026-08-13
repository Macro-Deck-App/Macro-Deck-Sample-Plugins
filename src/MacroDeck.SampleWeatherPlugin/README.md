# Sample Weather plugin

The smallest complete plugin: one integration covering actions, variables, an event, a config flow and
a provider capability, with everything reading the same synthetic weather reading. Start here if you
have not written a Macro Deck plugin before, then read
[`plugin-hosting.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/plugin-hosting.md)
for the builder API and the registration modes this assumes.

It needs no external service: the station computes its reading from a running counter, so the plugin
builds, runs and tests without credentials or network access.

## Read these first

- **`WeatherIntegration.cs`** - the integration: `IPluginIntegration` plus `IVariableProvider`,
  `IEventProvider`, `IConfigFlowProvider` and `IWeatherProvider`. It holds the two pieces of state
  everything else reads, and `InitializeAsync` is where a plugin's config-flow story completes - the
  configured location is read back out of `IIntegrationContext.Config` there, not in the flow.
- **`Weather/SyntheticWeatherStation.cs`** - the provider capability. `GetSnapshotAsync` hands back a
  cached reading rather than fetching one, which is the shape `IWeatherStation` asks for.
- **`Actions/RefreshWeatherAction.cs`** - the plain action, and the one button that makes everything
  else in the sample visibly move.
- **`Actions/SetAlertThresholdAction.cs`** - the slider action: `GetSliderStateAsync` reads the value
  back, so a Slider widget shows the threshold that is actually set.
- **`Actions/SetConditionAction.cs`** - the dynamic-options action: its field has no static options and
  `GetDynamicOptionsAsync` supplies them.
- **`ConfigFlow/LocationConfigFlow.cs`** - deliberately the contract's floor: one step, one required
  field. See the REST API sample for a multi-step flow with secrets and OAuth.
- **`Assets/icon.svg`** - declared by `manifest.json`; the plugin's own code never touches it.

## Running it against a local host

1. `dotnet run --project src/MacroDeck.SampleWeatherPlugin`, with `MACRO_DECK_PLUGIN_HOST_URL` pointing
   at your running host (for example `http://127.0.0.1:7193`).
2. This sample registers itself, so it runs in **self-registering** mode: create a token under
   **Developer → Plugin tokens** in the desktop app and set `MACRO_DECK_PLUGIN_ENROLLMENT_TOKEN` before
   starting. The plugin exchanges it for a secret once and reuses that secret afterwards.
3. It then shows up as "Sample Weather" with three actions, a config flow, a weather station and two
   variables. Configure a location, add the station to a Weather widget, and press "Refresh weather" to
   watch the location, the temperature and the event move together.

Self-registering mode only works against a host on the same machine - plugin endpoints are local-only
by design.

## Testing it

[`tests/MacroDeck.SampleWeatherPlugin.Tests`](../../tests/MacroDeck.SampleWeatherPlugin.Tests) uses all
three levels of `MacroDeck.Plugin.Testing`: `PluginTestHarness` for behaviour, `MacroDeckTestHost` for
the wire, and one `LaunchAsync` test that starts the built executable as a real process.

```bash
dotnet test tests/MacroDeck.SampleWeatherPlugin.Tests
```

```bash
macrodeck-plugin test --project src/MacroDeck.SampleWeatherPlugin
```
