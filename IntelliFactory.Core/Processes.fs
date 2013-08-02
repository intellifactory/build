// Copyright 2013 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.

[<AutoOpen>]
module IntelliFactory.Core.Processes

open System
open System.Collections
open System.Diagnostics
open System.IO
open System.Runtime
open System.Runtime.CompilerServices
open System.Security
open System.Text
open System.Threading
open System.Threading.Tasks
open IntelliFactory.Core

#nowarn "40"

[<Sealed>]
[<SecuritySafeCritical>]
type ProcessStartInfoWrapper private (psi: ProcessStartInfo) =

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetInfo() = psi

    static member Create(toolPath, args, workDir, env, enc1, enc2) =
        let psi = ProcessStartInfo()
        psi.Arguments <- args
        psi.CreateNoWindow <- true
        for KeyValue (k, v) in env do
            psi.EnvironmentVariables.[k] <- v
        psi.FileName <- toolPath
        psi.RedirectStandardError <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardInput <- true
        psi.WorkingDirectory <- workDir
        psi.StandardErrorEncoding <- enc2
        psi.StandardOutputEncoding <- enc1
        psi.UseShellExecute <- false
        psi.WindowStyle <- ProcessWindowStyle.Hidden
        ProcessStartInfoWrapper psi

/// This is necessary to satisfy security policy.
[<Sealed>]
[<SecuritySafeCritical>]
type ProcessWrapper private (p: Process) =
    let mutable disposed = 0

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.Kill() =
        if Interlocked.Increment &disposed = 1 then
            p.Kill()
            p.Dispose()

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.HasExited() =
        p.HasExited

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.OnExited(k: unit -> unit) =
        p.add_Exited (EventHandler (fun x args -> k ()))

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.EnableRaisingEvents() =
        p.EnableRaisingEvents <- true

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.Start() =
        p.Start() |> ignore

    interface IDisposable with

        [<SecuritySafeCritical>]
        member w.Dispose() = w.Kill()

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetExitCode() =
        p.ExitCode

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetStandardInput() =
        p.StandardInput

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetStandardError() =
        p.StandardError

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetStandardOutput() =
        p.StandardOutput

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member Create(info: ProcessStartInfoWrapper) =
        new ProcessWrapper(new Process(StartInfo = info.GetInfo()))

let defaultEncoding = FileSystem.DefaultEncoding

let nullCheck (n: string) (v: obj) =
    if v = null
        then Some (n + " member cannot be null")
        else None

type ProcessHandleMessage =
    | KillRequest
    | ProcessExited
    | TextInput of string

let createProcess opts =
    ProcessStartInfoWrapper.Create opts
    |> ProcessWrapper.Create

let startSender traceError (out: TextWriter) (isActive: ref<bool>) =
    MailboxProcessor<string>.Start(fun agent ->
        async {
            try
                while !isActive do
                    let! msg = agent.Receive()
                    do! out.WriteAsync(msg).Await()
                    do! out.FlushAsync().Await()
            with e ->
                return traceError e
        })

let asyncCopyText traceError (r: TextReader) (out: TextWriter) =
    async {
        use r = r
        let buf = Array.zeroCreate 1024
        let rec loop =
            async {
                let! k = r.ReadAsync(buf, 0, buf.Length).Await()
                match k with
                | 0 -> return ()
                | n ->
                    do out.Write(String(buf, 0, n))
                    return! loop
            }
        try
            return! loop
        with e ->
            return traceError e
    }

let startTextCopying traceError (input: TextReader) (output: string -> unit) =
    async {
        use i = input
        use o = TextWriter.NonBlocking output
        return! asyncCopyText traceError i o
    }
    |> Async.Start

[<Sealed>]
type ProcessHandle(p: MailboxProcessor<ProcessHandleMessage>, exitCode: Future<int>) =

    static let definePHAgent
            (opts: ProcessHandleConfig)
            (agent: MailboxProcessor<ProcessHandleMessage>) : Async<unit> =
        async {
            use p =
                createProcess
                    (
                        opts.ToolPath,
                        opts.Arguments,
                        opts.WorkingDirectory,
                        opts.EnvironmentVariables,
                        opts.StandardOutputEncoding,
                        opts.StandardErrorEncoding
                    )
            do
                p.OnExited(fun x -> agent.Post ProcessExited)
                p.EnableRaisingEvents()
            let isRunning = ref false
            let exitCode = ref -1
            try
                try
                    do p.Start()
                    do isRunning := true
                    do startTextCopying opts.TraceError (p.GetStandardError()) opts.OnStandardError
                    do startTextCopying opts.TraceError (p.GetStandardOutput()) opts.OnStandardOutput
                    let stdin = new StreamWriter(p.GetStandardInput().BaseStream, opts.StandardInputEncoding)
                    let sender = startSender opts.TraceError stdin isRunning
                    let rec loop =
                        async {
                            let! msg = agent.Receive()
                            match msg with
                            | KillRequest -> return ()
                            | ProcessExited -> return exitCode := p.GetExitCode()
                            | TextInput msg ->
                                do sender.Post msg
                                return! loop
                        }
                    return! loop
                with e ->
                    return opts.TraceError e
            finally
                if !isRunning then
                    isRunning := false
                opts.OnExit !exitCode
        }

    member h.ExitCode = exitCode
    member h.Kill() = p.Post KillRequest
    member h.SendInput i = p.Post (TextInput i)

    interface IDisposable with
        member h.Dispose() = p.Post KillRequest

    static member Configure(toolPath, ?args) =
        ProcessHandleConfig.Create(toolPath, ?args = args)

    static member Start(opts: ProcessHandleConfig) =
        opts.Validate()
        let exitCode = Future.Create()
        let onExit =
            let onExit = opts.OnExit
            fun code -> exitCode.Complete code; onExit code
        let opts = { opts with OnExit = onExit }
        new ProcessHandle(MailboxProcessor.Start(definePHAgent opts), exitCode)

