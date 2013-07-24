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
