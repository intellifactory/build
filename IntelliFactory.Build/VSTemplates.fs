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

module IntelliFactory.Build.VSTemplates

open System
open System.IO
open System.Text
open Microsoft.Win32
open Ionic.Zip

module F = FileSystem
module V = VsixPackages
module X = XmlGenerator

[<AutoOpen>]
module Util =

    let XmlNamespace = "http://schemas.microsoft.com/developer/vstemplate/2005"
    let XmlElement name = X.Element.Create(name, XmlNamespace)

    type ElementBuilder =
        | E

        static member ( ? ) (self: ElementBuilder, name: string) =
            XmlElement name

    type AttributeBuilder =
        | A

        static member ( ? ) (self: AttributeBuilder, name: string) =
            fun (value: string) -> (name, value)

    let ( ==> ) (x: option<'T>) (render: 'T -> 'R) : list<'R> =
        match x with
        | None -> []
        | Some x -> [render x]

    let BoolToString (b: bool) : string =
        if b then "true" else "false"

    let IntToString (x: int) : string =
        string x

type ProjectItem =
    {
        mutable Content : F.Content
        mutable FileName : string
        mutable ReplaceParameters : bool
        mutable TargetFileName : option<string>
    }

    member this.ToXml() =
        let attrs =
            [
                yield A?ReplaceParameters (BoolToString this.ReplaceParameters)
                yield! this.TargetFileName ==> A?TargetFileName
            ]
        E?ProjectItem + attrs -- this.FileName

    static member Create(fileName: string)(content: F.Content) : ProjectItem =
        {
            Content = content
            FileName = fileName
            ReplaceParameters = false
            TargetFileName = None
        }

    static member FromBinaryFile(fullPath: string) : ProjectItem =
        let fileName = Path.GetFileName fullPath
        ProjectItem.Create fileName (F.Content.ReadBinaryFile fullPath)

    static member FromTextFile(fullPath: string) : ProjectItem =
        let fileName = Path.GetFileName fullPath
        ProjectItem.Create fileName (F.Content.ReadTextFile fullPath)

type ItemSubType =
    | Form
    | Component
    | CustomControl
    | UserControl

    member this.GetLiteral() =
        match this with
        | Form -> "Form"
        | Component -> "Component"
        | CustomControl -> "CustomControl"
        | UserControl -> "UserControl"

    override this.ToString() =
        this.GetLiteral()

type Item =
    {
        mutable Content : F.Content
        mutable CustomTool : option<string>
        mutable FileName: string
        mutable ItemType : option<string>
        mutable ReplaceParameters : bool
        mutable SubType : option<ItemSubType>
        mutable TargetFileName : option<string>
    }

    member this.ToXml() =
        let i = this
        let attrs =
            [
                yield A?ReplaceParameters (BoolToString i.ReplaceParameters)
                yield! i.TargetFileName ==> A?TargetFileName
                yield! i.CustomTool ==> A?CustomTool
                yield! i.ItemType ==> A?ItemType
                yield! i.SubType ==> fun x -> A?SubType (x.GetLiteral())
            ]
        E?ProjectItem + attrs -- this.FileName

    static member Create(fileName: string)(content: F.Content) : Item =
        {
            Content = content
            CustomTool = None
            FileName = fileName
            ItemType = None
            ReplaceParameters = false
            SubType = None
            TargetFileName = None
        }

    static member FromBinaryFile(fullPath: string) : Item =
        let fileName = Path.GetFileName fullPath
        Item.Create fileName (F.Content.ReadBinaryFile fullPath)

    static member FromTextFile(fullPath: string) : Item =
        let fileName = Path.GetFileName fullPath
        Item.Create fileName (F.Content.ReadTextFile fullPath)

type Folder =
    {
        mutable Elements : list<FolderElement>
        mutable Name : string
        mutable TargetFolderName : option<string>
    }

    member this.ToXml() =
        let attrs =
            [
                yield A?Name this.Name
                yield! this.TargetFolderName ==> A?TargetFolderName
            ]
        E?Folder + attrs - Seq.cache (this.Elements |> Seq.map (fun x -> x.ToXml()))

    static member Create(name: string)(elements: seq<FolderElement>) : Folder =
        {
            Elements = List.ofSeq elements
            Name = name
            TargetFolderName = None
        }

and FolderElement =
    | NestedFolder of Folder
    | NestedProjectItem of ProjectItem

    member this.ToXml() : X.Element =
        match this with
        | NestedFolder f -> f.ToXml()
        | NestedProjectItem i -> i.ToXml()

type ProjectConfig =
    {
        IgnoreProjectParameter : option<string>
        ReplaceParameters : bool
        TargetFileName : option<string>
    }

    static member Default =
        {
            IgnoreProjectParameter = None
            ReplaceParameters = false
            TargetFileName = None
        }

type Project =
    {
        mutable Content : F.Content
        mutable Elements : list<FolderElement>
        mutable FileName : string
        mutable IgnoreProjectParameter : option<string>
        mutable ReplaceParameters : bool
        mutable TargetFileName : option<string>
    }

    member this.ToXml() =
        let p = this
        let attrs =
            [
                yield A?File this.FileName
                yield A?ReplaceParameters (BoolToString p.ReplaceParameters)
                yield! p.TargetFileName ==> A?TargetFileName
                yield! p.IgnoreProjectParameter ==> A?IgnoreProjectParameter
            ]
        E?Project + attrs - Seq.cache (this.Elements |> Seq.map (fun x -> x.ToXml()))

    static member Create(fileName: string)(content: string)(elements: seq<FolderElement>) =
        {
            Content = F.TextContent content
            Elements = Seq.toList elements
            FileName = fileName
            IgnoreProjectParameter = None
            ReplaceParameters = false
            TargetFileName = None
        }

    static member FromFile(fullPath: string)(elements: seq<FolderElement>) =
        let fileName = Path.GetFileName fullPath
        Project.Create fileName (File.ReadAllText fullPath) elements

type Icon =
    {
        mutable Binary : F.Binary
        mutable FileName : string
    }

    static member Create(fileName: string)(binary: F.Binary) : Icon =
        {
            Binary = binary
            FileName = fileName
        }

    static member FromFile(fullPath: string) : Icon =
        let fileName = Path.GetFileName fullPath
        Icon.Create fileName (F.Binary.ReadFile fullPath)

type ProjectType =
    | CSharp
    | FSharp
    | VisualBasic
    | Web

    member this.GetLiteral() =
        match this with
        | CSharp -> "CSharp"
        | FSharp -> "FSharp"
        | VisualBasic -> "VisualBasic"
        | Web -> "Web"

    override this.ToString() =
        this.GetLiteral()

type TemplateData =
    {
        mutable DefaultName : option<string>
        mutable Description : string
        mutable Icon : Icon
        mutable Name : string
        mutable ProjectSubType : option<string>
        mutable ProjectType : ProjectType
        mutable PromptForSaveOnCreation : bool
        mutable SortOrder : option<int>
    }

    static member Create ty name desc icon : TemplateData =
        {
            DefaultName = None
            Description = desc
            Icon = icon
            Name = name
            PromptForSaveOnCreation = true
            ProjectSubType = None
            ProjectType = ty
            SortOrder = None
        }

    member this.ToXml() =
        let d = this
        let content =
            [
                yield! d.DefaultName ==> fun x -> E?DefaultName -- x
                yield! d.SortOrder ==> fun x -> E?SortOrder -- IntToString x
                match d.ProjectSubType with
                | None ->
                    match d.ProjectType with
                    | Web -> yield E?ProjectSubType -- CSharp.GetLiteral()
                    | _ -> ()
                | Some s -> yield E?ProjectSubType -- s
            ]
            |> List.append
                [
                    E?Name -- d.Name
                    E?Description -- d.Description
                    E?Icon -- d.Icon.FileName
                    E?ProjectType -- d.ProjectType.GetLiteral()
                    E?PromptForSaveOnCreation -- BoolToString this.PromptForSaveOnCreation
                ]
        E?TemplateData - content

type NuGetPackages =
    {
        Identity : V.Identity
        Packages : list<NuGetUtils.Package>
    }

    static member Create id ps = { Identity = id; Packages = Seq.toList ps }

type ProjectTemplate =
    {
        mutable NuGetPackages : option<NuGetPackages>
        mutable Project : Project
        mutable TemplateData : TemplateData
        mutable Version : string
    }

    static member Create d p =
        {
            NuGetPackages = None
            Project = p
            TemplateData = d
            Version = "3.0.0"
        }

    member this.ToXml() =
        E?VSTemplate + [A?Version this.Version; A?Type "Project"] - [
            yield this.TemplateData.ToXml()
            yield E?TemplateContent - [this.Project.ToXml()]
            match this.NuGetPackages with
            | None -> ()
            | Some pkgs ->
                yield E?WizardExtension - [
                    E?Assembly -- "NuGet.VisualStudio.Interop, Version=1.0.0.0, \
                        Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
                    E?FullClassName -- "NuGet.VisualStudio.TemplateWizard"
                ]
                yield E?WizardData - [
                    E?packages + [A?repository "extension"; A?repositoryId (pkgs.Identity.GetFullId())]- [
                        for p in pkgs.Packages ->
                            E?package + [A?id p.Name; A?version (string p.Version)]
                    ]
                ]

        ]

let AddContent (zip: ZipFile) (path: string) (content: F.Content) =
    match content with
    | F.BinaryContent b -> zip.AddEntry(path, b.GetBytes())
    | F.TextContent t -> zip.AddEntry(path, t, F.DefaultEncoding)
    |> ignore

let rec AddEntries (zip: ZipFile) (path: string) (e: FolderElement) =
    match e with
    | NestedFolder f ->
        let subPath = Path.Combine(path, f.Name)
        for e in f.Elements do
            AddEntries zip subPath e
    | NestedProjectItem i ->
        let p = Path.Combine(path, i.FileName)
        AddContent zip p i.Content

type VisualStudioVersion =
    | VisualStudio2008
    | VisualStudio2010
    | VisualStudio2012

    member this.Code =
        match this with
        | VisualStudio2008 -> "9.0"
        | VisualStudio2010 -> "10.0"
        | VisualStudio2012 -> "11.0"

type TemplateKind =
    | ItemTemplateKind
    | ProjectTemplateKind

let TemplateLocation (version: VisualStudioVersion) (kind: TemplateKind) =
    let ( ? ) (x: RegistryKey) y = x.OpenSubKey(y)
    let ms = Registry.CurrentUser?Software?Microsoft
    match ms?VisualStudio with
    | null -> None
    | vs ->
        match (?) vs version.Code with
        | null -> None
        | hive ->
            let s =
                let obj =
                    match kind with
                    | ItemTemplateKind -> "UserItemTemplatesLocation"
                    | ProjectTemplateKind -> "UserProjectTemplatesLocation"
                    |> hive.GetValue
                obj.ToString()
            if String.IsNullOrEmpty(s) then None else Some s

type InstallConfig =
    {
        mutable Category : list<string>
        mutable VisualStudio : VisualStudioVersion
    }

    member this.Locate(templateKind) =
        let baseDir = TemplateLocation this.VisualStudio templateKind
        match baseDir with
        | None -> None
        | Some bD ->
            this.Category
            |> Seq.fold (fun d x -> Path.Combine(d, x)) bD
            |> Some

    static member Create c v =
        {
            Category = List.ofSeq c
            VisualStudio = v
        }

type Archive =
    {
        Kind : TemplateKind
        Zip : F.Binary
        ZipFileName : string
    }

    member this.WriteToDirectory(fullPath: string) =
        if Directory.Exists fullPath |> not then
            Directory.CreateDirectory fullPath |> ignore
        Path.Combine(fullPath, this.ZipFileName)
        |> this.Zip.WriteFile

    member this.Install(config: InstallConfig) =
        match config.Locate(this.Kind) with
        | None -> false
        | Some dir -> this.WriteToDirectory(dir); true

    member this.Uninstall(config: InstallConfig) =
        match config.Locate(this.Kind) with
        | None -> false
        | Some dir ->
            let path = Path.Combine(dir, this.ZipFileName)
            if File.Exists path
                then File.Delete path; true
                else false

    static member FromFile(kind)(fullPath: string) : Archive =
        {
            Kind = kind
            ZipFileName = Path.GetFileName fullPath
            Zip = F.Binary.ReadFile fullPath
        }

    static member Project(p: ProjectTemplate) : Archive=
        use zip = new ZipFile(F.DefaultEncoding)
        let xml = X.Write (p.ToXml())
        let zipName = Path.ChangeExtension(p.Project.FileName, ".zip")
        let icon = p.TemplateData.Icon
        AddContent zip p.Project.FileName p.Project.Content
        let _ = zip.AddEntry(Path.ChangeExtension(p.Project.FileName, ".vstemplate"), xml)
        let _ = zip.AddEntry(icon.FileName, icon.Binary.GetBytes())
        Seq.iter (AddEntries zip ".") p.Project.Elements
        use stream = new MemoryStream()
        zip.Save(stream)
        let bytes = stream.ToArray()
        {
            Kind = ProjectTemplateKind
            Zip = F.Binary.FromBytes bytes
            ZipFileName = zipName
        }
