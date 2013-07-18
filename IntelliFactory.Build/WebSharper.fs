namespace IntelliFactory.Build

open System
open System.IO

#if INTERACTIVE
open IntelliFactory.Build
#endif

type WebSharperKind =
    | WebSharperExtension
    | WebSharperLibrary

module WebSharperConfig =

    let WebSharperHome : Parameter<option<string>> =
        Parameter.Define (fun env ->
            match Environment.GetEnvironmentVariable("WebSharperHome") with
            | null | "" -> None
            | env -> Some env)

module WebSharperReferences =

    let Compute env =
        let rt = References.Current.Find env
        let rb = ReferenceBuilder.Current.Find env
        let ws = rb.NuGet("WebSharper")
        let rs =
            [
                "IntelliFactory.Formlet"
                "IntelliFactory.Html"
                "IntelliFactory.JavaScript"
                "IntelliFactory.Reactive"
                "IntelliFactory.WebSharper"
                "IntelliFactory.WebSharper.Collections"
                "IntelliFactory.WebSharper.Control"
                "IntelliFactory.WebSharper.Core"
                "IntelliFactory.WebSharper.Dom"
                "IntelliFactory.WebSharper.Ecma"
                "IntelliFactory.WebSharper.Formlet"
                "IntelliFactory.WebSharper.Html"
                "IntelliFactory.WebSharper.Html5"
                "IntelliFactory.WebSharper.InterfaceGenerator"
                "IntelliFactory.WebSharper.JQuery"
                "IntelliFactory.WebSharper.Sitelets"
                "IntelliFactory.WebSharper.Testing"
                "IntelliFactory.WebSharper.Web"
                "IntelliFactory.Xml"
            ]
        let makeRef (n: string) =
            let p = String.Format("/tools/net40/{0}.dll", n)
            ws.At(p).Reference()
        [
            for r in rs ->
                makeRef r
        ]

type WebSharperProjectConfig =
    {
        Kind : WebSharperKind
    }

