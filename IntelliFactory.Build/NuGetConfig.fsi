namespace IntelliFactory.Build

open IntelliFactory.Core

[<Sealed>]
type NuGetConfig =
    static member CurrentSettings : Parameter<option<NuGet.ISettings>>
    static member CurrentPackageManager : Parameter<NuGet.IPackageManager>
    static member LocalRepositoryPath : Parameter<string>
    static member PackageOutputPath : Parameter<string>
