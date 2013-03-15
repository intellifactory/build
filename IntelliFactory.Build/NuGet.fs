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

/// Provides some utilities for working with NuGet packages.
module IntelliFactory.Build.NuGet

open System.Xml
open System.Xml.Linq
open Ionic.Zip
module F = FileSystem

/// The NuGet XML namespace.
[<Literal>]
let XmlNamespace = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"

/// Represents a NuGet packages embedded into a VSIX extension.
type Package =
    {
        /// The content of the `.nupkg` package file.
        mutable Content : F.Content

        /// The name of the NuGet package.
        mutable Name : string

        /// The version of the NuGet package.
        mutable Version : string
    }

    /// Constructs a NuGet package from a given `.nupkg` file,
    /// inferring package identity and version automatically.
    static member FromFile(path: string) : Package =
        let zip = ZipFile.Read(path)
        let nuspec =
            zip.Entries
            |> Seq.find (fun x ->
                x.FileName.ToLower().EndsWith(".nuspec"))
        use r = nuspec.OpenReader()
        let doc = XDocument.Load(r)
        let idElement =
            doc.Descendants(XName.Get("id", XmlNamespace))
            |> Seq.head
        let id = idElement.Value
        let versionElement =
            doc.Descendants(XName.Get("version", XmlNamespace))
            |> Seq.head
        let version = versionElement.Value
        {
            Content = F.Content.ReadBinaryFile path
            Name = id
            Version = version
        }
