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

namespace IntelliFactory.Core

/// Simplifies defining functional update interfaces for collection fields.
module SubCollections =
    open System
    open System.Collections
    open System.Collections.Generic

    /// A lens-like abstraction for defining a member list
    /// with functional update.
    [<Sealed>]
    type SubCollection<'R,'T> =

        /// Adds an element to the end of the collection.
        member Add : 'T -> 'R

        /// Adds multiple elements to the end of the collection.
        member AddMany : seq<'T> -> 'R

        /// Clears the collection.
        member Clear : unit -> 'R

        /// Replaces the collection entirely.
        member Replace : seq<'T> -> 'R

        interface IEnumerable
        interface IEnumerable<'T>

    /// Combinators for defining sub-collections.
    [<Sealed>]
    type SubCollection =

        /// Creates a new sub-collection based on a list of elements and an update func.
        static member Create : list<'T> * (list<'T> -> 'R) -> SubCollection<'R,'T>
