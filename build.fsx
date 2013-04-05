#if BOOT
open Fake
module FB = Fake.Boot
FB.Prepare {
    FB.Config.Default __SOURCE_DIRECTORY__ with
        NuGetDependencies =
            let ( ! ) x = FB.NuGetDependency.Create x
            [
                !"FAKE"
                !"DotNetZip"
                !"NuGet.Build"
                !"NuGet.Core"
            ]
}
#else

#r "System.Xml"
#r "System.Xml.Linq"
#r "Microsoft.Build"
#r "Microsoft.Build.Engine"
#r "Microsoft.Build.Framework"

#load ".build/boot.fsx"
#load "IntelliFactory.Build/FileSystem.fs"
#load "IntelliFactory.Build/XmlGenerator.fs"
#load "IntelliFactory.Build/Mercurial.fs"
#load "IntelliFactory.Build/NuGetUtils.fs"
#load "IntelliFactory.Build/CommonBuildSetup.fs"

open System
open System.IO
open Fake

let ( +/ ) a b = Path.Combine(a, b)
let Root = __SOURCE_DIRECTORY__
module B = IntelliFactory.Build.CommonBuildSetup
module F = IntelliFactory.Build.FileSystem
module NG = IntelliFactory.Build.NuGetUtils

let Metadata =
    let m = B.Metadata.Create()
    m.Author <- Some "IntelliFactory"
    m.AssemblyVersion <- Some (Version "0.0.0.0")
    m.FileVersion <- Some (Version "0.0.8.0")
    m.Description <- Some "Provides build utilites, in particular API for generating VisualStudio extensibility packages"
    m.Product <- Some "IntelliFactory.Build"
    m.Website <- Some "http://bitbucket.org/IntelliFactory/build"
    m

let ReleaseNet40 : B.BuildConfiguration =
    {
        ConfigurationName = "Release"
        Debug = false
        FrameworkVersion = B.Net40
        NuGetDependencies =
            let (!) x = new NuGet.PackageDependency(x)
            let deps =
                [
                    !"DotNetZip"
                    !"NuGet.Build"
                    !"NuGet.Core"
                ]
            new NuGet.PackageDependencySet(B.Net40.ToFrameworkName(), deps)
    }

let Project : B.Project =
    let name = "IntelliFactory.Build"
    {
        Name = name
        BuildConfigurations = [ReleaseNet40]
        MSBuildProjectFilePath = Some (Root +/ name +/ (name + ".fsproj"))
    }

let Solution : B.Solution =
    {
        Metadata = Metadata
        Projects = [Project]
        RootDirectory = Root
    }

Target "CopyNuGetTargets" <| fun () ->
    let repo = NG.LocalRepository.Create (Root +/ "packages")
    match NG.FindPackage repo "NuGet.Build" with
    | None -> tracefn "Not found: NuGet.Build"
    | Some pkg ->
        let targets =
            pkg.GetFiles()
            |> Seq.tryFind (fun file -> Path.GetFileName file.Path = "NuGet.targets")
        match targets with
        | None -> tracefn "No targets"
        | Some t ->
            let src = repo.Directory +/ repo.PathResolver.GetPackageDirectory(pkg) +/ t.Path
            let tgt = Root +/ ".build" +/ "NuGet.targets"
            F.Content.ReadTextFile(src).WriteFile(tgt)

Target "Build" <| fun () ->
    Solution.MSBuild()
    |> Async.RunSynchronously

Target "Prepare" <| fun () ->
    B.Prepare (tracefn "%s") Root

"CopyNuGetTargets" ==> "Build"

RunTargetOrDefault "Build"


//module B = IntelliFactory.Build.CommonBuildSetup
//
//
//let Frameworks = [B.Net40]
//
//let Solution =
//    B.Solution.Standard __SOURCE_DIRECTORY__ Metadata [
//        B.Project.FSharp "IntelliFactory.Build" Frameworks
//    ]
//
//Target "Build" Solution.Build
//Target "Clean" Solution.Clean
//
//RunTargetOrDefault "Build"

#endif
