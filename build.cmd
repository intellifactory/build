tools\NuGet\NuGet.exe install NuGet.Core -version 2.6.0 -o packages
fsi --exec build.fsx %*

