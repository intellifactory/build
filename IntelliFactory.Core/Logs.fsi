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

/// Log-related functionality.
module IntelliFactory.Core.Logs

open System
open System.Diagnostics
open System.IO

/// Represents the level at which a message is being logged.
type Level =
    | Critical
    | Error
    | Warn
    | Info
    | Verbose

    /// The printable name.
    member Name : string

/// Represents a subscriber to the logging framework.
type ITrace =

    /// Decide if tracing at this level is supported.
    abstract ShouldTrace : Level -> bool

    /// Trace a message.
    abstract Trace : Level * string -> unit

/// Abstract logging configuration.
type IConfig =

    /// Get a logger for a given name.
    abstract GetTrace : name: string -> ITrace

/// Default logging configuration.
[<Sealed>]
type DefaultConfig =
    interface IConfig
    member Critical : unit -> DefaultConfig
    member Critical : string -> DefaultConfig
    member Default : Level -> DefaultConfig
    member Error : unit -> DefaultConfig
    member Error : string -> DefaultConfig
    member Info : unit -> DefaultConfig
    member Info : string -> DefaultConfig
    member Restrict : string * Level -> DefaultConfig
    member ToConsole : unit -> DefaultConfig
    member ToDiagnostics : unit -> DefaultConfig
    member ToTraceSource : TraceSource -> DefaultConfig
    member Verbose : unit -> DefaultConfig
    member Verbose : string -> DefaultConfig
    member Warn : unit -> DefaultConfig
    member Warn : string -> DefaultConfig

/// The parameter for passing logging configuration.
val Config : Parameter<IConfig>

/// The default configuration.
val Default : DefaultConfig

/// Implements support for hierarhical logging, similar to TraceSource.
[<Sealed>]
type Log =
    interface IParametric<Log>

    /// Sends a Critical-level message.
    member Critical : string -> unit

    /// Sends a Critical-level message.
    member Critical : string * obj -> unit

    /// Sends a Critical-level message.
    member Critical : string * [<ParamArray>] args: obj [] -> unit

    /// Sends an Error-level message.
    member Error : string -> unit

    /// Sends an Error-level message.
    member Error : string * obj -> unit

    /// Sends an Error-level message.
    member Error : string * [<ParamArray>] args: obj [] -> unit

    /// Sends an Info-level message.
    member Info : string -> unit

    /// Sends an Info-level message.
    member Info : string * obj -> unit

    /// Sends an Info-level message.
    member Info : string * [<ParamArray>] args: obj [] -> unit

    /// Sends a message at the specific level.
    member Message : Level * string -> unit

    /// Sends a message at the specific level.
    member Message : Level * string * obj -> unit

    /// Sends a Warn-level message.
    member Message : Level * string * [<ParamArray>] args: obj [] -> unit

    /// Creates a nested `Log`. If the current log is named "A.B", 
    /// `log.Nested("C")` will be named "A.B.C".
    member Nested : name: string -> Log

    /// Sends a Verbose-level message.
    member Verbose : string -> unit

    /// Sends a Verbose-level message.
    member Verbose : string * obj -> unit

    /// Sends a Verbose-level message.
    member Verbose : string * [<ParamArray>] args: obj [] -> unit

    /// Sends a Warn-level message.
    member Warn : string -> unit

    /// Sends a Warn-level message.
    member Warn : string * obj -> unit

    /// Sends a Warn-level message.
    member Warn : string * [<ParamArray>] args: obj [] -> unit

    /// Creates a log, inferring the name from the given type.
    static member Create<'T> : IParametric -> Log

    /// Creates a new log with a dotted hierarhical name, as in:
    /// `Log.Create("IntelliFactory.My.Module")`.
    static member Create : name: string * IParametric -> Log
