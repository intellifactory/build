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
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Security

type AssemblyContext =
    | ReflectionOnlyContext
    | RegularContext

type AssemblyResolution =
    AssemblyContext -> AssemblyName -> option<Assembly>

let isCompatible (ref: AssemblyName) (def: AssemblyName) =
    ref.Name = def.Name
    && ref.Version = def.Version

let combine a b : AssemblyResolution =
    fun ctx name ->
        match a ctx name with
        | None -> b ctx name
        | r -> r

let searchDirs (dirs: seq<string>) : AssemblyResolution =
    fun ctx name ->
        seq {
            for dir in dirs do
                for ext in [".dll"; ".exe"] do
                    let p = Path.Combine(dir, name.Name + ext)
                    let f = FileInfo p
                    if f.Exists then
                        let n = AssemblyName.GetAssemblyName f.FullName
                        if isCompatible name n then
                            match ctx with
                            | ReflectionOnlyContext ->
                                yield Assembly.ReflectionOnlyLoadFrom(f.FullName)
                            | RegularContext ->
                                yield Assembly.LoadFrom(f.FullName)
        }
        |> Seq.tryFind (fun x -> true)

let searchAssemblies (all: seq<Assembly>) : AssemblyResolution =
    fun ctx name ->
        all
        |> Seq.tryFind (fun a ->
            match ctx with
            | ReflectionOnlyContext -> a.ReflectionOnly
            | _ -> true
            && isCompatible name (a.GetName()))

let searchDomain (dom: AppDomain) : AssemblyResolution =
    fun ctx name ->
        let a = dom.GetAssemblies()
        searchAssemblies a ctx name

/// An utility for resolving assemblies from non-standard contexts.
/// TODO: this probably belongs in Core.
[<Sealed>]
[<SecuritySafeCritical>]
type AssemblyResolver(resolve: AssemblyResolution) =

    let root = obj ()
    let cache = Dictionary()

    let resolve (ctx: AssemblyContext) (x: AssemblyName) =
        lock root <| fun () ->
            let key = (ctx, string x.Name, string x.Version)
            match cache.TryGetValue key with
            | true, y -> y
            | _ ->
                let y = resolve ctx x
                cache.[key] <- y
                y

    static let zero () =
        AssemblyResolver(fun _ _ -> None)

    static let get (x: AssemblyResolver) : AssemblyResolution =
        x.Resolve

    let resolve1 (x: obj) (a: ResolveEventArgs) =
        let name = AssemblyName(a.Name)
        match resolve RegularContext name with
        | None -> null
        | Some r -> r

    let resolve2 (x: obj) (a: ResolveEventArgs) =
        let name = AssemblyName(a.Name)
        match resolve ReflectionOnlyContext name with
        | None -> null
        | Some r -> r

    let handler1 = ResolveEventHandler(resolve1)
    let handler2 = ResolveEventHandler(resolve2)

    /// Installs the resolver into an `AppDomain`.
    member r.Install(?domain) =
        let domain = defaultArg domain AppDomain.CurrentDomain
        domain.add_AssemblyResolve(handler1)
        domain.add_ReflectionOnlyAssemblyResolve(handler2)

    /// Uninstalls the resolver from an `AppDomain`.
    member r.Remove(?domain) =
        let domain = defaultArg domain AppDomain.CurrentDomain
        domain.remove_AssemblyResolve(handler1)
        domain.remove_ReflectionOnlyAssemblyResolve(handler2)

    /// Wraps an action in `Install/Remove`.
    member r.WrapOptional(domain: option<AppDomain>)(action: unit -> 'T) =
        try
            r.Install(?domain = domain)
            action ()
        finally
            r.Remove(?domain = domain)

    member r.WrapDomain dom act =
        r.WrapOptional (Some dom) act

    member r.Wrap act =
        r.WrapOptional None act

    member private r.Resolve = resolve

    static member ( + ) (a, b) =
        AssemblyResolver.Fallback(a, b)

    /// Combines two resolvers with the second one acting as fallback.
    static member Fallback(a, b) =
        AssemblyResolver(combine (get a) (get b))

    /// Searches the given AppDomain.
    static member SearchDomain(?domain)=
        let domain = defaultArg domain AppDomain.CurrentDomain
        AssemblyResolver(combine (searchDomain domain) (searchDirs [domain.BaseDirectory]))

    /// Creates an assembly resolver based on the given search paths.
    static member SearchPaths(searchPaths) =
        AssemblyResolver(searchDirs searchPaths)

    /// The `Zero` resolver always refueses to resolve.
    static member Zero =
        zero ()
