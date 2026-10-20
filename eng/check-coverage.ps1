# Gate pokrivenosti: kombinovana pokrivenost linija za Domain + Application mora biti >= prag.
# Upotreba: pwsh eng/check-coverage.ps1 -Path <dir sa cobertura .xml> [-Threshold 80]
param(
    [Parameter(Mandatory)][string]$Path,
    [double]$Threshold = 80
)
$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -Path $Path -Recurse -Filter *.xml
if (-not $files) { throw "Nema cobertura izvestaja u '$Path'." }

$assemblies = @('ExcelImporter.Domain', 'ExcelImporter.Application')
# Spaja vise izvestaja: linija se racuna jednom, pokrivena ako je ijedan izvestaj pokrije.
$lines = @{}
foreach ($f in $files) {
    $xml = [xml](Get-Content -Raw $f.FullName)
    foreach ($pkg in $xml.coverage.packages.package) {
        if ($assemblies -notcontains $pkg.name) { continue }
        foreach ($cls in $pkg.classes.class) {
            foreach ($l in $cls.lines.line) {
                $key = "$($pkg.name)|$($cls.filename)|$($l.number)"
                $hit = [int]$l.hits -gt 0
                if (-not $lines.ContainsKey($key) -or $hit) { $lines[$key] = $hit }
            }
        }
    }
}

$found = $lines.Keys | ForEach-Object { $_.Split('|')[0] } | Sort-Object -Unique
foreach ($a in $assemblies) {
    if ($found -notcontains $a) { throw "Izvestaj ne sadrzi assembly '$a' - gate se ne moze izracunati." }
}

$total = $lines.Count
$covered = @($lines.Values | Where-Object { $_ }).Count
$pct = [math]::Round(100.0 * $covered / $total, 2)
$msg = "Pokrivenost Domain+Application: $pct% ($covered/$total linija), prag $Threshold%"
Write-Host $msg
if ($env:GITHUB_STEP_SUMMARY) { "### $msg" | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($pct -lt $Threshold) {
    Write-Host "::error::$msg - ispod praga."
    exit 1
}
