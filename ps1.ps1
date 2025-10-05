git ls-files | ForEach-Object { Get-Content $_ | Measure-Object -Line | Select-Object -ExpandProperty Lines } | Measure-Object -Sum
# This PowerShell script counts the total number of lines in all files tracked by Git in the current repository.
