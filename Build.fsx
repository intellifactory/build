#r "packages/FAKE.1.74.131.0/tools/FakeLib.dll"
#r "System.Xml"
#r "System.Xml.Linq"
#r "Microsoft.Build"
#r "Microsoft.Build.Engine"
#r "Microsoft.Build.Framework"

#load "IntelliFactory.Build/FileSystem.fs"
#load "IntelliFactory.Build/XmlGenerator.fs"
#load "IntelliFactory.Build/Mercurial.fs"
#load "IntelliFactory.Build/CommonBuildSetup.fs"

open System
open System.IO
open Fake

module B = IntelliFactory.Build.CommonBuildSetup

let Metadata =
    let m = B.Metadata.Create()
    m.Author <- Some "IntelliFactory"
    m.AssemblyVersion <- Some (Version "0.0.0.0")
    m.FileVersion <- Some (Version "0.0.6.0")
    m.Description <- Some "Provides build utilites, in particular API for generating VisualStudio extensibility packages"
    m.Product <- Some "IntelliFactory.Build"
    m.Website <- Some "http://bitbucket.org/IntelliFactory/build"
    m

let Frameworks = [B.Net40]

let Solution =
    B.Solution.Standard __SOURCE_DIRECTORY__ Metadata [
        B.Project.FSharp "IntelliFactory.Build" Frameworks
    ]

Target "Build" Solution.Build
Target "Clean" Solution.Clean

match Environment.GetCommandLineArgs() with
| xs when xs.[xs.Length - 1] = "Clean" -> RunTargetOrDefault "Clean"
| _ -> RunTargetOrDefault "Build"
