param(
    [Parameter(Mandatory = $true)]
    [string] $SearchRoot,

    [decimal] $MinimumLineCoverage = 90
)

$coverageFiles = Get-ChildItem -Path $SearchRoot -Recurse -Filter coverage.cobertura.xml
if ($coverageFiles.Count -eq 0) {
    throw "No Cobertura coverage reports were found under '$SearchRoot'."
}

$excludedFragments = @(
    '\Migrations\',
    '\obj\',
    'LoansDbContextFactory.cs'
)

$lines = @{}
foreach ($coverageFile in $coverageFiles) {
    [xml] $coverage = Get-Content -Path $coverageFile.FullName
    foreach ($package in $coverage.coverage.packages.package) {
        foreach ($class in $package.classes.class) {
            $fileName = $class.filename -replace '/', '\'
            $isExcluded = $false
            foreach ($fragment in $excludedFragments) {
                if ($fileName.Contains($fragment)) {
                    $isExcluded = $true
                    break
                }
            }

            if ($isExcluded) {
                continue
            }

            foreach ($line in $class.lines.line) {
                $key = "$fileName`:$($line.number)"
                $hits = [int] $line.hits
                if (-not $lines.ContainsKey($key) -or $hits -gt $lines[$key]) {
                    $lines[$key] = $hits
                }
            }
        }
    }
}

if ($lines.Count -eq 0) {
    throw "Coverage reports did not contain any measurable lines after exclusions."
}

$covered = ($lines.Values | Where-Object { $_ -gt 0 }).Count
$coverageRate = [math]::Round(($covered * 100) / $lines.Count, 2)
Write-Host "Combined backend line coverage: $coverageRate% ($covered/$($lines.Count))"

if ($coverageRate -lt $MinimumLineCoverage) {
    throw "Combined backend line coverage is below $MinimumLineCoverage%."
}
