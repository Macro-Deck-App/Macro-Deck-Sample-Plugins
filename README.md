# Macro Deck sample plugins

Worked examples of out-of-process Macro Deck 3 plugins. They build against the published SDK packages
(`MacroDeck.Sdk`, `MacroDeck.Plugin.Hosting`, `MacroDeck.Plugin.Serilog`), so the Macro Deck source
tree is not needed to build them - only to run a host against them during development.

Each sample is one coherent, self-contained integration rather than a pile of snippets: its actions,
variables, events and providers read the same state, so what a widget shows and what a variable says
cannot disagree. None of them needs an external service, credentials or network access.

| Sample | Read it for |
| --- | --- |
| [`MacroDeck.SampleWeatherPlugin`](src/MacroDeck.SampleWeatherPlugin) | The smallest complete plugin: plain, slider and dynamic-options actions, variables, an event, a one-step config flow and a weather provider. |
| [`MacroDeck.SampleMusicPlayerPlugin`](src/MacroDeck.SampleMusicPlayerPlugin) | The full music-player surface: transport, artwork, catalogue browsing, output devices, two instances with different capabilities, dynamic event options and the client-side pickers. |
| [`MacroDeck.SampleRestApiPlugin`](src/MacroDeck.SampleRestApiPlugin) | A third-party REST API done properly: typed `HttpClient` through DI, a multi-step config flow with a secret and an OAuth branch, integration issues, notifications and API-backed variables and options. |
| [`MacroDeck.SampleVirtualProfilePlugin`](src/MacroDeck.SampleVirtualProfilePlugin) | A plugin-owned virtual profile with widget interactions, and the callbacks going the other way: deck navigation, widget appearance, scripts, host variables and notifications. |

Looking for a starting point for your own plugin rather than something to read? Use the
[plugin template repository](https://github.com/Macro-Deck-App/Macro-Deck-Plugin-Template) instead -
it is the same shape, set up to be renamed and stripped down.

## Capability coverage

Which sample demonstrates what, against the
[capability parity matrix](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/capability-parity.md).
Use it as the checklist when the Plugin API grows: a new capability belongs in an existing sample, or
in a new one.

| Capability | Weather | Music player | REST API | Virtual profile |
| --- | --- | --- | --- | --- |
| Actions | ● | ● | ● | ● |
| Dynamic action options | ● | ● | ● | ● |
| Slider actions (two-way state) | ● | ● | | |
| `ActionResult` error mapping | ● | ● | ● | ● |
| Conditional parameters (`OnlyWhen`) | | ● | ● | ● |
| Variables (text, numeric, boolean, unavailable) | ● | ● | ● | ● |
| Pushed variable updates (`IVariableApi`) | | | | ● |
| Events and payloads | ● | ● | ● | ● |
| Dynamic event options | | ● | | |
| Config flow | ● (one step) | | ● (multi-step, secret, OAuth) | |
| Integration issues and resolution | | | ● | |
| Icons / assets | ● | ● | ● | ● |
| Music player, catalog, devices, artwork | | ● | | |
| Weather provider | ● | | | |
| Virtual profiles and widget interactions | | | | ● |
| Deck navigation, widgets, scripts, notifications | | | ● (notifications) | ● |
| Action interaction pickers | | ● | | |
| `state.update` catalogue invalidation | ● | | | ● |

Two contracts have no sample on purpose:

- **`IConfigurableActionDefinition.DescriptiveUiSchema`** - the contract exists and travels over the
  wire, but no host renderer consumes it yet, so a sample would have to invent a schema format.
- **`IUserVariableApi`** - it edits user-owned variables a deck author created; a sample plugin has no
  such variable to edit without inventing one that only exists on the author's machine.

## Requirements

- .NET SDK 10.0
- Nothing else to build. Running against a real host needs a Macro Deck 3 host; the
  [`macrodeck-plugin` CLI](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/cli.md)
  runs a plugin against a disposable stub host without one.

## Quick start

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet tool install --global MacroDeck.Plugin.Cli --prerelease
```

```bash
macrodeck-plugin run --project src/MacroDeck.SampleWeatherPlugin
```

## Testing

Every sample has a test project next to it, built on
[`MacroDeck.Plugin.Testing`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/testing-plugins.md) -
the same package a plugin author is expected to test their own plugin with. The three subjects it
offers are used where each belongs, so the suite doubles as a worked example of how to test a plugin:

| Subject | Used for | Read |
| --- | --- | --- |
| `PluginTestHarness` | Behaviour: what an action does, what a variable reads, what an issue reports. | `*IntegrationTests.cs` in every test project |
| `MacroDeckTestHost.HostAsync` | The wire: serialization, published events, operations refused across the boundary. | `*OverTheWireTests.cs` |
| `MacroDeckTestHost.LaunchAsync` | The built artifact: a real process that starts, registers and shuts down. | `WeatherProcessTests.cs` |

The REST API sample's tests plug a deterministic fake server in as the typed client's primary handler
(`FakeTaskBoardApi.cs`), so the plugin's own request building and JSON parsing still run for real.

The [conformance suite](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/conformance.md)
checks a plugin against the protocol contract itself, and every sample passes it:

```bash
macrodeck-plugin test --project src/MacroDeck.SampleWeatherPlugin
```

## Building against a local SDK build

The samples track the SDK's *published* packages and float to the newest one, so a plain
`dotnet build` always resolves the latest release. While a change is still unreleased - a new API, a
fix you want to try - pack the SDK from a Macro Deck 3 checkout into this repository's `local-feed/`
and build against that version:

```bash
dotnet pack MacroDeck.slnx -c Release -p:Version=3.0.0-local.1 -o <path-to-this-repo>/local-feed
```

```bash
dotnet build -p:MacroDeckSdkVersion=3.0.0-local.1
```

`NuGet.config` already lists `local-feed/` as a package source, and `MacroDeckSdkVersion` sets the
version for every Macro Deck package at once (see `Directory.Packages.props`). Nothing in the
repository pins the local version, so a plain `dotnet build` goes back to the published one.

Pick a version that cannot collide with a real release - `3.0.0-local.N` rather than reusing a
published preview version, which would put a hand-built package into the global NuGet cache under the
name of a published one.

## Further reading

- [Plugin development docs](https://github.com/Macro-Deck-App/Macro-Deck-3/tree/main/docs/plugin-development)
- [`plugin-hosting.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/plugin-hosting.md) - the builder API, registration modes, the artifact format and every `MACRO_DECK_PLUGIN_*` variable
- [`sdk-reference.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/sdk-reference.md) - every interface and record you build against
- [`testing-plugins.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/testing-plugins.md) - the test harness, the fakes and the manual clock
- [`capability-parity.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/capability-parity.md) - where a plugin differs from an in-process integration, and why
- [`conformance.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/conformance.md) - the conformance suite and its check ids

## License

MIT - see [LICENSE](LICENSE). Macro Deck itself is licensed under Apache 2.0.
