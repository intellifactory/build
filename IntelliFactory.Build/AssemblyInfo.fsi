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
open System.Runtime.Versioning
open IntelliFactory.Core

[<Sealed>]
type AssemblyInfoAttribute =
    static member Create : typeName: string -> AssemblyInfoAttribute

type AssemblyInfoData =
    {
        ClsCompilant : option<bool>
        ComVisible : option<bool>
        Company : option<string>
        Configuration : option<string>
        Copyright : option<string>
        Culture : option<string>
        CustomAttributes : list<AssemblyInfoAttribute>
        Description : option<string>
        FileVersion : option<Version>
        Guid : option<Guid>
        InfoVersion : option<Version>
        Product : option<string>
        TargetFramework : option<FrameworkName>
        Title : option<string>
        Trademark : option<string>
        Version : option<Version>
    }

    static member Current : Parameter<AssemblyInfoData>

[<Sealed>]
type internal AssemblyInfoSyntax =
    static member CSharp : AssemblyInfoSyntax
    static member FSharp : AssemblyInfoSyntax

[<Sealed>]
type internal AssemblyInfoGenerator =
    member Generate : syntax: AssemblyInfoSyntax * info: AssemblyInfoData * outputFile: string -> unit
    static member Current : Parameter<AssemblyInfoGenerator>
