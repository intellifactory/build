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

module NuGetTools =
    open System
    open System.Collections
    open System.Collections.Generic
    open System.IO
    open System.Xml
    type FrameworkId = RuntimeFrameworks.FrameworkId
    type FrameworkVersion = RuntimeFrameworks.FrameworkVersion
    type SemanticVersion = SemanticVersions.SemanticVersion
    type SubCollection = SubCollections.SubCollection
    type SubCollection<'R,'T> = SubCollections.SubCollection<'R,'T>
    type VersionRange = SemanticVersions.Range

    let frameworkDict =
        dict [
            FrameworkId.Net, "net"
            FrameworkId.NetCore, "win"
            FrameworkId.NetMicro, "netmicro"
            FrameworkId.Portable, "portable"
            FrameworkId.Silverlight, "sl"
            FrameworkId.WindowsPhone, "wp"
        ]

    let normProfile (prof: string) =
        match prof.ToLower() with
        | "compactframework" -> "cf"
        | "windowsphone" -> "wp"
        | "windowsphone71" -> "wp71"
        | _ -> prof

    let GetShortFrameworkName (ver: FrameworkVersion) =
        let fail () = failwithf "Cannot convert framework to NuGet format: %O" ver
        match frameworkDict.TryGetValue(ver.Id) with
        | true, id ->
            let v = ver.Version
            match int v.MajorRevision, int v.MinorRevision, v.Build, v.Revision with
            | -1, -1, -1, -1 ->
                match ver.Profile with
                | None -> String.Format("{0}{1}", id, v.Major, v.Minor)
                | Some p -> String.Format("{0}{1}-{2}", id, v.Major, v.Minor, normProfile p)
            | _ -> fail ()
        | _ -> fail ()

    type PackageSpecMinimal =
        {
            Authors : list<string>
            Description : string
            Id : string
            Version : SemanticVersion
        }

    type PackageDependencySpec =
        {
            PDFramework : option<FrameworkVersion>
            PDId : string
            PDRange : option<VersionRange>
        }

        member pd.FrameworkVersion = pd.PDFramework
        member pd.Id = pd.PDId
        member pd.Range = pd.PDRange

        static member Create(id, ?range, ?ver) =
            {
                PDFramework = ver
                PDId = id
                PDRange = range
            }

    type AssemblyReferenceSpec =
        {
            ARFileName : string
            ARVersion : option<FrameworkVersion>
        }

        member ar.AssemblyFileName = ar.ARFileName
        member ar.FrameworkVersion = ar.ARVersion

        static member Create(fn, ?ver) =
            {
                ARFileName = fn
                ARVersion = ver
            }

    type FrameworkAssemblySpec =
        {
            FAName : string
            FAVersion : option<FrameworkVersion>
        }

        member fa.AssemblyName = fa.FAName
        member fa.TargetFramework = fa.FAVersion

        static member Create(n, ?ver) =
            {
                FAName = n
                FAVersion = ver
            }

    type FileRule =
        {
            FRExclude : option<string>
            FRSource : string
            FRTarget : string
        }

        member fr.Exclude = fr.FRExclude
        member fr.SourcePattern = fr.FRSource
        member fr.TargetPath = fr.FRTarget

        static member Create(src, tgt, ?exclude) =
            {
                FRExclude = exclude
                FRSource = src
                FRTarget = tgt
            }

    let ( +! ) a b =
        match a with
        | None -> b
        | Some a -> a

    let ( ++ ) a b =
        match a with
        | None -> b
        | _ -> a

    type PackageSpec =
        {
            PAuthors : list<string>
            PCopyright : option<string>
            PDependencies : list<PackageDependencySpec>
            PDescription : string
            PFileRules : list<FileRule>
            PFrameworkAssemblies : list<FrameworkAssemblySpec>
            PIconUrl : option<string>
            PId : string
            PLanguage : option<string>
            PLicenseUrl : option<string>
            PMinClientVersion : option<Version>
            POwners : list<string>
            PProjectUrl : option<string>
            PReferences : list<AssemblyReferenceSpec>
            PReleaseNotes : option<string>
            PRequireLicenseAcceptance : bool
            PSummary : option<string>
            PTags : list<string>
            PTitle : option<string>
            PVersion : SemanticVersion
        }

        member ps.Update(   ?id: string,
                            ?version: SemanticVersion,
                            ?title: string,
                            ?description: string,
                            ?releaseNotes: string,
                            ?summary: string,
                            ?language: string,
                            ?projectUrl: string,
                            ?iconUrl: string,
                            ?licenseUrl: string,
                            ?copyright: string,
                            ?requireLicenseAcceptance: bool,
                            ?minClientVersion: Version  ) =
            {
                ps with
                    PId = id +! ps.PId
                    PVersion = version +! ps.PVersion
                    PTitle = title ++ ps.PTitle
                    PDescription = description +! ps.PDescription
                    PReleaseNotes = releaseNotes ++ ps.PReleaseNotes
                    PSummary = summary ++ ps.PSummary
                    PLanguage = language ++ ps.PLanguage
                    PProjectUrl = projectUrl ++ ps.PProjectUrl
                    PIconUrl = iconUrl ++ ps.PIconUrl
                    PLicenseUrl = licenseUrl ++ ps.PLicenseUrl
                    PCopyright = copyright ++ ps.PCopyright
                    PRequireLicenseAcceptance =
                        requireLicenseAcceptance +! ps.PRequireLicenseAcceptance
                    PMinClientVersion = minClientVersion ++ ps.PMinClientVersion
            }

        member ps.ToXml() =
            let e (name: string) =
                XmlTools.XmlElement.Create(name)
            let opt x f =
                match x with
                | None -> []
                | Some x -> [f x]
            let optc x f =
                match x with
                | [] -> []
                | xs -> [f x]
            let opte n x =
                opt x (fun v -> e n -- v)
            let renderGrouped top (ds: list<'T>)
                    (getVer: 'T -> option<FrameworkVersion>) render =
                e top -< (
                    ds
                    |> Seq.groupBy getVer
                    |> Seq.map (fun (ver, xs) ->
                        let body = [for x in xs -> render x]
                        match ver with
                        | None -> e "group" -< body
                        | Some ver ->
                            let n = GetShortFrameworkName ver
                            e "group" + ["targetFramework", n] -< body)
                )
            let renderDeps (ds: list<PackageDependencySpec>) =
                renderGrouped "dependencies" ds
                    (fun d -> d.FrameworkVersion)
                    (fun x ->
                        e "dependency" + [
                            yield ("id", x.Id)
                            match x.Range with
                            | None -> ()
                            | Some r ->
                                yield ("version", string r)
                        ])
            let renderRefs (rs: list<AssemblyReferenceSpec>) =
                renderGrouped "references" rs
                    (fun r -> r.FrameworkVersion)
                    (fun x -> e "reference" + [("file", x.AssemblyFileName)])
            let renderFAs (fa: list<FrameworkAssemblySpec>) =
                e "frameworkAssemblies" -< [
                    for a in fa ->
                        e "frameworkAssembly" + [
                            yield ("assemblyName", a.AssemblyName)
                            match a.TargetFramework with
                            | None -> ()
                            | Some v ->
                                yield ("targetFramework", GetShortFrameworkName v)
                        ]
                ]
            let xml =
                e "package" -< [
                    yield e "metadata" -< List.concat [
                        [e "id" -- ps.PId]
                        [e "version" -- string ps.PVersion]
                        opte "title" ps.PTitle
                        [e "authors" -- String.concat ", " ps.PAuthors]
                        optc ps.POwners (fun o -> e "owners" -- String.concat ", " o)
                        [e "description" -- ps.PDescription]
                        opte "releaseNotes" ps.PReleaseNotes
                        opte "summary" ps.PSummary
                        opte "langauge" ps.PLanguage
                        opte "projectUrl" ps.ProjectUrl
                        opte "iconUrl" ps.IconUrl
                        opte "licenseUrl" ps.PLicenseUrl
                        opte "copyright" ps.Copyright
                        (if ps.RequireLicenseAcceptance then
                            [e "requireLicenseAcceptance" -- "true"] else [])
                        (match ps.PDependencies with
                            | [] -> []
                            | ds -> [renderDeps ds])
                        (match ps.PReferences with
                            | [] -> []
                            | rs -> [renderRefs rs])
                        (match ps.PFrameworkAssemblies with
                            | [] -> []
                            | fs -> [renderFAs fs])
                        optc ps.PTags (fun t -> e "tags" -- String.concat " " t)
                        (match ps.PMinClientVersion with
                            | None -> []
                            | Some v -> [e "minClientVersion" -- string v])
                    ]
                ]
            xml.Write()

        member ps.WriteXml(w: TextWriter) =
            w.Write(ps.ToXml())

        member ps.Authors =
            SubCollection.Create(ps.PAuthors, fun xs ->
                { ps with PAuthors = xs })

        member ps.Copyright = ps.PCopyright

        member ps.Dependencies =
            SubCollection.Create(ps.PDependencies, fun xs ->
                { ps with PDependencies = xs })

        member ps.Description = ps.PDescription

        member ps.FrameworkAssemblies =
            SubCollection.Create(ps.PFrameworkAssemblies, fun xs ->
                { ps with PFrameworkAssemblies = xs })

        member ps.IconUrl = ps.PIconUrl
        member ps.Id = ps.PId
        member ps.Language = ps.PLanguage
        member ps.LicenseUrl = ps.PLicenseUrl
        member ps.MinClientVersion = ps.PMinClientVersion

        member ps.Owners =
            SubCollection.Create(ps.POwners, fun xs ->
                { ps with POwners = xs })

        member ps.ProjectUrl = ps.PProjectUrl

        member ps.References =
            SubCollection.Create(ps.PReferences, fun xs ->
                { ps with PReferences = xs })

        member ps.ReleaseNotes = ps.PReleaseNotes
        member ps.RequireLicenseAcceptance = ps.PRequireLicenseAcceptance
        member ps.Summary = ps.PSummary

        member ps.Tags =
            SubCollection.Create(ps.PTags, fun xs ->
                { ps with PTags = xs })

        member ps.Title = ps.PTitle
        member ps.Version = ps.PVersion

        static member Create(spec) =
            if spec.Authors.IsEmpty then
                invalidArg "authors" "Author list cannot be empty"
            {
                PAuthors = spec.Authors
                PCopyright = None
                PDependencies = []
                PDescription = spec.Description
                PFileRules = []
                PFrameworkAssemblies = []
                PIconUrl = None
                PId = spec.Id
                PLanguage = None
                PLicenseUrl = None
                PMinClientVersion = None
                POwners = []
                PProjectUrl = None
                PReferences = []
                PReleaseNotes = None
                PRequireLicenseAcceptance = false
                PSummary = None
                PTags = []
                PTitle = None
                PVersion = spec.Version
            }
