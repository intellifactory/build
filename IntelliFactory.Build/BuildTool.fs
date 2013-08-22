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

    static let getDefaultEnv () =
        let logConfig = Logs.Default.Info().ToConsole()
        Parameters.Default()
        |> Cache.Init
        |> Logs.Config.Custom logConfig

    let env =
        match env with
        | None -> getDefaultEnv ()
        | Some env -> env

    let args =
        BuildConfig.CommandLineArgs.Find env

    let (|S|_|) (p: string) (x: string) =
        if x.StartsWith(p)
            then Some (x.Substring(p.Length))
            else None

    let shouldClean () =
        args
        |> Seq.exists ((=) "--clean")

    let shouldPrepare () =
        args
        |> Seq.exists ((=) "--references")

    let env =
        let customVersion =
            args
            |> Seq.tryPick (function
                | S "-v:" v -> Some (Version.Parse v)
                | _ -> None)
        match customVersion with
        | None -> env
        | Some vn -> PackageVersion.Full.Custom vn env

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

    member bt.Dispatch(sln: Solution) =
        if shouldPrepare () then
            sln.PrepareReferences()
        elif shouldClean () then
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

