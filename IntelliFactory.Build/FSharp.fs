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

#if INTERACTIVE
open IntelliFactory.Build
#endif

open System
open System.IO
open System.Reflection
open System.Xml
open System.Xml.Linq
module XG = IntelliFactory.Build.XmlGenerator
module FS = IntelliFactory.Build.FileSystem

type FSharpKind =
    | FSharpConsoleExecutable
    | FSharpLibrary
    | FSharpWindowsExecutable

module FSharpConstants =
    let MSBuild = "http://schemas.microsoft.com/developer/msbuild/2003"

module FSharpConfig =

    let FSharpHome =
        Parameter.Define(fun env ->
            match Environment.GetEnvironmentVariable("FSharpHome") with
            | null | "" ->
                let pf = GetProgramFiles ()
                Path.Combine(pf, "Microsoft SDKs", "F#", "3.0", "Framework", "v4.0")
            | h -> h)

    let BaseDir =
        Parameter.Define(fun env ->
            let name = BuildConfig.ProjectName.Find env
            let root = BuildConfig.RootDir.Find env
            Path.Combine(root, name))

    let Kind = Parameter.Create FSharpLibrary

    let OutputPath =
        Parameter.Define(fun env ->
            let outDir = BuildConfig.OutputDir.Find env
            let kind = Kind.Find env
            let ext =
                match kind with
                | FSharpConsoleExecutable | FSharpWindowsExecutable -> ".exe"
                | FSharpLibrary -> ".dll"
            let name = BuildConfig.ProjectName.Find env
            Path.Combine(outDir, name + ext))

    let DocPath =
        Parameter.Define(fun env ->
            let out = OutputPath.Find env
            Some (Path.ChangeExtension(out, ".xml")))

    let ReferenceProjectPath =
        Parameter.Define(fun env ->
            let outDir = BuildConfig.OutputDir.Find env
            let name = BuildConfig.ProjectName.Find env
            Some (Path.Combine(outDir, name + ".proj")))

    let OtherFlags : Parameter<seq<string>> =
        Parameter.Create Seq.empty

    let References : Parameter<seq<Reference>> =
        Parameter.Create Seq.empty

    let Sources : Parameter<seq<string>> =
        Parameter.Create Seq.empty

    let EmbeddedResources : Parameter<seq<string>> =
        Parameter.Create Seq.empty

[<Sealed>]
type FSharpProjectParser(env: Parameters) =

    member p.Parse(msbFile: string, baseDir: string) =
        let doc = XDocument.Load(msbFile)
        seq {
            for el in doc.Descendants(XName.Get("Compile", FSharpConstants.MSBuild)) do
                match el.Attribute(XName.Get("Include")) with
                | null -> ()
                | a ->
                    yield! Directory.GetFiles(Path.GetFullPath baseDir, a.Value)
        }
        |> Reify

[<Sealed>]
type FSharpProjectWriter(x: IParametric) =
    let env = x.Parameters
    let log = Log.Create<FSharpProjectWriter>(env)
    let path = FSharpConfig.ReferenceProjectPath.Find env
    let rootDir = BuildConfig.RootDir.Find env

    member pw.Write(refs: ResolvedReferences) =
        match path with
        | None -> ()
        | Some path ->
            let e n = XG.Element.Create(n, FSharpConstants.MSBuild)
            let xml =
                e "Project" - [
                    e "ItemGroup" - [
                        for r in refs.Paths do
                            let n = AssemblyName.GetAssemblyName(r)
                            yield e "Reference" + ["Include", string n.Name] - [
                                e "HintPath" -- Path.GetFullPath(Path.Combine(rootDir, r))
                                e "Private" -- "False"
                            ]
                    ]
                ]
                |> XG.Write
            log.Verbose("Generating {0}", path)
            FS.TextContent(xml).WriteFile(path)

    member pw.Clean() =
        match path with
        | None -> ()
        | Some path ->
            if IsFile path then
                File.Delete path

