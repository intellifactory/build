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
open IntelliFactory.Core

/// Package identifier parameters.
[<Sealed>]
type PackageId =

    /// The current package identifier, by default determined from the name of the root
    /// directory. This is used to tag assemblies with `AssemblyProductAttribute`, and determine
    /// full `PackageVersion.Full` by scanning NuGet to auto-increment the build/revision numbers.
    static member Current : Parameter<string>

/// Combines a basic version (major, minor numbers) with an optional textual suffix.
[<Sealed>]
type PackageVersion =

    /// The major number.
    member Major : int

    /// The minor number.
    member Minor : int

    /// The textual suffix for pre-release versions, if any.
    member Suffix : option<string>

    /// The textual representation, `major.minor-suffix`.
    member Text : string

    /// Creates a new version.
    static member Create : major: int * minor: int * ?suffix: string -> PackageVersion

    /// Parses from a string.
    static member Parse : string -> PackageVersion

    /// The current version, defaults to `0.0`.
    /// This is used to tag assemblies with `AssemblyVersionAttribute`, where build and
    /// revision numbers are set to `0`. This is also by default used to determine `PackageVersion.Full`.
    static member Current : Parameter<PackageVersion>

    /// The current full version. This is used to tag assemblies and with `AssemblyFileVersionAttribute`,
    /// and may also be used to determine the version of constructed NuGet packages. This defaults to
    /// `maj.min.bld.rev` where `maj`, `min` are taken from `PackageVersion.Current`, `bld` is determined
    /// by auto-incrementing until there is no conflict with global NuGet packages, and `rev` is only set
    /// in build server environments by consuling `BuildConfig.BuildNumber`.
    static member Full : Parameter<Version>

