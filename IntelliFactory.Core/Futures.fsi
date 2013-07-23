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

/// Implements a thread-safe future value abstraction for inverting control flow.
[<AutoOpen>]
module IntelliFactory.Core.Futures

/// Represents a synchronizable computation that completes exactly one without exceptions.
[<Sealed>]
type Future<'T> =

    /// Awaits completion asynchronously.
    member Await : unit -> Async<'T>

    /// Imperatively completes the computation.
    member Complete : 'T -> unit

    /// Exceutes the callback when the future completes.
    member On : ('T -> unit) -> unit

    /// Checks if the future is completed.
    member IsCompleted : bool

/// Static methods for working with `Future` values.
[<Sealed>]
type Future =

    /// Creates a new incomplete computation.
    static member Create : unit -> Future<'T>
