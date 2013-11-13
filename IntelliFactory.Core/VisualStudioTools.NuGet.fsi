﻿// Copyright 2013 IntelliFactory
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

namespace IntelliFactory.Core.VisualStudioTools

/// NuGet-related functionality.
module NuGet =
    open System

    #if NET40
    #else

    type Content = Utils.Content

    /// A compiled NuGet package together with its contents.
    [<Sealed>]
    type Package =

        /// The content.
        member Content : Content

        /// The short name.
        member Id : string

        /// The version.
        member Version : string

        /// Creates a new instance.
        static member Create : id: string * version: string * Content -> Package

    #endif
