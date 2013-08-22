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
open IntelliFactory.Core
open IntelliFactory.Build

type Solution =
    {
        env : Parameters
        projects : list<IProject>
    }

    member s.WithDomain(f: AppDomain -> 'T) =
        let setup = AppDomainSetup()
        setup.LoaderOptimization <- LoaderOptimization.MultiDomainHost
        let dom = AppDomain.CreateDomain("Build", null, setup)
        try
            f dom
        finally
            AppDomain.Unload(dom)

    member s.Build() =
        s.WithDomain <| fun dom ->
            for p in s.projects do
                let q = BuildConfig.AppDomain.Custom dom p.Parametric
                q.Build()

    member s.Clean() =
        s.WithDomain <| fun dom ->
            for p in s.projects do
                let q = BuildConfig.AppDomain.Custom dom p.Parametric
                q.Clean()

    member s.PrepareReferences() =
        for p in s.projects do
            p.PrepareReferences()

[<Sealed>]
type Solutions(env: Parameters) =
    static let current = Parameter.Define(fun env -> Solutions env)

    member t.Solution ps =
        {
            env = env
            projects = Seq.toList ps
        }

    static member Current = current
