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
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Runtime.Versioning
open System.Security
open IntelliFactory.Build
open IntelliFactory.Core

type ResolvedReference =
    | ResolvedRef of string
    | ResolvedFrameworkRef of string

    member rr.IsFrameworkReference =
        match rr with
        | ResolvedFrameworkRef _ -> true
        | _ -> false

    member rr.Path =
        match rr with
        | ResolvedRef p
        | ResolvedFrameworkRef p -> p

type PackageSet =
    Dictionary<string,SafeNuGetPackage>

type PackageReference =
    {
        id : string
        version : option<string>
    }

type NuGetReference =
    {
        package : PackageReference
        path : option<list<string>>
    }

    static member Wrap(ng: INuGetReference) =
        {
            package =
                {
                    id = ng.PackageId
                    version = ng.PackageVersion
                }
            path = Option.map Seq.toList ng.Paths
        }

    interface INuGetReference with
        member r.PackageId = r.package.id
        member r.PackageVersion = r.package.version
        member r.Paths = Option.map Seq.ofList r.path

    member r.At (path: seq<string>) =
        { r with path = Some (Seq.toList path) }

    member r.Latest() =
        { r with package = { r.package with version = None } }

    member r.Version() =
        r.package.version

    member r.Version v =
        { r with package = { r.package with version = Some v } }

    member r.Reference() =
        NuGetRef r

    member r.Id =
        r.package.id

type ResolvedReferences =
    {
        pack : PackageSet
        refs : seq<ResolvedReference>
    }

    member rr.Paths = seq { for r in rr.refs -> r.Path }
    member rr.References = rr.refs
    member rr.WithReferences(refs) = { rr with refs = refs }

    static member Empty =
        {
            pack = Dictionary()
            refs = Seq.empty
        }

[<Sealed>]
type ReferenceBuilder private (env: Parameters) =
    static let current = Parameter.Define(fun ps -> ReferenceBuilder ps)

    member b.Assembly r =
        AssemblyName(r)
        |> string
        |> SystemRef

    member b.File f =
        FileRef f

    member b.NuGet pkg =
        {
            package =
                {
                    id = pkg
                    version = None
                }
            path = None
        }

    member b.Project p =
        ProjectRef p

    static member Current = current

module ReferenceConfig =

    let getReferenceAssemblies () =
        GetProgramFiles () +/ "Reference Assemblies" +/ "Microsoft"

    let getFSharp3Runtime () =
        getReferenceAssemblies () +/ "FSharp" +/ "3.0" +/ "Runtime"

    let FSharp3Runtime20 =
        Parameter.Define (fun env ->
            Seq.ofList [getFSharp3Runtime () +/ "v2.0"])

    let FSharp3Runtime40 =
        Parameter.Define (fun env ->
            let ramf3r = getFSharp3Runtime ()
            Seq.ofList [ramf3r +/ "v4.0"; ramf3r +/ "v4.0" +/ "Type Providers"])

    let MonoLibs =
        Parameter.Define (fun env ->
            match Environment.GetEnvironmentVariable("MonoLibs") with
            | null | "" -> "/usr/lib/mono"
            | m -> m)

    let AssemblySearchPaths =
        Parameter.Define (fun env ->
            let fwt = Frameworks.Current.Find env
            let win = Environment.GetFolderPath Environment.SpecialFolder.Windows
            let pf = GetProgramFiles ()
            let ml = MonoLibs.Find env
            let ram = pf +/ "Reference Assemblies" +/ "Microsoft"
            let ramfn = ram +/ "Framework" +/ ".NETFramework"
            let ramf3r = ram +/ "FSharp" +/ "3.0" +/ "Runtime"
            let fs23 = List.ofSeq (FSharp3Runtime20.Find env)
            let fs43 = List.ofSeq (FSharp3Runtime40.Find env)
            let old = [win +/ "Microsoft.NET" +/ "Framework" +/ "v2.0.50727"] @ fs23
            fwt.Cache <| fun fw ->
                match fw with
                | Is fwt.Net45 -> [ramfn +/ "v4.5"; ml +/ "4.5"] @ fs43
                | Is fwt.Net40 -> [ramfn +/ "v4.0"; ml +/ "4.0"] @ fs43
                | Is fwt.Net40CP -> [ramfn +/ "v4.0" +/ "Profile" +/ "Client"] @ fs43
                | Is fwt.Net35CP -> [ramfn +/ "v3.5" +/ "Profile" +/ "Client"] @ fs23
                | Is fwt.Net35 -> [ram +/ "Framework" +/ "v3.5"; ml +/ "3.5"] @ old
                | Is fwt.Net30 -> [ram +/ "Framework" +/ "v3.0"] @ old
                | Is fwt.Net20 -> [ml +/ "2.0"] @ old
                | _ -> []
                |> Seq.ofList)

