$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Brak kompilatora .NET Framework 4.' }
$sources = @('Model.cs','Runtime.cs','Editor.cs','Examples.cs','Program.cs','AssemblyInfo.cs','DragDropUi.cs','DragDropTests.cs','TimingDialog.cs','TimingTests.cs','PauseUiTests.cs','CommandRunner.cs','CommandTests.cs','CommandWindow.cs','CommandWindowTests.cs','TerminalScreen.cs','TerminalTests.cs','RuntimeFiles.cs','StandaloneTests.cs','AllFeatureTests.cs','BackgroundCommandTests.cs','OllamaClient.cs','OllamaProject.cs','OllamaDialog.cs','OllamaTests.cs','OllamaUiTests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /reference:System.Core.dll /reference:System.Net.Http.dll ('/win32icon:' + (Join-Path $PSScriptRoot 'easyjava.ico')) ('/out:' + (Join-Path $PSScriptRoot 'easyjava.exe')) $sources
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja nie powiodła się.' }
Write-Output (Join-Path $PSScriptRoot 'easyjava.exe')
