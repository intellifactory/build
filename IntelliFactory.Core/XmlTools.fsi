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

/// Provides a facility to generate simple XML documents.
[<AutoOpen>]
module IntelliFactory.Core.XmlTools

open System
open System.Collections.Generic
open System.IO
open System.Xml
open System.Xml.Linq

/// Represents qualified names.
[<Sealed>]
type XmlName =
    interface IComparable

    /// The local part of the name.
    member Local : string

    /// The URI part of the name, or an empty string.
    member Uri : option<string>

    /// Creates a new Name with a given URI part.
    static member Create : local: string * ?uri: string -> XmlName

/// Represents simple XML elements.
[<Sealed>]
type XmlElement =
    interface IXmlNode

    /// Addds an attribute.
    member Attribute : XmlName * string -> XmlElement

    /// Addds an attribute.
    member Attribute : string * string -> XmlElement

    /// Appends a text node.
    member Text : text: string -> XmlElement

    /// Converts the `Element` to an `XElement`.
    member ToXElement : unit -> XElement

    /// Writes an `Element` to a string.
    member Write : unit -> string

    /// Writes an `Element` to a file.
    member WriteFile : file: string -> unit

    /// Appends/overwrites the attributes.
    member WithAttributes : attrs: Map<XmlName, string> -> XmlElement

    /// Appends/overwrites the attributes.
    member WithAttributes : attrs: seq<string * string> -> XmlElement

    /// Appends children.
    member WithChildren : children: seq<IXmlNode> -> XmlElement

    /// The attribute collection.
    member Attributes : Map<XmlName,string>

    /// The children collection.
    member Children : seq<XmlNode>

    /// The local name.
    member LocalName : string

    /// The qualified name.
    member Name : XmlName

    /// The URI part of the name, if qualified.
    member Uri : option<string>

    /// Constructs an new empty Element.
    static member Create : name: XmlName -> XmlElement

    /// Constructs an new empty Element.
    static member Create : name: string * ?uri: string -> XmlElement

    /// Constructs an `Element` from an `XElement`.
    static member FromXElement : element: XElement -> XmlElement

    /// Reads an XML string to an `Element`.
    static member Read : contents: string -> XmlElement

    /// Reads an XML file to an `Element`.
    static member ReadFile : file: string -> XmlElement

    /// Appends children.
    static member ( - ) : self: XmlElement * children: seq<IXmlNode> -> XmlElement

    /// Appends child elements.
    static member ( -< ) : self: XmlElement * children: seq<XmlElement> -> XmlElement

    /// Appends attributes.
    static member ( + ) : self: XmlElement * attrs: seq<string*string> -> XmlElement

    /// Appends a single text node.
    static member ( -- ) : self: XmlElement * text: string -> XmlElement

/// Represents simple XML nodes.
and XmlNode =
    | XmlCDataNode of string
    | XmlElementNode of XmlElement
    | XmlTextNode of string

    interface IXmlNode

/// An interface for node-equivalent types.
and IXmlNode =

    /// The equivalent node.
    abstract Node : XmlNode

/// Generates Xml documents.
[<Sealed>]
type XmlGenerator =

    /// Creates a new element.
    member Element : string -> XmlElement

    /// Creates a new text node.
    member Text : string -> XmlNode

    /// Creates a new XmlGenerator.
    static member Create : ?defaultNamespace: string -> XmlGenerator

    /// Constructs a new element.
    static member ( ? ) : XmlGenerator * name: string -> XmlElement