[<Sealed>]
type WebSharperProject(cfg: WebSharperProjectConfig, fs: FSharpProject) =
    let log = Log.Create<WebSharperProject>(fs)
    let aig = AssemblyInfoGenerator.Current.Find fs
    let rt = References.Current.Find fs
    let fw = BuildConfig.CurrentFramework.Find fs
    let wsHome = WebSharperConfig.WebSharperHome.Find fs
    let name = BuildConfig.ProjectName.Find fs
    let dom = BuildConfig.AppDomain.Find fs
    let snk = BuildConfig.KeyFile.Find fs
    let sourceFiles = FSharpConfig.Sources.Find fs |> Seq.toList
    let project = fs :> IProject

    let aid =
        let d = AssemblyInfoData.Current.Find fs
        let t =
            d.Title
            |> OrElse (Some name)
        { d with Title = t }

    let wsToolPath rr =
        match wsHome with
        | None ->
            let tool = rt.FindTool rr fw "WebSharper.exe"
            match tool with
            | None ->
                failwithf "Could not locate WebSharper.exe - \
                    consider setting WebSharperHome"
            | Some t -> t
        | Some x -> Path.Combine(x, "WebSharper.exe")

    let docPath = FSharpConfig.DocPath.Find fs
    let outputPath = FSharpConfig.OutputPath.Find fs
    let outputPath1 = Path.ChangeExtension(outputPath, ".Generator.exe")
    let outputPath2 = Path.Combine(Path.GetDirectoryName outputPath, "raw", Path.GetFileName outputPath)
    let ainfoPath = Path.ChangeExtension(outputPath, ".annotations.fs")
    let argsPath = Path.ChangeExtension(outputPath, ".args.txt")
    let ver = PackageVersion.Current.Find fs

    let exec (exe: string) (args: seq<string>) =
        let msg =
            seq {
                yield "ExecuteAssembly: " + exe
                for a in args do
                    yield "    " + a
            }
            |> String.concat Environment.NewLine
        log.Verbose(msg)
        match dom.ExecuteAssembly(exe, Seq.toArray args) with
        | 0 -> ()
        | n -> failwithf "Non-zero exit code: %i" n

    let execWS rr args =
        exec (wsToolPath rr) args

    let inputFiles (rr: ResolvedReferences) =
        ainfoPath
        :: argsPath
        :: sourceFiles
        |> List.append (Seq.toList rr.Paths)

    let buildFiles =
        [
            ainfoPath
            argsPath
        ]

    let outputFiles =
        outputPath
        :: Option.toList docPath

    let lastWrite files =
        files
        |> Seq.filter IsFile
        |> Seq.map File.GetLastWriteTimeUtc
        |> Seq.max

    let requiresBuild rr =
        Seq.append buildFiles outputFiles
        |> Seq.exists (IsFile >> not)
        || lastWrite outputFiles < lastWrite (inputFiles rr |> Seq.filter IsFile)

    let buildFS rr env =
        let t = FSharpCompilerTask(env, log, rr)
        let args = String.concat Environment.NewLine t.Arguments
        FileSystem.TextContent(args).WriteFile(argsPath)
        t.Build()

    let build1 rr =
        aig.Generate(AssemblyInfoSyntax.FSharp, aid, ainfoPath)
        let env =
            Parameters.Get fs
            |> FSharpConfig.Sources.Update(Seq.append [ainfoPath])
        match cfg.Kind with
        | WebSharperLibrary ->
            env
            |> FSharpConfig.OutputPath.Custom outputPath2
            |> FSharpConfig.DocPath.Custom docPath
            |> buildFS rr
        | WebSharperExtension ->
            env
            |> FSharpConfig.OutputPath.Custom outputPath1
            |> FSharpConfig.Kind.Custom FSharpWindowsExecutable
            |> buildFS rr

    let build2 (rr: ResolvedReferences) =
        match cfg.Kind with
        | WebSharperLibrary -> ()
        | WebSharperExtension ->
            exec outputPath1 [
                yield "-n:" + name
                yield "-o:" + outputPath2
                yield "-v:" + string (Version (ver.Major, ver.Minor, 0, 0))
                match snk with
                | None -> ()
                | Some snk -> yield "-snk:" + snk
                for r in rr.Paths do
                    yield "-r:" + r
            ]

    let build3 rr =
        execWS rr [
            for p in rr.Paths do
                yield "-r"
                yield p
            yield outputPath2
            yield outputPath
        ]

    let rm x =
        if IsFile x then
            File.Delete x

    let clean () =
        rm outputPath1
        rm outputPath2
        rm outputPath

    let build rr =
        if requiresBuild rr then
            clean ()
            build1 rr
            build2 rr
            build3 rr
        else
            log.Info("Skipping {0}", project.Name)

    interface INuGetExportingProject with
        member p.LibraryFiles = (fs :> INuGetExportingProject).LibraryFiles

    interface IProject with
        member p.Build(rr) = build rr
        member p.Clean() = clean ()
        member p.Framework = project.Framework
        member p.GeneratedAssemblyFiles = project.GeneratedAssemblyFiles
        member p.Name = project.Name
        member p.References = project.References

    interface IFSharpProjectContainer<WebSharperProject> with
        member p.FSharpProject = fs
        member p.WithFSharpProject fs = WebSharperProject(cfg, fs)

    interface IParametric with
        member x.Find p = p.Find fs
        member x.Parameters = Parameters.Get fs

    interface IParametric<WebSharperProject> with
        member x.Custom p v = WebSharperProject(cfg, p.Custom v fs)

[<Sealed>]
type WebSharperProjects(env) =
    static let current = Parameter.Define(fun env -> WebSharperProjects env)
    let fps = FSharpProjects.Current.Find env

    let make name cfg =
        let rs = WebSharperReferences.Compute(env)
        let fp = fps.Library(name).References(fun _ -> rs)
        WebSharperProject(cfg, fp)

    member ps.Extension name =
        make name { Kind = WebSharperExtension }

    member ps.Library name =
        make name { Kind = WebSharperLibrary }

    static member Current = current
