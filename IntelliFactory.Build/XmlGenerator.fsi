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
module IntelliFactory.Build.XmlGenerator

open System
open System.Collections.Generic
open System.IO
open System.Xml
open System.Xml.Linq

/// Represents qualified names.
[<Sealed>]
type Name =
    interface IComparable

    /// The local part of the name.
    member Local : string

    /// The URI part of the name, or an empty string.
    member Uri : string

    /// Creates a new Name without the URI part.
    static member Create : local: string -> Name

    /// Creates a new Name with a given URI part.
    static member Create : local: string * uri: string -> Name

/// Represents simple XML elements.
type Element =
    {
        /// The attribute collection.
        Attributes : IDictionary<Name,string>

        /// The children collection.
        Children : seq<INode>

        /// The qualified name.
        Name : Name
    }

    interface INode

    /// Constructs an new empty Element.
    static member Create : name: string -> Element

    /// Constructs an new empty Element.
    static member Create : name: string * uri: string -> Element

    /// Replaces the children.
    static member WithChildren : children: #seq<#INode> -> self: Element -> Element

    /// Replaces the children.
    static member ( - ) : self: Element * children: #seq<#INode> -> Element

    /// Replaces the attributes.
    static member WithAttributes : attrs: #seq<string*string> -> self: Element -> Element

    /// Replaces the attributes.
    static member ( + ) : self: Element * attrs: #seq<string*string> -> Element

    /// Replaces the children with a single text node.
    static member WithText : text: string -> self: Element-> Element

    /// Replaces the children with a single text node.
    static member ( -- ) : self: Element * text: string -> Element

/// Represents simple XML nodes.
and Node =
    | CDataNode of string
    | ElementNode of Element
    | TextNode of string

    interface INode

/// An interface for node-equivalent types.
and INode =

    /// The equivalent node.
    abstract Node : Node

/// Constructs an `Element` from an `XElement`.
val FromXElement : element: XElement -> Element

/// Reads an XML string to an `Element`.
val Read : contents: string -> Element

/// Reads an XML file to an `Element`.
val ReadFile : file: string -> Element

/// Converts the `Element` to an `XElement`.
val ToXElement : element: Element -> XElement

/// Writes an `Element` to a string.
val Write : element: Element -> string

/// Writes an `Element` to a file.
val WriteFile : file: string -> element: Element -> unit

