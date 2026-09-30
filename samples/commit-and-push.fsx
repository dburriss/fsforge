// Commit a change in a worktree and push it to origin.
// Usage: dotnet fsi samples/commit-and-push.fsx <owner/repo> <branch>
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

let root = Path.Combine(Path.GetTempPath(), "fsforge-sample")
let basePath = Path.Combine(root, "repo.git")
let worktreePath = Path.Combine(root, "wt")

async {
    let! _ = GitOps.ensureClone auth $"https://github.com/{repo}.git" basePath |> Async.map unwrap
    let! wt = GitOps.getWorktree basePath worktreePath branch |> Async.map unwrap

    File.WriteAllText(Path.Combine(wt, "hello.txt"), $"Hello from FsForge at {DateTime.UtcNow:o}\n")

    match! GitOps.commitAll wt "chore: add hello.txt" with
    | Error "no-diff" -> printfn "Nothing to commit"
    | Error e -> failwith e
    | Ok () ->
        // Force-pushes HEAD to origin/<branch>; use a branch you own.
        do! GitOps.pushBranch auth "origin" wt branch |> Async.map unwrap
        printfn $"Pushed {branch} to {repo}"

    GitOps.cleanupAll root |> unwrap
}
|> Async.RunSynchronously
