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

/// Implements a facade for `System.Text.RegularExpressions`.
/// Allows to define patterns incrementally using F# values.
/// This module API is experimental.
module RegexBuilder =
    open System.Text.RegularExpressions

    /// Represents a pattern descriptor.
    [<Sealed>]
    type Pattern

    /// Choice `a|b`.
    val Choice : Pattern -> Pattern -> Pattern

    /// Compiles a pattern.
    val Compile : Pattern -> Regex

    /// Digits `[0-9]`.
    val Digit : Pattern

    /// Grouping `(x)`.
    val Group : Pattern -> Pattern

    /// Grouping `[a-zA-Z0-9]`.
    val LetterOrNumber : Pattern

    /// Kleene star `x*`.
    val Many : Pattern -> Pattern

    /// Digits `[1-9]`.
    val NonZeroDigit : Pattern

    /// Optional modifier `x?`.
    val Optional : Pattern -> Pattern

    /// Character range `[a-z]`.
    val Range : char -> char -> Pattern

    /// Sequencing `ab`.
    val Sequence : Pattern -> Pattern -> Pattern

    /// One-or-more modifier `a+`.
    val Several : Pattern -> Pattern

    /// Derived pattern: `p (sep p)*`.
    val SeveralSeparatedBy : Pattern -> Pattern -> Pattern

    /// Escaped text.
    val Text : string -> Pattern

    type Pattern with

        /// Compiles a pattern.
        member Compile : unit -> Regex
