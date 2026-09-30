# `Forge.Process`

## `runProcess`

```fsharp
runProcess (executable: string) (args: string list) (env: (string * string) list) (workingDir: string)
    : Async<Result<string, string>>
```

Runs a child process and returns its stdout.

- `args` are passed via `ArgumentList` — no shell, no escaping needed.
- `env` entries are set on the child only; the parent environment is untouched.
- stdout and stderr are read concurrently to avoid buffer deadlocks.
- Exit code `0` → `Ok stdout`; otherwise `Error "Exit <code>: <stderr>"`.
- Throws if the process cannot be started.

This is the building block used by `GitOps` and `GhForgeClient`.
