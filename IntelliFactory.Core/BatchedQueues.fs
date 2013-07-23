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

module IntelliFactory.Core.BatchedQueues

open System
open System.Collections
open System.Collections.Generic

type Queue<'T> =
    | Q of list<'T> * list<'T>

let check f r =
    match f with
    | [] -> Q (List.rev r, [])
    | _ -> Q (f, r)

let empty<'T> : Queue<'T> =
    Q ([], [])

let enqueue (Q (f, r)) x =
    check f (x :: r)

let toList (Q (f, r)) =
    List.append f (List.rev r)

let ofList xs =
    Q (xs, [])

let isEmpty q =
    match q with
    | Q ([], _) -> true
    | _ -> false

let ofSeq xs =
    ofList (List.ofSeq xs)

[<Sealed>]
type BatchedQueue<'T>(q: Queue<'T>) =
    new () = BatchedQueue(empty)
    new (xs: seq<'T>) = BatchedQueue(ofSeq xs)
    member bq.Enqueue x = BatchedQueue(enqueue q x)
    member bq.ToList() = toList q
    member bq.IsEmpty = isEmpty q

    interface IEnumerable with
        member bt.GetEnumerator() =
            ((toList q) :> IEnumerable).GetEnumerator()

    interface IEnumerable<'T> with
        member bt.GetEnumerator() =
            ((toList q) :> IEnumerable<'T>).GetEnumerator()

    member bq.Queue = q

let (|Empty|With|) (q: BatchedQueue<_>) =
    match q.Queue with
    | Q ([], _) -> Empty
    | Q (x :: f, r) -> With (x, BatchedQueue(check f r))
