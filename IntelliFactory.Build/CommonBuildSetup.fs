﻿// Copyright 2013 IntelliFactory
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

module IntelliFactory.Build.CommonBuildSetup

open System
open System.Runtime.Versioning
open System.Reflection
open System.IO
open Microsoft.Build
open Microsoft.Build.Execution
open Microsoft.Build.Framework
open Microsoft.Build.Logging
module F = IntelliFactory.Build.FileSystem
module M = IntelliFactory.Build.Mercurial
module X = IntelliFactory.Build.XmlGenerator
module NG = IntelliFactory.Build.NuGetUtils

let ( +/ ) a b = Path.Combine(a, b)

type Metadata =
    {
        mutable AssemblyVersion : option<Version>
        mutable Author : option<string>
        mutable Description : option<string>
        mutable FileVersion : option<Version>
        mutable Product : option<string>
        mutable Website : option<string>
    }

    static member Create() =
        {
            AssemblyVersion = None
            Author = None
            Description = None
            FileVersion = None
            Product = None
            Website = None
        }

module AssemblyInfo =

    type Flavor =
        | CSharpFlavor
        | FSharpFlavor

    let private Escape (s: string) : string =
        let s = s.Replace(@"""", @"""""")
        String.Format(@"@""{0}""", s)

    let private Attr<'A when 'A :> Attribute> (out: TextWriter) (flavor: Flavor) (arg: string) : unit =
        let fullName = typeof<'A>.FullName
        match flavor with
        | CSharpFlavor -> out.WriteLine(@"[assembly: {0}({1})]", fullName, Escape arg)
        | FSharpFlavor -> out.WriteLine(@"[<assembly: {0}({1})>]", fullName, Escape arg)

    type Settings =
        {
            Metadata : Metadata
            Tag : option<string>
        }

    let GenerateAssemblyInfoText (flavor: Flavor) (settings: Settings) : string =
        let meta = settings.Metadata
        let product =
            List.concat [
                Option.toList meta.Product
                Option.toList meta.Website
                Option.toList settings.Tag
            ]
            |> String.concat " "
        use out = new StringWriter()
        if flavor = FSharpFlavor then
            out.WriteLine("module internal AutoAssemblyInfo")
        meta.AssemblyVersion
        |> Option.iter (string >> Attr<AssemblyVersionAttribute> out flavor)
        match meta.Author with
        | None -> ()
        | Some a ->
            let copy = String.Format("Copyright (c) {0} {1}", a, DateTime.Now.Year)
            Attr<AssemblyCompanyAttribute> out flavor a
            Attr<AssemblyCopyrightAttribute> out flavor copy
        meta.Description
        |> Option.iter (Attr<AssemblyDescriptionAttribute> out flavor)
        meta.FileVersion
        |> Option.iter (string >> Attr<AssemblyFileVersionAttribute> out flavor)
        if product <> "" then
            Attr<AssemblyProductAttribute> out flavor product
        match settings.Tag with
        | Some t ->
            try
                let v = Version(t)
                Attr<AssemblyInformationalVersionAttribute> out flavor t
            with _ -> ()
        | None -> ()
        if flavor = FSharpFlavor then
            out.WriteLine("do ()")
        out.ToString()

    let Generate (root: string) (meta: Metadata) =
        let tag = M.InferTag root
        let settings : Settings =
            {
                Metadata = meta
                Tag = tag
            }
        let t1 = GenerateAssemblyInfoText FSharpFlavor settings
        (F.TextContent t1).WriteFile(root +/ ".build" +/ "AutoAssemblyInfo.fs")
        let t2 = GenerateAssemblyInfoText CSharpFlavor settings
        (F.TextContent t2).WriteFile(root +/ ".build" +/ "AutoAssemblyInfo.cs")

type FrameworkVersion =
    | Net20
    | Net35
    | Net40
    | Net45

    member this.GetMSBuildLiteral() =
        match this with
        | Net20 -> "v2.0"
        | Net35 -> "v3.5"
        | Net40 -> "v4.0"
        | Net45 -> "v4.5"

    member this.GetNuGetLiteral() =
        match this with
        | Net20 -> "net20"
        | Net35 -> "net35"
        | Net40 -> "net40"
        | Net45 -> "net45"

    member this.ToFrameworkName() =
        match this with
        | Net20 -> FrameworkName(".NETFramework", Version("2.0"))
        | Net35 -> FrameworkName(".NETFramework", Version("3.5"))
        | Net40 -> FrameworkName(".NETFramework", Version("4.0"))
        | Net45 -> FrameworkName(".NETFramework", Version("4.5"))

