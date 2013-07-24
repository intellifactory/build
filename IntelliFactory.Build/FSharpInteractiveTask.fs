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

open System
open System.IO
open System.Runtime.Hosting
open System.Security
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Microsoft.Build.Tasks

[<SecurityCritical>]
[<Sealed>]
type FSharpInteractiveTask() =
    inherit Task()

    member val FSharpHome = null : string with get, set
    member val FSharpInteractive = null : string with get, set
    member val WorkingDirectory = "." : string with get, set

    [<Required>]
    member val Script = null : string with get, set

    [<SecurityCritical>]
    override t.Execute() =
        let pf = GetProgramFiles ()
        let fsHome =
            match t.FSharpHome with
            | null | "" -> Path.Combine(pf, "Microsoft SDKs", "F#", "3.0", "Framework", "v4.0")
            | home -> home
        let fsi =
            match t.FSharpInteractive with
            | null | "" -> Path.Combine(fsHome, "fsi.exe")
            | fsi -> fsi
        let args =
            [|
                "--exec"
                t.Script
            |]
        let execTask = Exec()
        execTask.BuildEngine <- t.BuildEngine
        execTask.Command <- String.concat " " (Array.append [| String.Format(@"""{0}""", fsi) |] args)
        execTask.WorkingDirectory <- t.WorkingDirectory
        execTask.Execute()
