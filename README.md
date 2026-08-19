# Fubar API Studio

A fast, cross-platform desktop **API client** — think Postman/Insomnia, but native, open source,
and built on [Avalonia](https://avaloniaui.net/) + .NET 10. Design requests, manage environments and
secrets, import OpenAPI/Swagger specs, and handle real OAuth 2.0 flows — all from one window.

[![CI](https://github.com/Fubar83/Fubar-API-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Fubar83/Fubar-API-Studio/actions/workflows/ci.yml)
[![Release](https://github.com/Fubar83/Fubar-API-Studio/actions/workflows/build.yml/badge.svg)](https://github.com/Fubar83/Fubar-API-Studio/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20macOS%20%7C%20Linux-blue.svg)](#download)

> **Status:** pre-1.0 and under active development. Expect rough edges; feedback and PRs welcome.

<!-- Add a screenshot or GIF here once you have one — it does more than any paragraph.
     e.g. ![Fubar API Studio](docs/screenshot.png) -->

## Features

- **Request builder** — method + URL bar with live `{{variable}}` highlighting, and tabs for
  Params, Headers, Body, Auth, and per-request History/replay. URL and Params stay in two-way sync.
- **Environments & variables** — `{{key}}` resolves from the active environment. Values can be marked
  **secret**, and some variables are **session-only** (held in memory, never written to disk).
- **OAuth 2.0 that actually works** — Client Credentials and Refresh Token grants, configurable scopes
  and client-auth method, a one-click **Test / Get token** and a **Verify request** preview. Access
  tokens and expiry are stored as session variables and auto-refreshed when expired.
- **Auth profiles** — reusable Bearer / API key / Basic / OAuth2 profiles, inheritable down the folder
  tree, previewed as the exact headers that will be sent.
- **OpenAPI / Swagger import** — pull a spec (JSON or YAML, from a file or URL) into a workspace:
  requests, environments, variables, and auth profiles, with `$ref` / `allOf` resolution.
  Re-importing shows a **diff** (add / update / unchanged / remove, per request and per variable) so
  your manual edits survive — you choose what to apply.
- **JSON schema intelligence** — when a body schema is known (e.g. from an import), you get validation,
  inline autocomplete, and a readable schema view. Header and query-parameter **names** are suggested
  too (schema-declared names plus common HTTP headers).
- **Workspace tabs** — Chrome-style tab strip: drag to reorder, drag between windows, or tear a tab off
  into its own window.
- **Cross-platform** — Windows, macOS, and Linux, from a single codebase.

## Download

Pre-built, self-contained binaries (no runtime install required) are attached to each
[GitHub Release](https://github.com/Fubar83/Fubar-API-Studio/releases):

| Platform | Artifact |
| --- | --- |
| Windows (x64 / ARM64) | `FubarAPIStudio-win-*.zip` |
| Linux (x64 / ARM64)   | `FubarAPIStudio-linux-*.tar.gz` |
| macOS (Apple Silicon / Intel) | `FubarAPIStudio-osx-*.zip` (a `.app` bundle) |

> On macOS the app is currently **unsigned** — first launch may need
> *right-click → Open* (or `xattr -dr com.apple.quarantine "Fubar API Studio.app"`).

## Build from source

**Prerequisites:** the [.NET 10 SDK](https://dotnet.microsoft.com/download). PowerShell 7+ is only
needed for the packaging script.

```bash
git clone https://github.com/Fubar83/Fubar-API-Studio.git
cd Fubar-API-Studio

# Restore + build everything
dotnet build Fubar.slnx

# Run the app
dotnet run --project src/Fubar.Studio.UI

# Run the tests
dotnet test Fubar.slnx
```

Explore the shared UI component library on its own in the sandbox:

```bash
dotnet run --project src/Fubar.Controls.Gallery
```

### Packaging release binaries

`build/publish.ps1` produces self-contained, per-runtime binaries and packages each one (zip for
Windows, `tar.gz` for Linux, a `.app` for macOS). It runs from any OS with PowerShell 7+:

```powershell
./build/publish.ps1                        # all default runtimes
./build/publish.ps1 -Runtimes osx-arm64    # just one
./build/publish.ps1 -Version 1.2.3
```

> macOS bundles keep their executable bit / symlinks only when zipped **on** macOS, which is why CI
> builds each OS on its native runner. See the script header for details.

## Project structure

This is a layered solution: an app-agnostic control library sits underneath the app-specific code.

| Project | Role |
| --- | --- |
| `src/Fubar.Controls` | **Reusable, app-agnostic** Avalonia control library + design system (tabs, tree view, key/value grid, JSON editor, theming). No dependency on the app. |
| `src/Fubar.Controls.Gallery` | A living style guide / dev sandbox that references **only** `Fubar.Controls`. |
| `src/Fubar.Studio.Core` | Domain models and abstractions — requests, auth, variables, workspaces, import contracts. |
| `src/Fubar.Studio.Infrastructure` | Implementations — HTTP execution, OpenAPI import, OAuth token service, variable resolution, persistence. |
| `src/Fubar.Studio.UI` | The desktop application (Avalonia + MVVM). Ships as `FubarAPIStudio`. |
| `tests/*` | xUnit test projects for Core, Infrastructure, and Controls. |

Deeper design notes live in [`docs/`](docs/): the [Left Pane](docs/LeftPane.md),
[Request Editor](docs/RequestEditorPane.md), and [Response Pane](docs/ResponsePane.md).

## Tech stack

- **[.NET 10](https://dotnet.microsoft.com/)** / C#
- **[Avalonia](https://avaloniaui.net/)** — cross-platform UI, Fluent theme
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)** — MVVM source generators
- **[AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)** + TextMate — the JSON editor & highlighting
- **[JsonSchema.Net](https://docs.json-everything.net/)** — body validation
- **[YamlDotNet](https://github.com/aaubry/YamlDotNet)** — YAML spec parsing
- **[xUnit](https://xunit.net/)** — tests (incl. headless Avalonia UI tests)

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, coding
conventions, and the architectural boundary between `Fubar.Controls` and the app. By participating you
agree to the [Code of Conduct](CODE_OF_CONDUCT.md).

Found a security issue? See [SECURITY.md](SECURITY.md) — please report it privately, not as a public
issue.

## License

Released under the [MIT License](LICENSE).
