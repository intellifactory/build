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

open System
open System.Collections.Generic
open System.Threading

type ParameterKey = int64

module ParameterKeys =
    let mutable counter = 0L
    let fresh () = Interlocked.Increment(&counter)

type Parameters =
    {
        cache : Dictionary<ParameterKey,obj>
        map : Map<ParameterKey,obj>
        root : obj
    }

    static member Get(p: IParametric) =
        p.Parameters

    static member Default =
        {
            cache = Dictionary()
            map = Map.empty
            root = obj ()
        }

    interface IParametric with

        member ps.Find p =
            lock ps.root <| fun () ->
                let (|T|_|) (x: obj) =
                    p.unpack x
                match ps.cache.TryGetValue(p.key) with
                | true, T v -> v
                | _ -> 
                    let v =
                        match Map.tryFind p.key ps.map with
                        | Some (T v) -> v
                        | _ -> p.def ps
                    ps.cache.[p.key] <- p.pack v
                    v

        member ps.Parameters = ps

    interface IParametric<Parameters> with
        member ps.Custom p v =
            {
                cache = Dictionary()
                map = Map.add p.key (p.pack v) ps.map
                root = obj ()
            }

and Parameter<'T> =
    {
        def : Parameters -> 'T
        key : ParameterKey
        pack : 'T -> obj
        unpack : obj -> option<'T>
    }

    member p.Custom(v: 'T)(ps: IParametric<'R>) =
        ps.Custom p v

    member p.Find(ps: IParametric) : 'T =
        ps.Find p

    member p.Update(f: 'T -> 'T)(ps: IParametric<'R>) =
        p.Custom (f (p.Find ps)) ps

and IParametric<'R> =
    inherit IParametric
    abstract Custom<'T> : Parameter<'T> -> 'T -> 'R

and IParametric =
    abstract Find<'T> : Parameter<'T> -> 'T
    abstract Parameters : Parameters

[<Sealed>]
type Parameter =

    static member Define<'T>(make: Parameters -> 'T) =
        {
            def = make
            key = ParameterKeys.fresh ()
            pack = fun x -> box x
            unpack = fun x ->
                match x with
                | :? 'T as r -> Some r
                | _ -> None
        }

    static member Create v =
        Parameter.Define (fun ps -> v)

    static member Convert(f: 'A -> 'B)(g: 'B -> 'A)(p: Parameter<'A>) =
        {
            def = p.def >> f
            key = p.key
            pack = g >> p.pack
            unpack = p.unpack >> Option.map f
        }
