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
[<AutoOpen>]
module IntelliFactory.Core.AssemblyResolution

open System

/// An utility for resolving assemblies from non-standard contexts.
[<Sealed>]
type AssemblyResolver =

    /// Installs the resolver into an `AppDomain`.
    member Install : ?domain: AppDomain -> unit

    /// Uninstalls the resolver from an `AppDomain`.
    member Remove : ?domain: AppDomain -> unit

    /// Wraps an action in `Install/Remove` on the current domain.
    member Wrap : (unit -> 'T) -> 'T

    /// Wraps an action in `Install/Remove` on the given domain.
    member WrapDomain : AppDomain -> (unit -> 'T) -> 'T

    /// Combines two resolvers with the second one acting as fallback.
    static member Fallback : AssemblyResolver * AssemblyResolver -> AssemblyResolver

    /// Searches the given AppDomain.
    static member SearchDomain : ?domain: AppDomain -> AssemblyResolver

    /// Creates an assembly resolver based on the given search paths.
    static member SearchPaths : searchPaths: seq<string> -> AssemblyResolver

    /// The `Zero` resolver always refueses to resolve.
    static member Zero : AssemblyResolver

    /// Alias for `Fallback`.
    static member ( + ) : AssemblyResolver * AssemblyResolver -> AssemblyResolver
