namespace IntelliFactory.Build

open IntelliFactory.Core

[<Sealed>]
type internal CacheKey =
    new : unit -> CacheKey

[<Sealed>]
type internal Cache =
    new : unit -> Cache
    member Lookup<'T1,'T2 when 'T1 : equality> : CacheKey -> (unit -> 'T2) -> 'T1 -> 'T2
    static member Init : IParametric<'R> -> 'R
    static member Current : Parameter<Cache>
