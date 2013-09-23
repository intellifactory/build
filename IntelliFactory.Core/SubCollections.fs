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

namespace IntelliFactory.Core

module SubCollections =
    open System
    open System.Collections
    open System.Collections.Generic

    type SubCollection<'R,'T> =
        {
            SElements : list<'T>
            SUpdate : list<'T> -> 'R
        }

        member sc.Add(x) =
            sc.SUpdate(sc.SElements @ [x])

        member sc.AddMany(xs) =
            sc.SUpdate(sc.SElements @ Seq.toList xs)

        member sc.Clear() =
            sc.SUpdate([])

        member sc.Replace(xs) =
            sc.SUpdate(Seq.toList xs)

        interface IEnumerable with
            member sc.GetEnumerator() =
                let els = List.rev sc.SElements
                (sc.SElements :> IEnumerable).GetEnumerator()

        interface IEnumerable<'T> with
            member sc.GetEnumerator() =
                (sc.SElements :> seq<'T>).GetEnumerator()

    [<Sealed>]
    type SubCollection =

        static member Create(elements, update) =
            {
                SElements = elements
                SUpdate = update
            }
