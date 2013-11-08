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
open IntelliFactory.Build
open IntelliFactory.Core

type WebSharperKind =
    | WebSharperExtension
    | WebSharperHtmlWebsite
    | WebSharperSiteletWebsite
    | WebSharperLibrary

module WebSharperConfig =

    let WebSharperVersion : Parameter<option<string>> =
        Parameter.Create None

    let WebSharperHome : Parameter<option<string>> =
        Parameter.Define (fun env ->
            match Environment.GetEnvironmentVariable("WebSharperHome") with
            | null | "" -> None
            | env -> Some env)

    let WebSharperHtmlDirectory : Parameter<string> =
        Parameter.Define (fun env ->
            Path.Combine(BuildConfig.BuildDir.Find env, "html"))

    let AppDomain =
        Parameter.Define (fun env ->
            let setup = AppDomainSetup()
            match WebSharperHome.Find env with
            | None -> setup.ApplicationBase <- AppDomain.CurrentDomain.BaseDirectory
            | Some wsHome -> setup.ApplicationBase <- wsHome
            AppDomain.CreateDomain("WebSharper", null, setup))

module WebSharperReferences =

    let Compute env =
        let rb = ReferenceBuilder.Current.Find env
        let wsHome = WebSharperConfig.WebSharperHome.Find env
        let paths =
            [
                "IntelliFactory.Core.dll"
                "IntelliFactory.Formlet.dll"
                "IntelliFactory.Html.dll"
                "IntelliFactory.JavaScript.dll"
                "IntelliFactory.Reactive.dll"
                "IntelliFactory.WebSharper.Collections.dll"
                "IntelliFactory.WebSharper.Control.dll"
                "IntelliFactory.WebSharper.Core.dll"
                "IntelliFactory.WebSharper.dll"
                "IntelliFactory.WebSharper.Dom.dll"
                "IntelliFactory.WebSharper.Ecma.dll"
                "IntelliFactory.WebSharper.Formlet.dll"
                "IntelliFactory.WebSharper.Html.dll"
                "IntelliFactory.WebSharper.Html5.dll"
                "IntelliFactory.WebSharper.InterfaceGenerator.dll"
                "IntelliFactory.WebSharper.JQuery.dll"
                "IntelliFactory.WebSharper.Sitelets.dll"
                "IntelliFactory.WebSharper.Testing.dll"
                "IntelliFactory.WebSharper.Web.dll"
            ]
        match wsHome with
        | None ->
            let paths =
                paths
                |> List.map (fun x -> "/tools/net45/" + x)
            match WebSharperConfig.WebSharperVersion.Find env with
            | None -> rb.NuGet("WebSharper").At(paths).Reference()
            | Some v -> rb.NuGet("WebSharper").Version(v).At(paths).Reference()
            |> Seq.singleton
        | Some wh ->
            paths
            |> List.map (fun p ->
                Path.Combine(wh, p)
                |> rb.File)
            |> Seq.ofList
        |> Seq.append (Seq.singleton (rb.Assembly "System.Web"))

type WebSharperProjectConfig =
    {
        Kind : WebSharperKind
    }

[<Sealed>]
type WebSharperUtility(env: IParametric, log: Log) =
    let wsHome = WebSharperConfig.WebSharperHome.Find env
    let dom = WebSharperConfig.AppDomain.Find env
    let rt = References.Current.Find env
    let fw = BuildConfig.CurrentFramework.Find env

    let wsToolPath (rr: ResolvedReferences) =
        match wsHome with
        | None ->
            let tool = rt.FindTool rr fw "WebSharper.exe"
            match tool with
            | None ->
                failwithf "Could not locate WebSharper.exe - \
                    consider setting WebSharperHome"
            | Some t -> t
        | Some x -> Path.Combine(x, "WebSharper.exe")

    member u.Domain = dom

    member u.Execute(exe: string, args: seq<string>) =
        let msg =
            seq {
                yield exe
                for a in args do
                    yield "    " + a
            }
            |> String.concat Environment.NewLine
        log.Verbose(msg)
        match dom.ExecuteAssembly(exe, Seq.toArray args) with
        | 0 -> ()
        | n -> failwithf "Non-zero exit code: %i" n

    member u.ExecuteWebSharper(rr, args) =
        let exe = wsToolPath rr
        u.Execute(exe, args)

    member u.GetWebSharperToolPath(rr) = wsToolPath rr
    member u.Home = wsHome

    member u.UnpackResources() =
        let refs = FSharpConfig.References.Find env
        let rr = References.Current.Find(env).ResolveReferences fw refs
        let baseDir = FSharpConfig.BaseDir.Find env
        let binDir = Path.Combine(baseDir, "bin")
        let refs = ResizeArray()
        for r in rr.References do
            if not r.IsFrameworkReference then
                let p = r.Path
                let fc = FileSystem.Content.ReadBinaryFile p
                fc.WriteFile(Path.Combine(binDir, Path.GetFileName p))
                refs.Add p
        let fpw = FSharpProjectWriter(env)
        fpw.Write rr
        u.ExecuteWebSharper(rr,
            [
                yield "-unpack"
                yield baseDir
                yield! refs
            ])

