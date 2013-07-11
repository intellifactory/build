# IntelliFactory.Build

Cross-platform and sandbox-friendly build automation library with
special support for building [F#](http://fsharp.org) and
[WebSharper](http://websharper.com) projects consuming and publishing
[NuGet](http://nuget.org) packages.

## Links

  * [Bitbucket repository - Mercurial](http://bitbucket.org/IntelliFactory/build)
  * [GitHub repository - Git](http://github.com/intellifactory/build)
  * [Issue tracker](http://bitbucket.org/IntelliFactory/build/issues)

## Copying

Code is available under Apache 2.0 license, see LICENSE.txt in source.

## Synopsis

Create a `build.fsx`:

    #r "IntelliFactory.Build.dll"
    open IntelliFactory.Build

    let b = BuildTool().PackageId("MyPackage", "0.1")

    b.Solution [
        b.FSharp.Library("A")
            .Modules(["M1", "M2", "M3"])
            .References(fun rt ->
                [
                    rt.Assembly("System.Xml")
                    rt.NuGet("DotNetZip").Reference()
                ])
    ]
    |> b.Dispatch

With this in place, `fsi --exec build.fsx` and `fsi --exec build.fsx
--clean` will build or clean a library consisting of `A/M1.fs`,
`A/M2.fs`, `A/M3.fs`, targeting .NET Framework 4.5.

## Contributing

Contributions are welcome.  The library is designed to be easily
parameterizable (see `Parameters.fsi`), so if some rules are too
hard-wired for your use case, feel free to make them parametric and
send a pull request on either GitHub or Bitbucket.

## Status

Current development focus is on providing ease of use, ability to run
in partial trust, and Mono support.  Previously NuGet-released version
0.1 supported more features - these are to be re-introduced, currently
they conflict with the partial trust requirement.

## Building

Invoke `build.cmd` in the root directory of the checkout.

On Mono, invoke `bash build.sh`.  You might need to configure
`MonoHome`, `NuGetHome` and `FSharpHome` environment variables.
Tested on Mono 3.0.8, Arch Linux.

## Contact

This software is being developed by IntelliFactory.  Please feel free
to [contact us](http://websharper.com/contact).

For public discussions we also recommend using
[FPish](http://fpish.net/topics), the functional programming community
site built with [WebSharper](http://websharper.com).
