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

module FSharpConfig =

    let Home =
        Parameter.Define(fun env ->
            match Environment.GetEnvironmentVariable("FSharpHome") with
            | null | "" ->
                let pf = GetProgramFiles ()
                Path.Combine(pf, "Microsoft SDKs", "F#", "3.0", "Framework", "v4.0")
            | h -> h)

type FSharpKind =
    | ConsoleExecutable
    | Library
    | WindowsExecutable

[<AutoOpen>]
module FSharpProjectUtils =

    let getProjTypeArg pt =
        match pt with
        | ConsoleExecutable -> "--target:exe"
        | Library -> "--target:library"
        | WindowsExecutable -> "--target:winexe"

    let getOutputPath name kind dir =
        let ext =
            match kind with
            | ConsoleExecutable | WindowsExecutable -> ".exe"
            | Library -> ".dll"
        Path.Combine(dir, name + ext)

    let getMSBuildExtension pt =
        match pt with
        | _ -> ".fsproj"

    module Namespaces =

        let MSBuild = "http://schemas.microsoft.com/developer/msbuild/2003"

[<Sealed>]
type FSharpProjectParser private (env: Parameters) =
    static let current = Parameter.Define(fun env -> FSharpProjectParser env)

    member p.Parse(msbFile: string, baseDir: string) =
        let doc = XDocument.Load(msbFile)
        seq {
            for el in doc.Descendants(XName.Get("Compile", Namespaces.MSBuild)) do
                match el.Attribute(XName.Get("Include")) with
                | null -> ()
                | a ->
                    yield! Directory.GetFiles(Path.GetFullPath baseDir, a.Value)
        }
        |> Reify

    static member Current = current

[<Sealed>]
type FSharpProjectWriter private (env) =
    static let current = Parameter.Define(fun env -> FSharpProjectWriter env)
    let log = Log.Create<FSharpProjectWriter>(env)
    let root = BuildConfig.RootDir.Find(env)
    let out = BuildConfig.OutputDir.Find(env)
    let genFile id = Path.Combine(out, id + ".proj")

    member pw.Write(id: string, refs: ResolvedReferences) =
        let e n = XG.Element.Create(n, Namespaces.MSBuild)
        let xml =
            e "Project" - [
                e "ItemGroup" - [
                    for r in refs.Paths do
                        let n = AssemblyName.GetAssemblyName(r)
                        yield e "Reference" + ["Include", string n.Name] - [
                            e "HintPath" -- Path.GetFullPath(Path.Combine(root, r))
                            e "Private" -- "False"
                        ]
                ]
            ]
            |> XG.Write
        let file = genFile id
        log.Verbose("Generating {0}", file)
        FS.TextContent(xml).WriteFile(file)

    member pw.Clean(id) =
        let f = genFile id
        if IsFile f then
            File.Delete f

    static member Current = current

type FSharpProjectOptions =
    {
        baseDir : string
        id : string
        kind : FSharpKind
        name : string
        otherFlags : list<string>
        references : list<Reference>
        sources : list<string>
    }

    static member Default root kind name =
        {
            baseDir = Path.Combine(root, name)
            id = name
            kind = kind
            name = name
            otherFlags = []
            references = []
            sources = []
        }

