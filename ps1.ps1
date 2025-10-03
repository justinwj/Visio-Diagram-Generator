git ls-files | ForEach-Object { Get-Content $_ | Measure-Object -Line | Select-Object -ExpandProperty Lines } | Measure-Object -Sum
# This PowerShell script counts the total number of lines in all files tracked by Git in the current repository.


# The error is from running an older CLI that only supports schema 1.0/1.1. I rebuilt the solution so the CLI now accepts 1.2.
# Do this
# • Re-run your command using the freshly built binary:
& "src/VDG.CLI/bin/Release/net48/VDG.CLI.exe" "samples/sample_architecture_layered.json" "out/sample-architecture_layered.vsdx"
# • If PowerShell still shows the same error, ensure you’re invoking this exact path (not a different copy on PATH). Quick check:
Get-Item src/VDG.CLI/bin/Release/net48/VDG.CLI.exe | Format-Table FullName,LastWriteTime
# Optional
# • Try the new diagnostics overrides:
& "src/VDG.CLI/bin/Release/net48/VDG.CLI.exe" --diag-height 9.5 --diag-lane-max 10 samples/sample_architecture_layered.json out/layered.vsdx