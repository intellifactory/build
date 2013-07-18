namespace IntelliFactory.Build

module WebSharperConfig =

    /// Path to the directory containing WebSharper tools.
    val WebSharperHome : Parameter<option<string>>

[<Sealed>]
type WebSharperProject =
    interface IFSharpProjectContainer<WebSharperProject>
    interface INuGetExportingProject
    interface IParametric
    interface IParametric<WebSharperProject>
    interface IProject

[<Sealed>]
type WebSharperProjects =
    member Extension : name: string -> WebSharperProject
    member Library : name: string -> WebSharperProject
    static member internal Current : Parameter<WebSharperProjects>
