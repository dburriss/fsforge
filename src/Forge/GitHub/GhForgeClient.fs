/// GitHub support backed by the `gh` CLI: git auth and an `IForgeClient` implementation.
module Forge.GitHub

open System.IO
open Forge
open Forge.Process

/// Env entries to inject a resolved gh token for the credential helper, or none
/// when the token is empty (i.e. rely on ambient `gh` auth).
let internal tokenEnv (token: string) = if token = "" then [] else [ "GH_TOKEN", token ]

/// Pure argument builder for `gh pr create`, kept separate from `runProcess` so
/// its shape is directly unit-testable without running a process.
let internal buildPrCreateArgs (repo: string) (head: string) (title: string) (body: string) : string list =
    ["pr"; "create"; "--repo"; repo; "--head"; head; "--title"; title; "--body"; body]

/// Build the `GitAuth` for git operations (clone/push) against GitHub, backed by
/// the `gh` CLI's credential helper.
let githubAuth (token: string) : GitAuth =
    CredentialHelper("!gh auth git-credential", tokenEnv token)

/// `IForgeClient` implementation backed by the `gh` CLI. `token` is passed to `gh`
/// as `GH_TOKEN`; pass an empty string to use the ambient `gh` authentication.
type GhForgeClient(token: string) =
    interface IForgeClient with
        member _.Fork(repo: string) : Async<Result<string, string>> =
            async {
                // Fork (idempotent — gh fork returns existing fork if already forked).
                let! forkResult = runProcess "gh" ["repo"; "fork"; repo; "--clone=false"] (tokenEnv token) (Path.GetTempPath())
                match forkResult with
                | Error e -> return Error $"gh repo fork failed: {e}"
                | Ok _ ->
                    // Determine the authenticated user's login to construct the fork name.
                    // `gh repo view` (no --repo) would return the *origin* repo, not the fork.
                    let! userResult = runProcess "gh" ["api"; "user"; "-q"; ".login"] (tokenEnv token) (Path.GetTempPath())
                    match userResult with
                    | Error e -> return Error $"Failed to resolve authenticated user: {e}"
                    | Ok userLogin ->
                        let login    = userLogin.Trim()
                        let repoName = repo.Split('/') |> Array.last
                        return Ok $"{login}/{repoName}"
            }

        member _.CurrentUserLogin() : Async<Result<string, string>> =
            async {
                let! result = runProcess "gh" ["api"; "user"; "-q"; ".login"] (tokenEnv token) (Path.GetTempPath())
                return result |> Result.map (fun s -> s.Trim())
            }

        member _.CreatePullRequest(repo: string, head: string, title: string, body: string, workingDir: string) : Async<Result<PrRef, string>> =
            async {
                let args = buildPrCreateArgs repo head title body
                let! result = runProcess "gh" args (tokenEnv token) workingDir
                return result |> Result.map (fun url -> { Url = url.Trim() })
            }