[<Sealed>]
type WebSharperProject(cfg: WebSharperProjectConfig, fs: FSharpProject) =
    let log = Log.Create<WebSharperProject>(fs)
    let aig = AssemblyInfoGenerator.Current.Find fs
    let rt = References.Current.Find fs
    let fw = BuildConfig.CurrentFramework.Find fs
    let name = BuildConfig.ProjectName.Find fs
    let snk = BuildConfig.KeyFile.Find fs
    let baseDir = FSharpConfig.BaseDir.Find fs
    let util = WebSharperUtility(fs, log)
    let dom = WebSharperConfig.AppDomain.Find fs
    let wsHome = WebSharperConfig.WebSharperHome.Find fs
    let fsXmlFile = FSharpXml.getFSharpXmlFile fs

    let sourceFiles =
        FSharpConfig.Sources.Find fs
        |> Seq.map (fun f -> Path.Combine(baseDir, f))
        |> Seq.toList

    let project = fs :> IProject

    let aid =
        let d = AssemblyInfoData.Current.Find fs
        let t =
            d.Title
            |> OrElse (Some name)
        { d with Title = t }

    let docPath = FSharpConfig.DocPath.Find fs
    let outputPath = FSharpConfig.OutputPath.Find fs
    let outputPath1 = Path.ChangeExtension(outputPath, ".Generator.dll")
    let outputPath2 = Path.Combine(Path.GetDirectoryName outputPath, "raw", Path.GetFileName outputPath)
    let ainfoPath = Path.ChangeExtension(outputPath, ".annotations.fs")
    let argsPath = Path.ChangeExtension(outputPath, ".args.txt")
    let ver = PackageVersion.Current.Find fs
    let rootDir = BuildConfig.RootDir.Find fs

    let inputFiles (rr: ResolvedReferences) =
        FileInfo argsPath
        :: FileInfo ainfoPath
        :: [for s in sourceFiles -> FileInfo(Path.Combine(baseDir, s))]
        |> List.append [for r in rr.Paths -> FileInfo(Path.Combine(rootDir, r))]
        |> List.append [
                match FSharpConfig.SourcesProject.Find fs with
                | Some file -> yield file
                | None -> ()
            ]

    let outputFiles =
        FileInfo outputPath
        :: [for d in Option.toList docPath -> FileInfo d]

    let buildFS rr env =
        let t = FSharpCompilerTask(env, log, rr)
        let args = String.concat Environment.NewLine t.Arguments
        FileSystem.TextContent(args).WriteFile(argsPath)
        t.Build()

    let getFSParams () =
        let env =
            Parameters.Get fs
            |> FSharpConfig.Sources.Update(Seq.append [ainfoPath])
        match cfg.Kind with
        | WebSharperLibrary | WebSharperHtmlWebsite | WebSharperSiteletWebsite ->
            env
            |> FSharpConfig.OutputPath.Custom outputPath2
            |> FSharpConfig.DocPath.Custom docPath
        | WebSharperExtension ->
            env
            |> FSharpConfig.OutputPath.Custom outputPath1

    let resolveRefs () =
        References.Current.Find(fs).ResolveReferences fw project.References

    let prepareRefs () =
        let ps = getFSParams ()
        let rr = resolveRefs ()
        FSharpXml.writeReferenceFile ps rr

    let build1 rr =
        aig.Generate(AssemblyInfoSyntax.FSharp, aid, ainfoPath)
        getFSParams ()
        |> buildFS rr

    let getWsHome rr =
        Path.GetDirectoryName (util.GetWebSharperToolPath rr)

    let build2 (rr: ResolvedReferences) =
        match cfg.Kind with
        | WebSharperLibrary
        | WebSharperHtmlWebsite
        | WebSharperSiteletWebsite-> ()
        | WebSharperExtension ->
            util.ExecuteWebSharper(rr,
                [
                    yield "ig"
                    yield outputPath1
                    yield "-n:" + name
                    yield "-o:" + outputPath2
                    yield "-v:" + string (Version (ver.Major, ver.Minor, 0, 0))
                    match docPath with
                    | None -> ()
                    | Some doc -> yield "-doc:" + doc
                    match snk with
                    | None -> ()
                    | Some snk -> yield "-snk:" + snk
                    for r in rr.Paths do
                        yield "-r:" + r
                    for res in FSharpConfig.EmbeddedResources.Find fs do
                        yield "-embed:" + Path.GetFullPath(Path.Combine(baseDir, res))
                ])

    let build3 (rr: ResolvedReferences) =
        FSharpXml.writeReferenceFile fs rr
        util.ExecuteWebSharper(rr,
            [
                match snk with
                | None -> ()
                | Some snk ->
                    yield "-snk"
                    yield snk
                for p in rr.Paths do
                    yield "-r"
                    yield p
                yield outputPath2
                yield outputPath
            ])

    let build4 (rr: ResolvedReferences) =
        match cfg.Kind with
        | WebSharperHtmlWebsite ->
            let wsHome = getWsHome rr
            util.ExecuteWebSharper(rr,
                [
                    yield "sitelets"
                    yield "-mode"
                    yield "Release"
                    yield "-project"
                    yield FSharpConfig.BaseDir.Find fs
                    for r in rr.Paths do
                        yield "-ref"
                        yield r
                    yield "-out"
                    yield WebSharperConfig.WebSharperHtmlDirectory.Find fs
                    yield "-site"
                    yield FSharpConfig.OutputPath.Find fs
                ])
        | _ -> ()

    let build5 () =
        match cfg.Kind with
        | WebSharperSiteletWebsite ->
            util.UnpackResources()
        | _ -> ()

    let rm x =
        if IsFile x then
            File.Delete x

    let clean () =
        printfn "Removing %s" fsXmlFile
        rm fsXmlFile
        rm argsPath
        Option.iter rm docPath
        rm ainfoPath
        rm outputPath1
        rm outputPath2
        rm outputPath
        match cfg.Kind with
        | WebSharperHtmlWebsite ->
            let html = WebSharperConfig.WebSharperHtmlDirectory.Find fs
            Directory.Delete(html, ``recursive`` = true)
        | _ -> ()

    let build () =
        let rr = resolveRefs ()
        let rd =
            RebuildProblem.Create(fs)
                .AddInputPaths(inputFiles rr)
                .AddOutputPaths(outputFiles)
                .Decide()
        if rd.IsStale then
            rm outputPath1
            rm outputPath2
            FSharpProjectWriter(fs).Write(rr)
            build1 rr
            build2 rr
            build3 rr
            build4 rr
            build5 ()
            rd.Touch()
        else
            log.Info("Skipping {0}", project.Name)

    interface INuGetExportingProject with
        member p.NuGetFiles =
            (fs :> INuGetExportingProject).NuGetFiles

    interface IReferenceProject with
        member p.GeneratedAssemblyFiles =
            (fs :> IReferenceProject).GeneratedAssemblyFiles

    interface IProject with
        member p.Build() = build ()
        member p.Clean() = clean ()
        member p.Framework = project.Framework
        member p.PrepareReferences() = prepareRefs ()
        member p.Name = project.Name
        member p.References = project.References

        member p.Parametric =
            {
                new IParametric<IProject> with
                    member fp.WithParameters env = WebSharperProject(cfg, Parameters.Set env fs) :> _
                interface IParametric with
                    member fp.Parameters = Parameters.Get fs
            }

    interface IParametric with
        member x.Parameters = Parameters.Get fs

    interface IParametric<WebSharperProject> with
        member x.WithParameters env = WebSharperProject(cfg, Parameters.Set env fs)

