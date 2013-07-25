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

[<AutoOpen>]
module IntelliFactory.Core.Futures

open System
open IntelliFactory.Core

type FutureState<'T> =
    | FutureCompleted of 'T
    | FutureWaiting of BatchedQueues.BatchedQueue<'T -> unit>

[<Sealed>]
type Future<'T>(st: AtomicReferences.AtomicReference<FutureState<'T>>) =

    let on k =
        st.Update <| fun ctx ->
            match ctx.State with
            | FutureCompleted x ->
                Async.Spawn(k, x)
                ctx.LeaveIntact()
            | FutureWaiting ks ->
                ctx.Set(FutureWaiting (ks.Enqueue k))

    let await =
        Async.FromContinuations(fun (ok, _, _) -> on ok)

    member f.Await() = await

    member f.Complete(v: 'T) =
        let ok = FutureCompleted v
        st.Update <| fun ctx ->
            match ctx.State with
            | FutureCompleted _ -> failwith "Future: cannot Complete more than one."
            | FutureWaiting fs -> ctx.Set(ok, fun () -> for f in fs do Async.Spawn(f, v))

    member f.On k = on k

    member f.IsCompleted =
        match st.Value with
        | FutureCompleted _ -> true
        | _ -> false

[<Sealed>]
type Future =

    static member Create() =
        let q = BatchedQueues.BatchedQueue()
        let r =
            FutureWaiting q
            |> AtomicReferences.AtomicReference.Create
        Future r
