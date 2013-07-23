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

/// Extensions to `System.IO` types.
module IntelliFactory.Core.IOExtensions

open System
open System.IO
open System.Text
open System.Threading

#nowarn "40"

/// TextWriter extensions.
type TextWriter with

    /// Constructs a writer that sends characters to a given function.
    static member NonBlocking(f: string -> unit, ?bufferSize, ?encoding) =
        NonBlockingTextWriter.Create(f, ?bufferSize = bufferSize, ?encoding = encoding)
