<#
.SYNOPSIS
    Сборка DupFinder Pro: restore, build, test, publish.

.DESCRIPTION
    Один скрипт для локальной проверки и для CI. Любой шаг можно пропустить.
    Публикация делает один самодостаточный exe под win-x64 (ТЗ §7 этап 7).

.EXAMPLE
    ./build.ps1
    ./build.ps1 -Publish
    ./build.ps1 -SkipTests -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipTests,

    [switch] $Publish,

    [switch] $Coverage,

    [string] $OutputPath = 'artifacts'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSCommandPath
$solution = Join-Path $root 'DupFinder.sln'
$app = Join-Path $root 'src/DupFinder.App/DupFinder.App.csproj'

function Invoke-Step {
    param([string] $Title, [scriptblock] $Action)

    Write-Host ''
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Шаг «$Title» завершился с кодом $LASTEXITCODE."
    }
}

Invoke-Step 'Восстановление пакетов' { dotnet restore $solution }

Invoke-Step "Сборка ($Configuration)" {
    dotnet build $solution -c $Configuration --no-restore
}

if (-not $SkipTests) {
    Invoke-Step 'Тесты' {
        $arguments = @($solution, '-c', $Configuration, '--no-build')
        if ($Coverage) {
            $arguments += @('--collect:XPlat Code Coverage', '--results-directory', (Join-Path $root $OutputPath 'coverage'))
        }
        dotnet test @arguments
    }
}

if ($Publish) {
    $target = Join-Path $root $OutputPath 'win-x64'
    Invoke-Step "Публикация в $target" {
        dotnet publish $app -c $Configuration -r win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -o $target
    }
}

Write-Host ''
Write-Host 'Готово.' -ForegroundColor Green
