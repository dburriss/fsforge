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

# Commits

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/#summary):

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

- Common types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`, `build`, `ci`
- `feat` triggers a MINOR version bump, `fix` a PATCH bump
- Breaking changes: add `!` after the type/scope (e.g. `fix!:`) or a `BREAKING CHANGE:` footer (MAJOR bump)

# Changelog

Maintain `CHANGELOG.md` following [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/):

- Newest release first, with an `## [Unreleased]` section at the top for pending changes
- Release headings: `## [x.y.z] - YYYY-MM-DD`
- Group entries under: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`
- Write for humans: describe user-visible changes, don't paste commit logs
- Link each version heading to its diff at the bottom of the file

# Bug fixes

Before fixing a bug, write a failing test that reproduces it. The test should fail against the current
code and pass after the fix is applied.
