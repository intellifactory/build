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

namespace IntelliFactory.Core

open System
open System.Collections
open System.Collections.Specialized
open System.Diagnostics
open System.IO
open System.Runtime
open System.Runtime.CompilerServices
open System.Security
open System.Text
open System.Threading
open System.Threading.Tasks
open IntelliFactory.Core

[<Sealed>]
[<SecuritySafeCritical>]
type ProcessStartInfoWrapper private (psi: ProcessStartInfo) =

    static let populateStringDictionary (d: StringDictionary) map =
        d.Clear()
        for KeyValue (k, v) in map do
            d.[k] <- v

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetInfo() = psi

    static member Create(toolPath, args, workDir, env) =
        let psi = ProcessStartInfo()
        psi.Arguments <- args
        psi.CreateNoWindow <- true
        populateStringDictionary psi.EnvironmentVariables env
        psi.FileName <- toolPath
        psi.RedirectStandardError <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardInput <- true
        psi.WorkingDirectory <- workDir
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
        p.StandardInput.BaseStream

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetStandardError() =
        p.StandardError.BaseStream

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    member w.GetStandardOutput() =
        p.StandardOutput.BaseStream

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member Create(info: ProcessStartInfoWrapper) =
        new ProcessWrapper(new Process(StartInfo = info.GetInfo()))

type ProcessAgentMessage<'T> =
    | Kill
    | OnExit of int
    | OnStandardError of 'T
    | OnStandardOutput of 'T
    | OnDoneReadingStandardError
    | OnDoneReadingStandardOutput
    | SendStandardInput of 'T

type ProcessAgent<'T> =
    {
        ExitCodeFuture : Future<int>
        SendMessage : ProcessAgentMessage<'T> -> unit
    }

    member this.Kill() =
        this.SendMessage Kill

    member this.SendInput(message) =
        this.SendMessage (SendStandardInput message)

    member this.ExitCode = this.ExitCodeFuture

