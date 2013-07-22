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

type internal FSharpKind =
    | FSharpConsoleExecutable
    | FSharpLibrary
    | FSharpWindowsExecutable

/// Global parameters building for F# projects.
module FSharpConfig =

    /// The base directory against which project-local paths are resolved.
    val BaseDir : Parameter<string>

    /// Path to the generated XML file, if one is desired.
    val DocPath : Parameter<option<string>>

    /// Paths to resources to embed into the assembly.
    val EmbeddedResources : Parameter<seq<string>>

    /// F# home directory where `fsc.exe` resides.
    val FSharpHome : Parameter<string>

    /// Extra flags to pass to the F# compiler.
    val OtherFlags : Parameter<seq<string>>

    /// The primary DLL or EXE output path.
    val OutputPath : Parameter<string>

    /// If set, gives the path of the the MSBuild project file to generate.
    /// The generated file will contain all resolved references.
    val ReferenceProjectPath : Parameter<option<string>>

    /// References to resovle before building the project.
    val References : Parameter<seq<Reference>>

    /// Paths to source files to compile.
    val Sources : Parameter<seq<string>>

    /// The kind of project to build.
    val internal Kind : Parameter<FSharpKind>

/// Internal facade for the F# builder.
[<Sealed>]
type internal FSharpCompilerTask =
    new : Parameters * Log * ResolvedReferences -> FSharpCompilerTask
    member Build : unit -> unit
    member Arguments : seq<string>
    member ToolPath : string

/// Internal utility for generating F# projects.
[<Sealed>]
type internal FSharpProjectWriter =
    new : IParametric -> FSharpProjectWriter
    member Write : ResolvedReferences -> unit

/// Represents an F# project building a single assembly.
[<Sealed>]
type FSharpProject =
    interface IFSharpProjectContainer<FSharpProject>
    interface INuGetExportingProject
    interface IParametric<FSharpProject>
    interface IProject

and IFSharpProjectContainer<'T> =
    abstract FSharpProject : FSharpProject
    abstract WithFSharpProject : FSharpProject -> 'T

[<AutoOpen>]
module FSharpProjectExtensinos =

    type IFSharpProjectContainer<'T> with

        /// Adds resource files to embed.
        member Embed : seq<string> -> 'T

        /// A shorthand for adding sources, for every module name `N`,
        /// `N.fsi` and `N.fs` are included automatically.
        member Modules : seq<string> -> 'T

        /// Adds references to the Project.
        member References : def: (ReferenceBuilder -> #seq<Reference>) -> 'T

        /// Adds paths to F# sources.
        member Sources : seq<string> -> 'T

        /// Approximately parses an MSBuild project file looking for `Compile` declarations.
        /// Adds all files it finds to the current project sources.
        /// If no project file is given, infers it from the project name.
        member SourcesFromProject : ?msBuildProject: string -> 'T

/// F#-related build facilities.
[<Sealed>]
type FSharpTool =

    /// Creates a console (exe) project.
    member ConsoleExecutable : name: string -> FSharpProject

    /// Executs an F# script in a sub-process.
    member ExecuteScript : scriptFile: string * ?refs: ResolvedReferences * ?args: seq<string> -> unit

    /// Creates a library project.
    member Library : name: string -> FSharpProject

    /// Creates a non-console executable (winexe) project.
    member WindowsExecutable : name: string -> FSharpProject

    /// Current `FSharpProjects` instance.
    static member internal Current : Parameter<FSharpTool>
