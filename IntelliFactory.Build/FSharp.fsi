// Copyright 2013 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.

namespace IntelliFactory.Build

module FSharpConfig =
    val Home : Parameter<string>

[<Sealed>]
type FSharpProject =
    interface IProject
    interface INuGetExportingProject

    /// Sets the base directory of the Project.
    member BaseDir : string -> FSharpProject

    /// Sets the project identifier (defaults to the project name).
    member Id : string -> FSharpProject

    /// A shorthand for adding sources, for every module name `N`,
    /// `N.fsi` and `N.fs` are included automatically.
    member Modules : seq<string> -> FSharpProject

    /// Adds miscellaneous F# flags to pass as arguments to FSC.
    member Flags : seq<string> -> FSharpProject

    /// Adds references to the Project.
    member References : def: (ReferenceBuilder -> #seq<Reference>) -> FSharpProject

    /// Adds paths to F# sources.
    member Sources : seq<string> -> FSharpProject

    /// Approximately parses an MSBuild project file looking for `Compile` declarations.
    /// Adds all files it finds to the current project sources.
    /// If no project file is given, infers it from the project name.
    member SourcesFromProject : ?msBuildProject: string -> FSharpProject

    /// The generated library and documentation files (DLL, XML).
    member LibraryFiles : seq<string>

[<Sealed>]
type FSharpProjects =
    member ConsoleExecutable : name: string -> FSharpProject
    member Library : name: string -> FSharpProject
    member WindowsExecutable : name: string -> FSharpProject
    static member internal Current : Parameter<FSharpProjects>
