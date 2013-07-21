namespace IntelliFactory.Build

open System
open System.IO

#if INTERACTIVE
open IntelliFactory.Build
#endif

/// Represents a file that is to become a part of a `NuGet` archive.
type INuGetFile =

    /// Reads the file contents.
    abstract Read : unit -> Stream

    /// Relative path inside the NuGet package archive, such as `/lib/net40/My.dll`.
    abstract TargetPath : string

[<Sealed>]
type NuGetFile =

    /// Reads a local file as a `INuGetFile` file.
    static member Local(sourcePath: string, targetPath: string) =
        {
            new INuGetFile with
                member p.Read() = File.Open(sourcePath, FileMode.Open) :> Stream
                member p.TargetPath = targetPath
        }

    /// Reads a library file as an `INuGetFile` in a `lib/netXX` folder.
    static member LibraryFile(framework: Framework, sourcePath: string) =
        NuGetFile.Local(sourcePath, "/lib/" + framework.Name + "/" + Path.GetFileName sourcePath)

/// A project that contirbutes files to a created NuGet package.
type INuGetExportingProject =

    /// Files exported by the project.
    abstract NuGetFiles : seq<INuGetFile>
