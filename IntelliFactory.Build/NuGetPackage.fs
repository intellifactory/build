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
open IntelliFactory.Build
open IntelliFactory.Core

type NuGetPackageConfig =
    {
        Authors : list<string>
        Description : string
        Id : string
        LicenseUrl : option<string>
        NuGetReferences : list<INuGetReference>
        OutputPath : string
        ProjectUrl : option<string>
        RequiresLicenseAcceptance : bool
        Version : Version
        VersionSuffix : option<string>
    }

    member cfg.WithApache20License() =
        {
            cfg with
                RequiresLicenseAcceptance = false
                LicenseUrl = Some "http://www.apache.org/licenses/LICENSE-2.0"
        }

    static member Create(id, ver, authors, desc, outPath) =
        {
            Authors = authors
            Description = desc
            Id = id
            LicenseUrl = None
            NuGetReferences = []
            OutputPath = outPath
            ProjectUrl = None
            RequiresLicenseAcceptance = false
            Version = ver
            VersionSuffix = None
        }

type NuGetPackageSettings =
    {
        Contents : seq<INuGetFile>
        PackageConfig : NuGetPackageConfig
    }

[<Sealed>]
type NuGetPackageBuilder(settings, env) =
    let fwt = Frameworks.Current.Find env
    let rs = References.Current.Find env
    let cfg = settings.PackageConfig
    let rB = ReferenceBuilder.Current.Find env
    let upd cfg = NuGetPackageBuilder({ settings with PackageConfig = cfg }, env )

    member p.AddNuGetRefs(ngr: seq<INuGetReference>) =
        let ng =
            ngr
            |> Seq.append cfg.NuGetReferences
            |> Seq.distinct
            |> Seq.toList
        let settings = { settings with PackageConfig = { settings.PackageConfig with NuGetReferences = ng } }
        NuGetPackageBuilder(settings, env)

    member p.AddNuGetExportingProject(proj: INuGetExportingProject) =
        let settings = { settings with Contents = Seq.append settings.Contents proj.NuGetFiles }
        NuGetPackageBuilder(settings, env)

    member p.AddProject(pr: IProject) =
        pr.References
        |> Seq.choose rs.GetNuGetReference
        |> p.AddNuGetRefs

    member p.Add<'T when 'T :> IProject and 'T :> INuGetExportingProject>(x: 'T) =
        p.AddProject(x :> IProject).AddNuGetExportingProject(x :> INuGetExportingProject)

    member p.Apache20License() =
        upd (cfg.WithApache20License())

    member p.Authors authors =
        upd { cfg with Authors = Seq.toList authors }

    member p.AddPackage(pkg: NuGetPackageBuilder) =
        p.AddNuGetRefs [pkg.SelfReference]

    member p.SelfReference : INuGetReference =
        rB.NuGet(cfg.Id).Version(string p.FullVersion) :> INuGetReference

    member p.FullVersion =
        SafeNuGetSemanticVersion(cfg.Version, ?suffix = cfg.VersionSuffix)

    member p.Build() =
