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

module SemanticVersions =
    open System
    open System.Text.RegularExpressions
    module RB = RegexBuilder

    (* Note: comparison constraints are satisified by ordering union cases *)

    type Label =
        | NumLabel of int
        | TextLabel of string

        static member Create(t: string) =
            let mutable out = 0
            if Int32.TryParse(t, &out) then NumLabel out else TextLabel t

    type Modifier =
        | PreRelease of list<Label>
        | Normal

        static member Parse(t: string) =
            match t with
            | "" | null -> Normal
            | t ->
                t.Split([| '.' |])
                |> Seq.map Label.Create
                |> Seq.toList
                |> PreRelease

    let versionPattern =
        let dot = RB.Text "."
        let ( + ) = RB.Choice
        let ( * ) = RB.Sequence
        let num = RB.Text "0" + RB.NonZeroDigit * RB.Many RB.Digit
        let hyphen = RB.Text "-"
        let label = RB.Several RB.Digit + RB.Several (RB.LetterOrNumber + hyphen)
        let ver = RB.Group num
        let idents = RB.Group (RB.SeveralSeparatedBy dot label)
        let pre = RB.Optional (hyphen * idents)
        let meta = RB.Optional (RB.Text "+" * idents)
        ver * dot * ver * dot * ver * pre * meta

    let versionRegex =
        versionPattern.Compile()

    [<CustomEquality>]
    [<CustomComparison>]
    [<Struct>]
    type Ignored<'T>(value: 'T) =
        member x.Value = value

        interface IComparable with
            member x.CompareTo(_) = 0

        override x.Equals(_) = true
        override x.GetHashCode() = 0

    [<Sealed>]
    exception InvalidVersion of string with

        override x.Message =
            match x :> exn with
            | InvalidVersion t ->
                String.Format("Invalid semantic version: {0} -- \
                    please refer to http://semver.org")
            | _ -> "impossible"

    type SemanticVersion =
        {
            VMajor : int
            VMinor : int
            VPatch : int
            VModifier : Modifier
            VMeta : Ignored<list<Label>>
            VText : Ignored<string>
        }

        override x.ToString() =
            x.VText.Value

        static member Parse(text) =
            match SemanticVersion.TryParse(text) with
            | None -> raise (InvalidVersion text)
            | Some v -> v

        static member TryParse(text) =
            if text = null then
                nullArg "text"
            let m = versionRegex.Match(text)
            if m.Success then
                Some {
                    VMajor = int m.Groups.[1].Value
                    VMinor = int m.Groups.[2].Value
                    VPatch = int m.Groups.[3].Value
                    VModifier = Modifier.Parse(m.Groups.[4].Value)
                    VMeta =
                        match Modifier.Parse(m.Groups.[5].Value) with
                        | Normal -> Ignored([])
                        | PreRelease ls -> Ignored(ls)
                    VText = Ignored(text)
                }
            else None

    type RangePoint =
        {
            Include : bool
            Ver : SemanticVersion
        }

        member rp.IsInclusive = rp.Include
        member rp.Version = rp.Ver

    type RangeShape =
        | AnyVersion
        | AtVersion of SemanticVersion
        | Between of RangePoint * RangePoint
        | EmptyRange
        | GreaterThan of RangePoint
        | LessThan of RangePoint

    type Range =
        { RShape : RangeShape }

        override r.ToString() =
            match r.RShape with
            | AnyVersion -> "[0.0.0, 65535.0.0]"
            | AtVersion ver -> String.Format("[{0},{0}]", ver)
            | Between (x, y) ->
                String.Format("{0}{1}, {2}{3}",
                    (if x.Include then '[' else '('),
                    x.Ver, y.Ver,
                    (if y.Include then ']' else ')'))
            | EmptyRange -> "(0.0.0-0.0.0)"
            | GreaterThan x ->
                String.Format("{0}{1},)",
                    (if x.Include then '[' else '('), x.Ver)
            | LessThan x ->
                String.Format("(,{0}{1}", x.Ver,
                    (if x.Include then ']' else ')'))

        member r.Shape = r.RShape

        static member Create(s) =
            { RShape = s }

        static member Between(a, b) =
            match compare a.Ver b.Ver with
            | 0 -> if a.Include || b.Include then AtVersion a.Ver else EmptyRange
            | 1 -> EmptyRange
            | _ -> Between (a, b)
            |> Range.Create

    let empty = Range.Create(EmptyRange)
    let any = Range.Create(AnyVersion)

    type Range with

        static member Intersect(a, b) =
            match a.RShape, b.RShape with
            | AnyVersion, _ -> b
            | _, AnyVersion -> a
            | EmptyRange, x | x, EmptyRange -> empty
            | _ ->
                let getLowerBound r =
                    match r with
                    | Between (x, _)
                    | GreaterThan x -> Some x
                    | AtVersion v -> Some { Ver = v; Include = true }
                    | _ -> None
                let getUpperBound u =
                    match u with
                    | Between (_, x)
                    | LessThan x -> Some x
                    | AtVersion v -> Some { Ver = v; Include = true }
                    | _ -> None
                let lb =
                    match getLowerBound a.RShape, getLowerBound b.RShape with
                    | None, x | x, None -> x
                    | Some a, Some b ->
                        match compare a.Ver b.Ver with
                        | 0 -> Some { Ver = a.Ver; Include = a.Include && b.Include }
                        | 1 -> Some b
                        | _ -> Some a
                let ub =
                    match getUpperBound a.RShape, getUpperBound b.RShape with
                    | None, x | x, None -> x
                    | Some a, Some b ->
                        match compare a.Ver b.Ver with
                        | 0 -> Some { Ver = a.Ver; Include = a.Include && b.Include }
                        | 1 -> Some a
                        | _ -> Some b
                match lb, ub with
                | None, None -> failwith "impossible"
                | Some p, None -> Range.Create(GreaterThan p)
                | None, Some p -> Range.Create(LessThan p)
                | Some a, Some b -> Range.Between(a, b)

        static member IntersectMany(rs) =
            rs
            |> Seq.append (Seq.singleton any)
            |> Seq.reduce (fun a b -> Range.Intersect(a, b))

        static member AnyVersion = any
        static member Empty = empty

        member r.Includes(v) =
            match r.RShape with
            | AnyVersion -> true
            | AtVersion x -> v = x
            | EmptyRange -> false
            | Between (a, b) ->
                (if a.Include then a.Ver <= v else a.Ver < v)
                && (if b.Include then v <= b.Ver else v <= a.Ver)
            | LessThan x -> if x.Include then v <= x.Ver else v < x.Ver
            | GreaterThan x -> if x.Include then x.Ver <= v else x.Ver < v

    [<Sealed>]
    exception InvalidRange of string with

        override x.Message =
            match x :> exn with
            | InvalidRange t ->
                String.Format("Invalid semantic version range: {0} -- \
                    please refer to http://semver.org for the format of versions. \
                    A range is a comma-separated versio pair in brackets or parens.")
            | _ -> "impossible"

    type OptionBuilder =
        | Option

        member x.Bind(v, f) = Option.bind f v
        member x.Return(v) = Some v
        member x.ReturnFrom(v: option<_>) = v

    let point i v =
        {
            Include = i
            Ver = v
        }

    let greater p = Range.Create(GreaterThan p)
    let lessThan p = Range.Create(LessThan p)
    let minVersion = SemanticVersion.Parse("0.0.0")
    let maxVersion = SemanticVersion.Parse("65535.0.0")
    let (|V|_|) t = SemanticVersion.TryParse(t)

    let between a b =
        if a.Ver = minVersion && b.Ver = maxVersion && a.Include && b.Include
            then any
            else Range.Between(a, b)

    let tryParseRange text =
        match text with
        | V v ->
            let p = point true v
            Some (greater p)
        | _ ->
            Option {
                let! lowerInclusive =
                    match text.[0] with
                    | '(' -> Some false
                    | '[' -> Some true
                    | _ -> None
                let! higherInclusive =
                    match text.[text.Length - 1] with
                    | ')' -> Some false
                    | ']' -> Some true
                    | _ -> None
                let midText = text.Substring(1, text.Length - 2)
                match midText with
                | V ver ->
                    let a = point lowerInclusive ver
                    let b = point higherInclusive ver
                    return between a b
                | _ ->
                    let! k =
                        match midText.IndexOf(',') with
                        | -1 -> None
                        | k -> Some k
                    let t1 = midText.Substring(0, k).Trim()
                    let t2 = midText.Substring(k + 1).Trim()
                    match t1, t2 with
                    | "", V x ->
                        return lessThan (point higherInclusive x)
                    | V x, "" ->
                        return greater (point lowerInclusive x)
                    | V x, V y ->
                        let a = point lowerInclusive x
                        let b = point higherInclusive y
                        return between a b
                    | _ ->
                        return! None
            }

    type Range with

        static member Parse(t) =
            match Range.TryParse(t) with
            | None -> raise (InvalidRange t)
            | Some r -> r

        static member TryParse(t) =
            if t = null then nullArg "text"
            tryParseRange t
