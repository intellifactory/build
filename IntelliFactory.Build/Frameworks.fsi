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
open System.Runtime
open System.Runtime.Versioning
open IntelliFactory.Core

/// Represents the target framework (with version and profile).
[<Sealed>]
type Framework =
    interface IComparable

    /// The NuGet-friendly name of the framework, such as "net45".
    member Name : string

/// Provides utilities for working with framework representations.
[<Sealed>]
type Frameworks =

    /// Memoizes a function over frameworks.
    member Cache : (Framework -> 'T) -> (Framework -> 'T)

    /// Given supported frameworks for a given assembly, lists
    /// all frameworks on which the assembly would work given the
    /// backwards compatibility relation defined by `IsCompatible`.
    member FindSupported : frameworks: seq<Framework> -> (Framework -> bool)

    /// Tries to parse a `FrameworkName` object.
    member FromFrameworkName : FrameworkName -> option<Framework>

    /// Tests backwards compatibility.
    member IsCompatible : newFramework: Framework -> oldFramework: Framework -> bool

    /// Constructs an equivalent `FrameworkName` object.
    member ToFrameworkName : Framework -> FrameworkName

    /// Lists all known frameworks.
    member All : seq<Framework>

    /// .NET Framework 2.0.
    member Net20 : Framework

    /// .NET Framework 3.0.
    member Net30 : Framework

    /// .NET Framework 3.5.
    member Net35 : Framework

    /// .NET Framework 3.5 Client Profile.
    member Net35CP : Framework

    /// .NET Framework 4.0.
    member Net40 : Framework

    /// .NET Framework 4.0 Client Profile.
    member Net40CP : Framework

    /// .NET Framework 4.5.
    member Net45 : Framework

    /// The current tool as a parameter.
    static member internal Current : Parameter<Frameworks>
