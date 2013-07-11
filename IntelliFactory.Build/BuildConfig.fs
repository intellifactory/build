namespace IntelliFactory.Build

open System
open System.IO

#if INTERACTIVE
open IntelliFactory.Build
#endif

module BuildConfig =

    let CurrentFramework =
        Parameter.Define(fun env -> Frameworks.Current.Find(env).Net45)

    let RootDir =
        Parameter.Create "."

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
