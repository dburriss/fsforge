# General

- Prefer simple solutions
- Ask if unsure
- Keep answers concise
- Break solutions into small incremental steps
- Do one step at a time

# Tech stack

- .NET 10, F#
- `git` CLI, `gh` CLI

# Commands

```bash
dotnet build FsForge.slnx
dotnet test FsForge.slnx
./publish.sh --dry-run   # release flow; pushing a `v*` tag triggers the publish workflows
```

# Bug fixes

Before fixing a bug, write a failing test that reproduces it. The test should fail against the current
code and pass after the fix is applied.
