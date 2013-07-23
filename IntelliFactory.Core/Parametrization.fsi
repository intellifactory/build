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

/// Implements lightweight support for dynamic variable binding.
[<AutoOpen>]
module IntelliFactory.Core.Parametrization

/// Represents a first-class configurable parameter, a typed generalization
/// of environment variables.  Parameter sets are passed through opaque
/// dictionaries of `Parameters` type.  Represented as an auto-generated
/// unique key combined with a recipe to construct a default value.
[<Sealed>]
type Parameter<'T> =

    /// Functionally updates the current value of the parameter
    /// to the given value in an object that supports parameterization.
    member Custom : 'T -> IParametric<'R> -> 'R

    /// Finds the current value of the parameter in an object
    /// that supports parameterization.
    member Find : IParametric -> 'T

    /// Looks up the current value from
    /// `System.Runtime.Remoting.Messaging.CallContext`.
    member FromCallContext : unit -> 'T

    /// Functionally updates the current value of the parameter
    /// in an object that supports parameterization.
    ///
    ///     p.Update f ps = p.Custom (f (p.Find ps)) ps
    member Update : ('T -> 'T) -> IParametric<'R> -> 'R

/// Represents a collection of parameter values. Logically is a
/// partial map from parameter objects to values of the corresponding type.
/// Parameters may be passed explicitly or implicitly through the
/// `System.Runtime.Remoting.Messaging.CallContext`.
and [<Sealed>] Parameters =
    interface IParametric
    interface IParametric<Parameters>

    /// Extends the map with the given overrides.
    member Extend : overrides: Parameters -> Parameters

    /// For the dynamic extent of the `worker` expression, extends the
    /// current `CallContext` with the overrides given in this `Parameters` object.
    member WithExtendedCallContext : worker: (unit -> 'T) -> 'T

    /// `Async`-friendly version of `WithExtendedCallContext`.
    member WithExtendedCallContext : worker: Async<'T> -> Async<'T>

    /// Constructs the default parameters, the empty map.
    /// Looking up parameter values from `Parameters.Default` will
    /// invoke parameter default value construction recipes.
    static member Default : unit -> Parameters

    /// Extracts the parameters passed via the
    /// `System.Runtime.Remoting.Messaging.CallContext`. If none are passed,
    /// returns `Parameters.Default`.
    static member FromCallContext : unit -> Parameters

    /// A utility method to extract `Parameters` from a conforming object.
    ///
    ///    Parameters.Get p = (p :> IParametric).Parameters
    static member Get : IParametric -> Parameters

    /// A utility method to update `Parameters` in a conforming object.
    ///
    ///    Parameters.Set p x = (x :> IParametric<'R>).WithParameters p
    static member Set : Parameters -> IParametric<'R> -> 'R

/// An interface for objects that contain an instance of `Parameters`.
and IParametric =

    /// The `Parameters` instance.
    abstract Parameters : Parameters

/// An interface for objects that contain an instance of `Parameters` and
/// support functionaly updating it.
and IParametric<'R> =
    inherit IParametric

    /// Functionally updates the current object with alternative `Parameters`.
    abstract WithParameters : Parameters -> 'R

/// Static methods for manipulating parameter objects.
[<Sealed>]
type Parameter =

    /// Given a bijection, constructs a differently typed view on the same logical parameter.
    static member Convert : ('A -> 'B) -> ('B -> 'A) -> Parameter<'A> -> Parameter<'B>

    /// Creates a parameter given a default value.
    static member Create : 'T -> Parameter<'T>

    /// Creates a parameter given a default recipe.
    static member Define : (Parameters -> 'T) -> Parameter<'T>
