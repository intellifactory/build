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

/// Attempt to target the 2010 format:
/// http://msdn.microsoft.com/en-us/library/vstudio/dd393754(v=vs.100).aspx
module IntelliFactory.Build.VsixExtensions

open System
open System.Globalization
open System.IO
open Ionic.Zip
module F = FileSystem
module X = XmlGenerator

[<AutoOpen>]
module Util =

    let XmlNamespace = "http://schemas.microsoft.com/developer/vsx-schema/2010"
    let XmlElement name = X.Element.Create(name, XmlNamespace)

    type ElementBuilder =
        | E

        static member ( ? ) (self: ElementBuilder, name: string) = XmlElement name

    type AttributeBuilder =
        | A

        static member ( ? ) (self: AttributeBuilder, name: string) =
            fun (value: string) -> (name, value)

    /// See <http://msdn.microsoft.com/en-us/library/dd997170.aspx>
    let InferContentTypeByExtension (ext: string) =
        match ext.ToLower().TrimStart('.') with
        | "txt" -> "text/plain"
        | "pkgdef" -> "text/plain"
        | "xml" -> "text/xml"
        | "vsixmanifest" -> "text/xml"
        | "htm" | "html" -> "text/html"
        | "rtf" -> "application/rtf"
        | "pdf" -> "application/pdf"
        | "gif" -> "image/gif"
        | "jpg" | "jpeg" -> "image/jpg"
        | "tiff" -> "image/tiff"
        | "vsix" | "zip" -> "application/zip"
        | _ -> "application/octet-stream"

    let GenerateContentTypesXml (paths: seq<string>) =
        let extensions =
            paths
            |> Seq.map Path.GetExtension
            |> Seq.distinct
            |> Seq.append [".xml"; ".vsixmanifest"]
        let e name = X.Element.Create(name, "http://schemas.openxmlformats.org/package/2006/content-types")
        e "Types" - [
            for ex in extensions do
                let ext = ex.TrimStart('.')
                if not (String.IsNullOrWhiteSpace ext) then
                    yield e "Default" + [
                        "Extension", ext
                        "ContentType", InferContentTypeByExtension ext
                    ]
        ]
        |> X.Write

type FrameworkVersion =
    | NET20
    | NET35
    | NET40
    | NET45

    member this.GetLiteral() =
        match this with
        | NET20 -> "2.0"
        | NET35 -> "3.5"
        | NET40 -> "4.0"
        | NET45 -> "4.5"

    override this.ToString() =
        this.GetLiteral()

type SupportedFrameworks =
    {
        mutable Max : FrameworkVersion
        mutable Min : FrameworkVersion
    }

    static member Create a b =
        {
            Min = a
            Max = b
        }

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

    member this.GetLiteral() =
        match this with
        | IntegratedShell -> "IntegratedShell"
        | Express_All -> "Express_All"
        | Premium -> "Premium"
        | Pro -> "Pro"
        | Ultimate -> "Ultimate"
        | VBExpress -> "VBExpress"
        | VCExpress -> "VCExpress"
        | VCSExpress -> "VCSExpress"
        | VWDExpress -> "VWDExpress"

    override this.ToString() =
        this.GetLiteral()

type VSProduct =
    {
        mutable Editions : list<VSEdition>
        mutable Version : string
    }

    static member Create v e =
        { Version = v; Editions = List.ofSeq e }

    member this.ToXml() =
        E?VisualStudio + [A?Version this.Version] - [
            for e in this.Editions ->
                E?Edition -- e.GetLiteral()
        ]

type IsolatedShellProduct =
    {
        mutable Name : string
        mutable Version : option<string>
    }

    static member Create n =
        { Version = None; Name = n}

    member this.ToXml() =
        let attrs =
            match this.Version with
            | None -> []
            | Some v -> [A?Version v]
        E?IsolatedShell + attrs -- this.Name

type SupportedProduct =
    | VS of VSProduct
    | IS of IsolatedShellProduct

    member this.ToXml() =
        match this with
        | VS x -> x.ToXml()
        | IS x -> x.ToXml()

type Identifier =
    {
        mutable Author : string
        mutable Culture : CultureInfo
        mutable Description : string
        mutable Id : Guid
        mutable Name : string
        mutable Products : list<SupportedProduct>
        mutable SupportedFrameworks : SupportedFrameworks
        mutable Version : Version
    }

    member this.ToXml() =
        E?Identifier + [A?Id (string this.Id)] - [
            E?Author -- this.Author
            E?Description -- this.Description
            E?Name -- this.Name
            E?Locale -- string this.Culture.LCID
            E?Version -- string this.Version
            E?SupportedFrameworkRuntimeEdition + [
                A?MinVersion (this.SupportedFrameworks .Min.GetLiteral())
                A?MaxVersion (this.SupportedFrameworks .Max.GetLiteral())
            ]
            E?SupportedProducts - [
                for p in this.Products ->
                    p.ToXml()
            ]
        ]

    static member Create author id name desc =
        {
            Author = author
            Culture = CultureInfo.InvariantCulture
            Description = desc
            Id = id
            Name = name
            Products = []
            SupportedFrameworks = { Min = NET20; Max = NET40 }
            Version = Version "1.0.0.0"
        }

