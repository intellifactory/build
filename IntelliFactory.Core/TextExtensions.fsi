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

/// Extension methods for classes in `System.Text`.
[<AutoOpen>]
module IntelliFactory.Core.TextExtensions

open System
open System.Text

/// Extension methods for `StringBuilder`.
type StringBuilder with

    /// Takes characters off the beginning of the builder and into the buffer.
    member Dequeue : out: ArraySegment<char> -> int

    /// Clears the buffer and returns the contents.
    member Reset : unit -> string
