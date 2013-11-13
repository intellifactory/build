namespace IntelliFactory.Core

[<AutoOpen>]
module internal Net40Utility =
    open System
    open System.IO
    open System.Threading.Tasks

    #if NET40

    type Task with

        static member FromResult(x: 'T) : Task<'T> =
            Task.Factory.StartNew(fun () -> x)

    type Stream with

        member this.FlushAsync() : Task =
            Unchecked.defaultof<_>

        member this.WriteAsync(data: byte[]) : Task =
            Unchecked.defaultof<_>

    type TextWriter with

        member this.FlushAsync() : Task =
            Unchecked.defaultof<_>

        member this.WriteAsync(data: string) : Task =
            Unchecked.defaultof<_>

    type TextReader with

        member __.ReadAsync(buf: char[], offset: int, count: int) : Task<int> =
            Unchecked.defaultof<_>

    #endif

//    #if NET40
//
//    let flushStreamAsync (s: Stream) =
//        async { return s.Flush () }
//
//    type Stream with
//
//        member this.WriteAsync(data: byte[]) =
//            async { return this.Write(data, 0, data.Length) }
//
//        member this.WriteAsync(data: byte[]) =
//            async { return this.Write(data, 0, data.Length) }
//
//    let flushTextWriterAsync (s: TextWriter) =
//        async { return s.Flush () }
//
//    #else
//
//    let flushStreamAsync (s: Stream) =
//        s.FlushAsync() |> await
//
//    let flushTextWriterAsync (s: TextWriter) =
//        s.FlushAsync() |> await
//
//    #endif