[<Sealed>]
type SystemResolver private (env) =
    static let current = Parameter.Define(fun env -> SystemResolver env)
    static let ck = CacheKey()

    let fwt = Frameworks.Current.Find env
    let paths = ReferenceConfig.AssemblySearchPaths.Find env |> fwt.Cache
    let log = Log.Create<SystemResolver>(env)
    let cache = Cache.Current.Find env

    let inDirByExt (ext: string) (ref: AssemblyName) (dir: string) =
        let p = Path.Combine(dir, ref.Name + ext)
        if IsFile p then
            let def = AssemblyName.GetAssemblyName(p)
            if def <> null && def.Name = ref.Name
                then Some p
                else None
        else None

    let inDir ref dir =
        match inDirByExt ".dll" ref dir with
        | None -> inDirByExt ".exe" ref dir
        | r -> r

    member this.Resolve fw (name: AssemblyName) =
        (fw, string name)
        |> cache.Lookup ck (fun () ->
            Seq.tryPick (inDir name) (paths fw)
            |> Option.map (fun p -> ResolvedFrameworkRef p))

    static member Current = current

type FitnessScore =
    | SupportedMatch of Framework
    | TheExactMatch

[<AutoOpen>]
module AssemblySets =

    /// TODO: can detect version conflicts and missing refs here.
    let buildAssemblySet (files: seq<ResolvedReference>) =
        files
        |> Seq.distinctBy (fun f ->
            let n = AssemblyName.GetAssemblyName f.Path
            n |> string)
        |> Reify

[<Sealed>]
type NuGetManager =

    [<SecuritySafeCritical>]
    static member Current env =
        let pm = NuGetConfig.CurrentPackageManager.Find env
        SafeNuGetPackageManager(pm)

