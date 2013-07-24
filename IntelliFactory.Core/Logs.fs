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

module IntelliFactory.Core.Logs

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open IntelliFactory.Core
type A = ParamArrayAttribute

[<AutoOpen>]
module Identifiers =
    let private pat = Regex("^\w+$")

    type Identifier =
        private { name : string }
        override this.ToString() = this.name

        static member Create(id: string) =
            if pat.IsMatch id then { name = id } else
                String.Format("Identifier must be alphanumeric. Given: {0}", id)
                |> invalidArg "id"

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

type Level =
    | Critical
    | Error
    | Warn
    | Info
    | Verbose

    override l.ToString() =
        l.Name

    member l.Name =
        match l with
        | Critical -> "critical"
        | Error -> "error"
        | Info -> "info"
        | Verbose -> "verbose"
        | Warn -> "warn"

    member l.EventType =
        match l with
        | Critical -> TraceEventType.Critical
        | Error -> TraceEventType.Error
        | Info -> TraceEventType.Information
        | Verbose -> TraceEventType.Verbose
        | Warn -> TraceEventType.Warning

type ITrace =
    abstract ShouldTrace : Level -> bool
    abstract Trace : Level * string -> unit

type IConfig =
    abstract GetTrace : string -> ITrace

type Restriction =
    | Restrict of Name * Level

type Sink =
    | ConsoleSink
    | DiagnosticsSink
    | NoSink
    | TraceSourceSink of TraceSource

type NullTrace =
    | NullTrace

    interface ITrace with
        member l.ShouldTrace(_) = false
        member l.Trace(_, _) = ()

[<Sealed>]
type SinkLogger(name: string, sink: Sink, lev: Level) =

    let trace =
        match sink with
        | ConsoleSink ->
            let out = Console.Out
            let err = Console.Error
            fun lev msg ->
                let out =
                    match lev with
                    | Error | Critical | Warn -> err
                    | Verbose | Info -> out
                out.WriteLine("{2}: [{0}] {1}", lev, msg, name)
                out.Flush()
        | DiagnosticsSink ->
            fun lev msg ->
                let msg = String.Format("{0}: {1}", name, msg)
                match lev with
                | Error -> Trace.TraceError(msg)
                | Critical -> Trace.TraceError("[CRITICAL] {0}", msg)
                | Warn -> Trace.TraceWarning(msg)
                | Info -> Trace.TraceInformation(msg)
                | Verbose -> Debug.WriteLine(msg)
        | TraceSourceSink ts ->
            fun lev msg ->
                let msg = String.Format("{0}: {1}", name, msg)
                let t = lev.EventType
                if ts.Switch.ShouldTrace(t) then
                    ts.TraceEvent(t, 0, msg)
        | NoSink ->
            fun lev msg -> ()

    interface ITrace with
        member l.ShouldTrace(x) = x <= lev
        member l.Trace(lev, msg) = trace lev msg

[<Sealed>]
type DefaultConfig(def: option<Level>, rs: list<Restriction>, sink: Sink) =

    new () = DefaultConfig(None, [], NoSink)

    member c.Restrict(name, level) =
        DefaultConfig(def, Restrict (Name.Parse name, level) :: rs, sink)

    member c.Critical(name) = c.Restrict(name, Critical)
    member c.Error(name) = c.Restrict(name, Error)
    member c.Info(name) = c.Restrict(name, Info)
    member c.Verbose(name) = c.Restrict(name, Verbose)
    member c.Warn(name) = c.Restrict(name, Warn)

    member c.Default(def) = DefaultConfig(Some def, rs, sink)
    member c.Critical() = c.Default(Critical)
    member c.Error() = c.Default(Error)
    member c.Info() = c.Default(Info)
    member c.Verbose() = c.Default(Verbose)
    member c.Warn() = c.Default(Warn)

    member c.ToSink(s) = DefaultConfig(def, rs, s)
    member c.ToConsole() = c.ToSink(ConsoleSink)
    member c.ToDiagnostics() = c.ToSink(DiagnosticsSink)
    member c.ToTraceSource(ts) = c.ToSink(TraceSourceSink ts)

    interface IConfig with
        member cf.GetTrace(name: string) =
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
            | None -> NullTrace :> ITrace
            | Some lev -> SinkLogger(string name, sink, lev) :> ITrace

let Default = DefaultConfig ()
let Config = Parameter.Create (Default :> IConfig)

[<Sealed>]
type Log(name: Name, env: Parameters) =
    let lc = Config.Find env
    let t = lc.GetTrace(string name)

    interface IParametric with
        member l.Parameters = env

    interface IParametric<Log> with
        member l.WithParameters p = Log(name, p)

    member this.Trace(level: Level, msg: string, [<A>] xs: obj[]) =
        if t.ShouldTrace(level) then
            t.Trace(level, String.Format(msg, xs))

    member this.Trace0(level: Level, msg: string) =
        if t.ShouldTrace(level) then
            t.Trace(level, msg)

    member this.Trace1(level: Level, msg: string, x: obj) =
        if t.ShouldTrace(level) then
            t.Trace(level, String.Format(msg, x))

    member this.Message(level, msg, [<A>] xs: obj []) =
        this.Trace(level, msg, xs)

    member this.Message(level, msg) =
        this.Trace0(level, msg)

    member this.Message(level, msg, x: obj) =
        this.Trace1(level, msg, x)

    member this.Nested(n: string) =
        Log(name.Nested(Name.Parse(n)), env)

    member this.Critical(m) =
        this.Trace0(Critical, m)

    member this.Critical(m, x: obj) =
        this.Trace1(Critical, m, x)

    member this.Critical(m, [<A>] xs) =
        this.Trace(Critical, m, xs)

    member this.Error(m) =
        this.Trace0(Error, m)

    member this.Error(m, x: obj) =
        this.Trace1(Error, m, x)

    member this.Error(m, [<A>] xs) =
        this.Trace(Error, m, xs)

    member this.Info(m) =
        this.Trace0(Info, m)

    member this.Info(m, x: obj) =
        this.Trace1(Info, m, x)

    member this.Info(m, [<A>] xs) =
        this.Trace(Info, m, xs)

    member this.Verbose(m) =
        this.Trace0(Verbose, m)

    member this.Verbose(m, x: obj) =
        this.Trace1(Verbose, m, x)

    member this.Verbose(m, [<A>] xs) =
        this.Trace(Verbose, m, xs)

    member this.Warn(m) =
        this.Trace0(Warn, m)

    member this.Warn(m, x: obj) =
        this.Trace1(Warn, m, x)

    member this.Warn(m, [<A>] xs) =
        this.Trace(Warn, m, xs)

    static member Create(name, env: IParametric) =
        Log(Name.Parse name, env.Parameters)

    static member Create<'T>(env: IParametric) =
        let name =
            typeof<'T>.FullName
                .Replace("+", ".")
                .Replace("/", ".")
                .Replace("`", "_")
        Log.Create(name, env.Parameters)