type Assembly =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    static member Create p c : Assembly =
        {
            Content = c
            Path = p
        }

    member this.ToXml() =
        let name =
            let temp = Path.Combine(Path.GetTempPath(), Path.GetTempFileName())
            this.Content.WriteFile(temp)
            try
                Reflection.AssemblyName.GetAssemblyName(temp)
            finally
                File.Delete(temp)
        E?Assembly + [A?AssemblyName (string name)] -- this.Path

type CustomExtension =
    {
        mutable Content : F.Content
        mutable Path : string
        mutable Type : string
    }

    static member Create t p c : CustomExtension =
        {
            Content = c
            Path = p
            Type = t
        }

    member this.ToXml() =
        E?CustomExtension + [A?Type this.Type] -- this.Path

type Template =
    {
        mutable Archive : VSTemplates.Archive
        mutable Category : list<string>
    }

    static member Create cat d : Template =
        {
            Archive = d
            Category = Seq.toList cat
        }

    member this.ToXml() =
        match this.Archive.Kind with
        | VSTemplates.ProjectTemplateKind ->
            E?ProjectTemplate -- "ProjectTemplates"
        | VSTemplates.ItemTemplateKind ->
            E?ItemTemplate -- "ItemTemplates"

    member this.Content =
        F.BinaryContent this.Archive.Zip

    member this.DirectoryPath =
        match this.Archive.Kind with
        | VSTemplates.ProjectTemplateKind -> "ProjectTemplates"
        | VSTemplates.ItemTemplateKind -> "ItemTemplates"

    member this.Path =
        Path.Combine [|
            yield this.DirectoryPath
            yield! this.Category
            yield this.Archive.ZipFileName
        |]

type MEFComponent =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    member this.ToXml() =
        E?MEFComponent -- this.Path

    static member Create p c : MEFComponent =
        {
            Content = c
            Path = p
        }

type ToolboxControl =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    member this.ToXml() =
        E?ToolboxControl -- this.Path

    static member Create p c : ToolboxControl =
        {
            Content = c
            Path = p
        }

type VSPackage =
    {
        mutable Content : F.Content
        mutable Path : string
    }

    member this.ToXml() =
        E?VSPackage -- this.Path

    static member Create p c : VSPackage =
        {
            Content = c
            Path = p
        }

type VsixContent =
    | AssemblyContent of Assembly
    | CustomExtensionContent of CustomExtension
    | MEFComponentContent of MEFComponent
    | TemplateContent of Template
    | ToolboxControlContent of ToolboxControl
    | VSPackageContent of VSPackage

    member this.Content =
        match this with
        | AssemblyContent x -> x.Content
        | CustomExtensionContent x -> x.Content
        | MEFComponentContent x -> x.Content
        | TemplateContent x -> x.Content
        | ToolboxControlContent x -> x.Content
        | VSPackageContent x -> x.Content

    member this.Path =
        match this with
        | AssemblyContent x -> x.Path
        | CustomExtensionContent x -> x.Path
        | MEFComponentContent x -> x.Path
        | TemplateContent x -> x.Path
        | ToolboxControlContent x -> x.Path
        | VSPackageContent x -> x.Path

    member this.ToXml() =
        match this with
        | AssemblyContent x -> x.ToXml()
        | CustomExtensionContent x -> x.ToXml()
        | MEFComponentContent x -> x.ToXml()
        | TemplateContent x -> x.ToXml()
        | ToolboxControlContent x -> x.ToXml()
        | VSPackageContent x -> x.ToXml()

    static member Template cat t =
        TemplateContent (Template.Create cat t)

/// The `<Vsix>` element.
type Vsix =
    {
        mutable Identifier : Identifier
        mutable Contents : list<VsixContent>
    }

    static member Create id c =
        {
            Identifier = id
            Contents = List.ofSeq c
        }

    member this.GetPaths() =
        [for c in this.Contents -> c.Path]

    member this.GetEntries() =
        [for c in this.Contents -> (c.Path, c.Content)]

    member this.ToXml() =
        E?Vsix + [A?Version "1.0.0"] - [
            this.Identifier.ToXml()
            E?References
            E?Content - [for x in this.Contents -> x.ToXml()]
        ]

/// Represents an in-memory VSIX package.
type VsixFile =
    {
        FileName : string
        Zip : F.Binary
    }

    member this.WriteToDirectory(fullPath: string) =
        this.Zip.WriteFile(Path.Combine(fullPath, this.FileName))

    static member Create(fileName: string)(vsix: Vsix) : VsixFile =
        use zip = new ZipFile(F.DefaultEncoding)
        let ( +> ) name value = zip.AddEntry(name, value, F.DefaultEncoding) |> ignore
        let ( +% ) name (value: F.Binary) = zip.AddEntry(name, value.GetBytes()) |> ignore
        "extension.vsixmanifest" +> X.Write (vsix.ToXml())
        "[Content_Types].xml" +> GenerateContentTypesXml (vsix.GetPaths())
        for (path, data) in vsix.GetEntries() do
            match data with
            | F.BinaryContent b -> zip.AddEntry(path, b.GetBytes()) |> ignore
            | F.TextContent t -> zip.AddEntry(path, t, F.DefaultEncoding) |> ignore
        use w = new MemoryStream()
        zip.Save(w)
        let bin = F.Binary.FromBytes (w.ToArray())
        {
            FileName = fileName
            Zip = bin
        }

    static member FromFile(fullPath: string) : VsixFile =
        {
            FileName = Path.GetFileName fullPath
            Zip = F.Binary.ReadFile fullPath
        }

