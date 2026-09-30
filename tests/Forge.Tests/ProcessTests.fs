module Forge.Tests.ProcessTests

open System.IO
open Xunit
open Forge.Process

let private tmp () = Path.GetTempPath()

[<Fact>]
let ``runProcess returns stdout on exit code 0`` () =
    let r = runProcess "git" ["--version"] [] (tmp ()) |> Async.RunSynchronously
    match r with
    | Ok out -> Assert.StartsWith("git version", out)
    | Error e -> failwith e

[<Fact>]
let ``runProcess returns Error with exit code and stderr on non-zero exit`` () =
    let r = runProcess "git" ["not-a-real-subcommand"] [] (tmp ()) |> Async.RunSynchronously
    match r with
    | Error e ->
        Assert.StartsWith("Exit ", e)
        Assert.Contains("not-a-real-subcommand", e)
    | Ok _ -> failwith "expected Error"

[<Fact>]
let ``runProcess passes env entries to the child only`` () =
    let key = "FSFORGE_TEST_ENV_VAR"
    let r = runProcess "git" ["config"; "--get-regexp"; "nothing"] [ key, "x" ] (tmp ()) |> Async.RunSynchronously
    // git exits 1 (no match) — we only care that the parent env is untouched
    ignore r
    Assert.Null(System.Environment.GetEnvironmentVariable key)

[<Fact>]
let ``runProcess throws when the executable does not exist`` () =
    let act () = runProcess "fsforge-no-such-exe" [] [] (tmp ()) |> Async.RunSynchronously |> ignore
    Assert.ThrowsAny<exn>(act) |> ignore
