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

module IntelliFactory.Build.XmlGenerator

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Xml
open System.Xml.Linq

type Name =
    {
        local : string
        uri : string
    }

    member this.Local = this.local
    member this.Uri = this.uri

    static member Create(local: string) =
        { local = local; uri = "" }

    static member Create(local: string, uri: string) =
        match uri with
        | null -> Name.Create local
        | _ -> { local = local; uri = uri }

    static member FromXName(n: XName) =
        Name.Create(n.LocalName, n.NamespaceName)

    static member ToXName(name) =
        XName.Get(name.local, name.uri)

type Element =
    {
        Attributes : IDictionary<Name,string>
        Children : seq<INode>
        Name : Name
    }

    interface INode with
        member this.Node = ElementNode this

    static member Create(n) =
        {
            Attributes = Map.empty
            Children = Seq.empty
            Name = Name.Create(n)
        }

    static member Create(n, u) =
        {
            Attributes = Map.empty
            Children = Seq.empty
            Name = Name.Create(n, u)
        }

    static member WithChildren (children: #seq<#INode>) (self: Element) =
        { self with Children = Seq.toArray (Seq.cast children) }

    static member ( - ) (self: Element, children: #seq<#INode>) =
        Element.WithChildren children self

    static member WithAttributes (attrs: #seq<string*string>) (self: Element) =
        let a = Dictionary()
        for (k, v) in attrs do
            a.[Name.Create k] <- v
        { self with Attributes = a }

    static member ( + ) (self: Element, attrs: #seq<string * string>) =
        Element.WithAttributes attrs self

    static member WithText (text: string) (self: Element) =
        { self with Children = Seq.singleton (TextNode text :> _) }

    static member ( -- ) (self, text) =
        Element.WithText text self

and Node =
    | CDataNode of string
    | ElementNode of Element
    | TextNode of string

    interface INode with
        member this.Node = this

and INode =
    abstract member Node : Node

let rec ToXNode (n: INode) : XNode =
    match n.Node with
    | CDataNode text -> XCData(text) :> _
    | ElementNode e -> ToXElement e :> _
    | TextNode text -> XText(text) :> _

and FromXNode (node: XNode) : option<Node> =
    match node.NodeType with
    | XmlNodeType.CDATA ->
        let node = node :?> XCData
        Some (CDataNode node.Value)
    | XmlNodeType.Element ->
        let node = node :?> XElement
        Some (ElementNode (FromXElement node))
    | XmlNodeType.Text ->
        let node = node :?> XText
        Some (TextNode node.Value)
    | _ ->
        None

and FromXElement (elem: XElement) : Element =
    let attrs = Dictionary()
    for attr in elem.Attributes() do
        attrs.[Name.FromXName attr.Name] <- attr.Value
    let children : INode [] =
        elem.Nodes()
        |> Seq.choose FromXNode
        |> Seq.cast
        |> Seq.toArray
    {
        Name = Name.FromXName elem.Name
        Attributes = attrs
        Children = children
    }

and ToXElement (e: Element) =
    let nodes = Queue<obj>()
    for KeyValue (n, v) in e.Attributes do
        XAttribute(Name.ToXName n, v)
        |> nodes.Enqueue
    for c in e.Children do
        ToXNode c
        |> nodes.Enqueue
    XElement(Name.ToXName e.Name, nodes)

let rec WriteXml (x: XmlWriter) (this: INode) : unit =
    match this.Node with
    | CDataNode text ->
        x.WriteCData(text)
    | ElementNode e ->
        x.WriteStartElement(e.Name.local, e.Name.uri)
        for KeyValue (n, v) in e.Attributes do
            match n.uri with
            | "" -> x.WriteAttributeString(n.Local, v)
            | _ -> x.WriteAttributeString(n.local, n.uri, v)
        for c in e.Children do
            WriteXml x c
        x.WriteEndElement()
    | TextNode text ->
        x.WriteString(text)

let WriteTo (create: XmlWriterSettings -> XmlWriter) (element: Element) : unit =
    let settings = XmlWriterSettings()
    settings.OmitXmlDeclaration <- true
    settings.Encoding <- FileSystem.DefaultEncoding
    settings.Indent <- true
    settings.IndentChars <- " "
    use w = create settings
    let doc = XDocument(ToXElement element)
    doc.Save(w)

let Write (element: Element) : string =
    use w = new StringWriter()
    WriteTo (fun s -> XmlTextWriter.Create(w, s)) element
    w.ToString()

let WriteFile (file: string) (element: Element) : unit =
    WriteTo (fun s -> XmlTextWriter.Create(file, s)) element

let ReadFile (file: string) : Element =
    XDocument.Load(file).Root
    |> FromXElement

let Read (xml: string) : Element =
    XDocument.Parse(xml).Root
    |> FromXElement