[<Sealed>]
type FSharpCompilerTask(env: Parameters, log: Log, rr: ResolvedReferences) =
    let docPath = FSharpConfig.DocPath.Find env
    let kind = FSharpConfig.Kind.Find env
    let outPath = FSharpConfig.OutputPath.Find env
    let baseDir = FSharpConfig.BaseDir.Find env
    let dom = BuildConfig.AppDomain.Find env
    let sources =
        FSharpConfig.Sources.Find env
        |> Seq.map (fun s -> Path.Combine(baseDir, s))

    let kf = BuildConfig.KeyFile.Find env
    let otherFlags = FSharpConfig.OtherFlags.Find env
    let fsharpHome = FSharpConfig.FSharpHome.Find env
    let emb = FSharpConfig.EmbeddedResources.Find env

    let getProjTypeArg pt =
        match pt with
        | FSharpConsoleExecutable -> "--target:exe"
        | FSharpLibrary -> "--target:library"
        | FSharpWindowsExecutable -> "--target:winexe"

    let args =
        [|
            yield getProjTypeArg kind
            yield "--noframework"
            yield "--out:" + outPath
            match docPath with
            | None -> ()
            | Some docPath ->
                yield "--doc:" + docPath
            for r in rr.Paths do
                yield "-r"
                yield r
            for e in emb do
                let p = Path.Combine(baseDir, e)
                yield "--resource:" + p
            match kf with
            | None -> ()
            | Some kf -> yield "--keyfile:" + kf
            yield! otherFlags
            yield! sources
        |]

    let fsc = Path.Combine(fsharpHome, "fsc.exe")

    member p.Build() =
        PrepareDir outPath
        let outputFiles =
            [
                yield! Option.toList docPath
                yield outPath
            ]
        let msg =
            seq {
                yield "ExecuteAssembly: " + fsc
                for a in args do
                    yield "    " + a
            }
            |> String.concat Environment.NewLine
        log.Verbose(msg)
        PrepareDir outPath
        let r = dom.ExecuteAssembly(fsc, args)
        if r <> 0 then
            failwithf "Non-zero exit code: %i" r

    member p.Arguments = Seq.ofArray args
    member p.ToolPath = fsc

[<Sealed>]
type FSharpProjectBuilder(env: Parameters, log: Log) =
    let aig = AssemblyInfoGenerator.Current.Find env
    let name = BuildConfig.ProjectName.Find env
    let outDir = BuildConfig.OutputDir.Find env
    let baseDir = FSharpConfig.BaseDir.Find env
    let sources = FSharpConfig.Sources.Find env
    let pW = FSharpProjectWriter(env)
    let docPath = FSharpConfig.DocPath.Find env
    let outPath = FSharpConfig.OutputPath.Find env
    let kf = BuildConfig.KeyFile.Find env
    let otherFlags = FSharpConfig.OtherFlags.Find env
    let kind = FSharpConfig.Kind.Find env
    let fsharpHome = FSharpConfig.FSharpHome.Find env
    let argsPath = Path.Combine(outDir, name + ".args.txt")
    let ainfoPath = Path.Combine(outDir, name + ".annotations.fs")
    let fw = BuildConfig.CurrentFramework.Find env
    let refs = FSharpConfig.References.Find env
    let aid =
        let d = AssemblyInfoData.Current.Find env
        let t =
            d.Title
            |> OrElse (Some name)
        { d with Title = t }

    let inputFiles (rr: ResolvedReferences) =
        argsPath
        :: ainfoPath
        :: Seq.toList sources
        |> List.append (Seq.toList rr.Paths)

    let buildFiles =
        [
            ainfoPath
            argsPath
        ]

    let outputFiles =
        outPath :: Option.toList docPath

    let lastWrite files =
        files
        |> Seq.filter IsFile
        |> Seq.map File.GetLastWriteTimeUtc
        |> Seq.max

    let requiresBuild rr =
        Seq.append buildFiles outputFiles
        |> Seq.exists (IsFile >> not)
        || lastWrite outputFiles < lastWrite (inputFiles rr |> Seq.filter IsFile)

    member p.Build(rr: ResolvedReferences) =
        aig.Generate(AssemblyInfoSyntax.FSharp, aid, ainfoPath)
        pW.Write rr
        let sources = Path.GetFullPath ainfoPath :: Seq.toList sources
        let task =
            let env =
                env
                |> FSharpConfig.Sources.Custom sources
            FSharpCompilerTask(env, log, rr)
        do
            let t =
                task.Arguments
                |> Seq.append [task.ToolPath]
                |> String.concat Environment.NewLine
            FS.TextContent(t).WriteFile(argsPath)
        if requiresBuild rr then
            task.Build()
        else
            log.Info("Skipping {0}", name)

    member p.Clean() =
        let rm p =
            if IsFile p then
                File.Delete(p)
        [
            yield outPath
            yield! Option.toList docPath
            yield ainfoPath
            yield argsPath
        ]
        |> List.iter rm
        pW.Clean()

    member p.Framework = fw
    member p.GeneratedAssemblyFiles = Seq.singleton outPath

    member p.LibraryFiles =
        match kind with
        | FSharpLibrary -> Seq.singleton outPath
        | _ -> Seq.empty

    member p.Name = name
    member p.References = refs

