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

module IntelliFactory.Core.AtomicReferences

open System
open System.Threading

type IBackoffStrategy =
    abstract Schedule : attempt: int * action: (unit -> unit) -> unit

let SpinWaitBackoffStrategy =
    {
        new IBackoffStrategy with
            member this.Schedule(attempt, action) =
                Thread.SpinWait(1 <<< min 24 attempt)
                action ()
    }

type Update<'T> =
    | LeaveIntact
    | SetState of 'T
    | SetAndContinue of 'T * (unit -> unit)

[<Sealed>]
type Context<'T when 'T : not struct>(value: 'T) =
    member c.LeaveIntact() : Update<'T> = LeaveIntact
    member c.Set(v: 'T) = SetState v
    member c.Set(v: 'T, f) = SetAndContinue(v, f)
    member c.State = value

let inline ( == ) a b = Object.ReferenceEquals(a, b)

type AtomicReference<'T when 'T : not struct> =
    private {
        Backoff : IBackoffStrategy
        mutable Slot : 'T
    }

    member r.Update(up: Context<'T> -> Update<'T>) =
        let bs = r.Backoff
        let rec loop (n: int) =
            let s = r.Value
            let ctx = Context<'T>(s)
            match up ctx with
            | LeaveIntact -> ()
            | SetState newState ->
                if Interlocked.CompareExchange(&r.Slot, newState, s) == s then () else backoff n
            | SetAndContinue (newState, onSuccess) ->
                if Interlocked.CompareExchange(&r.Slot, newState, s) == s then onSuccess () else backoff n
        and backoff n =
            let n = n + 1
            bs.Schedule(n, fun () -> loop n)
        loop 0

    member r.Value = r.Slot

[<Sealed>]
type AtomicReference =

    static member Create(value, ?backoff) =
        {
            Backoff = defaultArg backoff SpinWaitBackoffStrategy
            Slot = value
        }
