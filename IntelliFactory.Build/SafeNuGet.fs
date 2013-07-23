namespace IntelliFactory.Build

open System
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.Versioning
open System.Security
open NuGet
open IntelliFactory.Build
open IntelliFactory.Core

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetSemanticVersion(sv: SemanticVersion) =
    let t = string sv

    new (version: Version, ?suffix: string) =
        let sv =
            match suffix with
            | None -> SemanticVersion(version)
            | Some s -> SemanticVersion(version, s)
        SafeNuGetSemanticVersion(sv)

    member v.SemanticVersion = sv

    member v.Version =
        let maj = sv.Version.Major
        let min = sv.Version.Minor
        match sv.Version.Build, sv.Version.Revision with
        | b, r when b <= 0 && r <= 0 -> Version(maj, min)
        | b, r when r <= 0 -> Version(maj, min, b)
        | b, r -> Version(maj, min, b, r)

    member v.SpecialVersion =
        match sv.SpecialVersion with
        | null | "" -> None
        | sv -> Some sv

    override v.ToString() = t

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member Parse(text: string) = SafeNuGetSemanticVersion(SemanticVersion.Parse(text))

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    static member ForPackage(p: IPackage) = SafeNuGetSemanticVersion(p.Version)

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackageDependency(d: PackageDependency) =

    new (id, ?ver: SafeNuGetSemanticVersion) =
        let dep =
            match ver with
            | None ->
                PackageDependency(id)
            | Some ver ->
                PackageDependency(id, VersionSpec(ver.SemanticVersion))
        SafeNuGetPackageDependency(dep)

    member s.Dependency = d

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackageAssemblyReference(r: IPackageAssemblyReference) =
    member s.Name = r.Name
    member s.Path = r.Path
    member s.SupportedFrameworks = r.SupportedFrameworks
    member s.TargetFramework = NullGuard r.TargetFramework

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetFrameworkAssemblyReference(r: FrameworkAssemblyReference) =
    member s.AssemblyName = r.AssemblyName
    member s.SupportedFrameworks = r.SupportedFrameworks

[<Sealed>]
[<SecurityCritical>]
type SafeNuGetBasicPackageFile(f: INuGetFile, fw: FrameworkName) =
    let path = f.TargetPath

    interface IFrameworkTargetable with
        member p.SupportedFrameworks with [<SecurityCritical>] get () = Seq.singleton fw

    interface IPackageFile with

        [<SecurityCritical>]
        member p.GetStream() = f.Read()

        member p.EffectivePath with [<SecurityCritical>] get () = path
        member p.Path with [<SecurityCritical>] get () = path
        member p.TargetFramework with [<SecurityCritical>] get () = fw

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackageFile(p: IPackageFile) =
    member s.EffectivePath = p.EffectivePath
    member s.File = p
    member s.Path = p.Path
    member s.SupportedFrameworks = p.SupportedFrameworks
    member s.TargetFramework = NullGuard p.TargetFramework

    static member Create(source, target) =
        SafeNuGetPackageFile(PhysicalPackageFile(SourcePath = source, TargetPath = target))

    static member Create(fw, f) =
        SafeNuGetPackageFile(SafeNuGetBasicPackageFile(f, fw) :> IPackageFile)

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackage(p: IPackage) =

    member w.Package = p

    member w.Compare(o: SafeNuGetPackage) =
        PackageComparer.Version.Compare(p, o.Package)

    member w.GetCompatiblePackageDependencies(tF) =
        let r = ResizeArray()
        for d in p.GetCompatiblePackageDependencies(tF) do
            r.Add(SafeNuGetPackageDependency(d))
        r.ToArray() :> seq<_>

    member w.GetToolFiles() =
        let r = ResizeArray()
        for f in p.GetToolFiles() do
            r.Add (SafeNuGetPackageFile f)
        r.ToArray() :> seq<_>

    member w.Id = p.Id
    member w.Version = SafeNuGetSemanticVersion(p.Version)

    member w.AssemblyReferences =
        let a = ResizeArray()
        for r in p.AssemblyReferences do
            a.Add (SafeNuGetPackageAssemblyReference r)
        a.ToArray() :> seq<_>

    member w.FrameworkAssemblies =
        let a = ResizeArray()
        for r in p.FrameworkAssemblies do
            a.Add (SafeNuGetFrameworkAssemblyReference r)
        a.ToArray() :> seq<_>

    static member Create(p: IPackage) =
        match p with
        | null -> None
        | p -> Some (SafeNuGetPackage p)

