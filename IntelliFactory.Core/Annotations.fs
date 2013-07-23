namespace IntelliFactory.Core

open System
open System.Security

[<assembly: AllowPartiallyTrustedCallers>]
[<assembly: SecurityRules(SecurityRuleSet.Level2)>]
do ()
