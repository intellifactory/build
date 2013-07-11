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

[<Sealed>]
type Reference

[<Sealed>]
type NuGetReference =
    member At : path: string -> NuGetReference
    member Latest : unit -> NuGetReference
    member Reference : unit -> Reference
    member internal Version : unit -> option<string>
    member Version : string -> NuGetReference
    member Id : string

[<Sealed>]
type ReferenceBuilder =
    member Assembly : string -> Reference
    member File : string -> Reference
    member NuGet : string -> NuGetReference
    static member internal Current : Parameter<ReferenceBuilder>

[<Sealed>]
type ResolvedReferences =
    member Paths : seq<string>
    static member Empty : ResolvedReferences

module ReferenceConfig =
    val AssemblySearchPaths : Parameter<Framework -> seq<string>>
    val FSharp3Runtime20 : Parameter<seq<string>>
    val FSharp3Runtime40 : Parameter<seq<string>>

[<Sealed>]
type References =
    member GetNuGetReference : Reference -> option<NuGetReference>
    member Resolve : Framework -> seq<Reference> -> ResolvedReferences
    static member internal Current : Parameter<References>