[<Sealed>]
type FSharpProject(env, opts) =
    let pW = FSharpProjectWriter.Current.Find env
    let fw = BuildConfig.CurrentFramework.Find env
    let outDir = BuildConfig.OutputDir.Find env
    let log = Log.Create<FSharpProject>(env)
    let fsHome = FSharpConfig.Home.Find env
    let rB = ReferenceBuilder.Current.Find env
    let pP = FSharpProjectParser.Current.Find env
    let aig = AssemblyInfoGenerator.Current.Find env
    let kf = BuildConfig.KeyFile.Find env

    let orElse a b =
        match a with
        | None -> b
        | _ -> a

    let getAssemblyInfoData () =
        let data = AssemblyInfoData.Current.Find env
        {
            data with
                Title = data.Title |> orElse (Some opts.name)
                Version = data.Version 
        }

    let outPath = getOutputPath opts.name opts.kind outDir
    let docPath = Path.ChangeExtension(outPath, ".xml")
    let ainfoPath = Path.Combine(outDir, opts.id + ".annotations.fs")
    let argsPath = Path.Combine(outDir, opts.id + ".args.txt")

    interface INuGetExportingProject with

        member p.LibraryFiles =
            match opts.kind with
            | FSharpKind.Library -> Seq.singleton outPath
            | _ -> Seq.empty

    interface IProject with

        member p.Name = opts.name
        member p.Framework = fw

        member p.GeneratedAssemblyFiles = Seq.singleton outPath

        member p.Build rs =
            let aid =
                let d = AssemblyInfoData.Current.Find env
                let t =
                    d.Title
                    |> orElse (Some opts.name)
                { d with Title = t }
            aig.Generate(AssemblyInfoSyntax.FSharp, aid, ainfoPath)
            pW.Write(opts.id, rs)
            let sourceFiles =
                [
                    for s in opts.sources do
                        yield Path.Combine(opts.baseDir, s)
                ]
            let args =
                [|
                    yield getProjTypeArg opts.kind
                    yield "--noframework"
                    yield "--out:" + outPath
                    yield "--doc:" + docPath
                    for r in rs.Paths do
                        yield "-r"
                        yield r
                    match kf with
                    | Some kf -> yield "--keyfile:" + kf
                    | _ -> ()
                    yield! opts.otherFlags
                    yield ainfoPath
                    yield! sourceFiles
               |]
            PrepareDir outPath
            let fsc = Path.Combine(fsHome, "fsc.exe")
            do
                let t =
                    args
                    |> Seq.append [fsc]
                    |> String.concat Environment.NewLine
                FS.TextContent(t).WriteFile(argsPath)
            let inputFiles =
                [
                    yield argsPath
                    yield ainfoPath
                    yield! sourceFiles
                ]
            let outputFiles =
                [
                    docPath
                    outPath
                ]
            let lastWrite files =
                files
                |> Seq.map File.GetLastWriteTimeUtc
                |> Seq.max
            if lastWrite inputFiles > lastWrite outputFiles then
                log.Info("Building {0}", opts.id)
                for arg in args do
                    log.Verbose("    {0}", arg)
                let r = AppDomain.CurrentDomain.ExecuteAssembly(fsc, args)
                if r <> 0 then
                    failwithf "Non-zero exit code: %i" r
            else
                log.Info("Skipping {0}", opts.id)

        member p.Clean() =
            let rm p =
                if IsFile p then
                    File.Delete(p)
            [
                outPath
                docPath
                ainfoPath
                argsPath
            ]
            |> List.iter rm
            pW.Clean opts.id

        member p.References =
            Seq.ofList opts.references

    member p.Update opts =
        FSharpProject(env, opts)

    member p.Flags fs =
        p.Update { opts with otherFlags = opts.otherFlags @ Seq.toList fs }

    member p.Id id =
        p.Update { opts with id = id }

    member p.Modules ms =
        let sources =
            [
                for m in ms do
                    yield m + ".fsi"
                    yield m + ".fs"
            ]
            |> List.filter (fun path ->
                Path.Combine(opts.baseDir, path)
                |> IsFile)
        p.Update { opts with sources = opts.sources @ sources }

    member p.BaseDir d =
        p.Update { opts with baseDir = d }

    member p.References f =
        let rs = f rB |> Seq.toList
        p.Update { opts with references = opts.references @ rs }

    member p.Sources sources =
        p.Update { opts with sources = opts.sources @ Seq.toList sources }

    member p.SourcesFromProject(?msbuildProject) =
        let msbp = defaultArg msbuildProject (opts.name + getMSBuildExtension opts.kind)
        let msb = Path.GetFullPath(Path.Combine(opts.baseDir, msbp))
        p.Update { opts with sources = opts.sources @ Seq.toList (pP.Parse(msb, opts.baseDir)) }

    member p.LibraryFiles =
        Seq.ofList [
            outPath
            docPath
        ]

[<Sealed>]
type FSharpProjects(env) =
    static let current = Parameter.Define(fun env -> FSharpProjects env)
    let fwt = Frameworks.Current.Find env
    let root = BuildConfig.RootDir.Find env

    let create kind name =
        let opts = FSharpProjectOptions.Default root kind name
        FSharpProject(env, opts)

    member t.ConsoleExecutable name =
        create ConsoleExecutable name

    member t.Library name =
        create Library name

    member t.WindowsExecutable name =
        create WindowsExecutable name

    static member Current = current
