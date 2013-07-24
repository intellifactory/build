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

/// Extension methods for asynchronous workflows.
[<AutoOpen>]
module IntelliFactory.Core.AsyncExtensions

open System
open System.Threading

/// Extends `Async` type with extra methods.
type Async with

    /// Spawns a method with on the thread pool.
    static member Spawn : (unit -> unit) -> unit

    /// Spawns a method with an argument on the thread pool.
    static member Spawn : ('T -> unit) * 'T -> unit

    /// Adds a timeout to the execution of an asynchronous workflow.
    /// Warning: the timed out workflow must support cancellation.
    static member WithTimeout : TimeSpan -> work: Async<'T> -> Async<option<'T>>

/// Represents results of executing an `Async` workflow.
type AsyncResult<'T> =
    | AsyncCancelled of OperationCanceledException
    | AsyncCompleted of 'T
    | AsyncFaulted of exn

    /// Returns using the normal `Async` convention.
    member Return : unit -> Async<'T>

/// Utilities involving explicit representation of `Async` workflow results.
[<Sealed>]
type AsyncResult =

    /// Capturs an explicit result of an `Async` workflow.
    static member Capture : work: Async<'T> * ?token: CancellationToken -> Async<AsyncResult<'T>>

    /// Defines an `Async` workflow from a result continuation.
    static member DefineAsync : def: ((AsyncResult<'T> -> unit) -> unit) -> Async<'T>

    /// Unwraps a wrapped result to normal `Async` convention.
    static member Unwrap : work: Async<AsyncResult<'T>> * ?token: CancellationToken -> Async<'T>
