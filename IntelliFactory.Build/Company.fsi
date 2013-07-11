namespace IntelliFactory.Build

[<Sealed>]
type Company =
    member internal KeyFile : unit -> option<string>
    member KeyFile : string -> Company
    member Name : string
    static member Create : name: string -> Company
    static member Current : Parameter<option<Company>>
