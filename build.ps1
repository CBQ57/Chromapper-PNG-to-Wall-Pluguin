[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ChroMapperDir,

    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$resolvedChroMapperDir = (Resolve-Path -LiteralPath $ChroMapperDir).Path
$mainDll = Join-Path $resolvedChroMapperDir 'ChroMapper_Data\Managed\Main.dll'
$managedDir = Join-Path $resolvedChroMapperDir 'ChroMapper_Data\Managed'

if (-not (Test-Path -LiteralPath $mainDll -PathType Leaf)) {
    throw "Main.dll が見つかりません: $mainDll"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$hasSdk = $false
if ($dotnet) {
    $sdkVersions = @(& $dotnet.Source --list-sdks 2>$null)
    $hasSdk = $sdkVersions.Count -gt 0
}

if ($hasSdk) {
    dotnet run --project (Join-Path $projectRoot 'tests\RasterizerTests.csproj') -c Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Rasterizer tests failed.'
    }

    $deployValue = if ($Deploy) { 'true' } else { 'false' }
    dotnet build (Join-Path $projectRoot 'PngWall.Plugin.csproj') -c Release `
        "-p:ChroMapperDir=$resolvedChroMapperDir" `
        "-p:DeployToChroMapper=$deployValue"
    if ($LASTEXITCODE -ne 0) {
        throw 'Plugin build failed.'
    }
} else {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
        throw 'C# compiler が見つかりません。.NET SDKをインストールしてください。'
    }

    $testOutput = Join-Path $projectRoot 'tests\RasterizerTests.exe'
    & $compiler /nologo /optimize+ /out:$testOutput `
        (Join-Path $projectRoot 'src\ImageWallRasterizer.cs') `
        (Join-Path $projectRoot 'tests\Program.cs')
    if ($LASTEXITCODE -ne 0) {
        throw 'Rasterizer tests failed to compile.'
    }
    & $testOutput
    if ($LASTEXITCODE -ne 0) {
        throw 'Rasterizer tests failed.'
    }

    $outputDir = Join-Path $projectRoot 'bin\Release'
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    $references = @(
        'mscorlib.dll', 'netstandard.dll', 'System.dll', 'System.Core.dll',
        'Main.dll', 'Plugins.dll', 'FileBrowser.dll', 'LiteNetLib.dll', 'Input.dll',
        'Unity.InputSystem.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll',
        'UnityEngine.ImageConversionModule.dll', 'UnityEngine.UI.dll', 'Unity.TextMeshPro.dll'
    ) | ForEach-Object { "/reference:$(Join-Path $managedDir $_)" }
    $sources = @(
        (Join-Path $projectRoot 'src\ImageWallRasterizer.cs'),
        (Join-Path $projectRoot 'src\Plugin.cs')
    )
    & $compiler /nologo /noconfig /target:library /optimize+ /nostdlib+ `
        "/out:$(Join-Path $outputDir 'PngWall.dll')" $references $sources
    if ($LASTEXITCODE -ne 0) {
        throw 'Plugin build failed.'
    }

    if ($Deploy) {
        $pluginDir = Join-Path $resolvedChroMapperDir 'Plugins'
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $outputDir 'PngWall.dll') -Destination $pluginDir -Force
    }
}

$distPlugins = Join-Path $projectRoot 'dist\Plugins'
New-Item -ItemType Directory -Path $distPlugins -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\PngWall.dll') -Destination $distPlugins -Force

$archive = Join-Path $projectRoot 'dist\PngWall.zip'
if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $projectRoot 'dist\Plugins') -DestinationPath $archive

$versionArchives = @()
foreach ($version in @('CM0.12.874', 'CM0.13.892')) {
    $versionRoot = Join-Path $projectRoot "dist\$version"
    $versionPlugins = Join-Path $versionRoot 'Plugins'
    New-Item -ItemType Directory -Path $versionPlugins -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\PngWall.dll') -Destination $versionPlugins -Force

    $versionArchive = Join-Path $projectRoot "dist\PngWall-$version.zip"
    if (Test-Path -LiteralPath $versionArchive) {
        Remove-Item -LiteralPath $versionArchive -Force
    }
    Compress-Archive -Path $versionPlugins -DestinationPath $versionArchive
    $versionArchives += $versionArchive
}

Write-Host "Built: $archive"
foreach ($versionArchive in $versionArchives) {
    Write-Host "Built: $versionArchive"
}
if ($Deploy) {
    Write-Host "Deployed: $(Join-Path $resolvedChroMapperDir 'Plugins\PngWall.dll')"
}
