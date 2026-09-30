// Fork a repo, push a change to the fork, and open a cross-repo pull request.
// Usage: dotnet fsi samples/fork-pr.fsx <owner/repo> <branch>
#r "../src/Forge/bin/Debug/net10.0/Forge.dll"

open System
open System.IO
open Forge
open Forge.GitHub

let unwrap r = match r with Ok v -> v | Error e -> failwith e

module Async =
    let map f a = async { let! x = a in return f x }

let repo = fsi.CommandLineArgs[1]
let branch = fsi.CommandLineArgs[2]
let token = Environment.GetEnvironmentVariable "GH_TOKEN" |> Option.ofObj |> Option.defaultValue ""
let auth = githubAuth token
let forge = GhForgeClient(token) :> IForgeClient

let root = Path.Combine(Path.GetTempPath(), "fsforge-sample")
let basePath = Path.Combine(root, "repo.git")
let worktreePath = Path.Combine(root, "wt")

async {
    // Fork is idempotent; returns "<you>/<repo name>".
    let! fork = forge.Fork repo |> Async.map unwrap
    let! login = forge.CurrentUserLogin() |> Async.map unwrap

    // Clone the upstream repo and branch off its default branch.
    let! _ = GitOps.ensureClone auth $"https://github.com/{repo}.git" basePath |> Async.map unwrap
    let! wt = GitOps.getWorktree basePath worktreePath branch |> Async.map unwrap

    File.WriteAllText(Path.Combine(wt, "hello.txt"), $"Hello from FsForge at {DateTime.UtcNow:o}\n")

    match! GitOps.commitAll wt "chore: add hello.txt" with
    | Error "no-diff" -> printfn "Nothing to commit"
    | Error e -> failwith e
    | Ok () ->
        // Add the fork as a remote (ignore "already exists"), then push to it.
        let! _ = GitOps.addRemote wt "fork" $"https://github.com/{fork}.git"
        do! GitOps.pushBranch auth "fork" wt branch |> Async.map unwrap

        // Cross-repo head is "owner:branch".
        let! pr = forge.CreatePullRequest(repo, $"{login}:{branch}", "Add hello.txt", "Opened with FsForge.", wt) |> Async.map unwrap
        printfn $"Opened {pr.Url}"

    GitOps.cleanupAll root |> unwrap
}
|> Async.RunSynchronously
