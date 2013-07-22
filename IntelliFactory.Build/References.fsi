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

namespace IntelliFactory.Build

open System
open System.Collections
open System.Collections.Generic

/// Represents an abstract assembly reference.
[<Sealed>]
type Reference

/// Represents a resolved reference to an assembly.
[<Sealed>]
type ResolvedReference =
    interface IComparable

    /// Tests it the reference comes from the framework, as opposed to `NuGet`.
    member IsFrameworkReference : bool

    /// Full path to the resolved reference file.
    member Path : string

/// Represents a set of resolved assembly references.
[<Sealed>]
type ResolvedReferences =
    member Paths : seq<string>
    member References : seq<ResolvedReference>
    static member Empty : ResolvedReferences

type IProject =
    abstract Build : ResolvedReferences -> unit
    abstract Clean : unit -> unit
    abstract Framework : Framework
    abstract GeneratedAssemblyFiles : seq<string>
    abstract Name : string
    abstract References : seq<Reference>

[<Sealed>]
type NuGetReference =
    member At : path: string -> NuGetReference
    member Latest : unit -> NuGetReference
    member Reference : unit -> Reference
    member internal Version : unit -> option<string>
    member Version : string -> NuGetReference
    member Id : string

module ReferenceConfig =
    val AssemblySearchPaths : Parameter<Framework -> seq<string>>
    val FSharp3Runtime20 : Parameter<seq<string>>
    val FSharp3Runtime40 : Parameter<seq<string>>

[<Sealed>]
type ReferenceBuilder =
    member Assembly : string -> Reference
    member File : string -> Reference
    member NuGet : string -> NuGetReference
    member Project : IProject -> Reference
    static member internal Current : Parameter<ReferenceBuilder>

[<Sealed>]
type References =

    /// Resolves a path to a NuGet-supplied executable under a `tools` folder, such
    /// as `WebSharper.exe`.
    member FindTool : ResolvedReferences -> Framework -> fileName: string -> option<string>

    member GetNuGetReference : Reference -> option<NuGetReference>
    member Resolve : Framework -> seq<Reference> -> ResolvedReferences
    member ResolveProjectReferences : seq<Reference> -> ResolvedReferences -> ResolvedReferences
    static member internal Current : Parameter<References>
