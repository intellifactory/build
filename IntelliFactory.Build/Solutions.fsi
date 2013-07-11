namespace IntelliFactory.Build

type IProject =
    abstract Build : ResolvedReferences -> unit
    abstract Clean : unit -> unit
    abstract Framework : Framework
    abstract Name : string
    abstract References : seq<Reference>

[<Sealed>]
type Solution =
    member Build : unit -> unit
    member Clean : unit -> unit

[<Sealed>]
type Solutions =
    member Solution : seq<IProject> -> Solution
    static member internal Current : Parameter<Solutions>
