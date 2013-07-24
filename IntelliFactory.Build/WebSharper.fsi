namespace IntelliFactory.Build

open IntelliFactory.Core

module WebSharperConfig =

    /// Path to the directory containing WebSharper tools.
    val WebSharperHome : Parameter<option<string>>

[<Sealed>]
type WebSharperProject =
    interface INuGetExportingProject
    interface IParametric
    interface IParametric<WebSharperProject>
    interface IProject
    interface IReferenceProject

[<Sealed>]
type WebSharperHostWebsite =
    interface IParametric
    interface IParametric<WebSharperHostWebsite>
    interface IProject

[<Sealed>]
type WebSharperProjects =
    member Extension : name: string -> WebSharperProject
    member HostWebsite : name: string -> WebSharperHostWebsite
    member Library : name: string -> WebSharperProject
    static member internal Current : Parameter<WebSharperProjects>