[<Sealed>]
type FSharpProject(env: Parameters) =
    let log = Log.Create<FSharpProject>(env)
    let baseDir = FSharpConfig.BaseDir.Find env
    let name = BuildConfig.ProjectName.Find env
    let b = lazy FSharpProjectBuilder(env, log)
    let pP = lazy FSharpProjectParser(env)
    let fw = BuildConfig.CurrentFramework.Find env

    let appendParameters (par: Parameter<_>) xs ps =
        par.Custom (Reify (Seq.append (par.Find env) xs)) ps

    interface INuGetExportingProject with
        member p.NuGetFiles =
            seq {
                for file in b.Value.LibraryFiles do
                    yield NuGetFile.LibraryFile(fw, file)
            }

    interface IParametric with
        member fp.Find p = p.Find env
        member fp.Parameters = env

    interface IParametric<FSharpProject> with
        member fp.Custom p v = FSharpProject(p.Custom v env)

    interface IProject with
        member p.Build rr = b.Value.Build rr
        member p.Clean() = b.Value.Clean()
        member p.Framework = b.Value.Framework
        member p.GeneratedAssemblyFiles = b.Value.GeneratedAssemblyFiles
        member p.Name = b.Value.Name
        member p.References = b.Value.References

    interface IFSharpProjectContainer<FSharpProject> with
        member x.FSharpProject = x
        member x.WithFSharpProject p = p

    member p.WithModules ms =
        let sources =
            [
                for m in ms do
                    yield m + ".fsi"
                    yield m + ".fs"
            ]
            |> List.filter (fun path ->
                Path.Combine(baseDir, path)
                |> IsFile)
        appendParameters FSharpConfig.Sources sources p

    member p.WithReferences f =
        let rB = ReferenceBuilder.Current.Find env
        appendParameters FSharpConfig.References (f rB) p

    member p.WithEmbeddedResources rs =
        appendParameters FSharpConfig.EmbeddedResources rs p

    member p.WithSources sources =
        appendParameters FSharpConfig.Sources sources p

    member p.WithSourcesFromProject(?msbuildProject) =
        let msbp = defaultArg msbuildProject (name + ".fsproj")
        let msb = Path.GetFullPath(Path.Combine(baseDir, msbp))
        pP.Value.Parse(msb, baseDir)
        |> p.WithSources

and IFSharpProjectContainer<'T> =
    abstract FSharpProject : FSharpProject
    abstract WithFSharpProject : FSharpProject -> 'T

[<AutoOpen>]
module FSharpProjectExtensinos =

    type IFSharpProjectContainer<'T> with

        member x.Embed rs =
            x.FSharpProject.WithEmbeddedResources rs
            |> x.WithFSharpProject

        member x.Modules ms =
            x.FSharpProject.WithModules(ms)
            |> x.WithFSharpProject

        member x.References f =
            x.FSharpProject.WithReferences(f)
            |> x.WithFSharpProject

        member x.Sources xs =
            x.FSharpProject.WithSources(xs)
            |> x.WithFSharpProject

        member x.SourcesFromProject(?msbuildProject) =
            x.FSharpProject.WithSourcesFromProject(?msbuildProject = msbuildProject)
            |> x.WithFSharpProject

[<Sealed>]
type FSharpProjects(env) =
    static let current = Parameter.Define(fun env -> FSharpProjects env)

    let create kind name =
        let env =
            env
            |> FSharpConfig.Kind.Custom kind
            |> BuildConfig.ProjectName.Custom name
        FSharpProject(env)

    member t.ConsoleExecutable name =
        create FSharpConsoleExecutable name

    member t.Library name =
        create FSharpLibrary name

    member t.WindowsExecutable name =
        create FSharpWindowsExecutable name

    static member Current = current
