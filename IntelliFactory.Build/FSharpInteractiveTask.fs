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
