namespace IntelliFactory.Build

open System

#if INTERACTIVE
open IntelliFactory.Build
#endif

[<Sealed>]
type BuildTool(?env) =

    static let shouldClean = Parameter.Create false

    static let getDefaultEnv () =
        let logConfig = LogConfig().Info().ToConsole()
        Parameters.Default
        |> Cache.Init
        |> LogConfig.Current.Custom logConfig

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
        member t.Find p = p.Find env
        member t.Parameters = env

    interface IParametric<BuildTool> with
        member t.Custom p v = BuildTool(p.Custom v env)

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
            | S "-v:" v -> bt <- PackageVersion.Full.Custom (Version.Parse v) bt
            | _ -> ()
        bt

    member bt.Dispatch(sln: Solution) =
        if shouldClean.Find env then
            sln.Clean()
        else
            sln.Build()

    member bt.ResolveReferences fw refs = rr.Resolve fw refs
    member bt.NuGet = ng
    member bt.Framework = fw
    member bt.FSharp = fsharp

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