[<Sealed>]
type NuGetResolver private (env) =
    static let current = Parameter.Define(fun env -> NuGetResolver env)
    static let ck = CacheKey()
    let log = Log.Create<NuGetResolver>(env)
    let pm = NuGetManager.Current env
    let fwt = Frameworks.Current.Find env
    let srt = SystemResolver.Current.Find env
    let repo = NuGetConfig.LocalRepositoryPath.Find env
    let cache = Cache.Current.Find env

    let findPath pkg (p: string) =
        let p = p.TrimStart [| '\\'; '/' |]
        pm.GetPackageDirectory(pkg)
        |> Option.bind (fun pkgDir ->
            let file = repo +/ pkgDir +/ p
            if IsFile file then Some file else None)
        |> Option.map (fun p -> ResolvedRef p)

    let bestFit getTarget getSupported fw (xs: seq<'T>) : option<'T> =

        let score item =
            let target = getTarget item
            let supported =
                getSupported item
                |> Seq.choose fwt.FromFrameworkName
                |> fwt.FindSupported
            let allSupported =
                fwt.All |> Seq.filter supported
            let target =
                match target with
                | None ->
                    allSupported
                    |> Seq.map Some
                    |> Seq.append [None]
                    |> Seq.max
                | Some t ->
                    fwt.FromFrameworkName(t)
            if target = Some fw then
                Some TheExactMatch
            elif supported fw then
                Option.map SupportedMatch target
            else
                None
        if Seq.isEmpty xs then None else
            let r = Seq.maxBy score xs
            match score r with
            | None -> None
            | _ -> Some r

    let resolveAutoRefs fw (pkg: SafeNuGetPackage) =

        let resolveSystemRef fw (ref: string) =
            srt.Resolve fw (AssemblyName ref)

        let chooseRefs getName getTarget getSupported rs =
            rs
            |> Seq.groupBy getName
            |> Seq.choose (fun (_, rs) -> bestFit getTarget getSupported fw rs)
            |> Reify

        let choosePkgRefs fw (rs: seq<SafeNuGetPackageAssemblyReference>) =
            rs
            |> chooseRefs
                (fun r -> r.Name)
                (fun r -> r.TargetFramework)
                (fun r -> r.SupportedFrameworks)

        let chooseFwRefs fw (rs: seq<SafeNuGetFrameworkAssemblyReference>) =
            rs
            |> chooseRefs
                (fun r -> r.AssemblyName)
                (fun r -> None)
                (fun r -> r.SupportedFrameworks)

        let tryFindFrameworkRef fw (r: SafeNuGetFrameworkAssemblyReference) =
            resolveSystemRef fw r.AssemblyName

        Seq.concat [
            chooseFwRefs fw pkg.FrameworkAssemblies
            |> Seq.choose (tryFindFrameworkRef fw)
            choosePkgRefs fw pkg.AssemblyReferences
            |> Seq.choose (fun r -> findPath pkg r.Path)
        ]
        |> Reify

    let completePkgSet fw (ps: seq<SafeNuGetPackage>) =

        let getKey (pkg: SafeNuGetPackage) =
            (pkg.Id, string pkg.Version)

        let getDeps (pkg: SafeNuGetPackage) =
            fwt.ToFrameworkName(fw)
            |> pkg.GetCompatiblePackageDependencies
            |> Seq.choose pm.LocalRepository.FindByDependency

        let packages = Dictionary()
        let visited = HashSet()

        let addPackage (pkg: SafeNuGetPackage) =
            match packages.TryGetValue(pkg.Id) with
            | true, old when pkg.Compare(old) <= 0 -> ()
            | _ -> packages.[pkg.Id] <- pkg

        let rec visit (pkg: SafeNuGetPackage) =
            let key = getKey pkg
            if visited.Add(key) then
                addPackage pkg
                for d in getDeps pkg do
                    visit d

        Seq.iter visit ps
        packages

    static let latestKey = CacheKey()

    let installPkgSet (rs: seq<INuGetReference>) =
        let findLatestPackageById (pid: string) =
            pid
            |> cache.Lookup latestKey (fun () ->
                let pkgs =
                    pm.SourceRepository.FindPackagesById pid
                    |> Seq.toList
                match pkgs with
                | [] ->
                    log.Warn("Could not resolve package: {0}", pid)
                    None
                | pkgs ->
                    let pkg =
                        pkgs
                        |> Seq.maxBy (fun pkg -> pkg.Version.Version)
                    log.Verbose("Latest package: {0} --> {1}", pkg.Id, pkg.Version)
                    Some pkg)
        let findExact pid ver =
            pm.LocalRepository.FindExact(pid, ver,
                allowPreRelease = ver.SpecialVersion.IsSome,
                allowUnlisted = true)
        let install pid ver =
            pm.InstallExact(pid, ver)
            findExact pid ver
        let ensureInstalled pid ver =
            match findExact pid ver with
            | Some pkg -> Some pkg
            | None -> install pid ver
        seq {
            for r in rs do
                let pid = r.PackageId
                let ver =
                    match r.PackageVersion with
                    | None ->
                        match findLatestPackageById pid with
                        | Some pkg -> Some pkg.Version
                        | None -> None
                    | Some v -> Some (SafeNuGetSemanticVersion.Parse v)
                match ver with
                | Some ver ->
                    match ensureInstalled pid ver with
                    | None -> ()
                    | Some pkg -> yield pkg
                | None -> ()
        }
        |> Reify

    let applyRule fw (set: PackageSet) (r: INuGetReference) =
        match set.TryGetValue(r.PackageId) with
        | true, pkg ->
            match r.Paths with
            | None -> resolveAutoRefs fw pkg
            | Some paths ->
                seq {
                    for p in paths do
                        yield!
                            findPath pkg p
                            |> Option.toList
                }
        | _ -> Seq.empty

    let locateTool (fw: Framework) package (name: string) =
        let dir = pm.GetPackageDirectory(package)
        let hit =
            package.GetToolFiles()
            |> Seq.filter (fun f ->
                f.EffectivePath = name)
            |> bestFit
                (fun f -> f.TargetFramework)
                (fun f -> f.SupportedFrameworks)
                fw
        match hit with
        | None -> None
        | Some hit -> findPath package hit.Path

    member this.Resolve fw (rs: seq<INuGetReference>) =
        (fw, Set.ofSeq (Seq.map NuGetReference.Wrap rs))
        |> cache.Lookup ck (fun () ->
            let set =
                installPkgSet rs
                |> completePkgSet fw
            let isImplicitAuto (pkg: SafeNuGetPackage) =
                rs
                |> Seq.forall (fun r -> r.PackageId <> pkg.Id)
            let refs =
                seq {
                    for r in rs do
                        yield! applyRule fw set r
                    for KeyValue (_, pkg) in set do
                        if isImplicitAuto pkg then
                            yield! resolveAutoRefs fw pkg
                }
                |> buildAssemblySet
            {
                pack = set
                refs = refs
            })

    member this.FindTool r fw name =
        r.pack.Values
        |> Seq.tryPick (fun pkg ->
            locateTool fw pkg name)

    static member Current = current

[<Sealed>]
type References private (env: Parameters) =
    static let current = Parameter.Define(fun env -> References env)
    let log = Log.Create<References>(env)

    let srt = SystemResolver.Current.Find env
    let fwt = Frameworks.Current.Find env
    let rt = ReferenceBuilder.Current.Find env
    let ngrt = NuGetResolver.Current.Find env

    let mscorlib = rt.Assembly "mscorlib"
    let sysCore = rt.Assembly "System.Core"
    let sysNumerics = rt.Assembly "System.Numerics"
    let fsCore = rt.Assembly "FSharp.Core"
    let sys = rt.Assembly "System"
    let defs = [fsCore; mscorlib; sys]

    let baseSet fw =
        match fw with
        | Is fwt.Net20 | Is fwt.Net30 ->
            defs
        | Is fwt.Net35 | Is fwt.Net35CP ->
            sysCore :: defs
        | Is fwt.Net40 | Is fwt.Net40CP | Is fwt.Net45 ->
            sysNumerics :: sysCore :: defs
        | _ ->
            []

    member rs.GetNuGetReference r =
        match r with
        | NuGetRef r -> Some r
        | _ -> None

    member rs.FindTool r fw name =
        ngrt.FindTool r fw name
        |> Option.map (fun t -> t.Path)

    member rs.Resolve fw refs =
        let nSet =
            refs
            |> Seq.choose (function
                | NuGetRef r -> Some r
                | _ -> None)
            |> ngrt.Resolve fw
        Seq.append nSet.refs
            ([|
                for r in Seq.append refs (baseSet fw) do
                    match r with
                    | NuGetRef _ -> ()
                    | FileRef p ->
                        yield ResolvedRef p
                    | ProjectRef _ ->
                        ()
                    | SystemRef r ->
                        match srt.Resolve fw (AssemblyName r) with
                        | None -> ()
                        | Some r -> yield r
            |])
        |> buildAssemblySet
        |> nSet.WithReferences

    member rs.ResolveProjectReferences refs (rr: ResolvedReferences) =
        seq {
            for r in refs do
                match r with
                | ProjectRef p ->
                    for file in p.GeneratedAssemblyFiles do
                        yield ResolvedRef file
                | _ -> ()
        }
        |> Seq.append rr.refs
        |> buildAssemblySet
        |> rr.WithReferences

    member rs.ResolveReferences(fw: Framework)(refs: seq<Reference>) =
        rs.Resolve fw refs
        |> rs.ResolveProjectReferences refs

    static member Current = current
