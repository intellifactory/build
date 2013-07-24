// Copyright 2013 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License

namespace IntelliFactory.Build

open System
open System.IO
open IntelliFactory.Core

/// Configuration for building a NuGet package.
type NuGetPackageConfig =
    {
        Authors : list<string>
        Description : string
        Id : string
        LicenseUrl : option<string>
        NuGetReferences : list<INuGetReference>
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

    /// Adds another package as a dependency.
    member AddPackage : NuGetPackageBuilder -> NuGetPackageBuilder

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
type NuGetSpecProject =
    interface IProject

[<Sealed>]
type NuGetPackageTool =
    member CreatePackage : unit -> NuGetPackageBuilder
    member NuSpec : file: string -> NuGetSpecProject

    static member internal Current : Parameter<NuGetPackageTool>