module ProcessAgent =

    let await (t: Task) : Async<unit> =
        t.ContinueWith(ignore)
        |> Async.AwaitTask

    type Reader<'T> =
        {
            DisposeReader : unit -> unit
            Read : unit -> Async<option<'T>>
        }

        interface IDisposable with
            member this.Dispose() = this.DisposeReader()

    type Writer<'T> =
        {
            DisposeWriter : unit -> unit
            Write : 'T -> Async<unit>
        }

        interface IDisposable with
            member this.Dispose() = this.DisposeWriter()

    type MessageType<'T> =
        {
            GetReader : Stream -> Reader<'T>
            GetWriter : Stream -> Writer<'T>
        }


    module MessageType =

        let Binary =
            {
                GetReader = fun s ->
                    let buf = Array.zeroCreate 4096
                    let read () =
                        async {
                            let! n = s.AsyncRead(buf)
                            if n = 0 then return None else
                                return Some (Array.sub buf 0 n)
                        }
                    { DisposeReader = ignore; Read = read }
                GetWriter = fun s ->
                    let write (data: byte[]) =
                        async {
                            do! s.AsyncWrite(data)
                            return! s.FlushAsync() |> await
                        }
                    { DisposeWriter = ignore; Write = write }
            }

        let Text (enc: Encoding) =
            {
                GetReader = fun s ->
                    let r = new StreamReader(s, enc)
                    let buf = Array.zeroCreate 4096
                    let read () =
                        async {
                            let! n =
                                r.ReadAsync(buf, 0, 4096)
                                |> Async.AwaitTask
                            if n = 0 then return None else
                                return Some (new String(buf, 0, n))
                        }
                    let dispose () =
                        r.Dispose()
                    { DisposeReader = dispose; Read = read }
                GetWriter = fun s ->
                    let w = new StreamWriter(s, enc)
                    let write (data: string) =
                        async {
                            do! w.WriteAsync(data) |> await
                            return! w.FlushAsync() |> await
                        }
                    let dispose () =
                        w.Dispose()
                    { DisposeWriter = dispose; Write = write }
            }

        let UTF8 =
            let defaultEncoding = UTF8Encoding(false, true)
            Text defaultEncoding

        let ASCII =
            Text Encoding.ASCII

    type Config<'T> =
        {
            Arguments : string
            EnvironmentVariables : Map<string,string>
            FileName : string
            MessageType : MessageType<'T>
            OnError : 'T -> unit
            OnExit : int -> unit
            OnOutput : 'T -> unit
            Report : exn -> unit
            WorkingDirectory : string
        }

    let getEnvironment () =
        let vs = Environment.GetEnvironmentVariables()
        seq {
            for key in vs.Keys ->
                (key :?> string, vs.[key] :?> string)
        }
        |> Map.ofSeq

    let getCurrentDir () =
        Directory.GetCurrentDirectory()

    let Configure mT fileName =
        {
            Arguments = ""
            EnvironmentVariables = getEnvironment ()
            FileName = fileName
            MessageType = mT
            OnError = ignore
            OnExit = ignore
            OnOutput = ignore
            Report = ignore
            WorkingDirectory = getCurrentDir ()
        }

    let startStreamReader mt report stream out finish =
        async {
            use reader = mt.GetReader stream
            try
                let rec loop () =
                    async  {
                        let! k = reader.Read()
                        match k with
                        | None -> return ()
                        | Some msg ->
                            do out msg
                            return! loop ()
                    }
                try
                    return! loop ()
                finally
                    finish ()
            with e ->
                return report e
        }
        |> Async.Start

    type StreamWriterAgent<'T> =
        {
            SendWriteMessage : option<'T> -> unit
        }

        interface IDisposable with
            member this.Dispose() =
                this.SendWriteMessage None

        member this.Write(data) =
            this.SendWriteMessage(Some data)

    let startWriterAgent mt report stream =
        let w =
            MailboxProcessor.Start(fun self ->
                async {
                    try
                        use _ = stream
                        use writer = mt.GetWriter stream
                        let rec loop () =
                            async {
                                let! msg = self.Receive()
                                match msg with
                                | None -> return ()
                                | Some data ->
                                    do! writer.Write data
                                    return! loop ()
                            }
                        return! loop ()
                    with e ->
                        return report e
                })
        { SendWriteMessage = w.Post }

    type Agent<'T> =
        { Self : MailboxProcessor<ProcessAgentMessage<'T>> }

    let defineAgent cfg agent =
        async {
            try
                let ( !! ) m = agent.Self.Post m
                let psi =
                    ProcessStartInfoWrapper.Create(cfg.FileName,
                        cfg.Arguments,
                        cfg.WorkingDirectory,
                        cfg.EnvironmentVariables)
                use proc = ProcessWrapper.Create(psi)
                do
                    proc.OnExited(fun () -> !! (OnExit (proc.GetExitCode())))
                    proc.EnableRaisingEvents()
                let started = proc.Start()
                try
                    let exitCode = ref None
                    let stdoutDone = ref false
                    let stderrDone = ref false
                    let allDone () =
                        !stdoutDone && !stderrDone && exitCode.Value.IsSome
                    use stdin = startWriterAgent cfg.MessageType cfg.Report (proc.GetStandardInput())
                    do
                        startStreamReader cfg.MessageType cfg.Report (proc.GetStandardError())
                            (fun d -> !! (OnStandardError d))
                            (fun () -> !! OnDoneReadingStandardError)
                        startStreamReader cfg.MessageType cfg.Report (proc.GetStandardOutput())
                            (fun d -> !! (OnStandardOutput d))
                            (fun () -> !! OnDoneReadingStandardOutput)
                    let rec loop () =
                        async {
                            let! msg = agent.Self.Receive()
                            match msg with
                            | Kill ->
                                do proc.Kill()
                                return! loop ()
                            | OnExit c ->
                                do exitCode := Some c
                                return! finish ()
                            | OnStandardError data ->
                                do cfg.OnError data
                                return! loop ()
                            | OnStandardOutput data ->
                                do cfg.OnOutput data
                                return! loop ()
                            | OnDoneReadingStandardError ->
                                do stderrDone := true
                                return! finish ()
                            | OnDoneReadingStandardOutput ->
                                do stdoutDone := true
                                return! finish ()
                            | SendStandardInput data ->
                                if not (proc.HasExited()) then
                                    stdin.Write(data)
                                return! loop ()
                        }
                    and finish () =
                        if allDone () then
                            async.Return ()
                        else
                            loop ()
                    do! loop ()
                    return cfg.OnExit (defaultArg !exitCode -1)
                finally
                    if not (proc.HasExited()) then
                        proc.Kill()
            with e ->
                return cfg.Report e
         }

    let nullCheck (n: string) (v: obj) =
        if v = null
            then Some (n + " member cannot be null")
            else None

    let getValidationError opts =
        seq {
            yield nullCheck "Arguments" opts.Arguments
            yield nullCheck "FileName" opts.FileName
            yield nullCheck "WorkingDirectory" opts.WorkingDirectory
            for KeyValue (k, v) in opts.EnvironmentVariables do
                yield nullCheck "Key" k
                yield nullCheck (string k) v
            if not (FileInfo opts.FileName).Exists then
                yield Some ("does not exist: " + opts.FileName)
            if not (DirectoryInfo opts.WorkingDirectory).Exists then
                yield Some ("WorkingDirectory does not exist: " + opts.WorkingDirectory)
        }
        |> Seq.tryPick (Option.map (fun x -> "Invalid options: " + x))

    let Validate opts =
        match getValidationError opts with
        | None -> ()
        | Some err -> invalidArg "ProcessOptions" err

    let startAgent cfg =
        { Self = MailboxProcessor.Start(fun self -> defineAgent cfg { Self = self }) }

    let Start cfg =
        Validate cfg
        let exitCode = Future.Create()
        let cfg =
            let onExit = cfg.OnExit
            { cfg with OnExit = fun code ->
                    exitCode.Complete(code)
                    onExit code }
        let agent = startAgent cfg
        {
            ExitCodeFuture = exitCode
            SendMessage = agent.Self.Post
        }
