module MakeStuff

open System
open System.IO

type LocalPath = string

type Target =
    | PathTarget of LocalPath
    | PhonyTarget of string

type Input =
    | PathInput of LocalPath
    | TargetInput of Target

type Makefile =
    Target -> seq<Input> * (unit -> unit)

let lastWrite (p: LocalPath) =
    if File.Exists(p) then
        let info = FileInfo(p)
        info.Refresh()
        Some info.LastWriteTimeUtc
    elif Directory.Exists(p) then
        let info = DirectoryInfo(p)
        info.Refresh()
        Some info.LastAccessTimeUtc
    else
        None

let needsBuild (output: LocalPath) (inputs: seq<LocalPath>) =
    match lastWrite output with
    | None -> true
    | Some w ->
        inputs
        |> Seq.exists (fun p ->
            match lastWrite p with
            | None -> true
            | Some t -> t > w)

let rec Build (mk: Makefile) (t: Target) : unit =
    let (inputs, act) = mk t
    for i in inputs do
        match i with
        | PathInput _ -> ()
        | TargetInput t -> Build mk t
    let doBuild =
        match t with
        | PhonyTarget _ -> true
        | PathTarget p ->
            inputs
            |> Seq.choose (fun i ->
                match i with
                | PathInput p -> Some p
                | _ -> None)
            |> needsBuild p
    if doBuild then
        act ()

(*

makefile:

Target -> seq<Input> * recipe

when we build a given target,
  it recursively builds inputs, 
  then for decides if it is stale yet?


*)

