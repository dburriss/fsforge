# Samples

Runnable F# scripts for common workflows. Build the library first, then run a script with `dotnet fsi`:

```sh
dotnet build FsForge.slnx
dotnet fsi samples/worktree.fsx <remote-url>
```

| Script | Arguments | Shows |
| --- | --- | --- |
| [`worktree.fsx`](worktree.fsx) | `<remote-url>` | Clone, create a worktree, clean up. Local only, nothing is pushed. |
| [`commit-and-push.fsx`](commit-and-push.fsx) | `<owner/repo> <branch>` | Commit a change and push it to `origin`. |
| [`fork-pr.fsx`](fork-pr.fsx) | `<owner/repo> <branch>` | Fork, push to the fork, open a cross-repo PR. |

The GitHub scripts use the `gh` CLI; set `GH_TOKEN`, or leave it unset to use your ambient `gh` login.
`commit-and-push.fsx` and `fork-pr.fsx` push to real repositories, so point them at one you own.
