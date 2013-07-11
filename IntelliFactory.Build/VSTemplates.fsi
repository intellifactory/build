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

/// Provides a facility to generate VisualStudio template archive files.
/// These typically have a `.zip` extension and contain `.vstemplate` XML manifests.
/// Quickstart: use the static methods on `Archive` to construct
/// an in-memory `.zip` representation you can then manipulate.
module IntelliFactory.Build.VSTemplates

//open System
//module F = FileSystem
//module V = VsixPackages
//
///// Defines a project item corresponding to the `ProjectItem` XML element
///// within VisualStudio project templates.
///// See <http://msdn.microsoft.com/en-us/library/ys81cc94.aspx>
//type ProjectItem =
//    {
//        mutable Content : F.Content
//        mutable FileName : string
//        mutable ReplaceParameters : bool
//        mutable TargetFileName : option<string>
//    }
//
//    /// Creates a new `ProjectItem` from explicit components.
//    static member Create : fileName: string -> content: F.Content -> ProjectItem
//
//    /// Creates a new `ProjectItem` by reading a binary file.
//    static member FromBinaryFile : fullPath: string -> ProjectItem
//
//    /// Creates a new `ProjectItem` by reading a text file.
//    static member FromTextFile : fullPath: string -> ProjectItem
//
///// Subtypes for `Item`.
///// See <http://msdn.microsoft.com/en-us/library/ms171408(v=vs.80).aspx>
//type ItemSubType =
//    | Form
//    | Component
//    | CustomControl
//    | UserControl
//
///// Corresponds to the `ProjectItem` XML element
///// describing a VisualStudio item template.
///// See <http://msdn.microsoft.com/en-us/library/ms171408.aspx>
//type Item =
//    {
//        mutable Content : F.Content
//        mutable CustomTool : option<string>
//        mutable FileName: string
//        mutable ItemType : option<string>
//        mutable ReplaceParameters : bool
//        mutable SubType : option<ItemSubType>
//        mutable TargetFileName : option<string>
//    }
//
//    /// Creates a new `ProjectItem` from explicit components.
//    static member Create : fileName: string -> content: F.Content -> Item
//
//    /// Creates a new `ProjectItem` by reading a binary file.
//    static member FromBinaryFile : fullPath: string -> Item
//
//    /// Creates a new `ProjectItem` by reading a text file.
//    static member FromTextFile : fullPath: string -> Item
//
///// Defines a folder of project items corresponding to the `Folder` XML element.
///// See <http://msdn.microsoft.com/en-US/library/ahkztdcb(v=vs.110).aspx>
//type Folder =
//    {
//        mutable Elements : list<FolderElement>
//        mutable Name : string
//        mutable TargetFolderName : option<string>
//    }
//
//    /// Creates a new `Folder` explicitly.
//    static member Create : name: string -> elements: seq<FolderElement> -> Folder
//
///// Represents contents of a folder.
//and FolderElement =
//    | NestedFolder of Folder
//    | NestedProjectItem of ProjectItem
//
///// Represents a project corresponding to the `Project` XML element.
///// See <http://msdn.microsoft.com/en-US/library/ms171401.aspx>
//type Project =
//    {
//        mutable Content : F.Content
//        mutable Elements : list<FolderElement>
//        mutable FileName : string
//        mutable IgnoreProjectParameter : option<string>
//        mutable ReplaceParameters : bool
//        mutable TargetFileName : option<string>
//    }
//
//    /// Creates a new `Project` from explicit components.
//    static member Create :
//        fileName: string ->
//        content: string ->
//        elements: seq<FolderElement> ->
//        Project
//
//    /// Creates a new `Project` by reading a file.
//    static member FromFile :
//        fullPath: string ->
//        elements: seq<FolderElement> ->
//        Project
//
///// Represents project types.
//type ProjectType =
//    | CSharp
//    | FSharp
//    | VisualBasic
//    | Web
//
///// Represents icons.
//type Icon =
//    {
//        mutable Binary : F.Binary
//        mutable FileName : string
//    }
//
//    /// Creates explicitly.
//    static member Create : fileName: string -> binary: F.Binary -> Icon
//
//    /// Creates from a file.
//    static member FromFile : fullPath: string -> Icon
//
///// Describes templates, corresponds to the `TemplateData` XML element.
//type TemplateData =
//    {
//        mutable DefaultName : option<string>
//        mutable Description : string
//        mutable Icon : Icon
//        mutable Name : string
//        mutable ProjectSubType : option<string>
//        mutable ProjectType : ProjectType
//        mutable PromptForSaveOnCreation : bool
//        mutable SortOrder : option<int>
//    }
//
//    /// Creates with given required parameters.
//    static member Create : ty: ProjectType -> name: string -> desc: string -> icon: Icon -> TemplateData
//
///// Represents VisualStudio versions.
//type VisualStudioVersion =
//    | VisualStudio2008
//    | VisualStudio2010
//    | VisualStudio2012
//
///// The template kind used for installation.
//type TemplateKind =
//    | ItemTemplateKind
//    | ProjectTemplateKind
//
///// Describes template installation options.
//type InstallConfig =
//    {
//        mutable Category : list<string>
//        mutable VisualStudio : VisualStudioVersion
//    }
//
//    /// Creates a new `InstallConfig`.
//    static member Create : category: seq<string> -> studio: VisualStudioVersion -> InstallConfig
//
///// The NuGet packages required by the project templates.
//type NuGetPackages =
//    {
//        /// The unique identifier of the parent VSIX package.
//        Identity : V.Identity
//
//        /// The list of package references.
//        Packages : list<NuGetUtils.Package>
//    }
//
//    /// Constructs a new instance.
//    static member Create : V.Identity -> seq<NuGetUtils.Package> -> NuGetPackages
//
///// Corresponds to the `VSTemplate` element of type `Project`.
//type ProjectTemplate =
//    {
//        mutable NuGetPackages : option<NuGetPackages>
//        mutable Project : Project
//        mutable TemplateData : TemplateData
//        mutable Version : string
//    }
//
//    /// Creates a new ProjectTemplate.
//    static member Create : TemplateData -> Project -> ProjectTemplate
//
///// An in-memory representation of a VisualStudio `.vstemplate` file.
//type Archive =
//    {
//        Kind : TemplateKind
//        Zip : F.Binary
//        ZipFileName : string
//    }
//
//    /// Attempts to locally install the template.
//    member Install : InstallConfig -> bool
//
//    /// Attempts to locally uninstall the template.
//    member Uninstall : InstallConfig -> bool
//
//    /// Writes the zip file to a given directory.
//    member WriteToDirectory : fullPath : string -> unit
//
//    /// Reads a specific zip file.
//    static member FromFile : kind: TemplateKind -> fullPath: string -> Archive
//
//    /// Constructs a project template.
//    static member Project : project: ProjectTemplate -> Archive
//
//    (* TODO: item templates, multi-project (project group) templates *)
