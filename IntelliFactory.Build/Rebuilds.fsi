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

/// Facilities for making optimal rebuild decisions.
[<AutoOpen>]
module internal IntelliFactory.Build.Rebuilds

open IntelliFactory.Core

/// The response from the rebuild decision system.
[<Sealed>]
type RebuildDecision =

    /// The project should be rebuilt.
    member IsStale : bool

    /// Readable reason for the decision.
    member Reason : string

    /// Touch all output files.
    member Touch : unit -> unit

/// Defines a rebuild problem.
[<Sealed>]
type RebuildProblem =

    /// Adds input paths to consider.
    member AddInputPaths : seq<string> -> RebuildProblem

    /// Adds output paths to consider.
    member AddOutputPaths : seq<string> -> RebuildProblem

    /// Decides the problem.
    member Decide: unit -> RebuildDecision

    /// Constructor function.
    static member Create : env: IParametric -> RebuildProblem

