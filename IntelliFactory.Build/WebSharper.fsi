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

open IntelliFactory.Core

module WebSharperConfig =

    /// Version of WebSharper to use for resolution.
    val WebSharperVersion : Parameter<option<string>>

    /// Path to the directory containing WebSharper tools.
    val WebSharperHome : Parameter<option<string>>

    /// Path to the HTML output directory.
    val WebSharperHtmlDirectory : Parameter<string>

[<Sealed>]
type WebSharperProject =
    interface INuGetExportingProject
    interface IParametric
    interface IParametric<WebSharperProject>
    interface IProject
    interface IReferenceProject

[<Sealed>]
type WebSharperHostWebsite =
    interface IParametric
    interface IParametric<WebSharperHostWebsite>
    interface IProject

[<Sealed>]
type WebSharperProjects =
    member Extension : name: string -> WebSharperProject
    member HtmlWebsite : name: string -> WebSharperProject
    member HostWebsite : name: string -> WebSharperHostWebsite
    member Library : name: string -> WebSharperProject
    static member internal Current : Parameter<WebSharperProjects>
