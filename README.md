# Macro Deck sample plugins

Worked examples of out-of-process Macro Deck 3 plugins. They build against the published SDK packages
(`MacroDeck.Sdk`, `MacroDeck.Plugin.Hosting`, `MacroDeck.Plugin.Serilog`), so the Macro Deck source
tree is not needed to build them - only to run a host against them during development.

| Sample | What it shows |
| --- | --- |
| [`src/MacroDeck.SamplePlugin`](src/MacroDeck.SamplePlugin) | One integration across every capability kind: three actions (plain, slider, dynamic options), variables, an event, a config flow, a weather station and an icon. |

Looking for a starting point for your own plugin rather than something to read? Use the
[plugin template repository](https://github.com/Macro-Deck-App/Macro-Deck-Plugin-Template) instead -
it is the same shape, set up to be renamed and stripped down.

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
dotnet tool install --global MacroDeck.Plugin.Cli --prerelease
```

```bash
macrodeck-plugin run --project src/MacroDeck.SamplePlugin
```

## Building against a local SDK build

> **Right now this is not optional.** The samples use SDK surface that is not in `3.0.0-preview.1`
> yet (`IPluginIntegration`, the optional `ProviderName`), so a plain `dotnet build` fails until the
> next preview is published. Build against a locally packed SDK as described here until then.

The samples track the SDK's *published* packages. While a change is still unreleased - a new API, a
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

Pick a version that cannot collide with a real release - `3.0.0-local.N` rather than reusing
`3.0.0-preview.1`, which would put a hand-built package into the global NuGet cache under the name of
a published one.

## Further reading

- [Plugin development docs](https://github.com/Macro-Deck-App/Macro-Deck-3/tree/main/docs/plugin-development)
- [`plugin-hosting.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/plugin-hosting.md) - the builder API, registration modes, the artifact format and every `MACRO_DECK_PLUGIN_*` variable
- [`sdk-reference.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/sdk-reference.md) - every interface and record you build against
- [`conformance.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/conformance.md) - the conformance suite and its check ids

## License

MIT - see [LICENSE](LICENSE). Macro Deck itself is licensed under Apache 2.0.
