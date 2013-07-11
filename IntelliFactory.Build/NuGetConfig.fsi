﻿namespace IntelliFactory.Build

[<Sealed>]
type NuGetConfig =
    static member CurrentSettings : Parameter<option<NuGet.ISettings>>
    static member CurrentPackageManager : Parameter<NuGet.IPackageManager>
    static member LocalRepositoryPath : Parameter<string>
