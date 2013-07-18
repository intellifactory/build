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

namespace IntelliFactory.Build

type IParametric =
    abstract Find<'T> : Parameter<'T> -> 'T
    abstract Parameters : Parameters

and IParametric<'R> =
    inherit IParametric
    abstract Custom<'T> : Parameter<'T> -> 'T -> 'R

and [<Sealed>] Parameters =
    interface IParametric
    interface IParametric<Parameters>
    static member Default : Parameters
    static member Get : IParametric -> Parameters

and [<Sealed>] Parameter<'T> =
    member Custom : 'T -> IParametric<'R> -> 'R
    member Find : IParametric -> 'T
    member Update : ('T -> 'T) -> IParametric<'R> -> 'R

[<Sealed>]
type Parameter =
    static member Convert : ('A -> 'B) -> ('B -> 'A) -> Parameter<'A> -> Parameter<'B>
    static member Create : 'T -> Parameter<'T>
    static member Define : (Parameters -> 'T) -> Parameter<'T>
