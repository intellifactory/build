﻿namespace IntelliFactory.Build

open System

#if INTERACTIVE
open IntelliFactory.Build
#endif

[<Sealed>]
type BuildTool(?env) =
    static let defaultEnv =
        let lc = LogConfig().Info().ToConsole()
        Parameters.Default
        |> Log.Configure lc
    let env = defaultArg env defaultEnv
    let fsharp = FSharpProjects.Current.Find env
    let sln = Solutions.Current.Find env
    let ng = NuGetPackageTool.Current.Find env
    let log = Log.Create<BuildTool>(env)
    let rb = ReferenceBuilder.Current.Find env
    let rr = References.Current.Find env
    let fw = Frameworks.Current.Find env

    member bt.Configure(f: BuildTool -> BuildTool) = f bt

    member bt.With(p: Parameter<'T>, v: 'T) =
        BuildTool(p.Custom v env)

    member bt.Parameteres = env

    member bt.Dispatch(args: seq<string>) : Solution -> unit =
        fun sln ->
            if Seq.exists ((=) "--clean") args then
                sln.Clean()
            else
                sln.Build()

    member bt.Dispatch(sln: Solution) =
        bt.Dispatch (Environment.GetCommandLineArgs()) sln

    member bt.ResolveReferences fw refs = rr.Resolve fw refs
    member bt.NuGet = ng
    member bt.Framework = fw
    member bt.FSharp = fsharp

    /// Short-hand to set `PackageId.Current` and `PackageVersion.Current`.
    member bt.PackageId(pid, ?ver) =
        let bt = bt.With(PackageId.Current, pid)
        match ver with
        | None -> bt
        | Some ver ->
            let v = PackageVersion.Parse ver
            let r =
                match v.Suffix, BuildConfig.BuildNumber.Find bt.Parameteres with
                | None, Some n -> PackageVersion.Create(v.Major, v.Minor, "integration")
                | _ -> v
            bt.With(PackageVersion.Current, r)

    member bt.WithFramework(fw) =
        bt.With(BuildConfig.CurrentFramework, fw)

    member bt.Reference = rb
    member bt.Solution projects = sln.Solution projects

