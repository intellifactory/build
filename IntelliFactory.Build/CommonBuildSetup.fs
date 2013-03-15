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

/// Experimental support for convention-based matrix F# builds.
/// API subject to change - use at your own risk.
module IntelliFactory.Build.CommonBuildSetup

open System
open System.Reflection
open System.IO
open System.Xml
open System.Xml.Linq
open Microsoft.Build
module F = IntelliFactory.Build.FileSystem
module M = IntelliFactory.Build.Mercurial
module X = IntelliFactory.Build.XmlGenerator

let ( +/ ) a b = Path.Combine(a, b)

/// Reads root `packages.config` and attempts finding the version for a given package name.
let GetPackageVersion (packageId: string) (solutionDir: string) : option<string>=
    let packagesConfig = Path.Combine(solutionDir, "packages.config")
    if File.Exists packagesConfig then
        let doc = XDocument.Load packagesConfig
        doc.Root.Elements(XName.Get("package"))
        |> Seq.tryPick (fun el ->
            let idAttr = el.Attribute(XName.Get("id"))
            if idAttr = null then None else
                if idAttr.Value <> packageId then None else
                    let verAttr = el.Attribute(XName.Get "version")
                    if verAttr = null then None else
                        try
                            let v = Version(verAttr.Value)
                            Some verAttr.Value
                        with _ -> None)
    else None

/// Reads an embedded resource from the current assembly by name.
let ReadEmbeddedTextFile (name: string) : string =
    let a = Assembly.GetExecutingAssembly()
    let name =
        a.GetManifestResourceNames()
        |> Seq.find (fun n -> n.Contains(name))
    use s = a.GetManifestResourceStream(name)
    use r = new StreamReader(s, F.DefaultEncoding)
    r.ReadToEnd()

