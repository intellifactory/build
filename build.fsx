#I "packages/NuGet.Core.2.6.0/lib/net40-Client"
#r "NuGet.Core.dll"
#r "System.Xml"
#r "System.Xml.Linq"

open System
open System.IO
open NuGet

// NOTE: the #load directives are only necessary here because
// IntelliFactory.Build is bootstrapping (building itself).

#load "IntelliFactory.Build/FileSystem.fs"
#load "IntelliFactory.Build/XmlGenerator.fs"
#load "IntelliFactory.Build/Parameters.fs"
#load "IntelliFactory.Build/Cache.fs"
#load "IntelliFactory.Build/Logging.fs"
#load "IntelliFactory.Build/Frameworks.fs"
#load "IntelliFactory.Build/Utilities.fs"
#load "IntelliFactory.Build/Interfaces.fs"
#load "IntelliFactory.Build/Company.fs"
#load "IntelliFactory.Build/SafeNuGet.fs"
#load "IntelliFactory.Build/BuildConfig.fs"
#load "IntelliFactory.Build/NuGetConfig.fs"
#load "IntelliFactory.Build/Package.fs"
#load "IntelliFactory.Build/AssemblyInfo.fs"
#load "IntelliFactory.Build/References.fs"
#load "IntelliFactory.Build/Solutions.fs"
#load "IntelliFactory.Build/FSharp.fs"
#load "IntelliFactory.Build/AssemblyResolver.fs"
#load "IntelliFactory.Build/WebSharper.fs"
#load "IntelliFactory.Build/NuGetPackage.fs"
#load "IntelliFactory.Build/BuildTool.fs"

open IntelliFactory.Build

let bt =
    BuildTool()
        .PackageId("IntelliFactory.Build", "0.2")
        .Configure(fun bt ->
            bt.WithFramework(bt.Framework.Net40)
            |> LogConfig.Current.Custom (LogConfig().Verbose().ToConsole()))
        .WithCommandLineArgs()

let buildLib =
    bt.FSharp.Library("IntelliFactory.Build")
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
                rt.NuGet("DotNetZip").Version("1.9.1.8").Reference()
            ])
        .SourcesFromProject()
        .Embed(["../tools/NuGet/NuGet.exe"])

let buildTool =
    bt.FSharp.ConsoleExecutable("IB")
        .References(fun rt ->
            [
                rt.Project(buildLib)
            ])
        .SourcesFromProject()

bt.Solution [

    buildLib
    buildTool

    bt.NuGet.CreatePackage()
        .Description("Provides utilities for build automation, \
            in particular building WebSharper and F# projects, \
            without the need for MSBuild.")
        .ProjectUrl("http://bitbucket.com/IntelliFactory/build/")
        .Apache20License()
        .Add<_>(buildLib)

]
|> bt.Dispatch
