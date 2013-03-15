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

/// Provides a facility to generate VisualStudio extension `.vsix` files
/// using the 2010 VSIX format compatible with VisualStudio 2010 and VisualStudio 2012,
/// see <http://msdn.microsoft.com/en-us/library/vstudio/dd393754(v=vs.100).aspx>
/// Quickstart: use the static methods on `VsixFile` to construct
/// an in-memory `.vsix` representation you can then manipulate.
module IntelliFactory.Build.VsixExtensions

open System
open System.Globalization
module F = FileSystem
module V = VsixPackages

/// Represents VisualStudio editions.
type VSEdition =
    | IntegratedShell
    | Express_All
    | Premium
    | Pro
    | Ultimate
    | VBExpress
    | VCExpress
    | VCSExpress
    | VWDExpress

/// Represents a VisualStudio product entry.
type VSProduct =
    {
        mutable Editions : list<VSEdition>
        mutable Version : string
    }

    /// Constructs a new VisualStudio product entry.
    static member Create : ver: string -> eds: seq<VSEdition> -> VSProduct

/// Represents an Isolated Shell application product entry.
type IsolatedShellProduct =
    {
        mutable Name : string
        mutable Version : option<string>
    }

    /// Constructs the `IsolatedShellProduct`.
    static member Create : name: string -> IsolatedShellProduct

/// Unifies different kinds of supported product declarations.
type SupportedProduct =
    | VS of VSProduct
    | IS of IsolatedShellProduct

/// Represents .NET framework versions.
type FrameworkVersion =
    | NET20
    | NET35
    | NET40
    | NET45

/// Represents a range of supported frameworks.
type SupportedFrameworks =
    {
        mutable Max : FrameworkVersion
        mutable Min : FrameworkVersion
    }

    /// Constructs the range.
    static member Create : min: FrameworkVersion -> max: FrameworkVersion -> SupportedFrameworks

/// Represents extension identification.
type Identifier =
    {
        mutable Author : string
        mutable Culture : CultureInfo
        mutable Description : string
        mutable Id : V.Identity
        mutable Name : string
        mutable Products : list<SupportedProduct>
        mutable SupportedFrameworks : SupportedFrameworks
        mutable Version : Version
    }

    /// Creates a bare-bones identification section.
    static member Create : author: string -> id: V.Identity -> name: string -> desc: string -> Identifier

/// Represents installed assemblies.
type Assembly =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    /// Creates a new `Assembly`.
    static member Create : path: string -> content: F.Content -> Assembly

/// Represents installed custom extensions.
type CustomExtension =
    {
        mutable Content : F.Content
        mutable Path : string
        mutable Type : string
    }

    /// Constructs a new `CustomExtension`.
    static member Create : path: string -> F.Content -> CustomExtension

/// Represents installed project templates.
type ProjectTemplate =
    {
        mutable Archive : VSTemplates.Archive
        mutable Category : list<string>
        mutable Definition : VSTemplates.ProjectTemplate
    }

    /// Creates a new `Template`.
    static member Create : category: seq<string> -> archive: VSTemplates.ProjectTemplate -> ProjectTemplate

/// Represents installed MEF components.
type MEFComponent =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    /// Constructs a new `MEFComponent`.
    static member Create : path: string -> content: F.Content -> MEFComponent

/// Represents installed toolbox controls.
type ToolboxControl =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    /// Constructs a new `ToolboxControl`.
    static member Create : path: string -> content: F.Content -> ToolboxControl

/// Represents installed studio packages.
type VSPackage =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    /// Constructs a new `VSPackage`.
    static member Create : path: string -> content: F.Content -> VSPackage

/// Unifies different types of content declarations.
type VsixContent =
    | AssemblyContent of Assembly
    | CustomExtensionContent of CustomExtension
    | MEFComponentContent of MEFComponent
    | ProjectTemplateContent of ProjectTemplate
    | ToolboxControlContent of ToolboxControl
    | VSPackageContent of VSPackage

    /// Helper for quick definition of template contents.
    static member ProjectTemplate : category: seq<string> -> archive: VSTemplates.ProjectTemplate -> VsixContent

/// Respresents the top-level configuration element.
type Vsix =
    {
        mutable Identifier : Identifier
        mutable Contents : list<VsixContent>
    }

    /// Constructs a new `Vsix` element.
    static member Create : Identifier -> seq<VsixContent> -> Vsix

/// Represents an in-memory VSIX package.
type VsixFile =
    {
        FileName : string
        Zip : F.Binary
    }

    /// Writes the `.vsix` file to a directory.
    member WriteToDirectory : fullPath: string -> unit

    /// Creates a `VsixFile`.
    static member Create : fileName: string -> vsix: Vsix -> VsixFile

    /// Reads a `.vsix` file.
    static member FromFile : fullPath: string -> VsixFile

