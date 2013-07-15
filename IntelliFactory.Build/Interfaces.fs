namespace IntelliFactory.Build

/// A project that contirbutes files to a created NuGet package.
type INuGetExportingProject =
    abstract LibraryFiles : seq<string>