type BuildConfiguration =
    {
        ConfigurationName : string
        Debug : bool
        FrameworkVersion : FrameworkVersion
        NuGetDependencies : NuGet.PackageDependencySet
    }

    member this.GetName() =
        let fw = this.FrameworkVersion.GetMSBuildLiteral()
        String.Format("{0}-{1}", this.ConfigurationName, fw)

    static member Default =
        {
            ConfigurationName = "Release"
            Debug = false
            FrameworkVersion = Net40
            NuGetDependencies =
                let fw = Net40.ToFrameworkName()
                NuGet.PackageDependencySet(fw, [])
        }

type MSBuildOptions =
    {
        BuildConfiguration : option<BuildConfiguration>
        Properties : Map<string,string>
        Targets : list<string>
    }

    static member Default =
        {
            BuildConfiguration = Some BuildConfiguration.Default
            Properties = Map.empty
            Targets = []
        }

module MSBuildRunner =

    type Job =
        {
            Config : BuildConfiguration
            Options : MSBuildOptions
            Path : string
        }

    let RunJob (manager: BuildManager) (job: Job) =
        async {
            let framework = job.Config.FrameworkVersion
            let props =
                dict [
                    yield "Configuration", job.Config.GetName()
                    yield "TargetFrameworkVersion", framework.GetMSBuildLiteral()
                    yield! Map.toSeq job.Options.Properties
                ]
            let rd =
                BuildRequestData(job.Path, props, "4.0",
                    List.toArray job.Options.Targets, null)
            let! res =
                Async.FromContinuations(fun (ok, _, _) ->
                    let subm = manager.PendBuildRequest(rd)
                    let cb = BuildSubmissionCompleteCallback(ok)
                    subm.ExecuteAsync(cb, null))
            match res.BuildResult.OverallResult with
            | BuildResultCode.Success ->
                return ()
            | _ ->
                return!
                    Async.FromContinuations(fun (_, no, _) ->
                        no res.BuildResult.Exception)
        }

    let RunMSBuild (jobs: seq<Job>) =
        async {
            let manager = new BuildManager()
            let logger = new ConsoleLogger(LoggerVerbosity.Minimal)
            let bp = new BuildParameters(Loggers = [logger])
            manager.BeginBuild(bp)
            try
                return!
                    Async.Parallel(Seq.map (RunJob manager) jobs)
                    |> Async.Ignore
            finally
                manager.EndBuild()
        }

type Project =
    {
        BuildConfigurations : list<BuildConfiguration>
        MSBuildProjectFilePath : option<string>
        Name : string
    }

    member this.MSBuildJobs(options: MSBuildOptions) =
        match this.MSBuildProjectFilePath with
        | None -> []
        | Some path ->
            [
                for config in this.BuildConfigurations do
                    let job : MSBuildRunner.Job =
                        {
                            Config = config
                            Options = options
                            Path = path
                        }
                    yield job
            ]

    member this.MSBuild(?options: MSBuildOptions) =
        let options = defaultArg options MSBuildOptions.Default
        match this.MSBuildProjectFilePath with
        | None -> async.Return ()
        | Some path -> MSBuildRunner.RunMSBuild (this.MSBuildJobs(options))

