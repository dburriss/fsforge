/// Child-process execution helper shared by the git and forge operations.
module Forge.Process

open System.Diagnostics

/// Run a process, collecting stdout and stderr concurrently to avoid deadlocks
/// when either buffer fills. Args are passed via ArgumentList so no shell
/// escaping is needed — each element is passed verbatim to the OS.
/// `env` entries are set on the child process only, never mutating the
/// parent/global environment.
let runProcess (executable: string) (args: string list) (env: (string * string) list) (workingDir: string) : Async<Result<string, string>> =
    async {
        let psi = ProcessStartInfo(executable)
        psi.WorkingDirectory       <- workingDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError  <- true
        psi.UseShellExecute        <- false
        for arg in args do psi.ArgumentList.Add(arg)
        for (k, v) in env do psi.Environment[k] <- v
        use proc = Process.Start(psi)
        // Read stdout and stderr concurrently before waiting for exit to avoid
        // deadlock when both output streams fill their OS buffers simultaneously.
        let stdoutTask = proc.StandardOutput.ReadToEndAsync()
        let stderrTask = proc.StandardError.ReadToEndAsync()
        do! proc.WaitForExitAsync() |> Async.AwaitTask
        let! stdout = stdoutTask |> Async.AwaitTask
        let! stderr = stderrTask |> Async.AwaitTask
        if proc.ExitCode = 0 then
            return Ok stdout
        else
            return Error $"Exit {proc.ExitCode}: {stderr.Trim()}"
    }
