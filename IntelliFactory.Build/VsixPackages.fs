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

/// Declares common types for VSIX VisualStudio extensions and VSTemplate packages.
module IntelliFactory.Build.VsixPackages

//open System
//open System.Xml
//open System.Xml.Linq
//open Ionic.Zip
//module F = FileSystem
//
///// Uniquely identifies extension packages.
//type Identity =
//    {
//        /// The unique GUID to disambiguate.
//        mutable Guid : Guid
//
//        /// The human-readable identifier.
//        mutable Id : string
//    }
//
//    /// Constructs the full disambiguated identifier.
//    member this.GetFullId() =
//        String.Format("{0}.{1}", this.Id, this.Guid)
//
//    override this.ToString() =
//        this.GetFullId()
//
//    /// Constructs a new instance.
//    static member Create id guid =
//        { Id = id; Guid = guid }
