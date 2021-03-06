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

namespace IntelliFactory.Core

/// Facilities related to [NuGet][ng].
/// [ng]: http://nuget.org
module NuGetTools =
    open System
    open System.IO
    open System.Xml
    type FrameworkVersion = RuntimeFrameworks.FrameworkVersion
    type SemanticVersion = SemanticVersions.SemanticVersion
    type SubCollection<'R,'T> = SubCollections.SubCollection<'R,'T>
    type VersionRange = SemanticVersions.Range

    /// Helper record for `PackageSpec`.
    type PackageSpecMinimal =
        {
            /// A list of authors of the package code.
            Authors : list<string>

            /// A long description of the package.
            Description : string

            /// The unique identifier for the package.
            Id : string

            /// The version of the package.
            Version : SemanticVersion
        }

    /// Represents a package dependency in a `PackageSpec`.
    [<Sealed>]
    type PackageDependencySpec =

        /// The package identifier.
        member Id : string

        /// The framework version to specialize to, if any.
        member FrameworkVersion : option<FrameworkVersion>

        /// The range of the package dependency.
        member Range : option<VersionRange>

        /// Creates a new instance.
        static member Create :
            id: string
            * ?range: VersionRange
            * ?frameworkVersion: FrameworkVersion ->
            PackageDependencySpec

    /// Represents a reference to an assembly shipped with the current package
    /// in a `PackageSpec`. Corresponds to a `<reference/>` element.
    [<Sealed>]
    type AssemblyReferenceSpec =

        /// The file name of the assembly without the path, like `xunit.dll`.
        member AssemblyFileName : string

        /// The framework version to specialize to, if any.
        member FrameworkVersion : option<FrameworkVersion>

        /// Creates a new instance.
        static member Create :
            assemblyFileName: string
            * ?frameworkVersion: FrameworkVersion ->
            AssemblyReferenceSpec

    /// Represents a reference to a framework assembly in a `PackageSpec`.
    /// Corresponds to a `<frameworkAssembly>` element.
    [<Sealed>]
    type FrameworkAssemblySpec =

        /// The name of the assembly, as in `System.ServiceModel`.
        member AssemblyName : string

        /// Optional `targetFramework` attribute.
        member TargetFramework : option<FrameworkVersion>

        /// Creates a new instance.
        static member Create :
            assemblyName: string
            * ?targetFramework: FrameworkVersion ->
            FrameworkAssemblySpec

    /// Represents a file rewrite specification in a `PackageSpec`.
    /// Corresponds to a `<file>` element.
    [<Sealed>]
    type FileRule =

        /// Files or patterns to exclude.
        member Exclude : option<string>

        /// Source pattern to evaluate.
        member SourcePattern : string

        /// Target path for the rule.
        member TargetPath : string

        /// Creates a new instance.
        static member Create :
            sourcePattern: string
            * targetPath: string
            * ?exclude: string ->
            FileRule

    /// Represents a [package specification][nuspec].
    ///
    /// [nuspec]: http://docs.nuget.org/docs/reference/nuspec-reference
    [<Sealed>]
    type PackageSpec =

        /// Updates metadata for the package spec.
        member Update :
            ?id: string
            * ?version: SemanticVersion
            * ?title: string
            * ?description: string
            * ?releaseNotes: string
            * ?summary: string
            * ?language: string
            * ?projectUrl: string
            * ?iconUrl: string
            * ?licenseUrl: string
            * ?copyright: string
            * ?requireLicenseAcceptance: bool
            * ?minClientVersion: Version ->
            PackageSpec

        /// Gets the XML representation.
        member ToXml : unit -> string

        /// Writes the XML representation.
        member WriteXml : TextWriter ->  unit

        /// Package authors.
        member Authors : SubCollection<PackageSpec,string>

        /// Package copyright information.
        member Copyright : option<string>

        /// Package dependencies.
        member Dependencies : SubCollection<PackageSpec,PackageDependencySpec>

        /// Package description.
        member Description : string

        /// Framework assemblies to reference.
        member FrameworkAssemblies : SubCollection<PackageSpec,FrameworkAssemblySpec>

        /// Address of the package icon.
        member IconUrl : option<string>

        /// Package identifier.
        member Id : string

        /// Language localte, for example `en-US`.
        member Language : option<string>

        /// The URL to the package license.
        member LicenseUrl : option<string>

        /// Min version of NuGet this package is usable with.
        member MinClientVersion : option<Version>

        /// Package owners.
        member Owners : SubCollection<PackageSpec,string>

        /// Package homepage.
        member ProjectUrl : option<string>

        /// Assemblies to reference.
        member References  : SubCollection<PackageSpec,AssemblyReferenceSpec>

        /// Release notes.
        member ReleaseNotes : option<string>

        /// A flag indicating whether UI tools should require the
        /// package users to explicitly accept the license.
        member RequireLicenseAcceptance : bool

        /// Package summary.
        member Summary : option<string>

        /// Package tags.
        member Tags : SubCollection<PackageSpec,string>

        /// Package title.
        member Title : option<string>

        /// Package version.
        member Version : SemanticVersion

        /// Creates a new `PackageSpec`.
        static member Create : PackageSpecMinimal -> PackageSpec
