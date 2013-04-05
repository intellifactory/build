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
                !"Mono.Cecil"
                !"NuGet.Build"
                !"NuGet.Core"
            ]
}
#else

#r "System.ComponentModel.DataAnnotations"
#r "System.Xml"
#r "System.Xml.Linq"
#r "Microsoft.Build"
#r "Microsoft.Build.Engine"
#r "Microsoft.Build.Framework"

#load ".build/boot.fsx"
#load "IntelliFactory.Build/FileSystem.fs"
#load "IntelliFactory.Build/AssemblySigning.fs"
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

let Company = "IntelliFactory"
let PackageId = "IntelliFactory.Build"

let Metadata =
    let m = B.Metadata.Create()
    m.Author <- Some Company
    m.AssemblyVersion <- Some (Version "0.1.0.0")
    m.FileVersion <- Some (Version "0.1.0.0")
    m.Description <- Some "Provides build utilites, in particular API for generating VisualStudio extensibility packages"
    m.Product <- Some PackageId
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
                    !"Mono.Cecil"
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

let ComputeVersion () =
    let fv = Metadata.FileVersion.Value
    match NG.FindLatestOnlineVersion PackageId with
    | None -> global.NuGet.SemanticVersion.Parse(string fv)
    | Some v ->
        let b = v.Version.Build + 1
        match Metadata.VersionSuffix with
        | None -> new global.NuGet.SemanticVersion(fv.Major, fv.Minor, b, "")
        | Some vn -> new global.NuGet.SemanticVersion(fv.Major, fv.Minor, b, vn)

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

/// TODO: helpers for buliding packages from a solution spec.
Target "BuildNuGetPackage" <| fun () ->
    let version = ComputeVersion ()
    let content =
        use out = new MemoryStream()
        let builder = new NuGet.PackageBuilder()
        builder.Id <- PackageId
        builder.Version <- version
        builder.Authors.Add(Company) |> ignore
        builder.Owners.Add(Company) |> ignore
        builder.LicenseUrl <- Uri("http://apache.org/licenses/LICENSE-2.0.html")
        builder.ProjectUrl <- Uri(defaultArg Metadata.Website "")
        builder.Copyright <- String.Format("Copyright (c) {0} {1}", DateTime.Now.Year, Company)
        builder.Description <- defaultArg Metadata.Description ""
        "F# Build FAKE VSTemplate VSIX VisualStudio Extensibility".Split(' ')
        |> Seq.iter (builder.Tags.Add >> ignore)
        new NuGet.PackageDependencySet(
            B.Net40.ToFrameworkName(),
            [
                new NuGet.PackageDependency("DotNetZip")
                new NuGet.PackageDependency("Mono.Cecil")
                new NuGet.PackageDependency("NuGet.Core")
            ])
        |> builder.DependencySets.Add
        for ext in [".xml"; ".dll"] do
            let n = PackageId
            builder.Files.Add
                (
                    let f = new NuGet.PhysicalPackageFile()
                    f.SourcePath <- Root +/ n +/ "bin" +/ "Release-v4.0" +/ (n + ext)
                    f.TargetPath <- "lib" +/ "net40" +/ (n + ext)
                    f
                )
        builder.Save(out)
        F.Binary.FromBytes (out.ToArray())
        |> F.BinaryContent
    let out = Root +/ ".build" +/ String.Format("IntelliFactory.Build.{0}.nupkg", version)
    content.WriteFile(out)
    tracefn "Written %s" out

Target "Build" <| fun () ->
    Solution.MSBuild()
    |> Async.RunSynchronously

Target "Prepare" <| fun () ->
    B.Prepare (tracefn "%s") Root

"CopyNuGetTargets" ==> "Build" ==> "BuildNuGetPackage"

RunTargetOrDefault "BuildNuGetPackage"

#endif
