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

/// Implements utilities for tracing on top of `System.Diagnostics.TraceSource`.
namespace IntelliFactory.Build

open System
open System.Diagnostics
open System.IO

/// Represents the level at which a message is being logged..
[<Sealed>]
type LogLevel =
    interface IComparable

    /// The printable name.
    member Name : string

    /// Critical level.
    static member Critical : LogLevel

    /// Error level.
    static member Error : LogLevel

    /// Informational level.
    static member Info : LogLevel

    /// Verbose level.
    static member Verbose : LogLevel

    /// Warning level.
    static member Warn : LogLevel

type ILogger =
    abstract ShouldTrace : LogLevel -> bool
    abstract Trace : LogLevel * string -> unit

type ILogConfig =
    abstract GetNamedLogger : string -> ILogger

[<Sealed>]
type LogConfig =
    interface ILogConfig
    new : unit -> LogConfig
    member Critical : unit -> LogConfig
    member Critical : string -> LogConfig
    member Default : LogLevel -> LogConfig
    member Error : unit -> LogConfig
    member Error : string -> LogConfig
    member Info : unit -> LogConfig
    member Info : string -> LogConfig
    member Restrict : string * LogLevel -> LogConfig
    member ToConsole : unit -> LogConfig
    member ToDiagnostics : unit -> LogConfig
    member ToTraceSource : TraceSource -> LogConfig
    member Verbose : unit -> LogConfig
    member Verbose : string -> LogConfig
    member Warn : unit -> LogConfig
    member Warn : string -> LogConfig
    static member Current : Parameter<ILogConfig>

/// Wraps TraceSource with support for hierarhical logging and more utility methods.
///
/// How it works: the hierarhical name `A.B.C` creates `TraceSource` objects like this:
/// `A`, `A.B`, `A.B.C`. These `TraceSource` objects may be configured in `App.config` or
/// `Web.config` to propagate messages of certain priority to loggers.
/// When a message is sent to a `Log` object, it is transfered to the most specific
/// `TraceSource` ready to accept it, with prefixing as necessary to identify where it
/// comes from.
[<Sealed>]
type Log =

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
    member Message : LogLevel * string -> unit

    /// Sends a message at the specific level.
    member Message : LogLevel * string * obj -> unit

    /// Sends a Warn-level message.
    member Message : LogLevel * string * [<ParamArray>] args: obj [] -> unit

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

    /// Configures the logging system.
    static member Configure : ILogConfig -> Parameters -> Parameters

    /// Creates a log, inferring the name from the given type.
    static member Create<'T> : Parameters -> Log

    /// Creates a new log with a dotted hierarhical name, as in:
    /// `Log.Create("IntelliFactory.My.Module")`.
    static member Create : name: string * Parameters -> Log

