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
// permissions and limitations under the License.

/// Provides some utilities for working with NuGet packages.
module IntelliFactory.Build.NuGetUtils

open System
open System.Runtime.Versioning
open NuGet
module F = FileSystem

/// Encapsulates a local package repository.
[<Sealed>]
type LocalRepository =

    /// The path passed to constructor.
    member Path : string

    /// Resolver for package paths.
    member PathResolver : IPackagePathResolver

    /// The repository object.
    member Repository : LocalPackageRepository

    /// Constructor based on the full path to `packages` folder.
    static member Create : path: string -> LocalRepository

/// Filters NuGet packages to only include latest available versions.
val MostRecent : pkgs: seq<IPackage> -> seq<IPackage>

/// Represents an assembly reference.
type AssemblyReference =
    {
        AssemblyName : string
        AssemblyPath : option<string>
    }

/// Computes transitive assembly references using NuGet.
val ComputeReferences :
    framework: FrameworkName ->
    isRequired: (IPackage -> bool) ->
    repository: LocalRepository ->
    seq<AssemblyReference>

/// Finds a local package by ID.
val FindPackage :
    repository : LocalRepository ->
    id: string ->
    option<IPackage>

/// Represents local (in-memory) NuGet packages.
type Package =
    {
        /// The content of the `.nupkg` package file.
        mutable Content : F.Content

        /// The name of the NuGet package.
        mutable Name : string

        /// The version of the NuGet package.
        mutable Version : string
    }

    /// Loads a nupkg file.
    static member FromFile : file: string -> Package

/// Experimental utility method. Finds the most recent
/// online version of a package on the default NuGet feed.
val FindLatestOnlineVersion : packageId: string -> option<SemanticVersion>

/// Experimental utility method. Computes a version with an auto-incremented
/// build number based on the latest online version of a package.
val ComputeVersion :
    packageId: string ->
    baseVersion: NuGet.SemanticVersion ->
    NuGet.SemanticVersion
