# FsForge reference

| Page | Covers |
| --- | --- |
| [Types](types.md) | `GitAuth`, `PrRef`, `IForgeClient` |
| [GitOps](gitops.md) | `Forge.GitOps` — clone, worktree, commit, push, remote operations |
| [GitHub](github.md) | `Forge.GitHub` — `githubAuth`, `GhForgeClient` |
| [Process](process.md) | `Forge.Process.runProcess` — child-process helper |

## Conventions

- Operations that can fail return `Async<Result<_, string>>`; expected git/`gh` failures are returned as
  `Error`, not thrown. The exceptions are `cleanupWorktree` (best-effort, returns `unit`) and
  `cleanupAll` (synchronous `Result`).
- `git` and `gh` must be on `PATH`.
- Credentials are applied per call (`git -c credential.helper=...`, env on the child process only);
  global git config and the parent environment are never modified.
