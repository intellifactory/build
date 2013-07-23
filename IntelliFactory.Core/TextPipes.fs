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
module IntelliFactory.Core.TextPipes

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks

#nowarn "40"

type TextWriterMessage =
    | OnClose
    | OnFlush
    | OnWrite of string
    | OnWriteLine of string

[<Sealed>]
type FunctionTextWriter(i: TextWriterMessage -> unit, ?enc) =
    inherit TextWriter()

    let enc =
        match enc with
        | None -> UTF8Encoding(false, true) :> Encoding
        | Some enc -> enc

    let task x =
        Task.FromResult x :> Task

    override w.Close() = i OnClose

    override w.Write(data: char) = i (OnWrite (string data))
    override w.Write(data: char[]) = i (OnWrite (String data))
    override w.Write(data: string) = i (OnWrite data)
    override w.Write(data: char[], offset: int, count: int) = i (OnWrite (String(data, offset, count)))

    override w.WriteLine(data: char) = i (OnWriteLine (string data))
    override w.WriteLine(data: char[]) = i (OnWriteLine (String data))
    override w.WriteLine(data: string) = i (OnWriteLine data)
    override w.WriteLine(data: char[], offset: int, count: int) = i (OnWriteLine (String(data, offset, count)))

    override w.WriteAsync(data: char) = w.Write(data); task ()
    override w.WriteAsync(data: string) = w.Write(data); task ()
    override w.WriteAsync(d, o, c) = w.Write(d, o, c); task c

    override w.WriteLineAsync(data: char) = w.WriteLine(data); task ()
    override w.WriteLineAsync(data: string) = w.WriteLine(data); task ()
    override w.WriteLineAsync(d, o, c) = w.WriteLine(d, o, c); task c

    override w.Encoding = enc

    override w.Flush() = i OnFlush
    override w.FlushAsync() = i OnFlush; task ()

[<Sealed>]
type NonBlockingTextWriter =

    static member Create(out: string -> unit, ?bufferSize: int, ?encoding) =
        let size = defaultArg bufferSize (8 * 1024)
        let agent =
            MailboxProcessor.Start(fun agent ->
                let buf = StringBuilder(size)
                let flush () =
                    let s = buf.Reset()
                    if s.Length > 0 then
                        out s
                let autoFlush () =
                    if buf.Length >= size then
                        flush ()
                let rec loop =
                    async {
                        let! msg = agent.Receive()
                        match msg with
                        | OnClose ->
                            do flush ()
                            do out ""
                            return ()
                        | OnFlush ->
                            do flush ()
                            return! loop
                        | OnWrite t ->
                            do buf.Append(t) |> ignore
                            do autoFlush ()
                            return! loop
                        | OnWriteLine t ->
                            do buf.AppendLine(t) |> ignore
                            do autoFlush ()
                            return! loop
                    }
                loop)
        new FunctionTextWriter(agent.Post, ?enc = encoding) :> TextWriter

[<AutoOpen>]
module TextPipes =

    type Buf = ArraySegment<char>

    type ReadCont =
        {
            OnDone : int -> unit
            Buf : Buf
        }

    type Message =
        | OnDone
        | OnRead of ReadCont
        | OnWrite of string

    type ReadConts = BatchedQueues.BatchedQueue<ReadCont>

    type PipeState =
        | PipeEmpty
        | PipeFull
        | PipeWaiting of ReadConts

    [<Sealed>]
    type PipeAgent() =

        let b = StringBuilder(1024)

        let emptyBuf r =
            let k = b.Dequeue r.Buf
            Async.Spawn(r.OnDone, k)
            match b.Length with
            | 0 -> true
            | _ -> false

        let rec feedMany rs =
            match rs with
            | BatchedQueues.With (x, xs) ->
                if emptyBuf x then PipeWaiting xs else feedMany xs
            | BatchedQueues.Empty ->
                if b.Length = 0 then PipeEmpty else PipeFull

        member a.Read r st =
            match st with
            | PipeEmpty -> PipeWaiting (BatchedQueues.BatchedQueue().Enqueue(r))
            | PipeFull -> if emptyBuf r then PipeEmpty else PipeFull
            | PipeWaiting q -> PipeWaiting(q.Enqueue r)

        member a.WriteString (data: string) st =
            if data.Length > 0 then
                b.Append(data) |> ignore
                match st with
                | PipeEmpty | PipeFull -> PipeFull
                | PipeWaiting rs -> feedMany rs
            else st

        member a.Done st =
            match st with
            | PipeEmpty -> ()
            | PipeFull -> ()
            | PipeWaiting rs ->
                for r in rs do
                    Async.Spawn(r.OnDone, 0)

    let startAgent () =
        MailboxProcessor.Start(fun self ->
            let a = PipeAgent()
            let rec loop st =
                async {
                    let! msg = self.Receive()
                    match msg with
                    | OnRead r -> return! loop (a.Read r st)
                    | OnWrite s -> return! loop (a.WriteString s st)
                    | OnDone -> return a.Done st
                }
            loop PipeEmpty)

    type Agent = MailboxProcessor<Message>

    [<Sealed>]
    type PipeReader(agent: Agent) =
        inherit TextReader()
        let mutable closed = false

        override x.Close() =
            closed <- true
            agent.Post OnDone

        override x.Read() =
            if closed then -1 else
                let buf = Array.zeroCreate 1
                match x.ReadAsync(buf, 0, 1).Result with
                | 0 -> -1
                | _ -> int buf.[0]

        override x.Read(buf, pos, ct) =
            if closed then 0 else
                x.ReadAsync(buf, pos, ct).Result

        override x.ReadAsync(buf, pos, ct) =
            if closed then Task.FromResult 0 else
                Async.FromContinuations(fun (ok, _, _) ->
                    Message.OnRead {
                        OnDone = ok
                        Buf = Buf(buf, pos, ct)
                    }
                    |> agent.Post)
                |> Async.StartAsTask

[<Sealed>]
type TextPipe private (?buf, ?enc) =
    let agent = startAgent ()
    let r = new PipeReader(agent) :> TextReader
    let send s = agent.Post(OnWrite s)
    let w = NonBlockingTextWriter.Create(send, ?bufferSize = buf, ?encoding = enc)

    member x.Close() =
        agent.Post OnDone
        w.Close()

    member x.Reader = r
    member x.Writer = w

    static member Create(?bufferSize, ?encoding) =
        TextPipe(?buf = bufferSize, ?enc = encoding)

