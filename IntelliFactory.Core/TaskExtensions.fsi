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

/// Extension methods for `System.Task` functionality.
[<AutoOpen>]
module IntelliFactory.Core.TaskExtensions

open System
open System.Threading
open System.Threading.Tasks

/// Extensions for untyped tasks.
type Task with

    /// Awaits the task in an Async workflow.
    member Await : unit -> Async<unit>

/// Extensions for typed tasks.
type Task<'T> with

    /// Awaits the task in an Async workflow.
    member Await : unit -> Async<'T>

    /// Maps over the task.
    member Map : ('T -> 'R) -> Task<'R>
