namespace IntelliFactory.Build

open IntelliFactory.Core

[<Sealed>]
type NuGetConfig =
    static member CurrentSettings : Parameter<option<NuGet.ISettings>>
    static member CurrentPackageManager : Parameter<NuGet.IPackageManager>
    static member LocalRepositoryPath : Parameter<string>
    static member PackageOutputPath : Parameter<string>

[<Sealed>]
type NuGetFile =

    /// Reads a local file as a `INuGetFile` file.
    static member Local : sourcePath: string * targetPath: string -> INuGetFile

    /// Reads a library file as an `INuGetFile` in a `lib/netXX` folder.
    static member LibraryFile : framework: Framework * sourcePath: string -> INuGetFile
