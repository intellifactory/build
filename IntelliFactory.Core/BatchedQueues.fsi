﻿// Copyright 2013 IntelliFactory
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

/// Implements purely functional queues.
/// Implementation follows BatchedQueue from p. 43,
/// Purely Functional Data Structures by Chris Okasaki.
module IntelliFactory.Core.BatchedQueues

open System
open System.Collections
open System.Collections.Generic

/// Represents a queue.
[<Sealed>]
type BatchedQueue<'T> =
    interface IEnumerable
    interface IEnumerable<'T>

    new : unit -> BatchedQueue<'T>
    new : seq<'T> -> BatchedQueue<'T>

    member Enqueue : 'T -> BatchedQueue<'T>
    member ToList : unit -> list<'T>

/// Pattern-matches `BatchedQueue`.
val (|Empty|With|) : BatchedQueue<'T> -> Choice<unit,'T * BatchedQueue<'T>>
