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

/// Utilities for working with Mercurial (Hg) repositories.
module IntelliFactory.Build.Mercurial

open System
open System.IO

/// Parses Mercurial hash from the binary `.hg/dirstate` content.
let ParseHash (bytes: byte[]) : string =
    Array.sub bytes 0 20
    |> Array.map (fun b -> String.Format("{0:x2}", b))
    |> String.concat ""

let InferTag (folder: string) : option<string> =
    let tagsFile = Path.Combine(folder, ".hg", "cache", "tags")
    let tagMap =
        if File.Exists tagsFile then
            File.ReadAllLines tagsFile
            |> Seq.choose (fun line ->
                let parts = line.Trim().Split(' ')
                match parts.Length with
                | 2 -> Some (parts.[0], parts.[1])
                | _ -> None)
            |> dict
        else
            dict []
    let dirStateFile = Path.Combine(folder, ".hg", "dirstate")
    if File.Exists dirStateFile then
        let hash = ParseHash (File.ReadAllBytes dirStateFile)
        match tagMap.TryGetValue(hash) with
        | true, tag -> Some tag
        | _ -> Some hash
    else
        None
