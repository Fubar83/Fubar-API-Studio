# Code signing (Windows) via SignPath Foundation

This guide sets up **free Authenticode code signing** for the Windows release binaries using the
[SignPath Foundation](https://signpath.org/) open-source program. Once configured, Windows no longer
shows the "unknown publisher" SmartScreen warning for `FubarAPIStudio.exe`.

The release pipeline (`.github/workflows/build.yml`) already contains **Windows signing steps that stay
dormant until you configure them** — they run only on the Windows build leg when the
`SIGNPATH_PROJECT_SLUG` repository *variable* is set, so releases keep working unsigned until everything
below is in place.

> Scope: SignPath Foundation covers **Windows Authenticode** only. macOS Gatekeeper still requires an
> Apple Developer ID ($99/yr) + notarization — see the end of this doc. Linux has no OS signing model
> (the checksums + Sigstore provenance attestations are the trust signal there).

## What only you can do (the manual gate)

SignPath Foundation issues the certificate after a human review of the project — there is no API for it.

1. **Apply** at <https://signpath.org/apply> (the "Open source" / Foundation program). Provide:
   - repository URL `https://github.com/Fubar83/Fubar-API-Studio`,
   - license (MIT), and a short description of the project.
   Approval typically takes a few days.
2. When approved you get a **SignPath.io organization**. Note your **Organization ID** (a GUID on the
   organization page).

## One-time SignPath configuration (after approval)

In the SignPath.io web app:

1. **Install the SignPath GitHub App** on this repository (SignPath → *Trusted Build Systems* →
   connect GitHub). This is how SignPath verifies each artifact really came from this repo's GitHub
   Actions build (origin verification) before signing it.
2. Create a **Project** — slug e.g. `fubar-api-studio`.
3. Add an **Artifact Configuration** that signs the PE files inside the release zip: an
   *Authenticode* signing step over `**/*.exe` (recurse into the uploaded `.zip`). Name it, e.g.,
   `zip-exe`.
4. Create a **Signing Policy** — slug e.g. `release-signing` — bound to that artifact configuration and
   the SignPath Foundation certificate.
5. Create a **User API token** (SignPath → your user → API tokens).

## GitHub repository settings

Add these under **Settings → Secrets and variables → Actions**:

**Secret**
| Name | Value |
| --- | --- |
| `SIGNPATH_API_TOKEN` | the SignPath user API token |

**Variables**
| Name | Value |
| --- | --- |
| `SIGNPATH_ORGANIZATION_ID` | your SignPath organization GUID |
| `SIGNPATH_PROJECT_SLUG` | e.g. `fubar-api-studio` |
| `SIGNPATH_SIGNING_POLICY_SLUG` | e.g. `release-signing` |

Setting `SIGNPATH_PROJECT_SLUG` is what **enables** the signing job. Until it exists, the job is skipped
and releases publish exactly as they do today.

## What happens on the next release

With the above in place, pushing a `v*` tag runs:

1. `build` (Windows leg) — after producing the single-file zips, the gated SignPath steps submit them
   for Authenticode signing and overwrite the zips **in place** with the signed versions, *before* the
   provenance attestation and artifact upload — so provenance and checksums cover the signed files.
2. `build` (Linux/macOS legs) — unchanged.
3. `release` — publishes the signed Windows zips (and the unsigned Linux/macOS artifacts) plus
   `SHA256SUMS.txt`.

Verify a signed download on Windows with:

```powershell
Get-AuthenticodeSignature .\FubarAPIStudio.exe | Format-List Status, SignerCertificate
```

## macOS (not covered here)

macOS trust needs an **Apple Developer ID** ($99/yr): `codesign --deep --options runtime` the `.app`,
then `xcrun notarytool submit … --wait` and `xcrun stapler staple`. If you get an Apple account, that's
a separate follow-up to `build.yml`'s macOS leg.
