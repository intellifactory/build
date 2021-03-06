﻿// Copyright 2013 IntelliFactory
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
open System.Security
open NuGet
open IntelliFactory.Build
open IntelliFactory.Core

[<Sealed>]
type PackageId () =

    static let current =
        Parameter.Define (fun env ->
            BuildConfig.RootDir.Find env
            |> Path.GetFullPath
            |> Path.GetDirectoryName)

    static member Current =
        current

type private SV = SafeNuGetSemanticVersion

[<Sealed>]
type PackageVersionTool(env) =
    let log = Log.Create<PackageVersionTool>(env)
    let buildNumber = BuildConfig.BuildNumber.Find env
    let cache = Cache.Current.Find env

    [<SecuritySafeCritical>]
    let getPackageManager () =
        NuGetConfig.CurrentPackageManager.Find env
        |> box

    let pm = getPackageManager ()

    [<SecuritySafeCritical>]
    let getUsedVersions pid =
        (unbox pm : NuGet.PackageManager).SourceRepository.FindPackagesById(pid)
        |> Seq.toArray
        |> Array.map SV.ForPackage

    let ver maj min bld =
        match buildNumber with
        | None -> Version(maj, min, bld)
        | Some rv -> Version(maj, min, bld, rv)

    let revise rev (orig: SV) =
        let maj = max 0 orig.Version.Major
        let min = max 0 orig.Version.Minor
        SV(ver maj min rev, ?suffix = orig.SpecialVersion)

    let getBuild (v: SV) =
        max 0 v.Version.Build

    let pickVersion (all: SV[]) (orig: SV) =
        let o = orig.Version
        let versionWithMaxN3 =
            all
            |> Seq.filter (fun v ->
                v.Version.Major = o.Major
                && v.Version.Minor = o.Minor)
            |> Seq.append [orig]
            |> Seq.maxBy getBuild
        let n3 =
            let r = getBuild versionWithMaxN3
            if r >= getBuild orig then r + 1 else r
        revise n3 orig

    static let freshVersionKey = CacheKey()

    member t.PickFreshPackageVersion(pid, v) =
       (pid, string v)
       |> cache.Lookup freshVersionKey (fun () ->
            let all = getUsedVersions pid
            let r = pickVersion all v
            log.Info("{0} --> {1}", v, r)
            r)

[<Sealed>]
type PackageVersion(major: int, minor: int, ?suffix: string) =

    let text =
        match suffix with
        | None -> String.Format("{0}.{1}", major, minor)
        | Some s ->  String.Format("{0}.{1}-{2}", major, minor, s)

    static let current =
        let v = PackageVersion.Create(0, 0)
        Parameter.Create v

    static let computeFull env =
        let tool = PackageVersionTool(env)
        let cur : PackageVersion = current.Find env
        let pid = PackageId.Current.Find env
        let v = Version(cur.Major, cur.Minor)
        let s =
            match cur.Suffix with
            | None -> SV(v)
            | Some s -> SV(v, s)
        let r : SV = tool.PickFreshPackageVersion(pid, s)
        r.Version

    static let full =
        Parameter.Define computeFull

    override v.ToString() = text

    member v.Major = major
    member v.Minor = minor
    member v.Suffix = suffix
    member v.Text = text

    static member Create(major, minor, ?suffix) =
        PackageVersion(major, minor, ?suffix = suffix)

    static member Parse(ver: string) =
        let v = SV.Parse(ver)
        let s = v.SpecialVersion
        PackageVersion.Create(v.Version.Major, v.Version.Minor, ?suffix = s)

    static member Current = current
    static member Full = full
