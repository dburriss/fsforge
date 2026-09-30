# Types (`namespace Forge`)

## `GitAuth`

How git authenticates against a remote.

| Case | Meaning |
| --- | --- |
| `NoAuth` | Rely on whatever git is already configured with. |
| `CredentialHelper of helperCommand: string * env: (string * string) list` | Runs git with `-c credential.helper=<helperCommand>`; `env` is set on the child process only (e.g. a token variable the helper reads). |

## `PrRef`

```fsharp
type PrRef = { Url: string }
```

A pull request created on a forge.

## `IForgeClient`

Forge-specific operations that are not plain git. Implemented by [`GhForgeClient`](github.md).

| Member | Signature | Notes |
| --- | --- | --- |
| `Fork` | `repo: string -> Async<Result<string, string>>` | Forks `owner/name` under the authenticated user. Idempotent. Returns the fork's `owner/name`. |
| `CurrentUserLogin` | `unit -> Async<Result<string, string>>` | Login of the authenticated user. |
| `CreatePullRequest` | `repo * head * title * body * workingDir -> Async<Result<PrRef, string>>` | `head` is `branch` (same repo) or `owner:branch` (cross-repo). `workingDir` is the checked-out worktree the CLI runs in. |
