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
// permissions and limitations under the License

namespace IntelliFactory.Build

open System
open System.IO
open System.Security
open IntelliFactory.Build
open IntelliFactory.Core
module MSBuild = IntelliFactory.Build.MSBuild

[<Sealed>]
type BuildTool(?env) =

    static let shouldClean = Parameter.Create false
    static let shouldPrepare = Parameter.Create false

    static let getDefaultEnv () =
        let logConfig = Logs.Default.Info().ToConsole()
        Parameters.Default()
        |> Cache.Init
        |> Logs.Config.Custom logConfig

    let env =
        match env with
        | None -> getDefaultEnv ()
        | Some env -> env

    let fsharp = FSharpTool.Current.Find env
    let websharper = WebSharperProjects.Current.Find env
    let ng = NuGetPackageTool.Current.Find env
    let log = Log.Create<BuildTool>(env)
    let rb = ReferenceBuilder.Current.Find env
    let rr = References.Current.Find env
    let fw = Frameworks.Current.Find env
    let sln = Solutions.Current.Find env

    member bt.Configure f : BuildTool = f bt

    interface IParametric with
        member t.Parameters = env

    interface IParametric<BuildTool> with
        member t.WithParameters ps = BuildTool(ps)

    member bt.WithCommandLineArgs(?args: seq<string>) =
        let args =
            match args with
            | None -> Environment.GetCommandLineArgs() :> seq<_>
            | Some args -> args
        let (|S|_|) (p: string) (x: string) =
            if x.StartsWith(p)
                then Some (x.Substring(p.Length))
                else None
        let mutable bt = bt
        for a in args do
            match a with
            | "--clean" -> bt <- shouldClean.Custom true bt
            | "--references"-> bt <- shouldPrepare.Custom true bt
            | S "-v:" v -> bt <- PackageVersion.Full.Custom (Version.Parse v) bt
            | _ -> ()
        bt

    member bt.Dispatch(sln: Solution) =
        if shouldPrepare.Find env then
            sln.PrepareReferences()
        elif shouldClean.Find env then
            sln.Clean()
        else
            sln.Build()

    member bt.ResolveReferences fw refs = rr.ResolveReferences fw refs
    member bt.NuGet = ng
    member bt.Framework = fw
    member bt.FSharp = fsharp

    member bt.Verbose() =
        bt
        |> Logs.Config.Custom (Logs.Default.Verbose().ToConsole())

    /// Short-hand to set `PackageId.Current` and `PackageVersion.Current`.
    member bt.PackageId(pid, ?ver) =
        let bt = PackageId.Current.Custom pid bt
        match ver with
        | None -> bt
        | Some ver ->
            let v = PackageVersion.Parse ver
            let r =
                match v.Suffix, BuildConfig.BuildNumber.Find bt with
                | None, Some n -> PackageVersion.Create(v.Major, v.Minor, "integration")
                | _ -> v
            PackageVersion.Current.Custom r bt

    member bt.WithFramework(fw) =
        BuildConfig.CurrentFramework.Custom fw bt

    member bt.Reference = rb
    member bt.Solution projects = sln.Solution projects
    member bt.WebSharper = websharper

    [<SecurityCritical>]
    member bt.MSBuild(name) = MSBuild.MSBuildProject(env, name)

