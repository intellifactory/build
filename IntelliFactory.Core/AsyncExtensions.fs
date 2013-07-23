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

/// Extends `Async` type with extra methods.
type Async with

    /// Spawns a method with on the thread pool.
    static member Spawn(f) =
        async { return f () }
        |> Async.Start

    /// Spawns a method with an argument on the thread pool.
    static member Spawn(f, x) =
        async { return f x }
        |> Async.Start

    /// Adds a timeout to the execution of an asynchronous workflow.
    /// Warning: the timed out workflow must support cancellation.
    static member WithTimeout (t: TimeSpan) (work: Async<'T>) : Async<option<'T>> =
        async {
            let! c = Async.StartChild(work, int t.TotalMilliseconds)
            try
                let! x = c
                return Some x
            with :? TimeoutException ->
                return None
        }

/// Represents results of executing an `Async` workflow.
type AsyncResult<'T> =
    | AsyncCancelled of OperationCanceledException
    | AsyncCompleted of 'T
    | AsyncFaulted of exn

/// Utilities involving explicit representation of `Async` workflow results.
[<Sealed>]
type AsyncResult =

    /// Capturs an explicit result of an `Async` workflow.
    static member Capture (work: Async<'T>) : Async<AsyncResult<'T>> =
        Async.FromContinuations(fun (ok, _, _) ->
            let k1 x = ok (AsyncCompleted x)
            let k2 x = ok (AsyncFaulted x)
            let k3 x = ok (AsyncCancelled x)
            Async.Spawn(fun () -> Async.StartWithContinuations(work, k1, k2, k3)))

    /// Defines an `Async` workflow from a result continuation.
    static member DefineAsync (f: (AsyncResult<'T> -> unit) -> unit) : Async<'T> =
        Async.FromContinuations(fun (k1, k2, k3) ->
            let k = function
                | AsyncCancelled r -> k3 r
                | AsyncCompleted r -> k1 r
                | AsyncFaulted r -> k2 r
            Async.Spawn(f, k))
