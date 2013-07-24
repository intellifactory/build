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

open IntelliFactory.Core

[<Sealed>]
type NuGetConfig =
    static member CurrentSettings : Parameter<option<NuGet.ISettings>>
    static member CurrentPackageManager : Parameter<NuGet.IPackageManager>
    static member LocalRepositoryPath : Parameter<string>
    static member PackageOutputPath : Parameter<string>

[<Sealed>]
type NuGetFile =

    /// Reads a local file as a `INuGetFile` file.
    static member Local : sourcePath: string * targetPath: string -> INuGetFile

    /// Reads a library file as an `INuGetFile` in a `lib/netXX` folder.
    static member LibraryFile : framework: Framework * sourcePath: string -> INuGetFile
