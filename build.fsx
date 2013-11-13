#I "packages/NuGet.Core.2.7.0/lib/net40-Client"
#r "NuGet.Core.dll"
#r "System.Xml"
#r "System.Xml.Linq"
#r "Microsoft.Build"
#r "Microsoft.Build.Engine"
#r "Microsoft.Build.Framework"
#r "Microsoft.Build.Tasks.v4.0"
#r "Microsoft.Build.Utilities.v4.0"

open System
open System.IO
open NuGet

// NOTE: the #load directives are only necessary here because
// the project is bootstrapping (building itself).

#load "IntelliFactory.Core/Parametrization.fs"
#load "IntelliFactory.Core/Logs.fs"
#load "IntelliFactory.Core/FileSystem.fs"
#load "IntelliFactory.Core/XmlTools.fs"
#load "IntelliFactory.Core/AssemblyResolution.fs"
#load "IntelliFactory.Core/BatchedQueues.fs"
#load "IntelliFactory.Core/AtomicReferences.fs"
#load "IntelliFactory.Core/TaskExtensions.fs"
#load "IntelliFactory.Core/TextExtensions.fs"
#load "IntelliFactory.Core/AsyncExtensions.fs"
#load "IntelliFactory.Core/Futures.fs"
#load "IntelliFactory.Core/TextPipes.fs"
#load "IntelliFactory.Core/IOExtensions.fs"
#load "IntelliFactory.Core/ProcessAgent.fs"
#load "IntelliFactory.Core/ProcessService.fs"
#load "IntelliFactory.Core/AutoExports.fs"
#load "IntelliFactory.Build/Cache.fs"
#load "IntelliFactory.Build/Frameworks.fs"
#load "IntelliFactory.Build/Utilities.fs"
#load "IntelliFactory.Build/Interfaces.fs"
#load "IntelliFactory.Build/Company.fs"
#load "IntelliFactory.Build/SafeNuGet.fs"
#load "IntelliFactory.Build/BuildConfig.fs"
#load "IntelliFactory.Build/NuGet.fs"
#load "IntelliFactory.Build/Package.fs"
#load "IntelliFactory.Build/AssemblyInfo.fs"
#load "IntelliFactory.Build/References.fs"
#load "IntelliFactory.Build/Solutions.fs"
#load "IntelliFactory.Build/Rebuilds.fs"
#load "IntelliFactory.Build/FSharp.fs"
#load "IntelliFactory.Build/WebSharper.fs"
#load "IntelliFactory.Build/NuGetPackage.fs"
#load "IntelliFactory.Build/MSBuild.fs"
#load "IntelliFactory.Build/BuildTool.fs"

open IntelliFactory.Build
open IntelliFactory.Core

let common = BuildTool().Verbose()
let core = common.PackageId("IntelliFactory.Core", "0.2-alpha")
let build = common.PackageId("IntelliFactory.Build", "0.2-alpha")

let coreLib =
    core.FSharp.Library("IntelliFactory.Core")
        .SourcesFromProject()
        .References(fun rt ->
            [
                rt.Assembly("System.IO.Compression")
                rt.Assembly("System.IO.Compression.FileSystem")
                rt.Assembly("System.Xml")
                rt.Assembly("System.Xml.Linq")
            ])

let coreLib40 =
    coreLib
    |> BuildConfig.CurrentFramework.Custom common.Framework.Net40
    |> FSharpConfig.OtherFlags.Custom ["--define:NET40"]

let buildLib =
    build.FSharp.Library("IntelliFactory.Build")
        .SourcesFromProject()
        .References(fun rt ->
            [
                rt.Assembly("Microsoft.Build")
                rt.Assembly("Microsoft.Build.Engine")
                rt.Assembly("Microsoft.Build.Framework")
                rt.Assembly("Microsoft.Build.Tasks.v4.0")
                rt.Assembly("Microsoft.Build.Utilities.v4.0")
                rt.Assembly("System.Xml")
                rt.Assembly("System.Xml.Linq")
                rt.Assembly("System.IO.Compression")
                rt.Assembly("System.IO.Compression.FileSystem")
                rt.NuGet("NuGet.Core").Version("2.7.0").Reference()
                rt.Project(coreLib)
            ])
        .Embed(["../tools/NuGet/NuGet.exe"])

let buildTool =
    build.FSharp.ConsoleExecutable("IB").SourcesFromProject()
        .References(fun rt ->
            [
                rt.Project(coreLib)
                rt.Project(buildLib)
                rt.NuGet("NuGet.Core").Version("2.7.0").Reference()
            ])
    |> FSharpConfig.OtherFlags.Custom ["--platform:x86"]

let corePkg =
    core.NuGet.CreatePackage()
        .Description("Provides utilities missing from F# standard library")
        .ProjectUrl("http://bitbucket.com/IntelliFactory/build")
        .Apache20License()
        .Add(coreLib)
        .Add(coreLib40)

let buildPkg =
    build.NuGet.CreatePackage()
        .Description("Provides utilities for build automation, \
            in particular building WebSharper and F# projects, \
            without the need for MSBuild.")
        .ProjectUrl("http://bitbucket.com/IntelliFactory/build")
        .Apache20License()
        .Add(buildLib)
        .Add(buildTool)
        .AddPackage(corePkg)

common.Solution [
    coreLib
    coreLib40
    buildLib
    buildTool
    corePkg
    buildPkg
]
|> common.Dispatch
