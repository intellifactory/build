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

/// Support for running MSBuild projects.
module IntelliFactory.Build.MSBuild

open System
open System.IO
open System.Security
open Microsoft.Build.Execution
open IntelliFactory.Build
open IntelliFactory.Core

/// Simple MSBuild project builder.
[<Sealed>]
type MSBuildProject internal (env: IParametric, path: string, ?props: Map<string,string>) =
    let fullPath = Path.Combine(BuildConfig.RootDir.Find env, path)
    let fw = BuildConfig.CurrentFramework.Find env
    let props = defaultArg props Map.empty

    [<SecuritySafeCritical>]
    let getProject () =
        ProjectInstance(fullPath, props, "4.0")

    [<SecuritySafeCritical>]
    let build (target: string) =
        if getProject().Build(target, Seq.empty) |> not then
            failwith "MSBuild failed"

    interface IProject with
        member p.Build() = build "Build"
        member p.Clean() = build "Clean"
        member p.Name = Path.GetFileName fullPath
        member p.Framework = fw
        member p.References = Seq.empty

        member p.Parametric =
            {
                new IParametric<IProject> with
                    member p.WithParameters env = MSBuildProject(env, path, props) :> _
                interface IParametric with
                    member p.Parameters = env.Parameters
            }

    /// Sets any property.
    member p.Property(name, value) =
        MSBuildProject(env, path, Map.add name value props)

    /// Sets the configuration property.
    member p.Configuration(cfg) =
        p.Property("Configuration", cfg)
