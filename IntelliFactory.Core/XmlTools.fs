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

[<AutoOpen>]
module IntelliFactory.Core.XmlTools

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Xml
open System.Xml.Linq
open IntelliFactory.Core

type XmlName =
    {
        LocalName : string
        NameUri : option<string>
    }

    member n.ToXName() =
        match n.NameUri with
        | None -> XName.Get n.LocalName
        | Some uri -> XName.Get(n.LocalName, uri)

    member this.Local = this.LocalName
    member this.Uri = this.NameUri

    static member Create(local: string, ?uri: string) =
        match uri with
        | None | Some null ->
            {
                LocalName = local
                NameUri = None
            }
        | Some uri ->
            let uri = Uri(uri).ToString()
            {
                LocalName = local
                NameUri = Some uri
            }

    static member FromXName(n: XName) =
        XmlName.Create(n.LocalName, n.NamespaceName)

type XmlElement =
    {
        AttributeMap : Map<XmlName,string>
        ChildNodes : seq<XmlNode>
        ElementName : XmlName
    }

    interface IXmlNode with
        member this.Node = XmlElementNode this

    member e.Attribute(name: XmlName, value: string) =
        { e with AttributeMap = e.AttributeMap.Add(name, value) }

    member e.Attribute(name: string, value: string) =
        { e with AttributeMap = e.AttributeMap.Add(XmlName.Create name, value) }

    member e.Text(t) =
        { e with ChildNodes = Seq.append e.ChildNodes (Seq.singleton (XmlTextNode t)) }

    member e.ToXElement() =
        let nodes = Queue<obj>()
        for KeyValue (n, v) in e.AttributeMap do
            XAttribute(n.ToXName(), v)
            |> nodes.Enqueue
        for c in e.ChildNodes do
            c.ToXNode()
            |> nodes.Enqueue
        XElement(e.ElementName.ToXName(), nodes)

    member e.WithAttributes(attrs: Map<XmlName,string>) =
        let attrs =
            seq {
                yield! Map.toSeq e.AttributeMap
                yield! Map.toSeq attrs
            }
            |> Map.ofSeq
        { e with AttributeMap = attrs }

    member e.WithAttributes(attrs: seq<string * string>) =
        seq {
            for (k, v) in attrs do
                yield (XmlName.Create k, v)
        }
        |> Map.ofSeq
        |> e.WithAttributes

    member e.WithChildren(children: seq<IXmlNode>) =
        {
            e with
                ChildNodes =
                    seq { for n in children -> n.Node }
                    |> Seq.append e.ChildNodes
        }

    member e.WriteTo(create: XmlWriterSettings -> XmlWriter) : unit =
        let settings = XmlWriterSettings()
        settings.OmitXmlDeclaration <- true
        settings.Encoding <- FileSystem.DefaultEncoding
        settings.Indent <- true
        settings.IndentChars <- " "
        use w = create settings
        let doc = XDocument(e.ToXElement())
        doc.Save(w)

    member e.Write() : string =
        use w = new StringWriter()
        e.WriteTo(fun s -> XmlTextWriter.Create(w, s))
        w.ToString()

    member e.WriteFile(file: string) : unit =
        e.WriteTo(fun s -> XmlTextWriter.Create(file, s))

    member e.Attributes = e.AttributeMap
    member e.Children = e.ChildNodes
    member e.LocalName = e.ElementName.LocalName
    member e.Name = e.ElementName
    member e.Uri = e.ElementName.Uri

    static member Create(name) =
        {
            AttributeMap = Map.empty
            ChildNodes = Seq.empty
            ElementName = name
        }

    static member Create(name, ?uri) =
        XmlName.Create(name, ?uri = uri)
        |> XmlElement.Create

    static member FromXElement(elem: XElement) =
        let attrs =
            (Map.empty, elem.Attributes())
            ||> Seq.fold (fun m a ->
                let key = XmlName.FromXName a.Name
                let value = a.Value
                m.Add(key, value))
        let children =
            elem.Nodes()
            |> Seq.choose XmlNode.FromXNode
            |> Seq.toArray
        {
            AttributeMap = attrs
            ChildNodes = children
            ElementName = XmlName.FromXName elem.Name
        }

    static member ReadFile(file: string)  =
        XDocument.Load(file).Root
        |> XmlElement.FromXElement

    static member Read(xml: string) =
        XDocument.Parse(xml).Root
        |> XmlElement.FromXElement

    static member ( - ) (e: XmlElement, children: seq<IXmlNode>) =
        e.WithChildren children

    static member ( -< ) (e: XmlElement, children: seq<XmlElement>) =
        e - seq { for e in children -> e :> IXmlNode }

    static member ( + ) (e: XmlElement, attrs: seq<string*string>) =
        e.WithAttributes attrs

    static member ( -- ) (e: XmlElement, text: string) =
        e.Text text

and XmlNode =
    | XmlCDataNode of string
    | XmlElementNode of XmlElement
    | XmlTextNode of string

    member n.ToXNode() : XNode =
        match n with
        | XmlCDataNode text -> XCData(text) :> _
        | XmlElementNode e -> e.ToXElement() :> _
        | XmlTextNode text -> XText(text) :> _

    static member FromXNode(node: XNode) : option<XmlNode> =
        match node.NodeType with
        | XmlNodeType.CDATA ->
            let node = node :?> XCData
            Some (XmlCDataNode node.Value)
        | XmlNodeType.Element ->
            let node = node :?> XElement
            Some (XmlElementNode (XmlElement.FromXElement node))
        | XmlNodeType.Text ->
            let node = node :?> XText
            Some (XmlTextNode node.Value)
        | _ ->
            None

    interface IXmlNode with
        member this.Node = this

and IXmlNode =
    abstract Node : XmlNode

[<Sealed>]
type XmlGenerator(?ns: string) =

    member g.Element name =
        XmlElement.Create(name, ?uri = ns)

    member g.Text t =
        XmlTextNode t

    static member Create(?ns) =
        XmlGenerator(?ns = ns)

    static member ( ? ) (g: XmlGenerator, name: string) =
        g.Element name
