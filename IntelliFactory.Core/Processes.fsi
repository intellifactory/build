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

/// Utilities for working with system processes.
[<AutoOpen>]
module IntelliFactory.Core.Processes

open System
open System.Text

/// Wraps an operating system process.
[<Sealed>]
type ProcessHandle =

    /// Contains native resources.
    interface IDisposable

    /// The exit code of the current process, available when the process finishes.
    member ExitCode : Future<int>

    /// Kills the process.
    member Kill : unit -> unit

    /// Sends text on the standard input.
    member SendInput : string -> unit

    /// Creates a default configuration record.
    static member Configure : toolPath: string * ?args: string -> ProcessHandleConfig

/// Various options for starting processes.
and ProcessHandleConfig =
    {
        Arguments : string
        EnvironmentVariables : Map<string,string>
        OnExit : int -> unit
        OnStandardError : string -> unit
        OnStandardOutput : string -> unit
        StandardErrorEncoding : Encoding
        StandardInputEncoding : Encoding
        StandardOutputEncoding : Encoding
        ToolPath : string
        TraceError : exn -> unit
        WorkingDirectory : string
    }

    /// Starts a process based on the current configuration.
    member Start : unit -> ProcessHandle

/// Manages an operating system process, including automatically
/// starting it on first input and automaticlaly restarting it on failure.
[<Sealed>]
type ProcessService =

    /// Stops the internal process and finalizes everything.
    member Finalize : unit -> Async<unit>

    /// Stops and re-starts the process.
    member Restart : unit -> unit

    /// Sends text on the standard input. Starts the process if not started.
    member SendInput : string -> unit

    /// Starts the process, if in idle state. Does nothing if the process is already started.
    member Start : unit -> unit

    /// Stops the proces with `Kill`.
    member Stop : unit -> unit

    /// Creates a new ProcessService. It will be started on first input or explicitly.
    static member Configure : toolPath: string * ?args: string -> ProcessServiceConfig

/// Options for `ProcessService`.
and ProcessServiceConfig =
    {
        ProcessHandleConfig : ProcessHandleConfig
        RestartInterval : TimeSpan
    }
     
    /// Functionally updates the `ProcessHandleConfig` field.
    member Configure : (ProcessHandleConfig -> ProcessHandleConfig) -> ProcessServiceConfig

    /// Creates a `ProcessService` in stopped state.
    member Create : unit -> ProcessService

