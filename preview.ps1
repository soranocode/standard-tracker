[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 8765
)

$previewScript = Join-Path $PSScriptRoot 'design/serve-preview.py'
$interpreters = @(
    @{ Name = 'python'; Prefix = @() },
    @{ Name = 'py'; Prefix = @('-3') }
)

foreach ($interpreter in $interpreters) {
    $command = Get-Command $interpreter.Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $command) { continue }
    $prefix = $interpreter.Prefix
    & $command.Source @prefix -c 'import sys; sys.exit(0 if sys.version_info.major == 3 else 1)' 2>$null
    if ($LASTEXITCODE -ne 0) { continue }

    & $command.Source @prefix $previewScript --port $Port
    exit $LASTEXITCODE
}

Write-Error 'Python 3 is required. Install Python, then run preview.ps1 again.'
exit 1
