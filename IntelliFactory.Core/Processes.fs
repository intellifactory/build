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

let defaultEncoding =
    UTF8Encoding(false, false)

let nullCheck (n: string) (v: obj) =
    if v = null
        then Some (n + " member cannot be null")
        else None

type ProcessHandleConfig =
    {
        Arguments : string
        EnvironmentVariables : Map<string,string>
        OnExit : int -> unit
        OnStandardOutput : string -> unit
        OnStandardError : string -> unit
        StandardErrorEncoding : Encoding
        StandardOutputEncoding : Encoding
        ToolPath : string
        WorkingDirectory : string
    }

    member opts.GetValidationError() =
        seq {
            yield nullCheck "Arguments" opts.Arguments
            yield nullCheck "StandardErrorEncoding" opts.StandardErrorEncoding
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

    static member Create(toolPath: string, args: option<string>) =
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
            StandardOutputEncoding = defaultEncoding
            ToolPath = toolPath
            WorkingDirectory = Directory.GetCurrentDirectory()
        }

[<Sealed>]
[<SecuritySafeCritical>]
type ProcessStartInfoWrapper private (psi: ProcessStartInfo) =

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetInfo() = psi

    static member Create(opts: ProcessHandleConfig) =
        let psi = ProcessStartInfo()
        psi.Arguments <- opts.Arguments
        psi.CreateNoWindow <- true
        for KeyValue(k, v) in opts.EnvironmentVariables do
            psi.EnvironmentVariables.[k] <- v
        psi.FileName <- opts.ToolPath
        psi.RedirectStandardError <- true
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.WorkingDirectory <- opts.WorkingDirectory
        psi.StandardErrorEncoding <- opts.StandardErrorEncoding
        psi.StandardOutputEncoding <- opts.StandardOutputEncoding
        psi.UseShellExecute <- false
        psi.WindowStyle <- ProcessWindowStyle.Hidden
        ProcessStartInfoWrapper psi

[<MethodImpl(MethodImplOptions.NoInlining)>]
[<SecuritySafeCritical>]
let getArgs (dra: DataReceivedEventArgs) =
    dra.Data

[<MethodImpl(MethodImplOptions.NoInlining)>]
[<SecuritySafeCritical>]
let makeHandler (handle: string -> unit) : DataReceivedEventHandler =
    DataReceivedEventHandler(fun o x -> handle (getArgs x))

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
    member w.OnErrorDataReceived(h) =
        makeHandler h
        |> p.add_ErrorDataReceived

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.OnOutputDataReceived(h) =
        makeHandler h
        |> p.add_OutputDataReceived

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.OnExited(k: unit -> unit) =
        p.add_Exited (EventHandler (fun x args -> k ()))

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.EnableRaisingEvents() =
        p.EnableRaisingEvents <- true

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.Start() =
        p.Start() |> ignore

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.BeginReadLine() =
        p.BeginErrorReadLine()
        p.BeginOutputReadLine()

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
    static member Create(info: ProcessStartInfoWrapper) =
        new ProcessWrapper(new Process(StartInfo = info.GetInfo()))

type ProcessMessage =
    | ProcessExited
    | SendInputText of string
    | SendKillRequest

let createProcess opts =
    ProcessStartInfoWrapper.Create opts
    |> ProcessWrapper.Create

let startSender (out: TextWriter) (token: CancellationToken) =
    MailboxProcessor<string>.Start(fun agent ->
        async {
            while not token.IsCancellationRequested do
                let! msg = agent.Receive()
                do! out.WriteAsync(msg).Await()
                do! out.FlushAsync().Await()
        })