[<Sealed>]
type WebSharperHostWebsite(env: IParametric) =
    let log = Log.Create<WebSharperHostWebsite>(env)
    let util = WebSharperUtility(env, log)
    let name = BuildConfig.ProjectName.Find env
    let fw = BuildConfig.CurrentFramework.Find env
    let refs = FSharpConfig.References.Find env
    let baseDir = FSharpConfig.BaseDir.Find env
    let binDir = Path.Combine(baseDir, "bin")

    interface IProject with

        member h.PrepareReferences() =
            let rr = References.Current.Find(env).ResolveReferences fw refs
            FSharpXml.writeReferenceFile env rr

        member h.Build() =
            util.UnpackResources()

        member h.Clean() =
            Directory.Delete(binDir, true)

        member h.Framework = fw
        member h.Name = name
        member h.References = refs

        member h.Parametric =
            {
                new IParametric<IProject> with
                    member fp.WithParameters env = WebSharperHostWebsite(env) :> _
                interface IParametric with
                    member fp.Parameters = env.Parameters
            }

    interface IParametric with
        member hw.Parameters = env.Parameters

    interface IParametric<WebSharperHostWebsite> with
        member hw.WithParameters env = WebSharperHostWebsite(env)

[<Sealed>]
type WebSharperProjects(env) =
    static let current = Parameter.Define(fun env -> WebSharperProjects env)
    let fps = FSharpTool.Current.Find env

    let make name cfg =
        let rs = WebSharperReferences.Compute(env)
        let fp = fps.Library(name).References(fun _ -> rs)
        WebSharperProject(cfg, fp)

    let configWithWebReference name =
        let rs = WebSharperReferences.Compute(env)
        fps.Library(name).References(fun r ->
            [
                yield r.Assembly("System.Web")
                yield! rs
            ])
        |> BuildConfig.ProjectName.Custom name

    member ps.Extension name =
        make name { Kind = WebSharperExtension }

    member ps.HtmlWebsite name =
        make name { Kind = WebSharperHtmlWebsite }

    member ps.SiteletWebsite name =
        let p = make name { Kind = WebSharperSiteletWebsite }
        let pD = FSharpConfig.BaseDir.Find p
        p
        |> BuildConfig.OutputDir.Custom (Path.Combine(pD, "bin"))

    member ps.HostWebsite name =
        WebSharperHostWebsite(configWithWebReference name)

    member ps.Library name =
        make name { Kind = WebSharperLibrary }

    static member Current = current
