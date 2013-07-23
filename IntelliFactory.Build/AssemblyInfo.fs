namespace IntelliFactory.Build

open System
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Runtime.Versioning
open IntelliFactory.Build
open IntelliFactory.Core

type AssemblyInfoSyntax =
    | CSharpSyntax
    | FSharpSyntax

    static member CSharp = CSharpSyntax
    static member FSharp = FSharpSyntax

[<Sealed>]
type AssemblyInfoAttribute(typeName: string) =
    static member Create(n) = AssemblyInfoAttribute(n)

    member a.Generate(stx: AssemblyInfoSyntax, out: TextWriter) =
        match stx with
        | CSharpSyntax -> out.WriteLine("[{0}]", typeName)
        | FSharpSyntax -> out.WriteLine("[<assembly: global.{0}>]", typeName)

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

    static member Current =
        AssemblyInfoParameters.Current

    static member Default =
        {
            ClsCompilant = None
            ComVisible = None
            Company = None
            Configuration = None
            Copyright = None
            Culture = None
            CustomAttributes = []
            Description = None
            FileVersion = None
            Guid = None
            InfoVersion = None
            Product = None
            TargetFramework = None
            Title = None
            Trademark = None
            Version = None
        }

and [<Sealed>] AssemblyInfoParameters() =

    static let current =
        Parameter.Define(fun env ->
            let fwt = Frameworks.Current.Find env
            let pv = PackageVersion.Current.Find env
            let fv = PackageVersion.Full.Find env
            let cf = BuildConfig.CurrentFramework.Find env
            let comp = Company.Current.Find env
            let copy =
                comp
                |> Option.map (fun c ->
                    String.Format("Copyright (c) {0} {1}", DateTime.UtcNow.Year, c.Name))
            {
                AssemblyInfoData.Default with
                    Company = comp |> Option.map (fun c -> c.Name)
                    Copyright = copy
                    FileVersion = Some fv
                    InfoVersion = Some fv
                    Product = Some (PackageId.Current.Find env)
                    TargetFramework = Some (fwt.ToFrameworkName cf)
                    Version =
                        let v = Version(pv.Major, pv.Minor)
                        Some v
            })

    static member Current = current

[<AutoOpen>]
module AssemblyInfoGeneration =

    let escape (s: string) =
        let s = s.Replace(@"""", @"""""")
        String.Format(@"@""{0}""", s)

    let attr (t: Type) (o: TextWriter) s v =
        let fullName = t.FullName
        match s with
        | CSharpSyntax -> o.WriteLine(@"[assembly: {0}({1})]", fullName, v)
        | FSharpSyntax -> o.WriteLine(@"[<assembly: {0}({1})>]", fullName, v)

    let str t o s v =
        match v with
        | Some v -> attr t o s (escape v)
        | None -> ()

    let obj t o s (v: option<'T>) =
        match v with
        | Some v -> attr t o s (escape (string (box v)))
        | None -> ()

    let ver t o s (v: option<Version>) =
        obj t o s v

    let guid t o s (v: option<Guid>) =
        obj t o s v

    let fw t o s (v: option<FrameworkName>) =
        obj t o s v

    let bool t o s (v: option<bool>) =
        match v with
        | Some v -> attr t o s (if v then "true" else "false")
        | None -> ()

    let t<'T> = typeof<'T>

    let gen (fwt: Frameworks) s d =
        use o = new StringWriter()

        if s = FSharpSyntax then
            o.WriteLine "namespace System"

        bool t<CLSCompliantAttribute> o s d.ClsCompilant
        bool t<ComVisibleAttribute> o s d.ComVisible

        guid t<GuidAttribute> o s d.Guid

        match d.TargetFramework with
        | Some f ->
            if f.Version >= Version "4.0" then
                fw t<TargetFrameworkAttribute> o s d.TargetFramework
        | _ -> ()

        str t<AssemblyCompanyAttribute> o s d.Company
        str t<AssemblyConfigurationAttribute> o s d.Configuration
        str t<AssemblyCopyrightAttribute> o s d.Copyright
        str t<AssemblyCultureAttribute> o s d.Culture
        str t<AssemblyDescriptionAttribute> o s d.Description
        str t<AssemblyProductAttribute> o s d.Product
        str t<AssemblyTitleAttribute> o s d.Title
        str t<AssemblyTrademarkAttribute> o s d.Trademark

        ver t<AssemblyFileVersionAttribute> o s d.FileVersion
        ver t<AssemblyVersionAttribute> o s d.Version
        ver t<AssemblyInformationalVersionAttribute> o s d.InfoVersion

        for a in d.CustomAttributes do
            a.Generate(s, o)

        if s = FSharpSyntax then
            o.WriteLine "do ()"

        o.ToString()

[<Sealed>]
type AssemblyInfoGenerator(env) =
    static let current = Parameter.Define(fun env -> AssemblyInfoGenerator(env))
    let fwt = Frameworks.Current.Find env

    member g.Generate(s, d, outFile: string) =
        Content.Text(gen fwt s d).WriteFile(outFile)

    static member Current = current
