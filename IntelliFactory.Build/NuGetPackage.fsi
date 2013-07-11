namespace IntelliFactory.Build

open System
open System.IO

[<Sealed>]
type NuGetPackageBuilder =
    member Add : FSharpProject -> NuGetPackageBuilder
    member Apache20License : unit -> NuGetPackageBuilder
    member Authors : seq<string> -> NuGetPackageBuilder
    member Description : string -> NuGetPackageBuilder
    member Id : string -> NuGetPackageBuilder
    member LicenseAcceptance : ?requires: bool -> NuGetPackageBuilder
    member LicenseUrl : string -> NuGetPackageBuilder
    member ProjectUrl : string -> NuGetPackageBuilder

    interface IProject

[<Sealed>]
type NuGetPackageTool =
    member CreatePackage : unit -> NuGetPackageBuilder
    member NuSpec : file: string -> IProject
    static member internal Current : Parameter<NuGetPackageTool>
