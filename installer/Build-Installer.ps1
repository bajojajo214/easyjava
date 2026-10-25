param(
    [Parameter(Mandatory=$true)][string]$PayloadDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$IntermediateDirectory
)
$ErrorActionPreference = 'Stop'
$payloadSource = (Resolve-Path -LiteralPath $PayloadDirectory).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $IntermediateDirectory) { $IntermediateDirectory = Join-Path $env:TEMP ('EasyJava-InstallerBuild-' + [guid]::NewGuid().ToString('N')) }
$buildPath = [IO.Path]::GetFullPath($IntermediateDirectory)
$stagePath = Join-Path $buildPath 'payload'
$packagePath = Join-Path $buildPath 'package'
if (Test-Path -LiteralPath $stagePath) { throw 'Folder pośredni zawiera już payload. Wybierz nowy IntermediateDirectory.' }
New-Item -ItemType Directory -Force -Path $outputPath,$buildPath,$stagePath,$packagePath | Out-Null
foreach ($file in @('easyjava.exe','easyjava.ico')) {
    $source = Join-Path $payloadSource $file
    if (-not (Test-Path -LiteralPath $source)) { throw "Brak $source. Najpierw uruchom Build-EasyJava.ps1." }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stagePath $file)
}
foreach ($file in @('Instrukcja.txt','CHANGELOG.md','Raport-testow.txt','Pokrycie-bloczkow.txt','Ollama.md')) {
    $source = Join-Path $payloadSource $file
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $stagePath $file) }
}
$examples = Join-Path $payloadSource 'Przyklady'
$stageExamples = Join-Path $stagePath 'Przyklady'
New-Item -ItemType Directory -Force -Path $stageExamples | Out-Null
if (Test-Path -LiteralPath $examples) {
    Get-ChildItem -LiteralPath $examples -Filter '*.js' -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stageExamples $_.Name) }
} else {
    $exampleBuilder = Start-Process -FilePath (Join-Path $stagePath 'easyjava.exe') -ArgumentList '--examples',('"' + $stageExamples + '"') -WindowStyle Hidden -PassThru -Wait
    if ($exampleBuilder.ExitCode -ne 0) { throw 'Nie udało się zbudować przykładów.' }
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Brak kompilatora .NET Framework 4 w 64-bitowym Windows.' }
$builder = Join-Path $buildPath 'BuildMsi.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output ('/out:' + $builder) (Join-Path $PSScriptRoot 'BuildMsi.cs')
if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji generatora MSI.' }
& $builder $stagePath $packagePath
if ($LASTEXITCODE -ne 0) { throw 'Błąd budowy pakietu MSI.' }
$msi = Join-Path $packagePath 'EasyJava.msi'
$digest = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()
$info = Join-Path $buildPath 'PackageInfo.cs'
[IO.File]::WriteAllText($info,('public static class PackageInfo { public const string Sha256="' + $digest + '"; }'),[Text.Encoding]::UTF8)
$setup = Join-Path $outputPath 'EasyJava-Setup.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll ('/win32icon:' + (Join-Path $stagePath 'easyjava.ico')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'setup.manifest')) ('/resource:' + $msi + ',EasyJava.Setup.Package') ('/out:' + $setup) (Join-Path $PSScriptRoot 'Setup.cs') $info
if ($LASTEXITCODE -ne 0) { throw 'Błąd kompilacji instalatora EXE.' }
Write-Output $setup
