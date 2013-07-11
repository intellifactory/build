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

#if INTERACTIVE
open IntelliFactory.Build
#endif

open System
open System.Runtime
open System.Runtime.Versioning

type Framework =
    | Net20
    | Net30
    | Net35CP
    | Net35
    | Net40CP
    | Net40
    | Net45

    member this.Name =
        match this with
        | Net20 -> "net20"
        | Net30 -> "net30"
        | Net35 -> "net35"
        | Net35CP -> "net35-client"
        | Net40 -> "net40"
        | Net40CP -> "net40-client"
        | Net45 -> "net45"

    override this.ToString() =
        this.Name

[<Sealed>]
type Frameworks private (env) =
    static let current = Parameter.Define(fun ps -> Frameworks ps)

    static let all =
        [
            Net20
            Net30
            Net35CP
            Net35
            Net40CP
            Net40
            Net45
        ]

    member this.Cache(f: Framework -> 'T) : (Framework -> 'T) =
        let net20 = lazy f Net20
        let net30 = lazy f Net30
        let net35 = lazy f Net35
        let net35cp = lazy f Net35CP
        let net40 = lazy f Net40
        let net40cp = lazy f Net40CP
        let net45 = lazy f Net45
        let (!) (x: Lazy<'T>) = x.Value
        function
        | Net20 -> !net20
        | Net30 -> !net30
        | Net35 -> !net35
        | Net35CP -> !net35cp
        | Net40 -> !net40
        | Net40CP -> !net40cp
        | Net45 -> !net45

    member this.FindSupported(frameworks: seq<Framework>) =
        let frameworks = Seq.toArray frameworks
        this.Cache(fun nF -> frameworks |> Array.exists (this.IsCompatible nF))

    member this.FromFrameworkName(fn: FrameworkName) =
        match fn.Identifier.ToLower() with
        | ".netframework" ->
            match string(fn.Version), string(fn.Profile).ToLower() with
            | "2.0", _ -> Some Net20
            | "3.0", _ -> Some Net30
            | "3.5", "client" -> Some Net35CP
            | "3.5", _ -> Some Net35
            | "4.0", "client" -> Some Net40CP
            | "4.0", _ -> Some Net40
            | "4.5", _ -> Some Net45
            | _ -> None
        | _ -> None

    member this.IsCompatible(newFramework: Framework)(oldFramework: Framework) =
        match oldFramework, newFramework with
        | Net20, (Net30 | Net35 | Net40 | Net45)
        | Net30, (Net35 | Net40 | Net45)
        | Net35CP, (Net35 | Net40CP | Net40 | Net45)
        | Net35, (Net40 | Net45)
        | Net40CP, (Net40 | Net45)
        | Net40, Net45 -> true
        | a, b -> a = b

    member this.ToFrameworkName(fw: Framework) =
        let profile =
            match fw with
            | Net20 -> None
            | Net30 -> None
            | Net35 -> None
            | Net35CP -> Some "Client"
            | Net40 -> None
            | Net40CP -> Some "Client"
            | Net45 -> None
        let ver =
            match fw with
            | Net20 -> "2.0"
            | Net30 -> "3.0"
            | Net35 | Net35CP -> "3.5"
            | Net40 | Net40CP -> "4.0"
            | Net45 -> "4.5"
        match profile with
        | None -> FrameworkName(".NetFramework", Version ver)
        | Some p -> FrameworkName(".NetFramework", Version ver, p)

    member this.All = all :> seq<_>
    member this.Net20 = Net20
    member this.Net30 = Net30
    member this.Net35 = Net35
    member this.Net35CP = Net35CP
    member this.Net40 = Net40
    member this.Net40CP = Net40CP
    member this.Net45 = Net45

    static member Current = current
