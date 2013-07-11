namespace IntelliFactory.Build

module BuildConfig =
    val BuildNumber : Parameter<option<int>>
    val CurrentFramework : Parameter<Framework>
    val KeyFile : Parameter<option<string>>
    val BuildDir : Parameter<string>
    val OutputDir : Parameter<string>
    val RootDir : Parameter<string>
