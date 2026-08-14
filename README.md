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
- A running Macro Deck desktop app for interactive debugging

## Quick start

```bash
dotnet build
```

```bash
dotnet test
```

## Run and debug against Macro Deck

Every source project contains one `.NET` launch profile named **Macro Deck - Real Host**. It starts the
plugin project directly, connects to `http://127.0.0.1:8193` in self-registering mode and stores its
local development credential under that project's `.macrodeck-dev-state/` directory. Because the IDE
launches the project itself, breakpoints work without attaching to a child process.

For the first run:

1. Start the Macro Deck desktop app.
2. Open **Developer Tools → Plugin tokens**, create a token and copy it. It is shown only once.
3. Store the token in the selected source project's **.NET User Secrets** using one of the methods
   below. The projects are already initialized; do not run `dotnet user-secrets init`.
4. Select **Macro Deck - Real Host** for that source project and start it with **Debug**.
5. Once enrollment succeeds, remove the token from User Secrets. Later launches reuse the credential
   in `.macrodeck-dev-state/`.

### Set the token in Rider or Visual Studio

In Rider, right-click the **source project** in the Solution Explorer and select
**Tools → .NET User Secrets**. In Visual Studio, right-click the source project and select
**Manage User Secrets**. Do this on `MacroDeck.SampleWeatherPlugin`, for example, not its `.Tests`
project.

The IDE opens a `secrets.json` file stored in your user profile, outside this repository. Replace its
contents with:

```json
{
  "MacroDeck:Plugin:EnrollmentToken": "<paste the one-time token here>"
}
```

Save the file, then start **Macro Deck - Real Host**. After enrollment, reopen `secrets.json` and
remove the `MacroDeck:Plugin:EnrollmentToken` entry.

### Set the token from a terminal

From the repository root on macOS or Linux, set `project` to the sample you want to enroll. This form
reads the token without echoing it and does not put the value in shell history or process arguments:

```bash
project="src/MacroDeck.SampleWeatherPlugin/MacroDeck.SampleWeatherPlugin.csproj"
printf "Enrollment token: "
read -rs md_enrollment_token
printf '\n'
printf '{"MacroDeck:Plugin:EnrollmentToken":"%s"}\n' "$md_enrollment_token" |
  dotnet user-secrets set --project "$project"
unset md_enrollment_token
```

After the first successful profile launch, remove the one-time token:

```bash
dotnet user-secrets remove "MacroDeck:Plugin:EnrollmentToken" --project "$project"
```

With PowerShell 7, use the equivalent masked-input form:

```powershell
$project = "src/MacroDeck.SampleWeatherPlugin/MacroDeck.SampleWeatherPlugin.csproj"
$token = Read-Host "Enrollment token" -MaskInput
@{ "MacroDeck:Plugin:EnrollmentToken" = $token } |
  ConvertTo-Json -Compress |
  dotnet user-secrets set --project $project
Remove-Variable token
```

Then remove it after enrollment:

```powershell
dotnet user-secrets remove "MacroDeck:Plugin:EnrollmentToken" --project $project
```

Repeat the setup for each sample you want to launch; every source project has its own User Secrets
store and plugin identity. User Secrets are local-only but not encrypted. Never put an enrollment
token in `launchSettings.json`, a shared IDE configuration, a literal command argument or a commit.
See the official [Rider User Secrets guide](https://www.jetbrains.com/help/rider/Manage_NET_user_secrets.html)
and [.NET Secret Manager guide](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
for more background.

The checked-in launch profile is the only supported interactive start path in this repository. The
CLI remains useful for the non-interactive conformance check below.

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
