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
open System.Threading.Tasks

/// Various options for starting processes.
type ProcessHandleConfig =
    {
        Arguments : string
        EnvironmentVariables : Map<string,string>
        OnExit : int -> unit
        OnStandardOutput : string -> unit
        OnStandardError : string -> unit
        StandardErrorEncoding : Encoding
        StandardOutputEncoding : Encoding
        ToolPath : string
        WorkingDirectory : string
    }

/// Wraps a started operating system process.
[<Sealed>]
type ProcessHandle =

    /// Disposing of the handle kills the OS process and releases resources.
    interface IDisposable

    /// Kills the OS process and releases resources.
    member Dispose : unit -> unit

    /// The exit code of the current process, available when the process finishes
    member ExitCode : Future<int>

    /// Kills the process.
    member Kill : unit -> unit

    /// Sends text on the standard input.
    member SendInput : string -> unit

    /// Starts a new system process.
    static member Start : ProcessHandleConfig -> ProcessHandle

    /// Starts a new system process.
    static member Start :
        toolPath: string
        * ?args: string
        * ?config: (ProcessHandleConfig -> ProcessHandleConfig) ->
        ProcessHandle

/// Options for starting a `ProcessService`.
type ProcessServiceConfig =
    {
        ProcessHandleConfig : ProcessHandleConfig
        RestartInterval : TimeSpan
    }

/// Wraps a system process as a service.
[<Sealed>]
type ProcessService =

    /// Stops the internal process and finalizes everything.
    member Finalize : unit -> Async<unit>

    /// Stops and re-starts the process.
    member Restart : unit -> unit

    /// Starts the process, if in idle state. Does nothing if the process is already started.
    member Start : unit -> unit

    /// Stops the proces with a hard kill.
    member Stop : unit -> unit

    /// Sends text on the standard input. Starts the process if not started.
    member SendInput : string -> unit

    /// Creates a new ProcessService. It will be started on first input or explicitly.
    static member Create : ProcessServiceConfig -> ProcessService

    /// Creates a new ProcessService. It will be started on first input or explicitly.
    static member Create :
        toolPath: string
        * ?args: string
        * ?config: (ProcessServiceConfig -> ProcessServiceConfig) ->
        ProcessService
