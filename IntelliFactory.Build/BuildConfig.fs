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

module BuildConfig =

    let CurrentFramework =
        Parameter.Define(fun env -> Frameworks.Current.Find(env).Net45)

    let RootDir =
        Parameter.Create (Directory.GetCurrentDirectory())

    let BuildDir =
        Parameter.Define(fun env ->
            Path.Combine(RootDir.Find env, "build"))

    let OutputDir =
        Parameter.Define(fun env ->
            let bd = BuildDir.Find env
            let fw = CurrentFramework.Find env
            Path.Combine(bd, fw.Name))

    let BuildNumber =
        Parameter.Define(fun env ->
            match Environment.GetEnvironmentVariable("BUILD_NUMBER") with
            | null | "" -> None
            | n ->
                match Int32.TryParse(n) with
                | true, x -> Some x
                | _ -> None)

    let KeyFile =
        Parameter.Define(fun env ->
            let c = Company.Current.Find env
            match c with
            | None -> None
            | Some c -> c.KeyFile())

    let ProjectName =
        Parameter.Create "Library"

    let AppDomain =
        Parameter.Create AppDomain.CurrentDomain
