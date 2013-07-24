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

    static member Init(ps: IParametric<'R>) : 'R =
        ps
        |> current.Custom (Cache ())

    static member Current = current
