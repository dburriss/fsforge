# `Forge.GitOps`

Git primitives implemented by shelling out to `git`. None depend on a specific forge.

| Function | Signature | Returns |
| --- | --- | --- |
| [`ensureClone`](#ensureclone) | `GitAuth -> remoteUrl -> basePath -> Async<Result<string, string>>` | `basePath` |
| [`getDefaultBranch`](#getdefaultbranch) | `basePath -> Async<Result<string, string>>` | Branch name |
| [`getWorktree`](#getworktree) | `basePath -> worktreePath -> branchSlug -> Async<Result<string, string>>` | `worktreePath` |
| [`commitAll`](#commitall) | `worktreeDir -> message -> Async<Result<unit, string>>` | — |
| [`pushBranch`](#pushbranch) | `GitAuth -> remoteName -> worktreeDir -> remoteBranch -> Async<Result<unit, string>>` | — |
| [`addRemote`](#addremote) | `worktreeDir -> remoteName -> url -> Async<Result<unit, string>>` | — |
| [`lsRemoteHeads`](#lsremoteheads) | `GitAuth -> url -> branch -> Async<Result<bool, string>>` | Whether the branch exists |
| [`cleanupWorktree`](#cleanupworktree) | `basePath -> worktreePath -> unit` | — |
| [`cleanupAll`](#cleanupall) | `repoDir -> Result<unit, string>` | — |

## ensureClone

Ensures a **bare, shallow** (`--depth 1`) clone of `remoteUrl` exists at `basePath`. If the directory
already exists it is assumed to be a valid clone and nothing runs. On failure the partially created
directory is removed.

## getDefaultBranch

Reads the default branch of the bare clone at `basePath` via `git symbolic-ref HEAD`
(`refs/heads/` prefix stripped).

## getWorktree

Creates a worktree at `worktreePath` on a new branch `branchSlug` from the bare clone's HEAD. Prunes
stale worktree registrations first. If `worktreePath` already exists it is returned as-is.

## commitAll

Runs `git add -A` and commits in `worktreeDir`. Uses a fallback identity (`fsforge` /
`fsforge@users.noreply.github.com`) via environment so it works on CI runners with no git config.
Returns `Error "no-diff"` when there is nothing to commit — callers can match on this string to treat
it as a non-failure.

## pushBranch

Force-pushes (`--force`) the worktree's `HEAD` to `refs/heads/<remoteBranch>` on `remoteName`. The local
branch name is irrelevant. Plain `--force` is used deliberately: a fresh clone has no remote-tracking ref,
so `--force-with-lease` would reject the push. The branch must be owned by the caller.

## addRemote

Runs `git remote add <remoteName> <url>` in `worktreeDir`. Fails if the remote already exists; callers
wanting idempotence can ignore the `Error`.

## lsRemoteHeads

Checks whether `branch` exists as a head on the remote `url` (`git ls-remote --heads`). No local clone is
needed; works with any git-accessible remote, including a local bare repo path.

## cleanupWorktree

Best-effort removal of the worktree (`git worktree remove --force`, then a directory delete). Never
throws and reports nothing. No-op if the path is absent.

## cleanupAll

Deletes `repoDir` (base clone and all worktrees). Succeeds if it is already absent; returns `Error` if
deletion fails.

## Typical flow

```fsharp
let! clone    = GitOps.ensureClone auth url basePath
let! branch   = GitOps.getDefaultBranch basePath
let! worktree = GitOps.getWorktree basePath worktreePath "my-branch"
// ... modify files in worktreePath ...
let! commit   = GitOps.commitAll worktreePath "feat: change"
let! push     = GitOps.pushBranch auth "origin" worktreePath "my-branch"
GitOps.cleanupWorktree basePath worktreePath
```
