namespace IntelliFactory.Build

open IntelliFactory.Core

[<Sealed>]
type Company =
    member internal KeyFile : unit -> option<string>
    member KeyFile : string -> Company
    member Name : string
    static member Create : name: string -> Company
    static member Current : Parameter<option<Company>>
