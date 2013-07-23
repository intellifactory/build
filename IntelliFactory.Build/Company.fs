namespace IntelliFactory.Build

open System
open System.IO
open IntelliFactory.Build
open IntelliFactory.Core

[<Sealed>]
type Company(name: string, ?keyFile: string) =

    static let current =
        Parameter.Define(fun env ->
            match Environment.GetEnvironmentVariable("INTELLIFACTORY") with
            | null | "" -> None
            | dir ->
                let kf = Path.Combine(dir, "keys", "IntelliFactory.snk")
                let c = Company("IntelliFactory", kf)
                Some c)

    member c.KeyFile() = keyFile
    member c.KeyFile kf = Company(name, kf)
    member c.Name = name

    static member Create name = Company(name)
    static member Current = current
