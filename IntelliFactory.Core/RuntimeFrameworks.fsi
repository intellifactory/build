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
// permissions and limitations under the License.

namespace IntelliFactory.Core

/// Formalizes various .NET runtime framework choices.
module RuntimeFrameworks =
    open System
    open System.Runtime
    open System.Runtime.Versioning

    /// Represents various farmework identifiers.
    [<Sealed>]
    type FrameworkId =

        /// Default identifier, such as `.NETFramework`.
        member Id : string

        /// .NET Framework.
        static member Net : FrameworkId

        /// .NET Core Framework / Windows Store.
        static member NetCore : FrameworkId

        /// .NET Micro Framework.
        static member NetMicro : FrameworkId

        /// .NET Portable Framework.
        static member Portable : FrameworkId

        /// Silverlight.
        static member Silverlight : FrameworkId

        /// Windows Phone.
        static member WindowsPhone : FrameworkId

    /// Represents framework versions, combining an identifier,
    /// a version and a profile.
    [<Sealed>]
    type FrameworkVersion =

        /// Framework identifier.
        member Id : FrameworkId

        /// Framework name.
        member FrameworkName : FrameworkName

        /// Optional profile.
        member Profile : option<string>

        /// Version.
        member Version : Version

        /// .NET 2.0.
        static member Net20 : FrameworkVersion

        /// .NET 3.0.
        static member Net30 : FrameworkVersion

        /// .NET 3.5.
        static member Net35 : FrameworkVersion

        /// .NET 3.5 Client Profile.
        static member Net35Client : FrameworkVersion

        /// .NET 4.0.
        static member Net40 : FrameworkVersion

        /// .NET 4.0 Client Profile.
        static member Net40Client : FrameworkVersion

        /// .NET 4.0 Client Profile.
        static member Net45 : FrameworkVersion

        /// .NET Core 4.5 (apps for Windows Store).
        static member NetCore45 : FrameworkVersion

        /// Silverlight 3.0 / Windows Phone 7.0.
        static member Silverlight30WindowsPhone70 : FrameworkVersion

        /// Silverlight 4.0.
        static member Silverlight40 : FrameworkVersion

        /// Silverlight 4.0 / Windows Phone 7.1.
        static member Silverlight40WindowsPhone71 : FrameworkVersion

        /// Silverlight 5.0.
        static member Silverlight50 : FrameworkVersion

        /// Windows Phone 8.0.
        static member WindowsPhone80 : FrameworkVersion

        /// Creates a custom version.
        static member Create : FrameworkId * Version * ?profile: string -> FrameworkVersion