[<Sealed>]
[<SecuritySafeCritical>]
type SafePackageRepository(pr: IPackageRepository) =

    member r.FindByDependency(dep: SafeNuGetPackageDependency, ?allowPreRelease, ?allowUnlisted) =
        let allowPreRelease = defaultArg allowPreRelease true
        let allowUnlisted = defaultArg allowUnlisted true
        pr.FindPackage(dep.Dependency.Id, dep.Dependency.VersionSpec, allowPreRelease, allowUnlisted)
        |> SafeNuGetPackage.Create

    member r.FindExact(pid: string, ver: SafeNuGetSemanticVersion, ?allowPreRelease, ?allowUnlisted) =
        let allowPreRelease = defaultArg allowPreRelease true
        let allowUnlisted = defaultArg allowUnlisted true
        pr.FindPackage(pid, ver.SemanticVersion, allowPreRelease, allowUnlisted)
        |> SafeNuGetPackage.Create

    member r.FindById(pid: string) =
        pr.FindPackage(pid)
        |> SafeNuGetPackage.Create

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackageManager(m: IPackageManager) =

    member w.LocalRepository = SafePackageRepository(m.LocalRepository)
    member w.SourceRepository = SafePackageRepository(m.SourceRepository)

    member w.Install(pkg: SafeNuGetPackage, ?ignoreDependencies, ?allowPreRelease) =
        let ignoreDependencies = defaultArg ignoreDependencies false
        let allowPreRelease = defaultArg allowPreRelease true
        m.InstallPackage(pkg.Package, ignoreDependencies, allowPreRelease)

    member w.InstallExact(pid: string, ver: SafeNuGetSemanticVersion, ?ignoreDependencies, ?allowPreRelease) =
        let ignoreDependencies = defaultArg ignoreDependencies false
        let allowPreRelease = defaultArg allowPreRelease true
        m.InstallPackage(pid, ver.SemanticVersion, ignoreDependencies, allowPreRelease)

    member w.GetPackageDirectory(pkg: SafeNuGetPackage) =
        m.PathResolver.GetPackageDirectory(pkg.Package)
        |> NullGuard

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackageDependencySet(ds: PackageDependencySet) =

    member s.Dependencies =
        let r = ResizeArray()
        for d in ds.Dependencies do
            r.Add(SafeNuGetPackageDependency d)
        r.ToArray() :> seq<_>

    member s.DependencySet = ds
    member s.SupportedFrameworks = ds.SupportedFrameworks
    member s.TargetFramework = ds.TargetFramework

    static member Create(deps: seq<SafeNuGetPackageDependency>, ?framework) =
        let r = ResizeArray()
        do
            for d in deps do
                r.Add(d.Dependency)
        SafeNuGetPackageDependencySet(PackageDependencySet(defaultArg framework null, r))

[<Sealed>]
[<SecuritySafeCritical>]
type SafeNuGetPackageBuilder(p: PackageBuilder) =

    new () =
        SafeNuGetPackageBuilder(PackageBuilder())

    new (s: Stream, p: string) =
        SafeNuGetPackageBuilder(PackageBuilder(s, p))

    member b.Save s =
        p.Save s

    member b.DependencySets
        with get () : seq<SafeNuGetPackageDependencySet> =
            let r = ResizeArray()
            for d in p.DependencySets do
                r.Add(SafeNuGetPackageDependencySet d)
            r.ToArray() :> seq<_>
        and set (x: seq<SafeNuGetPackageDependencySet>) =
            let r = ResizeArray()
            for d in x do
                r.Add(d.DependencySet)
            p.DependencySets.Clear()
            p.DependencySets.AddRange(r)

    member b.Authors
        with get () = p.Authors :> seq<string>
        and set (x: seq<string>) =
            p.Authors.Clear()
            p.Authors.AddRange(x)

    member b.Description
        with get () = p.Description
        and set x = p.Description <- x

    member b.Files
        with get () =
            let r = ResizeArray()
            for f in p.Files do
                r.Add(SafeNuGetPackageFile f)
            r.ToArray() :> seq<_>
        and set (files: seq<SafeNuGetPackageFile>) =
            p.Files.Clear()
            let fs = ResizeArray()
            for f in files do
                fs.Add(f.File)
            p.Files.AddRange(fs)

    member b.Id
        with get () = p.Id
        and set x = p.Id <- x

    member b.LicenseUrl
        with get () = p.LicenseUrl
        and set x = p.LicenseUrl <- x

    member b.ProjectUrl
        with get () = p.ProjectUrl
        and set x = p.ProjectUrl <- x

    member b.RequireLicenseAcceptance
        with get () = p.RequireLicenseAcceptance
        and set x = p.RequireLicenseAcceptance <- x

    member b.Version
        with get () = SafeNuGetSemanticVersion(p.Version)
        and set (v: SafeNuGetSemanticVersion) =
            p.Version <- v.SemanticVersion
