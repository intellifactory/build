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
