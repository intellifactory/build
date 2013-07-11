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

/// Experimental support for convention-based matrix F# builds.
/// API subject to change - use at your own risk.
module IntelliFactory.Build.CommonBuildSetup

//open System
//open System.Runtime.Versioning
//
///// Enumerates framework versions.
//type FrameworkVersion =
//    | Net20
//    | Net35
//    | Net40
//    | Net45
//
//    /// Gets the MSBuild form such as "v4.0"
//    member GetMSBuildLiteral : unit -> string
//
//    /// Gets the NuGet form such as "net40"
//    member GetNuGetLiteral : unit -> string
//
//    /// Gets the corresponding FrameworkName.
//    member ToFrameworkName : unit -> FrameworkName
//
///// Represents a build confiugration for a project.
//type BuildConfiguration =
//    {
//        /// The name of the configuration, such as Release.
//        ConfigurationName : string
//
//        /// Flag determining if this is a Debug configuration
//        Debug : bool
//
//        /// The target framework.
//        FrameworkVersion : FrameworkVersion
//
//        /// The NuGet dependencies for this configuration.
//        NuGetDependencies : NuGet. PackageDependencySet
//    }
//
///// Configures MSBuild.
//type MSBuildOptions =
//    {
//        /// The specific build configuraiton. If None, will build all.
//        BuildConfiguration : option<BuildConfiguration>
//
//        /// The properties to pass to MSBuild; defaults like
//        /// Configuration will be set automatically.
//        Properties : Map<string,string>
//
//        /// The targets to run.
//        Targets : list<string>
//    }
//
///// Represents an project that is part of the solution.
//type Project =
//    {
//        /// List of configurations in which the project is built.
//        BuildConfigurations : list<BuildConfiguration>
//
//        /// Full path to the MSBuild project file, if present.
//        MSBuildProjectFilePath : option<string>
//
//        /// The name of the project.
//        Name : string
//    }
//
//    /// Builds the project with in-process MSBuild.
//    member MSBuild : ?options: MSBuildOptions -> Async<unit>
//
///// Descripts metadata for the solution.
//type Metadata =
//    {
//        mutable AssemblyVersion : option<Version>
//        mutable Author : option<string>
//        mutable Description : option<string>
//        mutable FileVersion : option<Version>
//        mutable Product : option<string>
//        mutable VersionSuffix : option<string>
//        mutable Website : option<string>
//    }
//
//    /// Constructs a default Metadata record.
//    static member Create : unit -> Metadata
//
///// Represents a solution to build.
//[<Sealed>]
//type Solution =
//
//    /// Constructs a solution representation.
//    new : rootDir: string -> Solution
//
//    /// Builds the solution with in-process MSBuild.
//    member MSBuild : ?options: MSBuildOptions -> Async<unit>
//
//    /// The solution metadata.
//    member Metadata : Metadata with get, set
//
//    /// A unique prefix to distinguish multiple solutions.
//    member Prefix : option<string> with get, set
//
//    /// The projects to build within the solution.
//    member Projects : list<Project> with get, set
//
//    /// The root directory as passed to the constructor.
//    member RootDirectory : string
//
///// Generates boostrapping MSBuild boilerplate in a given folder.
//val Prepare : trace: (string -> unit) -> solutionDir: string -> unit
