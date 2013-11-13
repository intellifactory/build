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
open System.Diagnostics
open System.IO
open System.Reflection
open System.Security
open System.Xml
open System.Xml.Linq
open IntelliFactory.Build
open IntelliFactory.Core

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

    let SourcesProject : Parameter<option<FileInfo>> =
        Parameter.Create None

[<Sealed>]
type FSharpProjectParser(env: IParametric) =

    member p.Parse(msbFile: string, baseDir: string) =
        let doc = XDocument.Load(msbFile)
        seq {
            for el in doc.Descendants(XName.Get("Compile", FSharpConstants.MSBuild)) do
                match el.Attribute(XName.Get("Include")) with
                | null -> ()
                | a ->
                    let p = a.Value
                    if p.Contains "*" then
                        yield! Directory.GetFiles(Path.GetFullPath baseDir, p)
                    else
                        let fullPath = Path.Combine(Path.GetFullPath baseDir, p)
                        if File.Exists fullPath then
                            yield fullPath
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
            let xg = XmlGenerator.Create FSharpConstants.MSBuild
            let xml =
                xg?Project - [
                    xg?ItemGroup -< [
                        for r in refs.Paths do
                            let n = AssemblyName.GetAssemblyName(r)
                            yield xg?Reference + ["Include", string n.Name] - [
                                xg?HintPath -- Path.GetFullPath(Path.Combine(rootDir, r))
                            ]
                    ]
                ]
            log.Verbose("Generating {0}", path)
            xml.WriteFile path

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
            yield "--define:TRACE"
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

module FSharpXml =

    let makeRelativePath (dir: string) (path: string) =
        if path.StartsWith(dir) then
            let p =
                path.Substring(dir.Length)
                    .Replace(@"\", "/")
                    .TrimStart('/')
            match p with
            | "" -> "."
            | _ -> p
        else path

    let fixupPath (env: IParametric) (path: string) =
        let baseDir = DirectoryInfo(FSharpConfig.BaseDir.Find env)
        let rootDir = DirectoryInfo(BuildConfig.RootDir.Find env)
        let p = Path.Combine(baseDir.FullName, path)
        if p.StartsWith rootDir.FullName then
            let p =
                p.Substring(rootDir.FullName.Length)
                    .Replace(@"\", "/")
                    .TrimStart('/')
            match p with
            | "" -> Some "."
            | _ -> Some p
        else None

    let getFSharpXmlFile (env: IParametric) =
        let name = BuildConfig.ProjectName.Find env
        let outDir = BuildConfig.OutputDir.Find env
        Path.Combine(outDir, name + ".fs.xml")

    let generateFSharpXmlFile env (rr: ResolvedReferences) sourcePaths output =
        let rootDir = BuildConfig.RootDir.Find env
        let x = XmlGenerator.Create()
        let xml =
            x.Element "Project" -< [
                for s in sourcePaths do
                    match fixupPath env s with
                    | None -> ()
                    | Some s ->
                        yield x.Element "Source" + ["File", s]
                for rr in rr.Paths do
                    yield x.Element "Reference" + ["File", makeRelativePath rootDir rr]
            ]
        xml.WriteFile output

    let writeReferenceFile (env: IParametric) (rr: ResolvedReferences) =
        let sourcePaths = FSharpConfig.Sources.Find env
        match Seq.toList sourcePaths with
        | [] -> ()
        | sources ->
            let output = getFSharpXmlFile env
            let fw = BuildConfig.CurrentFramework.Find env
            generateFSharpXmlFile env rr sources output

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
    let rootDir = BuildConfig.RootDir.Find env
    let resolved = lazy References.Current.Find(env).ResolveReferences fw refs
    let aid =
        let d = AssemblyInfoData.Current.Find env
        let t =
            d.Title
            |> OrElse (Some name)
        { d with Title = t }

    let inputFiles (rr: ResolvedReferences) =
        FileInfo argsPath
        :: FileInfo ainfoPath
        :: [for s in sources -> FileInfo(Path.Combine(baseDir, s))]
        |> List.append [
                match FSharpConfig.SourcesProject.Find env with
                | Some file -> yield file
                | None -> ()
            ]
        |> List.append [for r in rr.Paths -> FileInfo(Path.Combine(rootDir, r))]

    let outputFiles =
        FileInfo outPath :: [for d in Option.toList docPath -> FileInfo d]

    member p.Build() =
        let rr = resolved.Value
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
            Content.Text(t).WriteFile(argsPath)
        let rebuildDecision =
            RebuildProblem.Create(env)
                .AddInputPaths(inputFiles resolved.Value)
                .AddOutputPaths(outputFiles)
                .Decide()
        if rebuildDecision.IsStale then
            task.Build()
            rebuildDecision.Touch()
        else
            log.Info("Skipping {0}", name)
        match kind with
        | FSharpConsoleExecutable
        | FSharpWindowsExecutable ->
            // deploy dependencies to outDir for executables
            for r in rr.References do
                if not r.IsFrameworkReference || Path.GetFileName(r.Path) = "FSharp.Core.dll" then
                    let source = FileInfo r.Path
                    let target = FileInfo (Path.Combine(outDir, Path.GetFileName(r.Path)))
                    if target.Exists |> not || target.LastWriteTimeUtc < source.LastWriteTimeUtc then
                        Content.ReadBinaryFile(source.FullName).WriteFile(target.FullName)
        | _ -> ()
        p.PrepareReferences()

    member p.PrepareReferences() =
        let rr = resolved.Value
        let fsXmlPath = FSharpXml.getFSharpXmlFile env
        FSharpXml.generateFSharpXmlFile env rr sources fsXmlPath

    member p.Clean() =
        let rm p =
            if IsFile p then
                File.Delete(p)
        [
            yield outPath
            yield! Option.toList docPath
            yield ainfoPath
            yield argsPath
            yield FSharpXml.getFSharpXmlFile env
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
    member p.Resolved = resolved.Value

[<Sealed>]
type FSharpProject(env: Parameters) =
    let log = Log.Create<FSharpProject>(env)
    let baseDir = FSharpConfig.BaseDir.Find env
    let name = BuildConfig.ProjectName.Find env
    let b = lazy FSharpProjectBuilder(env, log)
    let pP = lazy FSharpProjectParser(env)
    let fw = BuildConfig.CurrentFramework.Find env
    let kind = FSharpConfig.Kind.Find env
    let mutable resolvedRefs = ResolvedReferences.Empty
    let outPath = FSharpConfig.OutputPath.Find env

    let appendParameters (par: Parameter<_>) xs ps =
        par.Custom (Reify (Seq.append (par.Find env) xs)) ps

    interface INuGetExportingProject with
        member p.NuGetFiles =
            match kind with
            | FSharpLibrary ->
                seq {
                    for file in b.Value.LibraryFiles do
                        yield NuGetFile.LibraryFile(fw, file)
                }
            | FSharpConsoleExecutable | FSharpWindowsExecutable ->
                let toolFile p =
                    NuGetFile.Local(p, String.Format("/tools/{0}/{1}", fw.Name, Path.GetFileName p))
                seq {
                    yield toolFile outPath
                    for f in b.Value.Resolved.References do
                        if not f.IsFrameworkReference then
                            yield toolFile f.Path
                }

    interface IParametric with
        member fp.Parameters = env

    interface IParametric<FSharpProject> with
        member fp.WithParameters env = FSharpProject(env)

    interface IProject with
        member p.Build() = b.Value.Build()
        member p.Clean() = b.Value.Clean()
        member p.PrepareReferences() = b.Value.PrepareReferences()
        member p.Framework = b.Value.Framework
        member p.Name = b.Value.Name

        member p.Parametric =
            {
                new IParametric<IProject> with
                    member fp.WithParameters env = FSharpProject(env) :> _
                interface IParametric with
                    member fp.Parameters = env
            }

        member p.References = b.Value.References

    interface IReferenceProject with
        member p.GeneratedAssemblyFiles = b.Value.GeneratedAssemblyFiles

[<AutoOpen>]
module FSharpProjectExtensinos =

    let appendParameters (par: Parameter<seq<'T>>) xs ps =
        par.Custom (Reify (Seq.append (par.Find ps) xs)) ps

    type IParametric<'T> with

        member p.References f =
            let rB = ReferenceBuilder.Current.Find p
            appendParameters FSharpConfig.References (f rB) p

        member p.Embed rs =
            appendParameters FSharpConfig.EmbeddedResources rs p

        member p.Sources ss =
            appendParameters FSharpConfig.Sources ss p

        member p.Modules ms =
            let baseDir = FSharpConfig.BaseDir.Find p
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

        member p.SourcesFromProject(?msbuildProject) =
            let name = BuildConfig.ProjectName.Find p
            let baseDir = FSharpConfig.BaseDir.Find p
            let msbp = defaultArg msbuildProject (name + ".fsproj")
            let msb = Path.GetFullPath(Path.Combine(baseDir, msbp))
            let projFile = FileInfo msb
            let env = p.Parameters
            let pP = FSharpProjectParser(p)
            let ss = pP.Parse(msb, baseDir)
            let env =
                appendParameters FSharpConfig.Sources ss env
                |> FSharpConfig.SourcesProject.Custom (Some projFile)
            p.WithParameters env

[<Sealed>]
type FSharpInteractive(env) =
    let fsHome = FSharpConfig.FSharpHome.Find env
    let fsiToolPath = Path.Combine(fsHome, "fsiAnyCPU.exe")
    let root = BuildConfig.RootDir.Find env

    let quote (s: string) =
        String.Format(@"""{0}""", s)

    let fullPath s =
        Path.Combine(root, s)
        |> Path.GetFullPath

    let quotedFullPath s =
        fullPath s
        |> quote

    let buildReferences scriptFile refs =
        let t =
            [
                for r in refs do
                    let p = fullPath r
                    match Path.GetFileName p with
                    | "FSharp.Core.dll"
                    | "System.Core.dll"
                    | "System.dll"
                    | "System.Numerics.dll"
                    | "mscorlib.dll" -> ()
                    | _ ->
                         yield "#r @" + quote p
            ]
            |> String.concat Environment.NewLine
        let includesFile = Path.Combine(root, "build", Path.ChangeExtension(Path.GetFileName scriptFile, ".includes.fsx"))
        FileSystem.TextContent(t).WriteFile(includesFile)

    [<SecurityCritical>]
    member i.ExecuteScript(scriptPath: string, ?refs: ResolvedReferences, ?args: seq<string>) =
        match refs with
        | None -> ()
        | Some refs -> buildReferences scriptPath refs.Paths
        let args =
            [
                yield "--exec"
                yield quotedFullPath scriptPath
                yield! defaultArg args Seq.empty
            ]
            |> String.concat " "
        let pc =
            {
                ProcessAgent.Configure
                    ProcessAgent.MessageType.ASCII
                    fsiToolPath with
                    Arguments = args
                    OnError = stderr.Write
                    OnOutput = stdout.Write
            }
        let ph = ProcessAgent.Start pc
        let exitCode =
            ph.ExitCode.Await()
            |> Async.RunSynchronously
        if exitCode <> 0 then
            failwithf "Non-zero exit code: %i" exitCode

[<Sealed>]
type FSharpTool(env) =
    let fsi = FSharpInteractive env
    static let current = Parameter.Define(fun env -> FSharpTool env)

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

    [<SecurityCritical>]
    member t.ExecuteScript(scriptPath, ?refs, ?args) =
        fsi.ExecuteScript(scriptPath, ?refs = refs, ?args = args)

    static member Current = current
