module Forge.Tests.GhForgeClientTests

open Xunit
open Forge.GitHub

// ---------------------------------------------------------------------------
// tokenEnv — GH_TOKEN must be injected iff the resolved token is non-empty, so
// App/PAT auth reaches `gh` without relying on ambient credentials.
// ---------------------------------------------------------------------------

[<Fact>]
let ``tokenEnv includes GH_TOKEN when the token is non-empty`` () =
    Assert.Equal<(string * string) list>(["GH_TOKEN", "resolved-token"], tokenEnv "resolved-token")

[<Fact>]
let ``tokenEnv is empty when the token is empty`` () =
    Assert.Equal<(string * string) list>([], tokenEnv "")

// ---------------------------------------------------------------------------
// buildPrCreateArgs — the `gh pr create` argument shape.
// ---------------------------------------------------------------------------

[<Fact>]
let ``buildPrCreateArgs passes repo, head, title and body as gh pr create arguments`` () =
    let args = buildPrCreateArgs "myorg/repo-a" "head-branch" "My Title" "My Body"
    Assert.Equal<string list>(
        ["pr"; "create"; "--repo"; "myorg/repo-a"; "--head"; "head-branch"; "--title"; "My Title"; "--body"; "My Body"],
        args)
