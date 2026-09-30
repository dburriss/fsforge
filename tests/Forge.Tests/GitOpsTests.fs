module Forge.Tests.GitOpsTests

open System.IO
open System.Diagnostics
open Xunit
open Forge
open Forge.GitOps

let private run (exe: string) (args: string list) (wd: string) =
    let psi = ProcessStartInfo(exe)
    psi.WorkingDirectory       <- wd
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError  <- true
    psi.UseShellExecute        <- false
    for a in args do psi.ArgumentList.Add(a)
    use p = Process.Start(psi)
    p.WaitForExit()
    p.ExitCode = 0

// ---------------------------------------------------------------------------
// pushBranch — local branch name vs remote branch name
// ---------------------------------------------------------------------------

[<Fact>]
let ``pushBranch can push HEAD to a remote branch whose name differs from the local branch`` () =
    // Reproduces the cmd-to-github push bug:
    // getWorktree creates local branch "test-slug" but pushBranch is called
    // with "fsforge/test-slug" (the rendered branch name, e.g. "fsforge/{{job_title_slug}}"),
    // causing `git push origin fsforge/test-slug` to fail with
    // "src refspec does not match any".
    let guid      = System.Guid.NewGuid().ToString("N")
    let seedDir   = Path.Combine(Path.GetTempPath(), $"forge-seed-{guid}")  // non-bare origin with a commit
    let remoteDir = Path.Combine(Path.GetTempPath(), $"forge-r-{guid}")     // bare clone used as "origin" for push
    let baseDir   = Path.Combine(Path.GetTempPath(), $"forge-b-{guid}")     // bare clone used as "base" (ensureClone)
    let wtDir     = Path.Combine(Path.GetTempPath(), $"forge-w-{guid}")     // worktree
    try
        // 1. Create a non-bare seed repo with one commit, then bare-clone it as "origin".
        //    This gives us a bare remote whose HEAD has a valid commit to branch from.
        Directory.CreateDirectory(seedDir) |> ignore
        Assert.True(run "git" ["-c"; "init.defaultBranch=main"; "init"; seedDir] (Path.GetTempPath()), "git init seed")
        File.WriteAllText(Path.Combine(seedDir, "README.md"), "init")
        Assert.True(run "git" ["add"; "README.md"] seedDir, "git add in seed")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "init"] seedDir, "git commit in seed")
        Assert.True(run "git" ["clone"; "--bare"; seedDir; remoteDir] (Path.GetTempPath()), "git clone bare as remote")

        // 2. Bare clone (simulating ensureClone against remoteDir as the push target)
        Assert.True(run "git" ["clone"; "--bare"; remoteDir; baseDir] (Path.GetTempPath()), "git clone bare as base")

        // 3. Worktree on branch "test-slug" (simulating getWorktree with branchSlug only)
        Assert.True(run "git" ["worktree"; "add"; wtDir; "-b"; "test-slug"] baseDir, "git worktree add")

        // 4. Make a diff-producing change and commit in the worktree
        File.WriteAllText(Path.Combine(wtDir, "touch.txt"), "hello")
        Assert.True(run "git" ["add"; "-A"] wtDir, "git add in wt")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "test"] wtDir, "git commit in wt")

        // 5. pushBranch called with "fsforge/test-slug" (rendered branch name).
        //    Local branch is "test-slug" — the names differ.
        //    Before fix: fails with "src refspec fsforge/test-slug does not match any".
        //    After fix:  succeeds via HEAD:refs/heads/fsforge/test-slug.
        let result =
            pushBranch NoAuth "origin" wtDir "fsforge/test-slug"
            |> Async.RunSynchronously
        Assert.True((result = Ok ()), $"Expected push to succeed but got: {result}")
    finally
        try Directory.Delete(seedDir,   true) with _ -> ()
        try Directory.Delete(remoteDir, true) with _ -> ()
        try Directory.Delete(baseDir,   true) with _ -> ()
        try if Directory.Exists(wtDir) then Directory.Delete(wtDir, true) with _ -> ()

// ---------------------------------------------------------------------------
// pushBranch — re-push after the local checkout is gone (e.g. lock file /
// checkoutRoot deleted between runs, so a fresh clone has no knowledge of the
// fsforge-owned branch that a *prior* run already pushed to the remote).
// ---------------------------------------------------------------------------

