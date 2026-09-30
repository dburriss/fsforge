// Clone a repo, create a worktree on a new branch, then clean up. Nothing is pushed.
// Usage: dotnet fsi samples/worktree.fsx <remote-url>
#r "../src/Forge/bin/Debug/net10.0/Forge.dll"

open System.IO
open Forge

let unwrap r = match r with Ok v -> v | Error e -> failwith e

module Async =
    let map f a = async { let! x = a in return f x }

let remoteUrl = fsi.CommandLineArgs[1]
let root = Path.Combine(Path.GetTempPath(), "fsforge-sample")
let basePath = Path.Combine(root, "repo.git")
let worktreePath = Path.Combine(root, "wt-demo")

async {
    // Bare, shallow clone. Skipped if basePath already exists.
    let! _ = GitOps.ensureClone NoAuth remoteUrl basePath |> Async.map unwrap
    let! branch = GitOps.getDefaultBranch basePath |> Async.map unwrap
    printfn $"Default branch: {branch}"

    let! wt = GitOps.getWorktree basePath worktreePath "demo-branch" |> Async.map unwrap
    printfn $"Worktree ready at {wt}"
    let files = Directory.GetFiles(wt) |> Array.map Path.GetFileName |> String.concat ", "
    printfn $"Files: {files}"

    GitOps.cleanupWorktree basePath worktreePath
    GitOps.cleanupAll root |> unwrap
    printfn "Cleaned up"
}
|> Async.RunSynchronously
