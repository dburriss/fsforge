namespace Forge

/// How to authenticate git operations (clone/push) against a remote. Generalises
/// the credential-helper approach so a specific forge's auth mechanism plugs in
/// without git-mechanics code depending on that forge.
type GitAuth =
    | NoAuth
    /// `helperCommand` is passed as `git -c credential.helper=<helperCommand>`;
    /// `env` entries are set on the child process only (e.g. a token env var the
    /// helper reads).
    | CredentialHelper of helperCommand: string * env: (string * string) list

/// A pull request created on a forge.
type PrRef = { Url: string }

/// Forge-specific operations that are not plain git: forking a repo, resolving
/// the authenticated user, and opening a pull request. A generic git host has no
/// notion of these — only a forge (GitHub, GitLab, ...) does.
type IForgeClient =
    /// Fork `repo` (owner/name) under the authenticated user. Idempotent — returns
    /// the existing fork if one is already present. Returns the fork's own
    /// owner/name identity on success.
    abstract Fork: repo: string -> Async<Result<string, string>>
    /// Resolve the authenticated user's login.
    abstract CurrentUserLogin: unit -> Async<Result<string, string>>
    /// Open a pull request. `head` may be `branch` (same-repo) or `owner:branch`
    /// (cross-repo, e.g. from a fork). `workingDir` is the directory the CLI call
    /// runs in (the checked-out worktree).
    abstract CreatePullRequest: repo: string * head: string * title: string * body: string * workingDir: string -> Async<Result<PrRef, string>>
