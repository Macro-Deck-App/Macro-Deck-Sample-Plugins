# Sample plugin

This project is the worked example: one out-of-process plugin exercising every capability kind a Macro
Deck plugin can offer, built against the public SDK surface only - `MacroDeck.Plugin.Hosting`,
`MacroDeck.Sdk` and `MacroDeck.Plugin.Protocol` - with no host-side change required to run it. Read
[`plugin-hosting.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/plugin-hosting.md) first if you have not: this README assumes the builder API, the registration modes and the reserved
routes it covers.

The sample is deliberately small and self-contained. It needs no external service: its weather station
reports a synthetic reading computed from a running counter, so the whole thing can be built, run and
tested without any credentials or network access. What it demonstrates is the shape a real plugin
takes, not a real weather integration.

## What it demonstrates, and where

Every piece connects to the others rather than standing alone - a Weather widget pointed at this
plugin, a variable reading its temperature, and a config flow that changes what both of them say, all
driven from the same `SampleIntegration`:

- **`SampleIntegration.cs`** is the integration itself: `IPluginIntegration` plus `IVariableProvider`,
  `IEventProvider`, `IConfigFlowProvider` and `IWeatherProvider`. It holds
  the two pieces of state everything else reads - the configured location name and the alert
  threshold - and `InitializeAsync` is where a real plugin's config-flow story completes: it reads the
  location back out of `IIntegrationContext.Config`, the same division of labor
  `SpotifyIntegration.ConnectFromConfig` uses in the host's own Spotify integration. `IWeatherProvider.ProviderName`
  is set explicitly to `"Sample"` - the sample deliberately demonstrates that path, rather than relying on
  the fallback to the manifest's `name` every other provider-shaped kind here uses.
- **`Actions/RefreshWeatherAction.cs`** is the plain action: no parameters, it just advances the
  synthetic reading by one tick. Pressing it is what makes the rest of the sample visibly do something.
- **`Actions/SetAlertThresholdAction.cs`** is the slider action (`ISliderActionDefinition`): drag it to
  set the temperature above which a refresh reports an alert; `GetSliderStateAsync` reads the value
  back for two-way binding with a Slider widget.
- **`Actions/SetConditionAction.cs`** is the dynamic-options action (`IDynamicOptionsActionDefinition`):
  its one field has no static options, and `GetDynamicOptionsAsync` supplies a curated list of
  `WeatherCondition` values itself, so a demo deck can force any condition on demand instead of waiting
  for the sine wave to reach it.
- **`ConfigFlow/SampleConfigFlow.cs`** is the config flow (`IConfigFlow`): one step, one required text
  field for the location name. It is intentionally the simplest legal shape - no OAuth, no multi-step
  branching - so it reads as the contract's floor; see the Spotify integration for an OAuth-based flow.
- **`Weather/SampleWeatherStation.cs`** is the provider capability (`IWeatherStation`), chosen because
  it is the one kind whose contract is synchronous-snapshot rather than fetch-on-demand:
  `GetSnapshotAsync` always hands back a cached reading, and only `Tick()` (called by the refresh
  action) computes a new one - the shape `IWeatherStation`'s own doc comments ask every provider for.
- **Variables** (`sample_location`, `sample_temperature_celsius`) and the **`weather-refreshed` event**
  are both declared directly on `SampleIntegration` and both read from the same station reading the
  weather provider serves, so a variable, an event payload and a widget snapshot never disagree.
- **`Assets/icon.svg`** is declared by `manifest.json`'s `icon` field and read straight off disk by
  `PluginHostBuilder.Build()` (#560) - the sample's own code never touches it. It is still an SDK-managed
  `Content` item, so it lands next to the built executable the same way `manifest.json` does.

## Running it against a local host

1. Build and run the project (`dotnet run --project src/MacroDeck.SamplePlugin`). By default it
   listens on a loopback port the OS assigns and tries to reach a host at `MACRO_DECK_PLUGIN_HOST_URL`
   (defaulting to whatever `MacroDeck.Plugin.Hosting`'s own configuration chain resolves - see
   `plugin-hosting.md`'s environment variable table). Point it at your running Macro Deck host, e.g.
   `MACRO_DECK_PLUGIN_HOST_URL=http://127.0.0.1:7193`.
2. This sample registers itself rather than being launched by the host, so it runs in
   **self-registering** mode: create a Developer token under **Developer → Plugin tokens** in the
   desktop app, then set `MACRO_DECK_PLUGIN_ENROLLMENT_TOKEN` to it before starting the process. On
   first start the plugin exchanges the token for a secret and persists it under the plugin's state
   directory (`MACRO_DECK_PLUGIN_STATE_DIRECTORY`, or the platform default `plugin-hosting.md`
   documents); every later run reuses that secret instead of enrolling again.
3. Once connected, the sample shows up as an integration named "Sample" with its icon, three actions, a
   config flow, a weather station and two variables - configure a location through its config flow,
   then add its weather station to a Weather widget and press "Refresh weather" to watch the location,
   temperature and event all move together.

Self-registering mode only works against a host on the same machine - plugin endpoints are local-only,
by design (see `plugin-hosting.md`'s "Registration modes" section).

## Testing it without a host

`MacroDeck.Plugin.Testing` runs a plugin against a loopback test host - no process, no socket, no
Macro Deck installed. `PluginTestHarness` builds the plugin exactly as `Program.cs` does and hands
back typed capability clients, so a test can execute an action, read a variable, step a config flow or
take a weather snapshot and assert on the result. See
[`testing-plugins.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/testing-plugins.md) for the harness and the
fakes it ships with.

## Conformance

The [conformance suite](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/conformance.md) checks a plugin against the
protocol contract - capability contracts, invocation and cancellation semantics, reconnect and resume
behaviour, the reserved `/_macrodeck/*` endpoints, logging limits. Run it against this sample, no host
required:

```bash
macrodeck-plugin test --project src/MacroDeck.SamplePlugin
```

See [cli.md](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/cli.md#test) for the command's options and report formats.
