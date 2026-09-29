module Forge.GitOps

open System
open System.IO
open Forge.Process

/// Translate a `GitAuth` into the inline `-c credential.helper=...` args (so we
/// never mutate global/user git config) and the env entries the helper needs.
let private authArgs (auth: GitAuth) : string list * (string * string) list =
    match auth with
    | NoAuth -> [], []
    | CredentialHelper (helperCommand, env) -> ["-c"; $"credential.helper={helperCommand}"], env

/// Ensure a bare shallow clone of `remoteUrl` exists at `basePath`.
/// If the directory already exists, assumes a valid clone and skips.
/// Returns the absolute base path on success.
let ensureClone (auth: GitAuth) (remoteUrl: string) (basePath: string) : Async<Result<string, string>> =
    async {
        if Directory.Exists(basePath) then
            return Ok basePath
        else
            try
                Directory.CreateDirectory(basePath) |> ignore
                let parent  = Path.GetDirectoryName(basePath)
                let dirName = Path.GetFileName(basePath)
                let helperArgs, env = authArgs auth
                let cloneArgs = helperArgs @ ["clone"; "--bare"; "--depth"; "1"; remoteUrl; dirName]
                let! result = runProcess "git" cloneArgs env parent
                match result with
                | Ok _ -> return Ok basePath
                | Error e ->
                    try Directory.Delete(basePath, true) with _ -> ()
                    return Error $"Failed to clone {remoteUrl}: {e}"
            with ex ->
                try Directory.Delete(basePath, true) with _ -> ()
                return Error $"Failed to clone {remoteUrl}: {ex.Message}"
    }

/// Read the default branch name from an existing bare clone at `basePath`.
/// Uses `git symbolic-ref HEAD` which reliably reflects the upstream default.
let getDefaultBranch (basePath: string) : Async<Result<string, string>> =
    async {
        let! result = runProcess "git" ["symbolic-ref"; "HEAD"] [] basePath
        match result with
        | Error e -> return Error $"Could not read default branch: {e}"
        | Ok symref ->
            let branch = symref.Trim().Replace("refs/heads/", "")
            return Ok branch
    }

/// Get or create a worktree for `branchSlug` at `worktreePath`, off the bare
/// clone at `basePath`. Prunes stale worktree registrations before adding to
/// avoid "already registered" errors. Creates a fresh branch from HEAD; if the
/// worktree path already exists returns it as-is.
let getWorktree (basePath: string) (worktreePath: string) (branchSlug: string) : Async<Result<string, string>> =
    async {
        if Directory.Exists(worktreePath) then
            return Ok worktreePath
        else
            // Prune stale registrations so a re-added branch slug doesn't fail.
            let! _ = runProcess "git" ["worktree"; "prune"] [] basePath
            let! result = runProcess "git" ["worktree"; "add"; worktreePath; "-b"; branchSlug] [] basePath
            match result with
            | Ok _    -> return Ok worktreePath
            | Error e -> return Error $"Failed to create worktree for {branchSlug}: {e}"
    }

/// Remove the worktree at `worktreePath`, registered against the bare clone at
/// `basePath`.
let cleanupWorktree (basePath: string) (worktreePath: string) : unit =
    if Directory.Exists(worktreePath) then
        try
            runProcess "git" ["worktree"; "remove"; worktreePath; "--force"] [] basePath
            |> Async.RunSynchronously
            |> ignore
        with _ -> ()
        try if Directory.Exists(worktreePath) then Directory.Delete(worktreePath, true) with _ -> ()

/// Remove an entire repo directory (base clone + all worktrees).
let cleanupAll (repoDir: string) : unit =
    try if Directory.Exists(repoDir) then Directory.Delete(repoDir, true) with _ -> ()

/// Stage all changes and commit in the given worktree directory.
/// Injects a fallback git identity for CI runners that have none configured.
/// Returns `Error "no-diff"` when there is nothing to commit.
let commitAll (worktreeDir: string) (message: string) : Async<Result<unit, string>> =
    async {
        let! addResult = runProcess "git" ["add"; "-A"] [] worktreeDir
        match addResult with
        | Error e -> return Error $"git add failed: {e}"
        | Ok _ ->
            // Exit 0 from diff --quiet means no staged changes; non-zero means changes exist.
            let! diffResult = runProcess "git" ["diff"; "--cached"; "--quiet"] [] worktreeDir
            match diffResult with
            | Ok _ ->
                return Error "no-diff"
            | Error _ ->
                // Provide a fallback identity for CI runners with no git config.
                let identityEnv =
                    [ "GIT_AUTHOR_NAME",     "orcai"
                      "GIT_AUTHOR_EMAIL",    "orcai@users.noreply.github.com"
                      "GIT_COMMITTER_NAME",  "orcai"
                      "GIT_COMMITTER_EMAIL", "orcai@users.noreply.github.com" ]
                let! commitResult = runProcess "git" ["commit"; "-m"; message] identityEnv worktreeDir
                match commitResult with
                | Ok _    -> return Ok ()
                | Error e -> return Error $"git commit failed: {e}"
    }

/// Push the worktree's current HEAD to `remoteBranch` on `remoteName`,
/// force-pushing unconditionally. The branch is orcai-owned and every run
/// starts from a fresh clone with no local remote-tracking ref for it, so
/// `--force-with-lease` would reject the push as "stale info" as soon as any
/// prior run had already pushed to that branch — there is nothing to compare
/// the lease against. Plain `--force` is correct here precisely because we
/// always intend to overwrite the branch regardless of its current remote state.
/// Uses HEAD:refs/heads/<remoteBranch> so the local branch name is irrelevant —
/// only the current HEAD commit and the desired remote branch name matter.
let pushBranch (auth: GitAuth) (remoteName: string) (worktreeDir: string) (remoteBranch: string) : Async<Result<unit, string>> =
    async {
        let helperArgs, env = authArgs auth
        let pushArgs = helperArgs @ ["push"; "--force"; remoteName; $"HEAD:refs/heads/{remoteBranch}"]
        let! result = runProcess "git" pushArgs env worktreeDir
        match result with
        | Ok _    -> return Ok ()
        | Error e -> return Error $"git push failed: {e}"
    }

/// Add a remote named `remoteName` pointing at `url` in `worktreeDir`. Errors
/// (e.g. the remote already existing) are surfaced but usually ignored by
/// callers that only care about idempotently having the remote present.
let addRemote (worktreeDir: string) (remoteName: string) (url: string) : Async<Result<unit, string>> =
    async {
        let! result = runProcess "git" ["remote"; "add"; remoteName; url] [] worktreeDir
        match result with
        | Ok _    -> return Ok ()
        | Error e -> return Error e
    }

/// Check whether `branch` exists as a head ref on the given remote `url`. No
/// local clone required — works against any git-accessible remote, e.g. a
/// local bare repo path, so it's directly testable.
let lsRemoteHeads (auth: GitAuth) (url: string) (branch: string) : Async<Result<bool, string>> =
    async {
        let helperArgs, env = authArgs auth
        let args = helperArgs @ ["ls-remote"; "--heads"; url; branch]
        let! result = runProcess "git" args env (Path.GetTempPath())
        match result with
        | Ok output -> return Ok (not (String.IsNullOrWhiteSpace(output.Trim())))
        | Error e   -> return Error e
    }
