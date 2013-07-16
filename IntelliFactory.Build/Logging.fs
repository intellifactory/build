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

namespace IntelliFactory.Build

#if INTERACTIVE
open IntelliFactory.Build
#endif

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
type A = ParamArrayAttribute

[<AutoOpen>]
module Identifiers =
    let private pat = Regex("^\w+$")

    type Identifier =
        private { name : string }
        override this.ToString() = this.name

        static member Create(id: string) =
            if pat.IsMatch id then { name = id } else
                invalidArg "id" "identifier must be alphanumeric"

[<AutoOpen>]
module Names =

    let private join (parts: seq<Identifier>) =
        String.concat "." (Seq.map string parts)

    type Name =
        private {
            name : string
            parts : Identifier []
        }

        static member private Create parts =
            {
                name = join parts
                parts = parts
            }

        override this.ToString() =
            this.name

        member this.AncestorsAndSelf() =
            seq {
                let parts = this.parts
                for i in 1 .. parts.Length do
                    let remainder = join (Array.sub parts i (parts.Length - i))
                    yield (Name.Create(Array.sub parts 0 i), remainder)
            }

        member this.Nested(name: Name) =
            Name.Create(Array.append this.parts name.parts)

        member this.ToParts() =
            this.parts |> Array.map string

        static member Create(ids: seq<Identifier>) =
            if Seq.isEmpty ids then
                invalidArg "ids" "identifier sequence must be non-empty"
            Name.Create(Seq.toArray ids)

        static member Parse(name: string) =
            name.Split('.')
            |> Name.ParseNames

        static member ParseNames(names: seq<string>) =
            names
            |> Seq.map Identifier.Create
            |> Name.Create

type LogLevel =
    | CriticalLevel
    | ErrorLevel
    | WarnLevel
    | InfoLevel
    | VerboseLevel

    override l.ToString() =
        l.Name

    member l.Name =
        match l with
        | CriticalLevel -> "critical"
        | ErrorLevel -> "error"
        | InfoLevel -> "info"
        | VerboseLevel -> "verbose"
        | WarnLevel -> "warn"

    member l.EventType =
        match l with
        | CriticalLevel -> TraceEventType.Critical
        | ErrorLevel -> TraceEventType.Error
        | InfoLevel -> TraceEventType.Information
        | VerboseLevel -> TraceEventType.Verbose
        | WarnLevel -> TraceEventType.Warning

    static member Critical = CriticalLevel
    static member Error = ErrorLevel
    static member Info = InfoLevel
    static member Verbose = VerboseLevel
    static member Warn = WarnLevel

type ILogger =
    abstract ShouldTrace : LogLevel -> bool
    abstract Trace : LogLevel * string -> unit

type ILogConfig =
    abstract GetNamedLogger : string -> ILogger

type Restriction =
    | Restrict of Name * LogLevel

type Sink =
    | ConsoleSink
    | DiagnosticsSink
    | NoSink
    | TraceSourceSink of TraceSource

type NullLogger =
    | NullLogger

    interface ILogger with
        member l.ShouldTrace(_) = false
        member l.Trace(_, _) = ()

[<Sealed>]
type SinkLogger(name: string, sink: Sink, lev: LogLevel) =

    let trace =
        match sink with
        | ConsoleSink ->
            let out = Console.Out
            let err = Console.Error
            fun lev msg ->
                let out =
                    match lev with
                    | ErrorLevel | CriticalLevel | WarnLevel -> err
                    | VerboseLevel | InfoLevel -> out
                out.WriteLine("{2}: [{0}] {1}", lev, msg, name)
                out.Flush()
        | DiagnosticsSink ->
            fun lev msg ->
                let msg = String.Format("{0}: {1}", name, msg)
                match lev with
                | ErrorLevel -> Trace.TraceError(msg)
                | CriticalLevel -> Trace.TraceError("[CRITICAL] {0}", msg)
                | WarnLevel -> Trace.TraceWarning(msg)
                | InfoLevel -> Trace.TraceInformation(msg)
                | VerboseLevel -> Debug.WriteLine(msg)
        | TraceSourceSink ts ->
            fun lev msg ->
                let msg = String.Format("{0}: {1}", name, msg)
                let t = lev.EventType
                if ts.Switch.ShouldTrace(t) then
                    ts.TraceEvent(t, 0, msg)
        | NoSink ->
            fun lev msg -> ()

    interface ILogger with
        member l.ShouldTrace(x) = x <= lev
        member l.Trace(lev, msg) = trace lev msg