[<Fact>]
let ``pushBranch succeeds re-pushing the same branch from a fresh clone with no local history of it`` () =
    let guid      = System.Guid.NewGuid().ToString("N")
    let seedDir   = Path.Combine(Path.GetTempPath(), $"forge-seed2-{guid}")
    let remoteDir = Path.Combine(Path.GetTempPath(), $"forge-r2-{guid}")
    let base1Dir  = Path.Combine(Path.GetTempPath(), $"forge-b2a-{guid}")
    let wt1Dir    = Path.Combine(Path.GetTempPath(), $"forge-w2a-{guid}")
    let base2Dir  = Path.Combine(Path.GetTempPath(), $"forge-b2b-{guid}")
    let wt2Dir    = Path.Combine(Path.GetTempPath(), $"forge-w2b-{guid}")
    try
        Directory.CreateDirectory(seedDir) |> ignore
        Assert.True(run "git" ["-c"; "init.defaultBranch=main"; "init"; seedDir] (Path.GetTempPath()), "git init seed")
        File.WriteAllText(Path.Combine(seedDir, "README.md"), "init")
        Assert.True(run "git" ["add"; "README.md"] seedDir, "git add in seed")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "init"] seedDir, "git commit in seed")
        Assert.True(run "git" ["clone"; "--bare"; seedDir; remoteDir] (Path.GetTempPath()), "git clone bare as remote")

        // "Run 1": clone, commit on the fsforge-owned branch, push. This is the
        // push that landed on the remote in a prior fsforge run.
        Assert.True(run "git" ["clone"; "--bare"; remoteDir; base1Dir] (Path.GetTempPath()), "git clone bare as base1")
        Assert.True(run "git" ["worktree"; "add"; wt1Dir; "-b"; "test-slug"] base1Dir, "git worktree add 1")
        File.WriteAllText(Path.Combine(wt1Dir, "touch.txt"), "run1")
        Assert.True(run "git" ["add"; "-A"] wt1Dir, "git add in wt1")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "run1"] wt1Dir, "git commit in wt1")
        let firstPush =
            pushBranch NoAuth "origin" wt1Dir "fsforge/test-slug"
            |> Async.RunSynchronously
        Assert.True((firstPush = Ok ()), $"Expected first push to succeed but got: {firstPush}")

        // "Run 2": the checkoutRoot/lock file was deleted, so fsforge starts from a
        // brand-new clone that has never heard of "fsforge/test-slug" — there is no
        // local remote-tracking ref for it to compare against.
        Assert.True(run "git" ["clone"; "--bare"; remoteDir; base2Dir] (Path.GetTempPath()), "git clone bare as base2")
        Assert.True(run "git" ["worktree"; "add"; wt2Dir; "-b"; "test-slug"] base2Dir, "git worktree add 2")
        File.WriteAllText(Path.Combine(wt2Dir, "touch.txt"), "run2")
        Assert.True(run "git" ["add"; "-A"] wt2Dir, "git add in wt2")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "run2"] wt2Dir, "git commit in wt2")
        let secondPush =
            pushBranch NoAuth "origin" wt2Dir "fsforge/test-slug"
            |> Async.RunSynchronously
        Assert.True((secondPush = Ok ()), $"Expected re-push from a fresh clone to succeed but got: {secondPush}")
    finally
        try Directory.Delete(seedDir,  true) with _ -> ()
        try Directory.Delete(remoteDir, true) with _ -> ()
        try Directory.Delete(base1Dir, true) with _ -> ()
        try if Directory.Exists(wt1Dir) then Directory.Delete(wt1Dir, true) with _ -> ()
        try Directory.Delete(base2Dir, true) with _ -> ()
        try if Directory.Exists(wt2Dir) then Directory.Delete(wt2Dir, true) with _ -> ()

// ---------------------------------------------------------------------------
// lsRemoteHeads — branch-exists-on-remote check, used by cmd-to-github's
// idempotency decision table (no local clone required).
// ---------------------------------------------------------------------------

[<Fact>]
let ``lsRemoteHeads returns true when the branch exists on the remote`` () =
    let guid    = System.Guid.NewGuid().ToString("N")
    let seedDir = Path.Combine(Path.GetTempPath(), $"forge-lsr-{guid}")
    try
        Directory.CreateDirectory(seedDir) |> ignore
        Assert.True(run "git" ["-c"; "init.defaultBranch=main"; "init"; seedDir] (Path.GetTempPath()), "git init seed")
        File.WriteAllText(Path.Combine(seedDir, "README.md"), "init")
        Assert.True(run "git" ["add"; "README.md"] seedDir, "git add in seed")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "init"] seedDir, "git commit in seed")
        Assert.True(run "git" ["branch"; "fsforge/test-branch"] seedDir, "git branch")

        let result = lsRemoteHeads NoAuth seedDir "fsforge/test-branch" |> Async.RunSynchronously
        Assert.Equal(Ok true, result)
    finally
        try Directory.Delete(seedDir, true) with _ -> ()

