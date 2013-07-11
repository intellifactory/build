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

[<AutoOpen>]
module internal IntelliFactory.Build.Utilities

#if INTERACTIVE
open IntelliFactory.Build
#endif

open System
open System.IO

let IsFile f =
    FileInfo(f).Exists

let IsDir d =
    DirectoryInfo(d).Exists

let PrepareDir f =
    let d = Path.GetDirectoryName(f)
    if not (IsDir d) then
        Directory.CreateDirectory(d)
        |> ignore

let (|Is|_|) x y =
    if x = y then Some () else None

let ( +/ ) a b =
    Path.Combine(a, b)

let NullGuard x =
    match x with
    | null -> None
    | _ -> Some x

let Reify xs =
    Seq.toArray xs
    |> Seq.ofArray

let GetProgramFiles () =
    [
        Environment.SpecialFolder.ProgramFilesX86
        Environment.SpecialFolder.ProgramFiles
    ]
    |> List.map (Environment.GetFolderPath >> NullGuard)
    |> List.pick (fun x -> x)

let GetLines (t: string) =
    let sep = [| "\n"; "\r\n" |]
    t.Split(sep, Int32.MaxValue, StringSplitOptions.RemoveEmptyEntries)
