namespace IntelliFactory.Build

open System
open System.IO

/// Implements command-line `IB.exe` logic.
[<Sealed>]
type ConsoleProgram() =

    let log =
        let env =
            Parameters.Default
            |> LogConfig.Current.Custom (LogConfig().Info().ToConsole())
        Log.Create("IB", env)

    let writeUsage () =
        log.Error("Usage: IB.exe prepare")

    let ensure file (c: FileSystem.Content) =
        if c.EnsureFile(file) then
            log.Info("Written {0}", file)
        else
            log.Verbose("Skipped {0}", file)

    let ensureText file t =
        ensure file (FileSystem.TextContent t)

    let ensureBinary file t =
        ensure file (FileSystem.BinaryContent t)

    let generateBuildCmd () =
        [
            @"@ECHO OFF"
            @"REM NOTE: This file was auto-generated with `IB.exe prepare` from `IntelliFactory.Build`."
            @"IF NOT ""%NuGetHome%""=="""" GOTO :nuget"
            @"SET NuGetHome=tools\NuGet"
            @":nuget"
            @"""%NuGetHome%\NuGet.exe"" install IntelliFactory.Build -pre -ExcludeVersion -o packages"
            @"IF NOT ""%FSharpHome%""=="""" GOTO :fs"
            @"SET PF=%ProgramFiles(x86)%"
            @"IF NOT ""%PF%""=="""" GOTO w64"
            @"SET PF=%ProgramFiles%"
            @":w64"
            @"SET FSharpHome=%PF%\Microsoft SDKs\F#\3.0\Framework\v4.0"
            @":fs"
            @"""%FSharpHome%\fsi.exe"" --exec build.fsx %*"
        ]
        |> String.concat "\r\n"
        |> ensureText "build.cmd"

    let generateNuGetExe () =
        let content =
            use m = new MemoryStream()
            do
                use s = typeof<ConsoleProgram>.Assembly.GetManifestResourceStream("NuGet.exe")
                s.CopyTo(m)
            m.ToArray()
            |> FileSystem.Binary.FromBytes
        ensureBinary "tools/NuGet/NuGet.exe" content

    let generateIncludesFsx () =
        [
            @"#I ""../packages/NuGet.Core/lib/net40-Client"""
            @"#I ""../packages/IntelliFactory.Build/lib/net40"""
            @"#r ""NuGet.Core"""
            @"#r ""IntelliFactory.Build"""
        ]
        |> String.concat Environment.NewLine
        |> ensureText "tools/includes.fsx"

    let generateBuildSh () =
        [
            @"#!/bin/bash"
            @"export EnableNuGetPackageRestore=true"
            @": ${MonoHome=/usr/lib/mono}"
            @": ${FSharpHome=$MonoHome/4.0}"
            @": ${NuGetHome=tools/NuGet}"
            @"export FSharpHome"
            @"export MonoHome"
            @"export NuGetHome"
            @"mono $NuGetHome/NuGet.exe install IntelliFactory.Build -pre -ExcludeVersion -o packages"
            @"mono $FSharpHome/fsi.exe --exec build.fsx %*"
        ]
        |> String.concat "\n"
        |> ensureText "build.sh"

    let generateBuildFsx () =
        if not (IsFile "build.fsx") then
            [
                @"#load ""tools/includes.fsx"""
                @"open IntelliFactory.Build"
                @"let bt = BuildTool()"
            ]
            |> String.concat Environment.NewLine
            |> ensureText "build.fsx"

    let prepare () =
        log.Info("Preparing {0}", Directory.GetCurrentDirectory())
        generateBuildCmd ()
        generateNuGetExe ()
        generateIncludesFsx ()
        generateBuildSh ()
        generateBuildFsx ()

    /// Implements the entry-point method returning the exit code.
    member e.Start(args: seq<string>) : int =
        let args = Seq.toList args
        match args with
        | ["prepare"] ->
            prepare ()
            0
        | _ ->
            writeUsage ()
            1

