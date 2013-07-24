#I "packages/NuGet.Core.2.6.0/lib/net40-Client"
#r "NuGet.Core.dll"
#r "System.Xml"
#r "System.Xml.Linq"

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
#load "IntelliFactory.Build/FSharp.fs"
#load "IntelliFactory.Build/WebSharper.fs"
#load "IntelliFactory.Build/NuGetPackage.fs"
#load "IntelliFactory.Build/BuildTool.fs"

open IntelliFactory.Build
open IntelliFactory.Core

let common =
    BuildTool().WithCommandLineArgs()
    |> Logs.Config.Custom (Logs.Default.Verbose().ToConsole())

let core =
    common.PackageId("IntelliFactory.Core", "0.1")

let build =
    common.PackageId("IntelliFactory.Build", "0.2")

let coreLib =
    core.FSharp.Library("IntelliFactory.Core")
        .SourcesFromProject()
        .References(fun rt ->
            [
                rt.Assembly("System.Xml")
                rt.Assembly("System.Xml.Linq")
            ])

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
                rt.NuGet("NuGet.Core").Version("2.6.0").Reference()
                rt.Project(coreLib)
            ])
        .Embed(["../tools/NuGet/NuGet.exe"])

let buildTool =
    build.FSharp.ConsoleExecutable("IB").SourcesFromProject()
        .References(fun rt ->
            [
                rt.Project(coreLib)
                rt.Project(buildLib)
                rt.NuGet("NuGet.Core").Version("2.6.0").Reference()
            ])

let corePkg =
    core.NuGet.CreatePackage()
        .Description("Provides utilities missing from F# standard library")
        .ProjectUrl("http://bitbucket.com/IntelliFactory/build")
        .Apache20License()
        .Add(coreLib)

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
    buildLib
    buildTool
    corePkg
    buildPkg
]
|> common.Dispatch