module CommonTargets =
    type LPR = NuGet.LocalPackageRepository

    let IsPackageRequired (p: Project) (c: BuildConfiguration) (pkg: NuGet.IPackage) =
        c.NuGetDependencies.Dependencies
        |> Seq.exists (fun dep -> dep.Id = pkg.Id)

    let NS = "http://schemas.microsoft.com/developer/msbuild/2003"
    let E name = X.Element.Create(name, NS)

    let GetReferencesXml
            (r: NG.LocalRepository)
            (p: Project)
            (c: BuildConfiguration) =
        NG.ComputeReferences (c.FrameworkVersion.ToFrameworkName())
            (IsPackageRequired p c) r
        |> Seq.cache
        |> Seq.map (fun ref ->
            E "Reference" + ["Include", ref.AssemblyName] - [
                match ref.AssemblyPath with
                | Some path ->
                    yield E "HintPath" -- path
                    yield E "Private" -- "true"
                | None -> ()
            ])

    let ( ==. ) (a: string) (b: string) =
        String.Format("'{0}' == '{1}'", a, b)

    let ( &&. ) (a: string) (b: string) =
        String.Format("({0}) AND ({1})", a, b)

    let Quote (text: string) =
        String.Format("'{0}'", text)

    let Var (name: string) =
        String.Format("$({0})", name)

    let Prop (name: string) (value: string) =
        E name + [ "Condition", Var name ==. "" ] -- value

    let GenerateProjectXml elements =
        E "Project" + ["ToolsVersion", "4.0"; "DefaultTargets", "Build"] - elements

    let Exists (text: string) =
        String.Format("Exists('{0}')", text)

    let GenerateConfigurationsXml (projects: list<Project>) =
        projects
        |> Seq.collect (fun c -> c.BuildConfigurations)
        |> Seq.distinctBy (fun c ->
            (c.ConfigurationName, c.FrameworkVersion.ToFrameworkName().ToString()))
        |> Seq.map (fun c ->
            let cond =
                String.Format(" '$(Configuration)|$(Platform)' == '{0}|AnyCPU'",
                    c.GetName())
            E "PropertyGroup" + [ "Condition", cond ] - (
                if c.Debug then
                    [
                        E "DebugSymbols" -- "true"
                        E "DebugType" -- "full"
                        E "Optimize" -- "false"
                        E "Tailcalls" -- "false"
                        E "DefineConstants" -- "DEBUG;TRACE"
                    ]
                else
                    [
                        E "DebugType" -- "pdbonly"
                        E "Optimize" -- "true"
                        E "Tailcalls" -- "true"
                        E "DefineConstants" -- "TRACE"
                    ]
            )
        )

    let GenerateCommonTargetsXml (lr: NG.LocalRepository) (projects: list<Project>) =
        let includes =
            [
                for p in projects do
                    match p.MSBuildProjectFilePath with
                    | Some pp ->
                        for c in p.BuildConfigurations do
                            let attrs =
                                [
                                    "Condition",
                                        (Var "Configuration" ==. c.GetName())
                                        &&. (Var "Name" ==. p.Name)
                                ]
                            yield E "ItemGroup" + attrs - GetReferencesXml lr p c
                    | None -> ()
            ]
        let content =
            E "PropertyGroup" - [
                Prop "Root" "$(MSBuildThisFileDirectory)/.."
                Prop "TargetFrameworkVersion" "v4.0"
                Prop "Platform" "AnyCPU"
                Prop "Configuration" "Release-$(TargetFrameworkVersion)"
                Prop "OutputPath" "bin/$(Configuration)/"
                Prop "DocumentationFile" "$(OutputPath)/$(Name).xml"
                Prop "RootNamespace" "$(Name)"
                Prop "AssemblyName" "$(Name)"
                Prop "WarningLevel" "3"
            ]
            :: List.ofSeq (GenerateConfigurationsXml projects)
        GenerateProjectXml (content @ includes)

    let GenerateFSharpHomeXml () =
        let h1 = "FSharpHome1"
        let h2 = "FSharpHome2"
        let h3 = "FSharpHome3"
        let h = "FSharpHome"
        let setFH hx = E h  + ["Condition", Exists (Var hx) &&. (Var h ==. "")] -- Var hx
        [
            Prop "Root" "$(MSBuildThisFileDirectory)/.."
            E h1 -- "$(MSBuildExtensionsPath32)/../Microsoft SDKs/F#/3.0/Framework/v4.0"
            E h2 -- "$(MSBuildExtensionsPath32)/../Microsoft F#/v4.0"
            E h3 -- "$(MSBuildExtensionsPath32)/FSharp/1.0"
            setFH h1
            setFH h2
            setFH h3
        ]

    let GenerateFSharpTargetsXml (lr: NG.LocalRepository) (projects: list<Project>) =
        GenerateProjectXml [
            E "PropertyGroup" -  GenerateFSharpHomeXml ()
            E "ItemGroup" - [
                E "Compile" + ["Include", "$(Root)/.build/AutoAssemblyInfo.fs"]
            ]
            E "Import" + ["Project", "$(MSBuildThisFileDirectory)/Common.targets"]
            E "Import" + ["Project", "$(FSharpHome)/Microsoft.FSharp.targets"]
        ]

    let Generate (rootDir: string) (projects: list<Project>) =
        let buildDir = rootDir +/ ".build"
        let commonTargets = buildDir +/ "Common.targets"
        let fsharpTargets = buildDir +/ "FSharp.targets"
        let packagesDir = rootDir +/ "packages"
        let lr = NG.LocalRepository.Create packagesDir
        let gen f t =
            let xml = f lr projects
            let text = F.TextContent (X.Write xml)
            text.WriteFile(t)
        gen GenerateCommonTargetsXml commonTargets
        gen GenerateFSharpTargetsXml fsharpTargets

