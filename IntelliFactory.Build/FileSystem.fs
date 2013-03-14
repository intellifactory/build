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

module IntelliFactory.Build.FileSystem

open System.IO
open System.Text

let DefaultEncoding =
    UTF8Encoding(false) :> Encoding

let PrepareFileForWriting (fullPath: string) : unit =
    let d = Path.GetDirectoryName fullPath
    if not (Directory.Exists d) then
        ignore (Directory.CreateDirectory d)

let EnsureBinaryFile (fullPath: string) (contents: byte []) : unit =
    PrepareFileForWriting fullPath
    let inline def () = File.WriteAllBytes(fullPath, contents)
    if File.Exists fullPath
        then if File.ReadAllBytes fullPath <> contents then def ()
        else def ()

let EnsureTextFile (fullPath: string) (contents: string) : unit =
    PrepareFileForWriting fullPath
    let inline def () = File.WriteAllText(fullPath, contents, DefaultEncoding)
    if File.Exists fullPath
        then if File.ReadAllText fullPath <> contents then def ()
        else def ()

type Binary =
    private { data: byte [] }

    member this.GetBytes() = Array.copy this.data
    member this.Read() = new MemoryStream(this.data, false) :> Stream
    member this.Write(s: Stream) = s.Write(this.data, 0, this.data.Length)
    member this.WriteFile(p) = EnsureBinaryFile p this.data
    static member FromBytes(bytes) = { data = Array.copy bytes }
    static member ReadFile(fullPath) = { data = File.ReadAllBytes(fullPath) }

    static member ReadStream(s: Stream) =
        use m = new MemoryStream()
        let buf = Array.zeroCreate (8 * 1024)
        let rec loop () =
            let k = s.Read(buf, 0, buf.Length)
            if k > 0 then
                m.Write(buf, 0, k)
                loop ()
        loop ()
        { data = m.ToArray() }

type Content =
    | BinaryContent of Binary
    | TextContent of string

    member this.WriteFile(p) =
        match this with
        | BinaryContent b -> b.WriteFile(p)
        | TextContent s -> EnsureTextFile p s

    static member ReadBinaryFile(p) =
        BinaryContent (Binary.ReadFile p)

    static member ReadTextFile(p) =
        File.ReadAllText(p, DefaultEncoding)
        |> TextContent

