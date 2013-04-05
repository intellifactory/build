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
// permissions and limitations under the License.

module IntelliFactory.Build.NuGetUtils

open System
open System.IO
open System.Runtime.Versioning
open System.Collections.Generic
open NuGet
module F = IntelliFactory.Build.FileSystem

type LocalRepository =
    {
        Directory : string
        Resolver : IPackagePathResolver
        State : LocalPackageRepository
    }

    static member Create(path: string) =
        {
            Directory = path
            Resolver = DefaultPackagePathResolver(path)
            State = LocalPackageRepository(path)
        }

    member this.Path = this.Directory
    member this.PathResolver = this.Resolver
    member this.Repository = this.State

let ( +/ ) a b =
    Path.Combine(a, b)

let MostRecent (pkgs: seq<IPackage>) : seq<IPackage> =
    pkgs
    |> Seq.groupBy (fun pkg -> pkg.Id)
    |> Seq.map (fun (id, variants) ->
        variants
        |> Seq.maxBy (fun pkg -> pkg.Version))

let TopologicalSort<'K,'T when 'K : equality>
        (getKey: 'T -> 'K)
        (getPreceding: 'T -> seq<'T>)
        (roots: seq<'T>) : seq<'T> =
    let visited = HashSet()
    let trace = Queue()
    let rec visit (node: 'T) =
        let key = getKey node
        if visited.Add(key) then
            Seq.iter visit (getPreceding node)
            trace.Enqueue(node)
    Seq.iter visit roots
    trace.ToArray() :> seq<_>

let CompleteAndSortPackages
        (lr: LocalRepository)
        (framework: FrameworkName)
        (pkgs: seq<IPackage>) : seq<IPackage> =
    let getKey (pkg: IPackage) = pkg.Id
    let getPreceding (pkg: IPackage) =
        pkg.GetCompatiblePackageDependencies(framework)
        |> Seq.choose (fun dep ->
            let pkg = lr.Repository.FindPackage(dep.Id, dep.VersionSpec, true, true)
            match pkg with
            | null -> None
            | _ -> Some pkg)
        |> MostRecent
    TopologicalSort getKey getPreceding pkgs

type AssemblyReference =
    {
        AssemblyName : string
        AssemblyPath : option<string>
    }

let IsSupported (fw: FrameworkName) (frameworks: seq<FrameworkName>) =
    VersionUtility.IsCompatible(fw, frameworks)

let MostRecentReferences (refs: seq<IPackageAssemblyReference>) =
    refs
    |> Seq.groupBy (fun ref -> ref.Name)
    |> Seq.map (fun (name, variants) ->
        variants
        |> Seq.maxBy (fun ref -> ref.TargetFramework.Version))

let ComputeReferences
        (fw: FrameworkName)
        (isRequired: IPackage -> bool)
        (lr: LocalRepository) : seq<AssemblyReference> =
    let assemblyRefs = HashSet()
    let pkgRefs = Queue()
    let lpr = lr.Repository
    let pr = lr.PathResolver
    lpr.GetPackages()
    |> Seq.filter isRequired
    |> MostRecent
    |> CompleteAndSortPackages lr fw
    |> Seq.iter (fun pkg ->
        let dir = pr.GetPackageDirectory(pkg)
        pkg.FrameworkAssemblies
        |> Seq.filter (fun r -> IsSupported fw r.SupportedFrameworks)
        |> Seq.iter (fun r -> assemblyRefs.Add(r.AssemblyName) |> ignore)
        pkg.AssemblyReferences
        |> Seq.filter (fun r -> IsSupported fw r.SupportedFrameworks)
        |> MostRecentReferences
        |> Seq.map (fun r ->
            {
                AssemblyName = r.Name
                AssemblyPath = Some (lr.Path +/ dir +/ r.Path)
            })
        |> Seq.iter pkgRefs.Enqueue)
    Seq.ofArray [|
        for a in assemblyRefs do
            yield {
                AssemblyName = a
                AssemblyPath = None
            }
        yield! pkgRefs
    |]

let FindPackage (repository: LocalRepository) (id: string) =
    repository.Repository.GetPackages()
    |> MostRecent
    |> Seq.tryFind (fun pkg -> pkg.Id = id)

type Package =
    {
        mutable Content : F.Content
        mutable Name : string
        mutable Version : string
    }

    static member FromFile(path: string) : Package =
        let pkg = NuGet.ZipPackage(path)
        {
            Content = F.Content.ReadBinaryFile path
            Name = pkg.Id
            Version = string pkg.Version
        }