namespace IntelliFactory.Build

open System

//[<Sealed>]
//type WebSharperConfig() =
//    static let home : Parameter<option<string>> = Parameter.Create None
//    static member Home = home
//
//(* now: add default *)
//
//type WebSharperProjectKind =
//    | WebSharperLibrary
//
//module WebSharperReferences =
//
//    let Compute env =
//        let rt = References.Current.Find env
//        let rb = ReferenceBuilder.Current.Find env
//        let ws = rb.NuGet("WebSharper")
//        let rs =
//            [
//                "IntelliFactory.Formlet"
//                "IntelliFactory.Html"
//                "IntelliFactory.JavaScript"
//                "IntelliFactory.Reactive"
//                "IntelliFactory.WebSharper"
//                "IntelliFactory.WebSharper.Collections"
//                "IntelliFactory.WebSharper.Control"
//                "IntelliFactory.WebSharper.Core"
//                "IntelliFactory.WebSharper.Dom"
//                "IntelliFactory.WebSharper.Ecma"
//                "IntelliFactory.WebSharper.Formlet"
//                "IntelliFactory.WebSharper.Html"
//                "IntelliFactory.WebSharper.Html5"
//                "IntelliFactory.WebSharper.InterfaceGenerator"
//                "IntelliFactory.WebSharper.JQuery"
//                "IntelliFactory.WebSharper.Sitelets"
//                "IntelliFactory.WebSharper.Testing"
//                "IntelliFactory.WebSharper.Web"
//                "IntelliFactory.Xml"
//            ]
//        let makeRef (n: string) =
//            let p = String.Format("/tools/net40/{0}.dll", n)
//            ws.At(p).Reference()
//        [
//            for r in rs ->
//                makeRef r
//        ]
//
//[<Sealed>]
//type WebSharperProject(fs: FSharpProject, env) =
//    let fp = fs :> IProject
//    let rs = WebSharperReferences.Compute env
//
//    member p.Build(rr) =
//        let out = fs.Config.Ou
//        fs.Configure(fun cfg -> { cfg with OutputPath = Some out})
//        fp.Build(rr)
//
//    member p.Clean() =
//        fp.Clean()
//
//    member p.LibraryFiles =
//        Seq.singleton "A"
//
//    interface INuGetExportingProject with
//        member p.LibraryFiles = p.LibraryFiles
//
//    interface IProject with
//        member p.Build(rr) = p.Build(rr)
//        member p.Clean() = p.Clean()
//        member p.Framework = fp.Framework
//        member p.GeneratedAssemblyFiles = fp.GeneratedAssemblyFiles
//        member p.Name = fp.Name
//        member p.References = Seq.append rs fp.References
//
//[<Sealed>]
//type WebSharperProjects(env) =
//    static let current = Parameter.Define(fun env -> WebSharperProjects env)
//    let fps = FSharpProjects.Current.Find env
//
//    member ps.Library(name: string) =
//        WebSharperProject(fps.Library name, env)
//
//    static member Current = current