[<Fact>]
let ``lsRemoteHeads returns false when the branch does not exist on the remote`` () =
    let guid    = System.Guid.NewGuid().ToString("N")
    let seedDir = Path.Combine(Path.GetTempPath(), $"forge-lsr2-{guid}")
    try
        Directory.CreateDirectory(seedDir) |> ignore
        Assert.True(run "git" ["-c"; "init.defaultBranch=main"; "init"; seedDir] (Path.GetTempPath()), "git init seed")
        File.WriteAllText(Path.Combine(seedDir, "README.md"), "init")
        Assert.True(run "git" ["add"; "README.md"] seedDir, "git add in seed")
        Assert.True(run "git" ["-c"; "user.email=t@t.com"; "-c"; "user.name=t"; "commit"; "-m"; "init"] seedDir, "git commit in seed")

        let result = lsRemoteHeads NoAuth seedDir "fsforge/does-not-exist" |> Async.RunSynchronously
        Assert.Equal(Ok false, result)
    finally
        try Directory.Delete(seedDir, true) with _ -> ()

// ---------------------------------------------------------------------------
// cleanupAll — failures must be surfaced, not swallowed
// ---------------------------------------------------------------------------

[<Fact>]
let ``cleanupAll removes the directory and returns Ok`` () =
    let dir = Path.Combine(Path.GetTempPath(), $"forge-cleanup-{System.Guid.NewGuid():N}")
    Directory.CreateDirectory(Path.Combine(dir, "sub")) |> ignore
    File.WriteAllText(Path.Combine(dir, "sub", "f.txt"), "x")

    let result = cleanupAll dir

    Assert.Equal(Ok (), result)
    Assert.False(Directory.Exists dir)

[<Fact>]
let ``cleanupAll returns Error when the directory cannot be deleted`` () =
    if System.OperatingSystem.IsWindows() then () else
    let parent = Path.Combine(Path.GetTempPath(), $"forge-cleanup-ro-{System.Guid.NewGuid():N}")
    let dir    = Path.Combine(parent, "repo")
    Directory.CreateDirectory(dir) |> ignore
    File.WriteAllText(Path.Combine(dir, "f.txt"), "x")
    // Read-only directory: its entries cannot be removed.
    File.SetUnixFileMode(dir, UnixFileMode.UserRead ||| UnixFileMode.UserExecute)
    try
        let result = cleanupAll dir
        match result with
        | Error _ -> ()
        | Ok () -> Assert.Fail "expected Error when the directory cannot be deleted"
    finally
        File.SetUnixFileMode(dir, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute)
        try Directory.Delete(parent, true) with _ -> ()

// ---------------------------------------------------------------------------
// runProcess — GH_TOKEN env injection, used by ensureClone/pushBranch/forkAndPush
// (via Forge.GitHub) to hand the already-resolved App/PAT token to the gh
// credential helper.
// ---------------------------------------------------------------------------

let private echoGhTokenExe, echoGhTokenArgs =
    if System.OperatingSystem.IsWindows() then "cmd", ["/C"; "echo %GH_TOKEN%"]
    else "sh", ["-c"; "echo \"$GH_TOKEN\""]

[<Fact>]
let ``runProcess injects GH_TOKEN into the child environment when provided`` () =
    let result =
        Forge.Process.runProcess echoGhTokenExe echoGhTokenArgs ["GH_TOKEN", "fsforge-test-token-abc123"] (Path.GetTempPath())
        |> Async.RunSynchronously
    match result with
    | Ok stdout -> Assert.Contains("fsforge-test-token-abc123", stdout)
    | Error e   -> Assert.Fail($"Expected process to succeed, got: {e}")

[<Fact>]
let ``runProcess injects no GH_TOKEN into the child environment when env is empty`` () =
    let result =
        Forge.Process.runProcess echoGhTokenExe echoGhTokenArgs [] (Path.GetTempPath())
        |> Async.RunSynchronously
    match result with
    | Ok stdout -> Assert.DoesNotContain("fsforge-test-token-abc123", stdout)
    | Error e   -> Assert.Fail($"Expected process to succeed, got: {e}")
