namespace IntelliFactory.Build

#if INTERACTIVE
open IntelliFactory.Build
#endif

open System
open System.IO

type NuGetPackageConfig =
    internal {
        authors : list<string>
        contents : list<SafeNuGetPackageFile>
        description : string
        id : string
        licenseUrl : option<Uri>
        nuGetRefs : list<NuGetReference>
        outputPath : string
        projectUrl : option<Uri>
        requiresLicenseAcceptance : bool
        version : Version
        versionSuffix : option<string>
    }

[<Sealed>]
type NuGetPackageBuilder(cfg: NuGetPackageConfig, env) =
    let fwt = Frameworks.Current.Find env
    let rs = References.Current.Find env

    member p.Add(proj: FSharpProject) =
        let pr = proj :> IProject
        let ng =
            pr.References
            |> Seq.choose rs.GetNuGetReference
            |> Seq.append cfg.nuGetRefs
            |> Seq.distinct
            |> Seq.toList
        let fs =
            [
                for f in proj.LibraryFiles do
                    let t = Path.Combine("lib", pr.Framework.Name, Path.GetFileName f)
                    yield SafeNuGetPackageFile.Create(f, t)
            ]
        p.Update { cfg with nuGetRefs = ng; contents = cfg.contents @ fs }

    member p.Apache20License() =
        p.Update {
            cfg with
                requiresLicenseAcceptance = false
                licenseUrl = Some (Uri "http://www.apache.org/licenses/LICENSE-2.0")
        }

    member p.Authors(a: seq<string>) =
        p.Update { cfg with authors = Seq.toList cfg.authors }

    member p.Description d =
        p.Update { cfg with description = d }

    member p.Id id =
        p.Update { cfg with id = id }

    member p.LicenseAcceptance(?requires: bool) =
        p.Update { cfg with requiresLicenseAcceptance = defaultArg requires false }

    member p.LicenseUrl url =
        p.Update { cfg with licenseUrl = Some (Uri url) }

    member p.ProjectUrl url =
        p.Update { cfg with projectUrl = Some (Uri url) }

    member p.Build() =
        let pb = SafeNuGetPackageBuilder()
        pb.Id <- cfg.id
        if cfg.licenseUrl.IsSome then
            pb.LicenseUrl <- cfg.licenseUrl.Value
        pb.RequireLicenseAcceptance <- cfg.requiresLicenseAcceptance
        if cfg.projectUrl.IsSome then
            pb.ProjectUrl <- cfg.projectUrl.Value
        pb.Authors <- cfg.authors
        pb.Description <- cfg.description
        pb.Version <- SafeNuGetSemanticVersion(cfg.version, ?suffix = cfg.versionSuffix)
        pb.Files <- cfg.contents
        pb.DependencySets <-
            let deps =
                cfg.nuGetRefs
                |> Seq.distinctBy (fun r -> r.Id)
                |> Seq.map (fun r ->
                    let v =
                        r.Version()
                        |> Option.map SafeNuGetSemanticVersion.Parse
                    SafeNuGetPackageDependency(r.Id, ?ver = v))
            [SafeNuGetPackageDependencySet.Create(deps)]
        PrepareDir cfg.outputPath
        use out = File.Open(cfg.outputPath, FileMode.Create)
        pb.Save(out)

    member p.Clean() =
        if IsFile cfg.outputPath then
            File.Delete cfg.outputPath

    member p.Update cfg =
        NuGetPackageBuilder(cfg, env)

    interface IProject with
        member pr.Build(rr) = pr.Build()
        member pr.Clean() = pr.Clean()
        member pr.References = Seq.empty
        member pr.Framework = fwt.Net45
        member pr.Name = cfg.outputPath

    static member Create(env) =
        let pid = PackageId.Current.Find env
        let comp = Company.Current.Find env
        let vn = PackageVersion.Current.Find env
        let fvn = PackageVersion.Full.Find env
        let out = BuildConfig.BuildDir.Find env
        let ver = SafeNuGetSemanticVersion(fvn, ?suffix = vn.Suffix)
        let path = Path.Combine(out, String.Format("{0}.{1}.nupkg", pid, ver))
        let log = Log.Create<NuGetPackageBuilder>(env)
        let cfg =
            {
                authors =
                    match comp with
                    | Some comp -> [comp.Name]
                    | None ->
                        log.Warn("Building NuGet package {0} requires the Authors field - assumed 'Unknown'", pid)
                        ["Unknown"]
                contents = []
                description = pid
                id = pid
                licenseUrl = None
                nuGetRefs = []
                outputPath = path
                projectUrl = None
                requiresLicenseAcceptance = false
                version = fvn
                versionSuffix = vn.Suffix
            }
        NuGetPackageBuilder(cfg, env)

[<Sealed>]
type NuGetSpecBuilder private (builder: Lazy<SafeNuGetPackageBuilder>, env) =
    let log = Log.Create<NuGetPackageBuilder>(env)
    let fw = BuildConfig.CurrentFramework.Find env

    let pid = PackageId.Current.Find env

    let version =
        lazy
        let v1 = builder.Value.Version
        let v2 =
            let v = PackageVersion.Current.Find env
            let fv = PackageVersion.Full.Find env
            match v.Suffix with
            | None -> SafeNuGetSemanticVersion(fv)
            | Some s -> SafeNuGetSemanticVersion(fv, s)
        if v1.Version > v2.Version then v1 else v2

    let path =
        lazy
        let version = version.Value
        let name = String.Format("{0}.{1}.nupkg", pid, version)
        let out = BuildConfig.BuildDir.Find env
        Path.Combine(out, name)

    let build () =
        let path = path.Value
        log.Info("Writing {0}", path)
        use out = File.Open(path, FileMode.Create)
        let b = builder.Value
        b.Version <- version.Value
        b.Save(out)

    let clean () =
        File.Delete(path.Value)

    interface IProject with
        member b.Build(refs) = build ()
        member b.Clean() = clean ()
        member b.Framework = fw
        member b.Name = "NuGetPackage"
        member b.References = Seq.empty

    static member Create(env, nuspecFile) =
        let root = BuildConfig.RootDir.Find env
        let builder =
            lazy
                use stream = File.OpenRead(nuspecFile)
                SafeNuGetPackageBuilder(stream, root)
        NuGetSpecBuilder(builder, env)

[<Sealed>]
type NuGetPackageTool private (env: Parameters) =
    static let current = Parameter.Define(fun env -> NuGetPackageTool env)
    member t.CreatePackage() = NuGetPackageBuilder.Create(env)
    member t.NuSpec(file: string) = NuGetSpecBuilder.Create(env, file) :> IProject
    static member internal Current = current