let BuildProjXml (fakeVersion: string) =
    let e name = X.Element.Create(name, "http://schemas.microsoft.com/developer/msbuild/2003")
    e "Project" + ["ToolsVersion", "4.0"; "DefaultTargets", "Build"] - [
        e "PropertyGroup" - [
            e "Root" -- "$(MSBuildThisFileDirectory)"
            e "SolutionDir" -- "$(Root)"
        ]
        e "PropertyGroup" - [
            e "FSharpHome1" -- "$(MSBuildExtensionsPath32)/../Microsoft SDKs/F#/3.0/Framework/v4.0"
            e "FSharpHome2" -- "$(MSBuildExtensionsPath32)/../Microsoft F#/v4.0"
            e "FSharpHome3" -- "$(MSBuildExtensionsPath32)/FSharp/1.0"
            e "FSharpHome"
                + ["Condition", @"Exists('$(FSharpHome1)') AND '$(FSharpHome)' == ''"]
                -- "$(FSharpHome1)"
            e "FSharpHome"
                + ["Condition", @"Exists('$(FSharpHome2)') AND '$(FSharpHome)' == ''"]
                -- "$(FSharpHome2)"
            e "FSharpHome"
                + ["Condition", @"Exists('$(FSharpHome3)') AND '$(FSharpHome)' == ''"]
                -- "$(FSharpHome3)"
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
                "Command", @"""$(FSharpHome)/Fsi.exe"" --exec Build.fsx OutDir ""$(OutDir)."""
                "WorkingDirectory", "$(Root)"
                "LogStandardErrorAsError", "true"
            ]
        ]
        e "Target" + ["Name", "Clean"] - [
            e "Exec" + [
                "Command", @"""$(FSharpHome)/Fsi.exe"" --exec Build.fsx OutDir ""$(OutDir)."" Clean"
                "WorkingDirectory", "$(Root)"
                "LogStandardErrorAsError", "true"
            ]
        ]
    ]

let Prepare (trace: string -> unit) (solutionDir: string) =
    match GetPackageVersion "FAKE" solutionDir with
    | None -> trace "Unable to detect FAKE version. Is packages.config present?"
    | Some fakeVersion ->
        let nugetTargets = F.TextContent (ReadEmbeddedTextFile "NuGet.targets")
        trace "Writing Build/NuGet.targets"
        nugetTargets.WriteFile(solutionDir +/ "Build" +/ "NuGet.targets")
        trace (String.Format("Writing Build.proj with FAKE version = {0}", fakeVersion))
        X.WriteFile (Path.Combine(solutionDir, "Build.proj")) (BuildProjXml fakeVersion)

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

    static member All =
        [Net20; Net35; Net40; Net45]

    member this.IsSpecificPath(path) =
        Path.GetFileName(path).ToLower() = this.GetNuGetLiteral()

    member this.LowerFrameworks() =
        List.filter (fun x -> x <= this) FrameworkVersion.All

    member this.FindSearchDirs(root: string) =
        let allDirs =
            Directory.GetDirectories(Path.Combine(root, "packages"), "lib", SearchOption.AllDirectories)
            |> Seq.collect (fun dir -> dir :: Seq.toList (Directory.GetDirectories(dir)))
            |> Seq.distinct
            |> Seq.cache
        seq {
            for f in List.rev (this.LowerFrameworks()) do
                yield! Seq.filter f.IsSpecificPath allDirs
        }
        |> Seq.map (fun s -> s.Replace(root, "$(Root)"))
        |> Seq.toList

type ProjectKind =
    | CSharp
    | FSharp

type Project =
    {
        Frameworks : list<FrameworkVersion>
        ProjectKind : ProjectKind
        ProjectPath : string
        Properties : Map<string,string>
    }

    static member CSharp(name: string)(frameworks: seq<FrameworkVersion>)(solutionDir: string) : Project =
        {
            Frameworks = Seq.toList frameworks
            ProjectKind = CSharp
            ProjectPath = solutionDir +/ name +/ (name + ".csproj")
            Properties = Map.empty
        }

    static member FSharp(name: string)(frameworks: seq<FrameworkVersion>)(solutionDir: string) : Project =
        {
            Frameworks = Seq.toList frameworks
            ProjectKind = FSharp
            ProjectPath = solutionDir +/ name +/ (name + ".fsproj")
            Properties = Map.empty
        }

let GenerateFSharpTargetsXml (fsharpPackage: string) (root: string) =
    let e name = X.Element.Create(name, "http://schemas.microsoft.com/developer/msbuild/2003")
    let prop (name: string) (value: string) =
        e name + [ "Condition", String.Format(" '$({0})' == '' ", name) ] -- value
    let includes =
        [
            for f in FrameworkVersion.All ->
                e "PropertyGroup"
                    + ["Condition", String.Format(" '$(TargetFrameworkVersion)' == '{0}' ", f.GetMSBuildLiteral())]
                    - [
                        e "AssemblySearchPaths"
                            -- String.concat ";" ("$(AssemblySearchPaths)" :: f.FindSearchDirs(root))
                    ]
        ]
    let content =
        [
            e "PropertyGroup" - [
                prop "Root" "$(MSBuildThisFileDirectory)/.."
                prop "FSharpHome" "$(MSBuildExtensionsPath32)/../Microsoft SDKs/F#/3.0/Framework/v4.0"
                prop "TargetFrameworkVersion" "v4.0"
                prop "Platform" "AnyCPU"
                prop "Configuration" "Release-$(TargetFrameworkVersion)"
                prop "OutputPath" "bin/$(Configuration)/"
                prop "DocumentationFile" "$(OutputPath)/$(Name).xml"
                prop "RootNamespace" "$(Name)"
                prop "AssemblyName" "$(Name)"
                prop "WarningLevel" "3"
                prop "UseFSharp23" "false"
            ]
            e "PropertyGroup"
                + ["Condition", " '$(Configuration)|$(Platform)' == 'Debug-$(TargetFrameworkVersion)|AnyCPU' "]
                - [
                    e "DebugSymbols" -- "true"
                    e "DebugType" -- "full"
                    e "Optimize" -- "false"
                    e "Tailcalls" -- "false"
                    e "DefineConstants" -- "DEBUG;TRACE"
                ]
            e "PropertyGroup"
                + ["Condition", " '$(Configuration)|$(Platform)' == 'Release-$(TargetFrameworkVersion)|AnyCPU' "]
                - [
                    e "DebugType" -- "pdbonly"
                    e "Optimize" -- "true"
                    e "Tailcalls" -- "true"
                    e "DefineConstants" -- "TRACE"
                ]
            e "ItemGroup" - [
                e "Compile" + ["Include", "$(Root)/.build/AutoAssemblyInfo.fs"]
            ]
            e "ItemGroup" + ["Condition", "$(UseFSharp23)"] - [
                e "Reference" + ["Include", "mscorlib"]
                e "Reference" + ["Include", "System"]
                e "Reference"
                    + ["Include", "FSharp.Core, Version=2.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"]
                    - [
                        e "Private" -- "true"
                        e "HintPath" -- String.Format("$(Root)/packages/{0}/lib/net20/FSharp.Core.dll", fsharpPackage)
                        e "SpecificVersion" -- "true"
                    ]
            ]
            e "ItemGroup" + ["Condition", "!$(UseFSharp23)"] - [
                e "Reference" + ["Include", "mscorlib"]
                e "Reference" + ["Include", "System"]
                e "Reference" + ["Include", "System.Core"]
                e "Reference" + ["Include", "System.Numerics"]
                e "Reference"
                    + ["Include", "FSharp.Core, Version=4.3.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"]
                    - [
                        e "Private" -- "true"
                        e "HintPath" -- String.Format("$(Root)/packages/{0}/lib/net40/FSharp.Core.dll", fsharpPackage)
                        e "SpecificVersion" -- "true"
                    ]
            ]
            e "Import" + ["Project", "$(FSharpHome)/Microsoft.FSharp.targets"]
        ]
    e "Project" + ["ToolsVersion", "4.0"; "DefaultTargets", "Build"] - (content @ includes)

let GenerateFSharpTargets (root: string) =
    let fsVer = defaultArg (GetPackageVersion "FSharp.Core.3" root) "3.0.0.2"
    let xml = GenerateFSharpTargetsXml ("FSharp.Core.3." + fsVer) root
    let text = F.TextContent (X.Write xml)
    text.WriteFile(root +/ ".build" +/ "FSharp.targets")

let GenerateAssemblyInfos (root: string) (meta: Metadata) =
    let tag = M.InferTag root
    let settings : AssemblyInfo.Settings =
        {
            Metadata = meta
            Tag = tag
        }
    let t1 = AssemblyInfo.GenerateAssemblyInfoText AssemblyInfo.FSharpFlavor settings
    (F.TextContent t1).WriteFile(root +/ ".build" +/ "AutoAssemblyInfo.fs")
    let t2 = AssemblyInfo.GenerateAssemblyInfoText AssemblyInfo.CSharpFlavor settings
    (F.TextContent t2).WriteFile(root +/ ".build" +/ "AutoAssemblyInfo.cs")

let MSBuild (projectFile: string) (framework: FrameworkVersion) (targets: seq<string>) (props: seq<string * string>) =
    let manager = Microsoft.Build.Execution.BuildManager.DefaultBuildManager
    let logger = Microsoft.Build.Logging.ConsoleLogger()
    let props =
        dict [
            yield! props
            yield "Configuration", "Release-" + framework.GetMSBuildLiteral()
            yield "TargetFrameworkVersion", framework.GetMSBuildLiteral()
            yield "UseFSharp23",
                match framework with
                | Net20 | Net35 -> "true"
                | _ -> "false"
        ]
    let proj = Microsoft.Build.Execution.ProjectInstance(projectFile)
    let par = Microsoft.Build.Execution.BuildParameters(Loggers = [logger])
    let rd = Microsoft.Build.Execution.BuildRequestData(projectFile, props, "4.0", Seq.toArray targets, null)
    let result = manager.Build(par, rd)
    result.OverallResult = Microsoft.Build.Execution.BuildResultCode.Success

let GetOutDir () =
    let args = Environment.GetCommandLineArgs()
    args
    |> Array.tryFindIndex ((=) "OutDir")
    |> Option.bind (fun i ->
        let k = i + 1
        if k < args.Length then
            match args.[k] with
            | null | "" | "." -> None
            | _ -> Some args.[k]
        else None)

type Solution =
    {
        Metadata : Metadata
        Projects : list<Project>
        RootDirectory : string
    }

    member this.Build(?props: seq<string * string>) =
        GenerateFSharpTargets this.RootDirectory
        GenerateAssemblyInfos this.RootDirectory this.Metadata
        this.MSBuild("Build", defaultArg props Seq.empty)

    member this.Clean(?props: seq<string * string>) =
        this.MSBuild("Clean", defaultArg props Seq.empty)

    member this.MSBuild(target: string, ?props: seq<string * string>) : unit=
        for p in this.Projects do
            let props = Seq.append (Map.toSeq p.Properties) (defaultArg props Seq.empty)
            for fw in p.Frameworks do
                let res = MSBuild p.ProjectPath fw [target] props
                if not res then
                    failwith ("MSBuild failed: " + p.ProjectPath)

    static member Standard(solutionDir: string)(meta: Metadata)(projects: seq<string -> Project>) : Solution =
        {
            Metadata = meta
            Projects = [ for p in projects -> p solutionDir ]
            RootDirectory = solutionDir
        }
