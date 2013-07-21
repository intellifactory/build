namespace IntelliFactory.Build

open System
open System.IO

/// Configuration for building a NuGet package.
type NuGetPackageConfig =
    {
        Authors : list<string>
        Description : string
        Id : string
        LicenseUrl : option<string>
        NuGetReferences : list<NuGetReference>
        OutputPath : string
        ProjectUrl : option<string>
        RequiresLicenseAcceptance : bool
        Version : Version
        VersionSuffix : option<string>
    }

    /// Sets `LicenseUrl` to point to Apache 2.0 license.
    /// Sets `RequiresLicesnseAcceptance` to `false`.
    member WithApache20License : unit -> NuGetPackageConfig

/// Defines how to build a NuGet package.
[<Sealed>]
type NuGetPackageBuilder =
    interface IProject

    /// Adds NuGet references for a given project as package references.
    member AddProject : IProject -> NuGetPackageBuilder

    /// Adds project exports to the NuGet package files.
    member AddNuGetExportingProject : INuGetExportingProject -> NuGetPackageBuilder

    /// Combines the previous two overloads.
    member Add<'T when 'T :> IProject and 'T :> INuGetExportingProject> : 'T -> NuGetPackageBuilder

    /// Confgures package license to point to Apache 2.0.
    member Apache20License : unit -> NuGetPackageBuilder

    /// Configures authors.
    member Authors : seq<string> -> NuGetPackageBuilder

    /// Allows to set various configuration options.
    member Configure : (NuGetPackageConfig -> NuGetPackageConfig) -> NuGetPackageBuilder

    /// Configures description.
    member Description : string -> NuGetPackageBuilder

    /// Configures package ID.
    member Id : string -> NuGetPackageBuilder

    /// Configures project Url.
    member ProjectUrl : string -> NuGetPackageBuilder

[<Sealed>]
type NuGetPackageTool =
    member CreatePackage : unit -> NuGetPackageBuilder

    // member NuSpec : file: string -> IProject

    static member internal Current : Parameter<NuGetPackageTool>
