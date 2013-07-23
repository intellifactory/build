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
module IntelliFactory.Core.Parametrization

open System
open System.Collections.Generic
open System.Runtime.Remoting.Messaging
open System.Security
open System.Threading

type ParameterKey = int64

let mutable counter = 0L

let freshKey () =
    Interlocked.Increment(&counter)

[<Literal>]
let callContextKey =
    "IntelliFactory.Core.Parameters"

type Parameters =
    {
        Cache : Dictionary<ParameterKey,obj>
        Map : Map<ParameterKey,obj>
        Root : obj
    }

    [<SecuritySafeCritical>]
    static member ContextLoad() =
        match CallContext.LogicalGetData callContextKey with
        | :? Parameters as ps -> ps
        | _ -> Parameters.Default()

    [<SecuritySafeCritical>]
    static member ContextStore(ps: Parameters) =
        CallContext.LogicalSetData(callContextKey, ps)

    member ps.Extend(overrides: Parameters) =
        let map =
            seq {
                yield! Map.toSeq ps.Map
                yield! Map.toSeq overrides.Map
            }
            |> Map.ofSeq
        {
            Cache = Dictionary()
            Map = map
            Root = obj ()
        }

    [<SecurityCritical>]
    member ps.WithExtendedCallContext(f: unit -> 'T) : 'T =
        let old = Parameters.ContextLoad()
        try
            old.Extend ps
            |> Parameters.ContextStore
            f ()
        finally
            Parameters.ContextStore old

    [<SecurityCritical>]
    member ps.WithExtendedCallContext(work: Async<'T>) : Async<'T> =
        async {
            let old = Parameters.ContextLoad()
            try
                old.Extend ps
                |> Parameters.ContextStore
                return! work
            finally
                Parameters.ContextStore old
        }

    [<SecurityCritical>]
    static member FromCallContext() =
        Parameters.ContextLoad()

    static member Get(p: IParametric) =
        p.Parameters

    static member Set ps (p: IParametric<'R>) =
        p.WithParameters ps

    static member Default() =
        {
            Cache = Dictionary()
            Map = Map.empty
            Root = obj ()
        }

    interface IParametric with
        member x.Parameters = x

    interface IParametric<Parameters> with
        member x.WithParameters ps = ps

and Parameter<'T> =
    {
        Def : Parameters -> 'T
        Key : ParameterKey
        Pack : 'T -> obj
        Unpack : obj -> option<'T>
    }

    member p.Custom(v: 'T)(ps: IParametric<'R>) =
        {
            Cache = Dictionary()
            Map = Map.add p.Key (p.Pack v) ps.Parameters.Map
            Root = obj ()
        }
        |> ps.WithParameters

    member p.Find(ps: IParametric) : 'T =
        let ps = ps.Parameters
        let (|T|_|) (x: obj) = p.Unpack x
        lock ps.Root <| fun () ->
            match ps.Cache.TryGetValue(p.Key) with
            | true, T v -> v
            | _ -> 
                let v =
                    match Map.tryFind p.Key ps.Map with
                    | Some (T v) -> v
                    | _ -> p.Def ps
                ps.Cache.[p.Key] <- p.Pack v
                v

    [<SecurityCritical>]
    member p.FromCallContext() =
        Parameters.FromCallContext()
        |> p.Find

    member p.Update(f: 'T -> 'T)(ps: IParametric<'R>) =
        p.Custom (f (p.Find ps)) ps

and IParametric =
    abstract Parameters : Parameters

and IParametric<'R> =
    inherit IParametric
    abstract WithParameters : Parameters -> 'R

[<Sealed>]
type Parameter =

    static member Define<'T>(make: Parameters -> 'T) =
        {
            Def = make
            Key = freshKey ()
            Pack = fun x -> box x
            Unpack = fun x ->
                match x with
                | :? 'T as r -> Some r
                | _ -> None
        }

    static member Create v =
        Parameter.Define (fun ps -> v)

    static member Convert(f: 'A -> 'B)(g: 'B -> 'A)(p: Parameter<'A>) =
        {
            Def = p.Def >> f
            Key = p.Key
            Pack = g >> p.Pack
            Unpack = p.Unpack >> Option.map f
        }