//        let rr = References.Current.Find(env).ResolveReferences fw refs
        let pb = SafeNuGetPackageBuilder()
        pb.Id <- cfg.Id
        cfg.LicenseUrl |> Option.iter (fun url -> pb.LicenseUrl <- Uri url)
        pb.RequireLicenseAcceptance <- cfg.RequiresLicenseAcceptance
        cfg.ProjectUrl |> Option.iter (fun url -> pb.ProjectUrl <- Uri url)
        pb.Authors <- cfg.Authors
        pb.Description <- cfg.Description
        pb.Version <- p.FullVersion
        let fw = BuildConfig.CurrentFramework.Find env
        let fwt = Frameworks.Current.Find env
        pb.Files <-
            [
                for f in settings.Contents ->
                    SafeNuGetPackageFile.Create(fwt.ToFrameworkName fw, f)
            ]
        pb.DependencySets <-
            let deps =
                cfg.NuGetReferences
                |> Seq.map NuGetReference.Wrap
                |> Seq.distinctBy (fun r -> r.Id)
                |> Seq.map (fun r ->
                    let v =
                        r.Version()
                        |> Option.map SafeNuGetSemanticVersion.Parse
                    SafeNuGetPackageDependency(r.Id, ?ver = v))
            [SafeNuGetPackageDependencySet.Create(deps)]
        PrepareDir cfg.OutputPath
        use out = File.Open(cfg.OutputPath, FileMode.Create)
        pb.Save(out)

    member p.Clean() =
        if IsFile cfg.OutputPath then
            File.Delete cfg.OutputPath

    member p.Configure(f) =
        NuGetPackageBuilder({ settings with PackageConfig = f settings.PackageConfig }, env)

    member p.Description d =
        upd { cfg with Description = d }

    member p.Id id =
        upd { cfg with Id = id }

    member p.ProjectUrl url =
        upd { cfg with ProjectUrl = Some url }

    interface IProject with
        member pr.Build() = pr.Build()
        member pr.Clean() = pr.Clean()
        member pr.References = Seq.empty
        member pr.Framework = fwt.Net45
        member pr.Name = settings.PackageConfig.Id

    static member Create(env) =
        let pid = PackageId.Current.Find env
        let cfg =
            let comp = Company.Current.Find env
            let vn = PackageVersion.Current.Find env
            let fvn = PackageVersion.Full.Find env
            let out = NuGetConfig.PackageOutputPath.Find env
            let log = Log.Create<NuGetPackageBuilder>(env)
            let ver = SafeNuGetSemanticVersion(fvn, ?suffix = vn.Suffix)
            let path = Path.Combine(out, String.Format("{0}.{1}.nupkg", pid, ver))
            let authors =
                match comp with
                | Some comp -> [comp.Name]
                | None ->
                    log.Warn("Building NuGet package {0} requires the Authors field - assumed 'Unknown'", pid)
                    ["Unknown"]
            {
                NuGetPackageConfig.Create(pid, fvn, authors, pid, path) with
                    VersionSuffix = vn.Suffix
            }
        let settings =
            {
                Contents = Seq.empty
                PackageConfig = cfg
            }
        NuGetPackageBuilder(settings, env)

[<Sealed>]
type NuGetSpecProject(env: IParametric, file: string) =
    let log = Log.Create<NuGetSpecProject>(env)
    let fullPath = Path.Combine(BuildConfig.RootDir.Find(env), file)
    let fw = BuildConfig.CurrentFramework.Find env
    let out = NuGetConfig.PackageOutputPath.Find env
    let root = BuildConfig.RootDir.Find env

    let sourceBytes = lazy File.ReadAllBytes fullPath

    let getPB () =
        use input = new MemoryStream(sourceBytes.Value, false)
        SafeNuGetPackageBuilder(input, root)

    let getNupkgPath () =
        let builder = getPB ()
        Path.Combine(out, String.Format("{0}.{1}.nupkg", builder.Id, builder.Version))

    interface IProject with

        member p.Build() =
            let builder = getPB ()
            let nupkg = getNupkgPath ()
            log.Info("Writing {0}", nupkg)
            let outBytes =
                use output = new MemoryStream()
                builder.Save(output)
                output.ToArray()
            FileSystem.Binary.FromBytes(outBytes).WriteFile(nupkg)

        member p.Clean() =
            let nupkg = getNupkgPath ()
            if FileInfo(nupkg).Exists then
                log.Info("Deleting {0}", nupkg)
                File.Delete nupkg

        member p.Framework = fw
        member p.Name = Path.GetFileName fullPath
        member p.References = Seq.empty

[<Sealed>]
type NuGetPackageTool private (env: Parameters) =
    static let current = Parameter.Define(fun env -> NuGetPackageTool env)
    member t.CreatePackage() = NuGetPackageBuilder.Create(env)
    member t.NuSpec(file: string) = NuGetSpecProject(env, file)
    static member internal Current = current