let definePHAgent (opts: ProcessHandleConfig) (agent: MailboxProcessor<ProcessMessage>) : Async<unit> =
    async {
        use cts = new CancellationTokenSource()
        use p = createProcess opts
        let code = ref -1
        try
            do
                p.OnErrorDataReceived(fun x -> Async.Spawn(opts.OnStandardError, x))
                p.OnOutputDataReceived(fun x -> Async.Spawn(opts.OnStandardOutput, x))
                p.OnExited(fun x -> agent.Post ProcessExited)
                p.EnableRaisingEvents()
                p.Start()
                p.BeginReadLine()
            let sender = startSender (p.GetStandardInput()) cts.Token
            let rec loop =
                async {
                    let! msg = agent.Receive()
                    match msg with
                    | ProcessExited ->
                        return code := p.GetExitCode()
                    | SendInputText msg ->
                        do sender.Post msg
                        return! loop
                    | SendKillRequest ->
                        do p.Kill()
                        return code := -1
                }
            return! loop
        finally
            Async.Spawn(opts.OnExit, !code)
            p.Kill()
    }

type PHAgent = MailboxProcessor<ProcessMessage>

[<Sealed>]
type ProcessHandle(p: PHAgent, exitCode: Future<int>) =
    member h.Dispose() = p.Post SendKillRequest
    member h.Kill() = p.Post SendKillRequest
    member h.SendInput i = p.Post(SendInputText i)
    member h.ExitCode = exitCode

    interface IDisposable with
        member h.Dispose() =
            h.Dispose()

    static member Start(opts: ProcessHandleConfig) =
        opts.Validate()
        let exitCode = Future.Create()
        let opts =
            {
                opts with
                    OnExit = fun code ->
                        exitCode.Complete code
                        opts.OnExit code
            }
        new ProcessHandle(MailboxProcessor.Start(definePHAgent opts), exitCode)

    static member Start(toolPath, ?args, ?configure) =
        let opts = ProcessHandleConfig.Create(toolPath, args)
        let opts =
            match configure with
            | Some c -> c opts
            | None -> opts
        ProcessHandle.Start opts

type ProcessServiceConfig =
    {
        ProcessHandleConfig : ProcessHandleConfig
        RestartInterval : TimeSpan
    }

    member cfg.Validate() =
        cfg.ProcessHandleConfig.Validate()

    static member Create opts =
        {
            ProcessHandleConfig = opts
            RestartInterval = TimeSpan.FromSeconds(5.)
        }

type Message =
    | Dispose
    | Restart
    | Send of string
    | Start
    | Stop
    | Stopped

let startProcessService (cfg: ProcessServiceConfig) =
    let opts = cfg.ProcessHandleConfig
    let finalized = Future.Create()
    MailboxProcessor<Message>.Start(fun self ->
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
        let rec idle =
            async {
                let! msg = self.Receive()
                match msg with
                | Dispose -> return! finish None
                | Restart | Start -> return! start []
                | Send msg -> return! start [msg]
                | Send _ | Stop | Stopped -> return! idle
            }
        and start messages =
            async {
                let p = ProcessHandle.Start(opts)
                do
                    p.ExitCode.On(fun code -> self.Post Stopped)
                    for m in messages do
                        p.SendInput m
                return! running p
            }
        and running p =
            async {
                let! msg = self.Receive()
                match msg with
                | Dispose ->
                    return! finish (Some p)
                | Restart ->
                    do! stop p
                    return! start []
                | Start ->
                    return! running p
                | Send msg ->
                    do p.SendInput(msg)
                    return! running p
                | Stop ->
                    do! stop p
                    return! idle
                | Stopped ->
                    do! Async.Sleep(int cfg.RestartInterval.TotalMilliseconds)
                    return! start []
            }
        async.TryFinally(idle, fun () -> finalized.Complete()))
    |> fun agent -> (agent, finalized)

[<Sealed>]
type ProcessService(opts) =
    let (a, f) = startProcessService opts

    member s.Finalize() = a.Post Dispose; f.Await()
    member s.Restart() = a.Post Restart
    member s.Start() = a.Post Start
    member s.Stop() = a.Post Stop
    member s.SendInput i = a.Post (Send i)
    member s.Disposed = f

    static member Create(opts: ProcessServiceConfig) =
        opts.Validate()
        new ProcessService(opts)

    static member Create(toolPath, ?args, ?configure) =
        let opts = ProcessHandleConfig.Create(toolPath, args)
        let cfg = ProcessServiceConfig.Create(opts)
        match configure with
        | None -> ProcessService.Create cfg
        | Some k -> ProcessService.Create(k cfg)
