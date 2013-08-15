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
// permissions and limitations under the License

namespace IntelliFactory.Build

open System
open System.Collections.Generic
open IntelliFactory.Core

[<Sealed>]
type CacheKey() =
    class
    end

type CacheEntryKey<'T when 'T : equality> =
    {
        CacheKey : CacheKey
        Input : 'T
    }

[<Sealed>]
type Cache() =
    static let mutable n = 0
    let k = System.Threading.Interlocked.Increment(&n)
    let root = obj ()
    let dict = Dictionary<obj,obj>(HashIdentity.Structural)
    static let current = Parameter.Create(Cache())

    member c.Lookup<'T1,'T2 when 'T1 : equality> (key: CacheKey) (f: unit -> 'T2) (input: 'T1) : 'T2 =
        lock root <| fun () ->
            let k = { CacheKey = key; Input = input }
            match dict.TryGetValue(k) with
            | true, (:? 'T2 as res) -> res
            | _ ->
                let res = f ()
                dict.Add(k, res)
                res

    member c.TryLookup<'T1,'T2 when 'T1 : equality> (key: CacheKey) (f: unit -> option<'T2>) (input: 'T1) : option<'T2> =
        lock root <| fun () ->
            let k = { CacheKey = key; Input = input }
            match dict.TryGetValue(k) with
            | true, (:? 'T2 as res) -> Some res
            | _ ->
                let res = f ()
                match res with
                | None -> None
                | Some r ->
                    dict.Add(k, r)
                    res

    static member Init(ps: IParametric<'R>) : 'R =
        ps
        |> current.Custom (Cache ())

    static member Current = current
