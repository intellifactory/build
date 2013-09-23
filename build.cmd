@ECHO OFF
IF NOT "%NuGetHome%"=="" GOTO :nuget
SET NuGetHome=tools\NuGet
:nuget
"%NuGetHome%\NuGet.exe" install NuGet.Core -version 2.7.0 -o packages
IF NOT "%FSharpHome%"=="" GOTO :fs
SET PF=%ProgramFiles(x86)%
IF NOT "%PF%"=="" GOTO w64
SET PF=%ProgramFiles%
:w64
SET FSharpHome=%PF%\Microsoft SDKs\F#\3.0\Framework\v4.0
:fs
"%FSharpHome%\fsi.exe" --exec build.fsx %*
