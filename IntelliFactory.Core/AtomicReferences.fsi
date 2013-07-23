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

/// Implements concurrent references with optimistic compare-exchange update strategy.
module IntelliFactory.Core.AtomicReferences

open System

/// Represents the strategy to resolve contention.
type IBackoffStrategy =

    /// Executs retry action on a given attempt.
    abstract Schedule : attempt: int * action: (unit -> unit) -> unit

/// Default strategy is to spin-wait on the current thread with exponentially increasing timeouts.
val SpinWaitBackoffStrategy : IBackoffStrategy

/// Helper type for updating atomic references.
[<Sealed>]
type Update<'T>

/// Helper type for updating atomic references.
[<Sealed>]
type Context<'T when 'T : not struct> =

    /// Leaves the current state intact.
    member LeaveIntact : unit -> Update<'T>

    /// Sets the state to a new value.
    member Set : value: 'T -> Update<'T>

    /// Sets the state to a new value and executes the continuation when the transaction completes.
    member Set : value: 'T * onSuccess: (unit -> unit) -> Update<'T>

    /// Peeks at the current state.
    member State : 'T

/// A concurrent references with optimistic compare-exchange update strategy.
[<Sealed>]
type AtomicReference<'T when 'T : not struct> =

    /// Updates the value atomically.
    member Update : update: (Context<'T> -> Update<'T>) -> unit

    /// Fetches the current value.
    member Value : 'T

/// Static methods for atomcic references.
[<Sealed>]
type AtomicReference =

    /// Creates a new reference with an initial value.
    static member Create : value: 'T * ?backoff: IBackoffStrategy -> AtomicReference<'T>
