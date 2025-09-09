namespace VisioDiagramGenerator.CliFs

[<AutoOpen>]
module ExitCodes =
    // See Prompt 7 exit map
    [<Literal>] let OK               = 0
    [<Literal>] let CONFIG_INVALID   = 2
    [<Literal>] let USAGE            = 64
    [<Literal>] let RUNNER_NOT_FOUND = 66
    [<Literal>] let UNAVAILABLE      = 69
    [<Literal>] let IO_ERROR         = 70
    [<Literal>] let NOT_ON_WINDOWS   = 71
    [<Literal>] let TIMEOUT          = 124

