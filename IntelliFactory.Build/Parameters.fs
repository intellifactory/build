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

type Key = int64

module Keys =
    let mutable counter = 0L
    let fresh () = Interlocked.Increment(&counter)

type Parameters =
    {
        cache : Dictionary<Key,obj>
        map : Map<Key,obj>
        root : obj
    }

    static member Default =
        {
            cache = Dictionary()
            map = Map.empty
            root = obj ()
        }

type Parameter<'T> =
    {
        def : Parameters -> 'T
        key : Key
        pack : 'T -> obj
        unpack : obj -> option<'T>
    }

    member p.Custom(v: 'T)(ps: Parameters) =
        {
            cache = Dictionary()
            map = Map.add p.key (p.pack v) ps.map
            root = obj ()
        }

    member p.Find(ps: Parameters) : 'T =
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

[<Sealed>]
type Parameter =

    static member Define<'T>(make: Parameters -> 'T) =
        {
            def = make
            key = Keys.fresh ()
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
