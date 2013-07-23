namespace IntelliFactory.Build

open System
open IntelliFactory.Core

module BuildConfig =
    val AppDomain : Parameter<AppDomain>
    val BuildNumber : Parameter<option<int>>
    val CurrentFramework : Parameter<Framework>
    val KeyFile : Parameter<option<string>>
    val BuildDir : Parameter<string>
    val OutputDir : Parameter<string>
    val RootDir : Parameter<string>
    val ProjectName : Parameter<string>
