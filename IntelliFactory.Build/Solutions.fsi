namespace IntelliFactory.Build

[<Sealed>]
type Solution =
    member Build : unit -> unit
    member Clean : unit -> unit

[<Sealed>]
type Solutions =
    member Solution : seq<IProject> -> Solution
    static member internal Current : Parameter<Solutions>
