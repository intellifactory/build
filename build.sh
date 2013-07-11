#!/bin/bash
export EnableNuGetPackageRestore=true
: ${MonoHome=/usr/lib/mono}
: ${FSharpHome=$MonoHome/4.0}
: ${NuGetHome=tools/NuGet}
export FSharpHome
export MonoHome
export NuGetHome
mono $NuGetHome/NuGet.exe install Nuget.Core -version 2.6.0 -o packages
mono $FSharpHome/fsi.exe --exec build.fsx %*
