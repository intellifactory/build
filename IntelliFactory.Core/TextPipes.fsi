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

/// Implements text reading/writing utilities.
[<AutoOpen>]
module IntelliFactory.Core.TextPipes

open System
open System.IO
open System.Text

/// Configures `NonBlockingTextWriter` (see below).
type NonBlockingTextWriterConfig =
    {
        /// Default: 1024.
        BufferSize : int

        /// Encoding used in `TextWriter.Encoding` - informational use only.
        Encoding : Encoding

        /// The interval at which the internal timer fires to flush the buffer. Defaults to 1 second.
        FlushInterval : TimeSpan

        /// The newline string to use. Defaults to "\n" for symmetry with `ReadLine` implementation on `TextReader`.
        NewLine : string
    }

    /// The default configuration.
    static member Default : NonBlockingTextWriterConfig

/// Implements writing as message-passing.
/// Writers created with this class are thread-safe.
[<Sealed>]
type NonBlockingTextWriter =

    /// Constructs a new writer that does not block when written to.
    /// Continuation is invoked in a single-threaded manner.
    /// It should not block or raise exceptions.
    /// All strings passed to the continuation are non-null and non-empty.
    /// An empty string signals that the writer is closed.
    static member Create : out: (string -> unit) * ?config: NonBlockingTextWriterConfig -> TextWriter

/// Thread-safe text pipe with two ends - a reader and a writer.
/// Writes are non-blocking, the pipe accumulates without bounds.
/// Reads "block" by queuing continuations in the pipe.
/// Asynchronous reads are preferred, as they involve
/// more efficient continuation representations:
/// waiting async readers use less memory than blocking readers.
[<Sealed>]
type TextPipe =

    /// Closes the pipe. All queued readers receive empty results.
    member Close : unit -> unit

    /// The reader end.
    member Reader : TextReader

    /// The writer end.
    member Writer : TextWriter

    /// Creates a new text pipe. Parameters configure the `NonBlockingTextWriter`.
    static member Create : ?config: NonBlockingTextWriterConfig -> TextPipe
