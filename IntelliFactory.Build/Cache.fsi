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
// permissions and limitations under the License

namespace IntelliFactory.Build

open IntelliFactory.Core

[<Sealed>]
type internal CacheKey =
    new : unit -> CacheKey

[<Sealed>]
type internal Cache =
    new : unit -> Cache
    member Lookup<'T1,'T2 when 'T1 : equality> : CacheKey -> (unit -> 'T2) -> 'T1 -> 'T2
    member TryLookup<'T1,'T2 when 'T1 : equality> : CacheKey -> (unit -> option<'T2>) -> 'T1 -> option<'T2>
    static member Init : IParametric<'R> -> 'R
    static member Current : Parameter<Cache>