[<Sealed>]
type LogConfig(def: option<LogLevel>, rs: list<Restriction>, sink: Sink) =
    static let current = Parameter.Create(LogConfig() :> ILogConfig)

    new () = LogConfig(None, [], NoSink)

    member c.Restrict(name, level) =
        LogConfig(def, Restrict (Name.Parse name, level) :: rs, sink)

    member c.Critical(name) = c.Restrict(name, LogLevel.Critical)
    member c.Error(name) = c.Restrict(name, LogLevel.Error)
    member c.Info(name) = c.Restrict(name, LogLevel.Info)
    member c.Verbose(name) = c.Restrict(name, LogLevel.Verbose)
    member c.Warn(name) = c.Restrict(name, LogLevel.Warn)

    member c.Default(def) = LogConfig(Some def, rs, sink)
    member c.Critical() = c.Default(LogLevel.Critical)
    member c.Error() = c.Default(LogLevel.Error)
    member c.Info() = c.Default(LogLevel.Info)
    member c.Verbose() = c.Default(LogLevel.Verbose)
    member c.Warn() = c.Default(LogLevel.Warn)

    member c.ToSink(s) = LogConfig(def, rs, s)
    member c.ToConsole() = c.ToSink(ConsoleSink)
    member c.ToDiagnostics() = c.ToSink(DiagnosticsSink)
    member c.ToTraceSource(ts) = c.ToSink(TraceSourceSink ts)

    interface ILogConfig with
        member cf.GetNamedLogger(name: string) =
            let name = Name.Parse(name)
            let names =
                name.AncestorsAndSelf()
                |> Seq.map fst
                |> Seq.toArray
            let score (Restrict (n, _)) =
                let s = Array.tryFindIndex ((=) n) names
                match s with
                | None -> 0
                | Some k -> 1 + k
            let maxBy f xs =
                if Seq.isEmpty xs then None else
                    Some (Seq.maxBy f xs)
            let orElse a b =
                match a with
                | None -> b
                | _ -> a
            let lev =
                rs
                |> Seq.filter (fun r -> score r > 0)
                |> maxBy score
                |> Option.map (function Restrict (_, lev) -> lev)
                |> orElse def
            match lev with
            | None ->
                NullLogger :> ILogger
            | Some lev ->
                SinkLogger(string name, sink, lev) :> ILogger

    static member Current = current

[<Sealed>]
type Log(name: Name, env: Parameters) =
    let lc = LogConfig.Current.Find env
    let logger = lc.GetNamedLogger(string name)

    interface IParametric with
        member l.Find p = p.Find env

    interface IParametric<Log> with
        member l.Custom p v = Log(name, p.Custom v env)

    member this.Trace(level: LogLevel, msg: string, [<A>] xs: obj[]) =
        if logger.ShouldTrace(level) then
            logger.Trace(level, String.Format(msg, xs))

    member this.Trace0(level: LogLevel, msg: string) =
        if logger.ShouldTrace(level) then
            logger.Trace(level, msg)

    member this.Trace1(level: LogLevel, msg: string, x: obj) =
        if logger.ShouldTrace(level) then
            logger.Trace(level, String.Format(msg, x))

    member this.Message(level, msg, [<A>] xs: obj []) =
        this.Trace(level, msg, xs)

    member this.Message(level, msg) =
        this.Trace0(level, msg)

    member this.Message(level, msg, x: obj) =
        this.Trace1(level, msg, x)

    member this.Nested(n: string) =
        Log(name.Nested(Name.Parse(n)), env)

    member this.Critical(m) =
        this.Trace0(CriticalLevel, m)

    member this.Critical(m, x: obj) =
        this.Trace1(CriticalLevel, m, x)

    member this.Critical(m, [<A>] xs) =
        this.Trace(CriticalLevel, m, xs)

    member this.Error(m) =
        this.Trace0(ErrorLevel, m)

    member this.Error(m, x: obj) =
        this.Trace1(ErrorLevel, m, x)

    member this.Error(m, [<A>] xs) =
        this.Trace(ErrorLevel, m, xs)

    member this.Info(m) =
        this.Trace0(InfoLevel, m)

    member this.Info(m, x: obj) =
        this.Trace1(InfoLevel, m, x)

    member this.Info(m, [<A>] xs) =
        this.Trace(InfoLevel, m, xs)

    member this.Verbose(m) =
        this.Trace0(VerboseLevel, m)

    member this.Verbose(m, x: obj) =
        this.Trace1(VerboseLevel, m, x)

    member this.Verbose(m, [<A>] xs) =
        this.Trace(VerboseLevel, m, xs)

    member this.Warn(m) =
        this.Trace0(WarnLevel, m)

    member this.Warn(m, x: obj) =
        this.Trace1(WarnLevel, m, x)

    member this.Warn(m, [<A>] xs) =
        this.Trace(WarnLevel, m, xs)

    static member Create(name, env) =
        Log(Name.Parse name, env)

    static member Create<'T>(env) =
        Log.Create(typeof<'T>.FullName, env)
