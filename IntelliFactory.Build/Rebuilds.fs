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

[<AutoOpen>]
module internal IntelliFactory.Build.Rebuilds

open System
open System.IO
open IntelliFactory.Core
open IntelliFactory.Build

let getFile (p: string) =
    let f = FileInfo p
    f.Refresh()
    f

type RebuildDecision =
    {
        isStale : bool
        outputs : FileInfo []
        reason : string
    }

    override d.ToString() = d.reason
    member d.IsStale = d.isStale
    member d.Reason = d.reason

    member d.Touch() =
        for f in d.outputs do
            f.LastWriteTimeUtc <- DateTime.UtcNow

let addPaths (p1: seq<string>) (p2: seq<string>) =
    Seq.append p1 p2
    |> Seq.filter (String.IsNullOrWhiteSpace >> not)
    |> Seq.distinct
    |> Seq.toArray

let stale (log: Log) outputs (reason: string) =
    log.Verbose("Rebuilding: {0}", reason)
    {
        isStale = true
        outputs = outputs
        reason = reason
    }

let valid (log: Log) outputs (reason: string) =
    log.Verbose("Skipping: {0}", reason)
    {
        isStale = false
        outputs = outputs
        reason = reason
    }

let lastWrite (file: FileInfo) =
    file.LastWriteTimeUtc

[<Sealed>]
type RebuildProblem(env, input: string[], output: string[]) =
    let log = Log.Create<RebuildProblem>(env)

    member p.AddInputPaths(ps) = RebuildProblem(env, addPaths ps input, output)
    member p.AddOutputPaths(ps) = RebuildProblem(env, input, addPaths ps output)

    member p.Decide() =
        let input = Array.map getFile input
        let output = Array.map getFile output
        let missing =
            output
            |> Array.tryFind (fun f -> f.Exists |> not)
        match missing with
        | Some m -> stale log output ("Missing output file: " + m.FullName)
        | None ->
            let input =
                input
                |> Array.filter (fun i ->
                    if i.Exists then true else
                        log.Warn("Missing input file: " + i.FullName)
                        false)
            let i = Seq.maxBy lastWrite input
            let o = Seq.minBy lastWrite output
            if lastWrite i >= lastWrite o then
                stale log output ("Input file has changed: " + i.FullName)
            else
                use out = new StringWriter()
                out.WriteLine("Skipping build. Files checked:")
                for i in input do
                    out.WriteLine("    IN  {0} {1}", i.FullName, lastWrite i)
                for o in output do
                    out.WriteLine("    OUT {0} {1}", o.FullName, lastWrite o)
                let msg = out.ToString()
                log.Verbose msg
                valid log output "No changed since last build."

    static member Create(env) =
        RebuildProblem(env, Array.empty, Array.empty)
