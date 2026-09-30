# `Forge.GitHub`

GitHub support backed by the `gh` CLI.

## `githubAuth`

```fsharp
githubAuth (token: string) : GitAuth
```

Returns `CredentialHelper("!gh auth git-credential", env)` for use with `GitOps.ensureClone`,
`pushBranch` and `lsRemoteHeads`. A non-empty `token` is passed as `GH_TOKEN`; an empty string uses the
ambient `gh` authentication.

## `GhForgeClient`

```fsharp
let forge = GhForgeClient(token) :> IForgeClient
```

`IForgeClient` implementation. `token` is passed to `gh` as `GH_TOKEN` (empty string = ambient auth). The
methods are explicit interface members, so cast to `IForgeClient` to call them.

| Method | Underlying call | Result |
| --- | --- | --- |
| `Fork repo` | `gh repo fork <repo> --clone=false`, then `gh api user` | `"<login>/<repo name>"`. Idempotent. |
| `CurrentUserLogin ()` | `gh api user -q .login` | Trimmed login. |
| `CreatePullRequest (repo, head, title, body, workingDir)` | `gh pr create --repo --head --title --body` in `workingDir` | `{ Url }` of the new PR. |

## Fork-and-PR example

```fsharp
let! fork  = forge.Fork "owner/repo"          // Ok "me/repo"
let! login = forge.CurrentUserLogin()
// push to the fork, then open a cross-repo PR:
let! pr = forge.CreatePullRequest("owner/repo", $"{login}:my-branch", "Title", "Body", worktree)
```
