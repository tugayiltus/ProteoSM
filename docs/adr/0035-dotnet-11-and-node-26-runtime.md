# ADR-0035: .NET 11 and Node 26 LTS Runtime

**Date**: 2026-11-23
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The backend ran on .NET 10 (LTS) and the frontend image built on Node 22. .NET 11 became generally available on 2026-11-10 as a Standard Term Support release. Under the current .NET support policy an STS release gets 24 months, so .NET 11 and .NET 10 both reach end of support in November 2028. Node 26 became the Active LTS line in October 2026. Dependabot opened PRs for `dotnet/sdk:11.0` (#21), `dotnet/aspnet:11.0` (#20) and `node:26-bookworm-slim` (#11). The image PRs alone are not safe: an `aspnet:11.0` image cannot run a `net10.0` build without roll-forward, and CI builds the images but never starts them. The owner asked for every library to move to its latest version. Related decisions: ADR-0001 (modular monolith), ADR-0024 (Docker Compose deployment).

## Decision

- Move the target framework, SDK, images and CI together in one commit (`ff1d5ec`): `TargetFramework` `net11.0` in `backend/Directory.Build.props`; `global.json` pins SDK `11.0.100` with `rollForward: latestFeature`; CI `setup-dotnet` `11.0.x`; images `mcr.microsoft.com/dotnet/sdk:11.0` and `aspnet:11.0`; the LDAP contract runner uses `sdk:11.0.100`.
- Move the Microsoft, EF Core and Npgsql packages to 11.0 at the same time, so that the runtime and the libraries come from one release train.
- Fix the new warnings, do not silence them: drop the explicit `Microsoft.Extensions.Options` reference (NU1510, now in the framework) and replace the obsolete `ExecuteSqlInterpolatedAsync` with `ExecuteSqlAsync` (CS0618).
- Build the frontend image on `node:26-bookworm-slim` and run CI on Node 26, the Active LTS line.

## Alternatives Considered

### Alternative 1: Stay on .NET 10 LTS until .NET 12 LTS
- **Pros**: No change of runtime; .NET 10 gets patches until November 2028; Dependabot image PRs #20 and #21 can be closed.
- **Cons**: The team skips the .NET 11 runtime, library and analyzer improvements and does the move from 10 to 12 in one larger step later. The 11.x packages stay outside the Dependabot minor and patch group.
- **Why not**: Support lasts equally long for both releases, so .NET 11 costs no support time. The move needed only two small code fixes, and all 1,395 backend tests pass on it.

### Alternative 2: Node 24 LTS for the image
- **Pros**: The older LTS line, in maintenance only from October 2026.
- **Cons**: It reaches end of life earlier than Node 26, and development already runs on Node 26.
- **Why not**: Node 26 is the Active LTS line and matches the development machines.

## Consequences

### Positive
- Runtime, SDK, packages, images and CI use one .NET release, and the Dependabot PRs #11, #20 and #21 become obsolete.
- The support window does not change: November 2028, as with .NET 10.

### Negative
- Every development machine and CI runner needs the .NET 11 SDK. The `global.json` pin makes an older SDK fail with a clear message.
- ASP.NET Core 11 emits OpenAPI 3.2.0. The generated `schema.d.ts` does not change.

### Risks
- .NET 12 (LTS, November 2027) must follow before November 2028.
- The deployed stack runs a new runtime. Run the full gates and a restore drill on the new images before the next release.
