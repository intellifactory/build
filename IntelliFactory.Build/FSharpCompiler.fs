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

module IntelliFactory.Build.FSharpCompiler

open System
open System.IO
open IntelliFactory.Core

type Target =
    | Exe
    | Library
    | Module
    | WinExe

type Platform =
    | AnyCPU
    | AnyCPU32
    | Itanium
    | X86
    | X64

    override p.ToString() =
        match p with
        | AnyCPU -> "anycpu"
        | AnyCPU32 -> "anycpu32bitpreferred"
        | Itanium -> "Itanium"
        | X86 -> "x86"
        | X64 -> "x64"

type Debug =
    | Full
    | NoDebug
    | PdbOnly

type Flags =
    | Checked = 1
    | CrossOptimize = 2
    | DelaySign = 4
    | FullPaths = 8
    | NoFramework = 16
    | NoInterfaceData = 32
    | NoLogo = 64
    | NoOptimizationData = 128
    | Optimize = 256
    | Standalone = 512
    | TailCalls = 1024

let private getDefaultFscPath () =
    let fsharpHome =
        match Environment.GetEnvironmentVariable("FSharpHome") with
        | null | "" ->
            let pf = GetProgramFiles ()
            Path.Combine(pf, "Microsoft SDKs", "F#", "3.0", "Framework", "v4.0")
        | h -> h
    Path.Combine(fsharpHome, "fsc.exe")

type Config =
    {
        AppDomain : AppDomain
        Define : seq<string>
        Debug : Debug
        DocPath : option<string>
        Flags : Flags
        FscPath : string
        KeyContainer : option<string>
        KeyFilePath : option<string>
        OutputPath : string
        PdbPath : option<string>
        Parameters : Parameters
        Platform : Platform
        ReferencePaths : seq<string>
        ResourcePaths : seq<string>
        SignaturePath : option<string>
        SourcePaths : seq<string>
        Target : Target
    }

    static member Create(outputPath) =
        {
            AppDomain = AppDomain.CurrentDomain
            Define = Seq.empty
            Debug = NoDebug
            DocPath = None
            Flags = enum 0
            FscPath = getDefaultFscPath ()
            KeyContainer = None
            KeyFilePath = None
            OutputPath = outputPath
            PdbPath = None
            Parameters = Parameters.Default()
            Platform = AnyCPU
            ReferencePaths = Seq.empty
            ResourcePaths = Seq.empty
            SignaturePath = None
            SourcePaths = Seq.empty
            Target = Library
        }

type Result =
    | Built
    | Failed
    | Skipped

    member r.IsError =
        match r with
        | Built | Skipped -> false
        | Failed -> true

[<Sealed>]
type Job private (cfg: Config) =
    let log = Log.Create<Job>(cfg.Parameters)

    let args () =
        [|
            match cfg.Target with
            | Exe -> yield "--target:exe"
            | Library -> yield "--target:library"
            | Module -> yield "--target:module"
            | WinExe -> yield "--target:winexe"
            for c in cfg.Define do
                yield "--define:" + c
            if cfg.Flags.HasFlag Flags.NoFramework then
                yield "--noframework"
            yield "--out:" + cfg.OutputPath
            match cfg.DocPath with
            | None -> ()
            | Some docPath ->
                yield "--doc:" + docPath
            for r in cfg.ReferencePaths do
                yield "-r"
                yield r
            for r in cfg.ResourcePaths do
                yield "--resource:" + r
            match cfg.KeyFilePath with
            | None -> ()
            | Some kf -> yield "--keyfile:" + kf
            yield "--platform:" + string cfg.Platform
            yield! cfg.SourcePaths
        |]

    let prepareDir p =
        let d = DirectoryInfo(p)
        if not d.Exists then
            log.Verbose("Creating {0}", d)
            d.Create()

    let outputs () =
        [
            yield cfg.OutputPath
            yield! Option.toList cfg.DocPath
            yield! Option.toList cfg.PdbPath
            yield! Option.toList cfg.SignaturePath
        ]

    let createDirs () =
        outputs ()
        |> Seq.map Path.GetDirectoryName
        |> Seq.distinct
        |> Seq.iter prepareDir

    let inputs () =
        [
            yield! Option.toList cfg.KeyFilePath
            yield! cfg.ReferencePaths
            yield! cfg.ResourcePaths
            yield! cfg.SourcePaths
        ]
        |> List.map (fun f -> FileInfo f)

    let check () =
        let otherFiles =
            [
                FileInfo cfg.FscPath
            ]
        for f in Seq.append otherFiles (inputs ()) do
            if not f.Exists then
                invalidArg "Config" ("File does not exist: " + string f.FullName)

    let build () =
        check ()
        createDirs ()
        let shouldBuild =
            RebuildProblem
                .Create(cfg.Parameters)
                .AddInputPaths(inputs ())
                .AddOutputPaths(Seq.map (fun x -> FileInfo x) (outputs ()))
                .Decide()
        if shouldBuild.IsStale then
            let r = cfg.AppDomain.ExecuteAssembly(cfg.FscPath, args ())
            if r = 0 then
                Built
            else
                Failed
        else
            Skipped

    let clean () =
        for o in outputs () do
            let f = FileInfo o
            if f.Exists then
                log.Verbose("Deleting {0}", f)
                f.Delete()

    member j.Check() = check ()
    member j.Clean() = clean ()
    member j.Configure f = Job(f cfg)
    member j.Build() = build ()
    member j.Config = cfg

    static member Configure(outputPath) =
        Job (Config.Create outputPath)

