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

namespace IntelliFactory.Core

module RuntimeFrameworks =
    open System
    open System.Runtime
    open System.Runtime.Versioning
    open IntelliFactory

    type FrameworkId =
        | FwNet
        | FwNetCore
        | FwNetMicro
        | FwNetPortable
        | FwSilverlight
        | FwWindowsPhone

        override fw.ToString() =
            fw.Id

        member fw.Id =
            match fw with
            | FwNet -> ".NETFramework"
            | FwNetCore -> ".NETCore"
            | FwNetMicro -> ".NETMicroFramework"
            | FwNetPortable -> ".NETPortable"
            | FwSilverlight -> "Silverlight"
            | FwWindowsPhone -> "WindowsPhone"

        member fw.NuGetId =
            match fw with
            | FwNet -> "net"
            | FwNetCore -> "win"
            | FwNetMicro -> "netmf"
            | FwNetPortable -> "portable"
            | FwSilverlight -> "sl"
            | FwWindowsPhone -> "wp"

        static member Net = FwNet
        static member NetCore = FwNetCore
        static member NetMicro = FwNetMicro
        static member Portable = FwNetPortable
        static member Silverlight = FwSilverlight
        static member WindowsPhone = FwWindowsPhone

    let ( == ) a b =
        Object.ReferenceEquals(a, b)

    let nullToOpt t =
        if t == null then None else Some t

    type FrameworkVersion =
        {
            FFwName : FrameworkName
            FId : FrameworkId
            FProfile : string
            FVersion : Version
        }

        member fw.Id = fw.FId
        member fw.FrameworkName = fw.FFwName
        member fw.Profile = nullToOpt fw.FProfile
        member fw.Version = fw.FVersion

//    let normProfile (prof: string) =
//        match prof.ToLower() with
//        | "compactframework" -> "cf"
//        | "windowsphone" -> "wp"
//        | "windowsphone71" -> "wp71"
//        | _ -> prof
//
//    let getNuGetId (id: FrameworkId) (maj: int) (min: int) (prof: string) =
//        match prof with
//        | null -> String.Format("{0}{1}{2}", id.NuGetId, maj, min)
//        | p -> String.Format("{0}{1}{2}-{3}", id.NuGetId, maj, min, normProfile p)

    let getFwName (id: FrameworkId) (ver: Version) (prof: string) =
        match prof with
        | null -> FrameworkName(id.Id, ver)
        | p -> FrameworkName(id.Id, ver, p)

    let defFramework id maj min prof =
        let ver = Version(maj, min)
        {
            FFwName = getFwName id ver prof
            FId = id
//            FNuGetId = getNuGetId id maj min null
            FProfile = prof
            FVersion = ver
        }

    let defF id maj min = defFramework id maj min null
    let defFP id maj min prof = defFramework id maj min prof

    let net20 = defF FwNet 2 0
    let net30 = defF FwNet 3 0
    let net35 = defF FwNet 3 5
    let net35cp = defFP FwNet 3 5 "Client"
    let net40 = defF FwNet 4 0
    let net40cp = defFP FwNet 4 0 "Client"
    let net45 = defF FwNet 4 5
    let netCore45 = defF FwNetCore 4 5
    let sl30wp70 = defFP FwSilverlight 3 0 "WindowsPhone"
    let sl40 = defF FwSilverlight 4 0
    let sl50 = defF FwSilverlight 5 0
    let sl40wp71 = defFP FwSilverlight 4 0 "WindowsPhone71"
    let wp80 = defF FwWindowsPhone 8 0

    type FrameworkVersion with
        static member Net20 = net20
        static member Net30 = net30
        static member Net35 = net35
        static member Net35Client = net35cp
        static member Net40 = net40
        static member Net40Client = net40cp
        static member Net45 = net45
        static member NetCore45 = netCore45
        static member Silverlight30WindowsPhone70 = sl30wp70
        static member Silverlight40 = sl40
        static member Silverlight40WindowsPhone71 = sl40wp71
        static member Silverlight50 = sl50
        static member WindowsPhone80 = wp80

        static member Create(id, ver, ?profile) =
            { defFramework id 0 0 (defaultArg profile null) with
                FVersion = ver }