type Solution =
    {
        Metadata : Metadata
        Projects : list<Project>
        RootDirectory : string
    }

    member this.MSBuild(?options: MSBuildOptions) : Async<unit> =
        async {
            do CommonTargets.Generate this.RootDirectory this.Projects
            do AssemblyInfo.Generate this.RootDirectory this.Metadata
            let options = defaultArg options MSBuildOptions.Default
            return! MSBuildRunner.RunMSBuild [
                for p in this.Projects do
                    yield! p.MSBuildJobs options
            ]
        }

module Preparation =

    /// Finds the latest installed version of a package in a local repository.
    let GetPackageVersion
            (packageId: string)
            (repository: NuGet.IPackageRepository) :
            option<NuGet.SemanticVersion> =
        repository.GetPackages()
        |> NG.MostRecent
        |> Seq.tryPick (fun pkg ->
            if pkg.Id = packageId then Some pkg.Version else None)

    /// Reads an embedded resource from the current assembly by name.
    let ReadEmbeddedTextFile (name: string) : string =
        let a = Assembly.GetExecutingAssembly()
        let name =
            a.GetManifestResourceNames()
            |> Seq.find (fun n -> n.Contains(name))
        use s = a.GetManifestResourceStream(name)
        use r = new StreamReader(s, F.DefaultEncoding)
        r.ReadToEnd()

    let BuildProjXml (fakeVersion: NuGet.SemanticVersion) =
        let e name = X.Element.Create(name, "http://schemas.microsoft.com/developer/msbuild/2003")
        e "Project" + ["ToolsVersion", "4.0"; "DefaultTargets", "Build"] - [
            e "PropertyGroup" - [
                e "Root" -- "$(MSBuildThisFileDirectory)"
                e "SolutionDir" -- "$(Root)"
                e "FAKE" -- String.Format("$(Root)/packages/FAKE.{0}/tools/FAKE.exe", fakeVersion)
            ]
            e "Target" + ["Name", "Boot"] - [
                e "MakeDir" + ["Directories", "$(Root)/.nuget"]
                e "MSBuild" + [
                    "Projects", "$(Root)/Build/NuGet.targets"
                    "Properties", "DownloadNuGetExe=true;ProjectDir=$(Root);SolutionDir=$(Root)"
                    "Targets", "RestorePackages"
                ]
            ]
            e "Target" + ["Name", "Build"; "DependsOnTargets", "Boot"] - [
                e "Exec" + [
                    "Command", @"""$(FAKE)"" build.fsx Build WebOutDir=""$(OutDir)."""
                    "WorkingDirectory", "$(Root)"
                    "LogStandardErrorAsError", "true"
                ]
            ]
            e "Target" + ["Name", "Clean"] - [
                e "Exec" + [
                    "Command", @"""$(FAKE)"" build.fsx Clean WebOutDir=""$(OutDir)."""
                    "WorkingDirectory", "$(Root)"
                    "LogStandardErrorAsError", "true"
                ]
            ]
        ]

let Prepare (trace: string -> unit) (solutionDir: string) =
    let pkgDir = solutionDir +/ "packages"
    if Directory.Exists pkgDir then
        let repo = NuGet.LocalPackageRepository(pkgDir)
        let fakeVersion = Preparation.GetPackageVersion "FAKE" repo
        match fakeVersion with
        | None ->
            trace "Unable to detect FAKE version. Is FAKE present in packages?"
        | Some fakeVersion ->
            let nugetTargets =
                let p = solutionDir +/ ".build" +/ "NuGet.targets"
                if File.Exists p then
                    F.Content.ReadTextFile p
                else
                    Preparation.ReadEmbeddedTextFile "NuGet.targets"
                    |> F.TextContent
            trace "Writing Build/NuGet.targets"
            nugetTargets.WriteFile(solutionDir +/ "Build" +/ "NuGet.targets")
            String.Format("Writing Build.proj with FAKE version = {0}",
                fakeVersion)
            |> trace
            X.WriteFile
                (Path.Combine(solutionDir, "Build.proj"))
                (Preparation.BuildProjXml fakeVersion)
    else
        trace (sprintf "Could not find packages directory: %s" pkgDir)

//let GetOutDir () =
//    let args = Environment.GetCommandLineArgs()
//    args
//    |> Array.tryFindIndex ((=) "OutDir")
//    |> Option.bind (fun i ->
//        let k = i + 1
//        if k < args.Length then
//            match args.[k] with
//            | null | "" | "." -> None
//            | _ -> Some args.[k]
//        else None)