and ProcessHandleConfig =
    {
        Arguments : string
        EnvironmentVariables : Map<string,string>
        OnExit : int -> unit
        OnStandardError : string -> unit
        OnStandardOutput : string -> unit
        StandardErrorEncoding : Encoding
        StandardInputEncoding : Encoding
        StandardOutputEncoding : Encoding
        ToolPath : string
        TraceError : exn -> unit
        WorkingDirectory : string
    }

    member opts.Start() =
        ProcessHandle.Start opts

    member opts.GetValidationError() =
        seq {
            yield nullCheck "Arguments" opts.Arguments
            yield nullCheck "StandardErrorEncoding" opts.StandardErrorEncoding
            yield nullCheck "StandardInputEncoding" opts.StandardInputEncoding
            yield nullCheck "StandardOutputEncoding" opts.StandardOutputEncoding
            yield nullCheck "ToolPath" opts.ToolPath
            yield nullCheck "ToolPath" opts.WorkingDirectory
            if not (FileInfo opts.ToolPath).Exists then
                yield Some ("ToolPath does not exist: " + opts.ToolPath)
            if not (DirectoryInfo opts.WorkingDirectory).Exists then
                yield Some ("WorkingDirectory does not exist: " + opts.WorkingDirectory)
        }
        |> Seq.tryPick (Option.map (fun x -> "Invalid options: " + x))

    member opts.Validate() =
        match opts.GetValidationError() with
        | None -> ()
        | Some err -> invalidArg "ProcessOptions" err

    static member Create(toolPath: string, ?args: string) =
        {
            Arguments = defaultArg args ""
            EnvironmentVariables =
                Map [
                    for kv in Environment.GetEnvironmentVariables() do
                        let kv = kv :?> DictionaryEntry
                        yield (kv.Key :?> string, kv.Value :?> string)
                ]
            OnExit = ignore
            OnStandardOutput = ignore
            OnStandardError = ignore
            StandardErrorEncoding = defaultEncoding
            StandardInputEncoding = defaultEncoding
            StandardOutputEncoding = defaultEncoding
            ToolPath = toolPath
            TraceError = ignore
            WorkingDirectory = Directory.GetCurrentDirectory()
        }

type ProcessSerivceMessage =
    | Dispose
    | Restart
    | Send of string
    | Start
    | Stop
    | Stopped
    | StopAsync of AsyncReplyChannel<unit>

[<Sealed>]
type ProcessServiceState
    (
        self: MailboxProcessor<ProcessSerivceMessage>,
        opts: ProcessHandleConfig,
        restartInterval: TimeSpan
    ) =

    let stop (p: ProcessHandle) =
        async {
            do p.Kill()
            let! c = p.ExitCode.Await()
            return ()
        }

    let finish proc =
        match proc with
        | None -> async.Return()
        | Some p -> stop p

    member s.Idle =
        async {
            let! msg = self.Receive()
            match msg with
            | Dispose -> return! finish None
            | Restart | Start -> return! s.Start []
            | Send msg -> return! s.Start [msg]
            | Send _ | Stop | Stopped -> return! s.Idle
            | StopAsync k ->
                do k.Reply()
                return! s.Idle
        }

    member s.Running p =
        async {
            let! msg = self.Receive()
            match msg with
            | Dispose ->
                return! finish (Some p)
            | Restart ->
                do! stop p
                return! s.Start []
            | Start ->
                return! s.Running p
            | Send msg ->
                do p.SendInput(msg)
                return! s.Running p
            | Stop ->
                do! stop p
                return! s.Idle
            | StopAsync k ->
                do! stop p
                do k.Reply()
                return! s.Idle
            | Stopped ->
                do! Async.Sleep(int restartInterval.TotalMilliseconds)
                return! s.Start []
        }

    member s.Start messages =
        async {
            let p = opts.Start()
            do
                p.ExitCode.On(fun code -> self.Post Stopped)
                for m in messages do
                    p.SendInput m
            return! s.Running p
        }

let startProcessService opts interval =
    let finalized = Future.Create()
    let agent =
        MailboxProcessor.Start(fun self ->
            let idle = ProcessServiceState(self, opts, interval).Idle
            async.TryFinally(idle, fun () -> finalized.Complete()))
    (agent, finalized)

[<Sealed>]
type ProcessService(opts) =
    let (a, f) = startProcessService opts.ProcessHandleConfig opts.RestartInterval

    member s.Finalize() = a.Post Dispose; f.Await()
    member s.Restart() = a.Post Restart
    member s.Start() = a.Post Start
    member s.Stop() = a.Post Stop
    member s.StopAsync() = a.PostAndAsyncReply StopAsync
    member s.SendInput i = a.Post (Send i)
    member s.Disposed = f

    static member Configure(toolPath, ?args) =
        ProcessServiceConfig.Create(toolPath, ?args = args)

and ProcessServiceConfig =
    {
        ProcessHandleConfig : ProcessHandleConfig
        RestartInterval : TimeSpan
    }

    member cfg.Configure k =
        { cfg with ProcessHandleConfig = k cfg.ProcessHandleConfig }

    member cfg.Create() =
        cfg.ProcessHandleConfig.Validate()
        ProcessService cfg

    static member Create(toolPath: string, ?args) =
        {
            ProcessHandleConfig = ProcessHandleConfig.Create(toolPath, ?args = args)
            RestartInterval = TimeSpan.FromSeconds(5.)
        }



