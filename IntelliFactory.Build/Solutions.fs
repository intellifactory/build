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

    member s.Build() =
        let fwt = Frameworks.Current.Find s.env
        let rt = References.Current.Find s.env
        let refs =
            fwt.Cache(fun fw ->
                s.projects
                |> List.filter (fun p -> p.Framework = fw)
                |> Seq.collect (fun p -> p.References)
                |> rt.Resolve fw)
        let log = Log.Create<Solution> s.env
        for p in s.projects do
            let rs =
                refs p.Framework
                |> rt.ResolveProjectReferences p.References
            log.Info("Building {0} for {1}", p.Name, p.Framework.Name)
            p.Build(rs)

    member s.Clean() =
        for p in s.projects do
            p.Clean()

[<Sealed>]
type Solutions(env: Parameters) =
    static let current = Parameter.Define(fun env -> Solutions env)

    member t.Solution ps =
        {
            env = env
            projects = Seq.toList ps
        }

    static member Current = current
