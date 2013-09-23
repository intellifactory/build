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

module RegexBuilder =
    open System
    open System.IO
    open System.Text.RegularExpressions

    type Pattern =
        | PChoice of Pattern * Pattern
        | PGroup of Pattern
        | PMod of Pattern * string
        | PRanges of list<char * char>
        | PSeq of Pattern * Pattern
        | PText of string

    let (|Range|_|) x =
        match x with
        | PRanges xs -> Some xs
        | PText x when x.Length = 1 -> Some [(x.[0], x.[0])]
        | _ -> None

    let Choice a b =
        match a, b with
        | Range a, Range b -> PRanges (a @ b)
        | _ -> PChoice (a, b)

    let ( +. ) = Choice

    let Sequence a b = PSeq (a, b)
    let ( *. ) = Sequence

    let Range a b = PRanges [(a, b)]
    let Text t = PText t
    let Many p = PMod (p, "*")
    let Optional p = PMod (p, "?")
    let Several p = PMod (p, "+")
    let Digit = Range '0' '9'
    let NonZeroDigit = Range '1' '9'
    let Group p = PGroup p
    let SeveralSeparatedBy sep p = p *. Many (sep *. p)
    let LetterOrNumber = Range '0' '9' +. Range 'A' 'Z' +. Range 'a' 'z'

    let compile p =
        use w = new StringWriter()
        w.Write('^')
        let rec visit p =
            match p with
            | PChoice (a, b) ->
                w.Write("(?:")
                visit a
                w.Write('|')
                visit b
                w.Write(')')
            | PGroup p ->
                w.Write('(')
                visit p
                w.Write(')')
            | PMod (p, s) ->
                w.Write("(?:")
                visit p
                w.Write(')')
                w.Write(s)
            | PRanges rs ->
                w.Write('[')
                for (a, b) in rs do
                    if a = b then
                        w.Write(Regex.Escape(string a))
                    else
                        w.Write(Regex.Escape(string a))
                        w.Write('-')
                        w.Write(Regex.Escape(string b))
                w.Write(']')
            | PSeq (a, b) ->
                visit a
                visit b
            | PText t ->
                w.Write(Regex.Escape(t))
        visit p
        w.Write('$')
        w.ToString()

    let Compile p =
        Regex(compile p)

    type Pattern with
        member p.Compile() = Compile p
