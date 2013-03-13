#r "packages/FAKE.1.74.127.0/tools/FakeLib.dll"

open System
open System.IO
open Fake

module Config =
    let FileVersion = Version("0.0.1.0")
    let AssemblyVersion = Version("0.0.0.0")
    let Product = "IntelliFactory.Build"
    let Repo = "http://bitbucket.org/IntelliFactory/build"

let Root = __SOURCE_DIRECTORY__
let ( +/ ) a b = Path.Combine(a, b)

/// Infers the current Mercurial revision from the `.hg` folder.
let InferTag () =
    let bytes = File.ReadAllBytes(Root +/ ".hg" +/ "dirstate")
    Array.sub bytes 0 20
    |> Array.map (fun b -> String.Format("{0:x2}", b))
    |> String.concat ""

[<AutoOpen>]
module Tagging =
    type private A = AssemblyInfoFile.Attribute

    let PrepareAssemblyInfo =
        Target "PrepareAssemblyInfo" <| fun () ->
            let tag = InferTag ()
            let buildDir = Root +/ ".build"
            ensureDirectory buildDir
            let fsInfo = buildDir +/ "AutoAssemblyInfo.fs"
            let csInfo = buildDir +/ "AutoAssemblyInfo.cs"
            let desc =
                String.Format("See \
                    the source code at <{2}>. \
                    Mercurial tag: {0}. Build date: {1}", tag, DateTimeOffset.UtcNow, Config.Repo)
            let attrs =
                [
                    A.Company "IntelliFactory"
                    A.Copyright (String.Format("(c) {0} IntelliFactory", DateTime.Now.Year))
                    A.FileVersion (string Config.FileVersion)
                    A.Description desc
                    A.Product (String.Format("{0} (tag: {1})", Config.Product, tag))
                    A.Version (string Config.AssemblyVersion)
                ]
            AssemblyInfoFile.CreateFSharpAssemblyInfo fsInfo attrs
            AssemblyInfoFile.CreateCSharpAssemblyInfo csInfo attrs

let Projects =
    !+ (Root +/ "*" +/ "*.csproj")
    ++ (Root +/ "*" +/ "*.fsproj")
    |> Scan

type Framework =
    | V20
    | V40

let Properties _ =
    [
        "Configuration", "Release"
    ]

Target "Build" <| fun () ->
    tracefn "Building"
    MSBuildWithProjectProperties "" "Build" Properties Projects
    |> ignore

Target "Clean" <| fun () ->
    tracefn "Cleaning"
    DeleteDir (Root +/ ".build")
    MSBuildWithProjectProperties "" "Clean" Properties Projects
    |> ignore

"Clean" ==> "PrepareAssemblyInfo" ==> "Build"

RunTargetOrDefault "Build"
