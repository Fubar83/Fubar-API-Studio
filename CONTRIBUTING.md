# Contributing to Fubar API Studio

Thanks for your interest in improving Fubar API Studio! This guide covers how to get set up, the
conventions the codebase follows, and how to get a change merged.

By participating in this project you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Any editor works; Visual Studio,
JetBrains Rider, and VS Code (with the C# Dev Kit) all understand the `.slnx` solution.

```bash
git clone https://github.com/Fubar83/Fubar-API-Studio.git
cd Fubar-API-Studio
dotnet build Fubar.slnx
dotnet test  Fubar.slnx
dotnet run   --project src/Fubar.Studio.UI
```

For UI-component work, the sandbox is faster to iterate in than the full app:

```bash
dotnet run --project src/Fubar.Controls.Gallery
```

## How to contribute

- **Bugs & features:** please [open an issue](https://github.com/Fubar83/Fubar-API-Studio/issues)
  first (use the templates). For bugs, include your OS, the version/commit, and repro steps.
- **Small fixes** (typos, obvious bugs): a direct PR is fine.
- **Larger changes:** open an issue to discuss the approach before investing a lot of time — it saves
  everyone rework.
- **Security issues:** do **not** open a public issue. Follow [SECURITY.md](SECURITY.md).

### Pull request workflow

1. Fork and create a branch off `main` (e.g. `feat/oauth-pkce`, `fix/tab-drag-cursor`).
2. Make your change with tests where it makes sense.
3. Ensure `dotnet build Fubar.slnx` is warning-clean and `dotnet test Fubar.slnx` is green.
4. Update `CHANGELOG.md` under **Unreleased** and any affected docs.
5. Open the PR against `main`, fill in the template, and link the issue it closes.

CI (build + test) must pass before a PR can be merged.

## Architecture & conventions

The single most important rule in this codebase:

> **`Fubar.Controls` is app-agnostic.** It is a reusable Avalonia control library and must **never**
> reference `Fubar.Studio.*`, view models, or any API-client domain concept. App-specific panes
> (Request/Response/Left pane) live in `Fubar.Studio.UI`; only their generic building blocks belong in
> `Fubar.Controls`. The `Fubar.Controls.Gallery` sandbox references *only* `Fubar.Controls`, which is
> what keeps this boundary honest.

Other conventions:

- **MVVM** via CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`).
  View models hold logic; views stay thin.
- **Variables** resolve from the **active environment** (plus the in-memory session store for things
  like OAuth tokens), never directly from files.
- **Match the surrounding code** — naming, comment density, and idioms. Comments explain *why*, not
  *what*.
- Keep the build **warning-clean**; analyzers are enabled repo-wide (`Directory.Build.props`).
- Package versions are centrally managed in `Directory.Packages.props` (Central Package Management) —
  add versions there, not in individual `.csproj` files.
- `.editorconfig` defines formatting; run `dotnet format` if in doubt.

## Tests

- `tests/Fubar.Studio.Core.Tests` and `tests/Fubar.Studio.Infrastructure.Tests` — xUnit unit tests.
- `tests/Fubar.Controls.Tests` — headless Avalonia UI tests for the control library.

Run everything with `dotnet test Fubar.slnx`.

## License

By contributing, you agree that your contributions are licensed under the project's
[MIT License](LICENSE).
