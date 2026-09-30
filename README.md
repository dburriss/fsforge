# FsForge

Provider-agnostic git and pull request write-back for F#: clone, worktree, commit, push and fork
mechanics behind a pluggable auth abstraction, plus a GitHub implementation built on the `gh` CLI.

## Install

[![NuGet](https://img.shields.io/nuget/vpre/FsForge)](https://www.nuget.org/packages/FsForge/)

Install from [NuGet](https://www.nuget.org/packages/FsForge/). Only prerelease versions are published so far, so
pass `--prerelease`:

```sh
dotnet add package FsForge --prerelease
```

Or reference it in your project file:

```xml
<PackageReference Include="FsForge" Version="0.0.1-beta1" />
```

In an F# script (`.fsx`):

```fsharp
#r "nuget: FsForge, 0.0.1-beta1"
```

Requires `git` (and `gh` when using the GitHub client) on `PATH`. Targets `net10.0`.

## Concepts

- **`GitAuth`** — `NoAuth | CredentialHelper of helperCommand * env`. Passed to git operations that
  touch a remote; passed to git as `-c credential.helper=<helperCommand>` with `env` set on the child
  process only.
- **`GitOps`** — remote-agnostic git primitives: `ensureClone`, `getDefaultBranch`, `getWorktree`,
  `commitAll`, `pushBranch`, `addRemote`, `lsRemoteHeads`, `cleanupWorktree`, `cleanupAll`.
- **`IForgeClient`** — the forge-specific operations that are not plain git: `Fork`,
  `CurrentUserLogin`, `CreatePullRequest`.
- **`Forge.GitHub`** — `githubAuth token` and `GhForgeClient(token)`, an `IForgeClient` using `gh`.

## Example

```fsharp
open Forge
open Forge.GitHub

let token = System.Environment.GetEnvironmentVariable "GH_TOKEN"
let auth = githubAuth token
let forge = GhForgeClient(token) :> IForgeClient

async {
    let! clone = GitOps.ensureClone auth "https://github.com/owner/repo.git" "/tmp/repo.git"
    // ... getWorktree, make changes, commitAll, pushBranch auth "origin" dir "my-branch" ...
    let! pr = forge.CreatePullRequest("owner/repo", "my-branch", "Title", "Body", "/tmp/worktree")
    return pr
}
```

All operations return `Async<Result<_, string>>`; nothing throws for an expected git/`gh` failure.

## Documentation

See the [reference docs](docs/README.md) for every operation on the main modules and types, and the [samples](samples/README.md) for runnable
workflows (worktree, commit and push, fork and PR).

## License

MIT
