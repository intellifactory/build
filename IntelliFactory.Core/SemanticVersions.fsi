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

/// Implements semantic version types.
/// See `http://semver.org/` for the spec.
module SemanticVersions =

    /// Represents a semantic version.
    [<Sealed>]
    type SemanticVersion =

        /// Parses a text representation.
        static member Parse : string -> SemanticVersion

        /// Tries to parse a text version.
        static member TryParse : string -> option<SemanticVersion>

    /// Represents an endpoint of a range.
    [<Sealed>]
    type RangePoint =

        /// True if the version is included.
        member IsInclusive : bool

        /// The maximum or mimimum version.
        member Version : SemanticVersion

    /// Represents a decomposed version range (see below).
    type RangeShape =
        | AnyVersion
        | AtVersion of SemanticVersion
        | Between of RangePoint * RangePoint
        | EmptyRange
        | GreaterThan of RangePoint
        | LessThan of RangePoint

    /// Represents a version range.
    [<Sealed>]
    type Range =

        /// Tests for inclusion.
        member Includes : SemanticVersion -> bool

        /// Decomposes the range.
        member Shape : RangeShape

        /// Creates a `Range` object.
        static member Create : RangeShape -> Range

        /// Computes an intersection of two ranges.
        static member Intersect : Range * Range -> Range

        /// Computes an intersection of multiple ranges.
        static member IntersectMany : seq<Range> -> Range

        /// Parses a text representation.
        static member Parse : string -> Range

        /// Tries to parse a text representation.
        static member TryParse : string -> option<Range>

        /// An all-inclusive range.
        static member AnyVersion : Range

        /// An empty range.
        static member Empty : Range

    /// Thrown when parsing a Range fails.
    [<Sealed>]
    exception InvalidRange of string

    /// Thrown when parsing a Version fails.
    [<Sealed>]
    exception InvalidVersion of string
