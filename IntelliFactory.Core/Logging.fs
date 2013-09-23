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

module Logging =
    open System

    type LogLevel =
        | Verbose
        | Info
        | Warn
        | Error
        | Critical

        override ll.ToString() =
            match ll with
            | Verbose -> "Verbose"
            | Info -> "Info"
            | Warn -> "Warn"
            | Error -> "Error"
            | Critical -> "Critical"

    let nullCoded (x: string) =
        match x with
        | null -> None
        | t -> Some t

    let nullCode (x: option<string>) =
        match x with
        | None -> null
        | Some t -> t

    type LogMessage =
        {
            MCat : string
            MLevel : LogLevel
            MText : string
        }

        member lm.Category = nullCoded lm.MCat
        member lm.Level = lm.MLevel
        member lm.Text = lm.MText

        override lm.ToString() =
            match lm.MCat with
            | null -> String.Format("[{0}] {1}", lm.MLevel, lm.Text)
            | cat -> String.Format("[{0}] [{1}] {2}", lm.MLevel, cat, lm.Text)

        static member Create(lev, category, text: string) =
            {
                MCat = category
                MLevel = lev
                MText = string text
            }

        static member Create(lev, text) =
            LogMessage.Create(lev, null, text)

    type Log =
        {
            LCat : string
            LNull : bool
            LSink : LogMessage -> unit
        }

        static member Create(f: LogMessage -> unit) =
            { LCat = null; LNull = false; LSink = f }

        member l.WithCategory(?category: string) =
            { l with LCat = nullCode category }

        member l.Send(msg) = l.LSink msg

        member l.Send0(lev, t) =
            if not l.LNull then
                l.Send(LogMessage.Create(lev, l.LCat, t))

        member l.Send1(lev, t, x: obj) =
            if not l.LNull then
                l.Send0(lev, String.Format(t, x))

        member l.SendN(lev, t, xs: obj []) =
            if not l.LNull then
                l.Send0(lev, String.Format(t, xs))

        member l.Category = nullCoded l.LCat

        member l.Critical(t) = l.Send0(Critical, t)
        member l.Critical(t, x: obj) = l.Send1(Critical, t, x)
        member l.Critical(t, [<ParamArray>] xs: obj[]) = l.SendN(Critical, t, xs)

        member l.Error(e: exn) = l.Send0(Error, string e)
        member l.Error(t) = l.Send0(Error, t)
        member l.Error(t, x: obj) = l.Send1(Error, t, x)
        member l.Error(t, [<ParamArray>] xs: obj[]) = l.SendN(Error, t, xs)

        member l.Info(t) = l.Send0(Info, t)
        member l.Info(t, x: obj) = l.Send1(Info, t, x)
        member l.Info(t, [<ParamArray>] xs: obj[]) = l.SendN(Info, t, xs)

        member l.Verbose(t) = l.Send0(Verbose, t)
        member l.Verbose(t, x: obj) = l.Send1(Verbose, t, x)
        member l.Verbose(t, [<ParamArray>] xs: obj[]) = l.SendN(Verbose, t, xs)

        member l.Warn(t) = l.Send0(Warn, t)
        member l.Warn(t, x: obj) = l.Send1(Warn, t, x)
        member l.Warn(t, [<ParamArray>] xs: obj[]) = l.SendN(Warn, t, xs)

    let nullLog =
        { Log.Create(ignore) with LNull = true }

    type Log with
        static member Null = nullLog
