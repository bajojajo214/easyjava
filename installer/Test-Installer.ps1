param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$TestDirectory
)
$ErrorActionPreference = 'Stop'
$product = '{A99B8264-B2AA-5D28-9C82-706300DF4696}'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class EasyJavaInstallerProbe {
 [DllImport("msi.dll",CharSet=CharSet.Unicode)] public static extern int MsiQueryProductState(string product);
 [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiGetProductInfo(string product,string property,StringBuilder value,ref uint length);
 public static string Info(string product,string property){uint length=4096;StringBuilder value=new StringBuilder((int)length);uint code=MsiGetProductInfo(product,property,value,ref length);if(code!=0)throw new Exception("MsiGetProductInfo "+property+": "+code);return value.ToString();}
}
'@
if ([EasyJavaInstallerProbe]::MsiQueryProductState($product) -ne -1) { throw 'Ta wersja EasyJava jest już zarejestrowana. Test nie zmieni istniejącej instalacji.' }
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcut = Join-Path $desktop 'EasyJava.lnk'
if (Test-Path -LiteralPath $shortcut) { throw 'Skrót EasyJava.lnk już istnieje. Test nie nadpisze go.' }
$root = [IO.Path]::GetFullPath($TestDirectory)
if (Test-Path -LiteralPath $root) { throw 'Wybierz nowy, nieistniejący folder testowy.' }
New-Item -ItemType Directory -Path $root | Out-Null
$target = Join-Path $root 'app'
$msi = Join-Path $root 'extracted.msi'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$extract = Start-Process -FilePath $installerPath -ArgumentList '--extract-msi',('"' + $msi + '"') -WindowStyle Hidden -PassThru -Wait
if ($extract.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $msi)) { throw 'Nie udało się odczytać pakietu z finalnego EXE.' }
$results = [ordered]@{ ExeExtraction=$true; InstallExit=$null; RegisteredName=$null; ShortcutTarget=$false; ShortcutIcon=$false; InstalledAppStarts=$false; UninstallExit=$null; OwnedFilesRemoved=$false; ShortcutRemoved=$false; UserProjectPreserved=$false; ProductUnregistered=$false }
$installed = $false
try {
    $process = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList '/i',('"' + $msi + '"'),'/qn','/norestart','ALLUSERS=""',('INSTALLDIR="' + $target + '"'),'/L*V',('"' + (Join-Path $root 'install.log') + '"') -WindowStyle Hidden -PassThru -Wait
    $results.InstallExit = $process.ExitCode
    if ($process.ExitCode -ne 0) { throw "Błąd testowej instalacji: $($process.ExitCode)" }
    $installed = $true
    $results.RegisteredName = [EasyJavaInstallerProbe]::Info($product,'InstalledProductName')
    if ($results.RegisteredName -ne 'EasyJava') { throw 'Produkt nie zarejestrował się w Windows Installer.' }
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
    $results.ShortcutTarget = $link.TargetPath -eq (Join-Path $target 'easyjava.exe')
    if (-not $results.ShortcutTarget) { throw 'Niewłaściwy cel skrótu.' }
    $cachedIcon = $link.IconLocation.Substring(0,$link.IconLocation.LastIndexOf(','))
    $results.ShortcutIcon = (Get-FileHash -LiteralPath $cachedIcon).Hash -eq (Get-FileHash -LiteralPath (Join-Path $target 'easyjava.ico')).Hash
    if (-not $results.ShortcutIcon) { throw 'Skrót nie używa dostarczonej ikony.' }
    $snapshot = Start-Process -FilePath (Join-Path $target 'easyjava.exe') -ArgumentList '--snapshot',('"' + (Join-Path $root 'app.png') + '"') -WorkingDirectory $target -WindowStyle Hidden -PassThru -Wait
    $results.InstalledAppStarts = $snapshot.ExitCode -eq 0 -and (Test-Path -LiteralPath (Join-Path $root 'app.png'))
    if (-not $results.InstalledAppStarts) { throw 'Zainstalowana aplikacja nie uruchomiła się.' }
    [IO.File]::WriteAllText((Join-Path $target 'wlasny-projekt.js'),'// Osobny plik użytkownika powinien pozostać po odinstalowaniu.')
} finally {
    if ($installed) {
        # Remove only the product installed by this test; verify its exact registered path first.
        $registered = [EasyJavaInstallerProbe]::Info($product,'InstallLocation').TrimEnd('\')
        if ($registered -ne $target.TrimEnd('\')) { throw "Test nie odinstaluje innej lokalizacji: $registered" }
        $remove = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList '/x',$product,'/qn','/norestart','/L*V',('"' + (Join-Path $root 'uninstall.log') + '"') -WindowStyle Hidden -PassThru -Wait
        $results.UninstallExit = $remove.ExitCode
        $results.OwnedFilesRemoved = -not (Test-Path -LiteralPath (Join-Path $target 'easyjava.exe'))
        $results.ShortcutRemoved = -not (Test-Path -LiteralPath $shortcut)
        $results.UserProjectPreserved = Test-Path -LiteralPath (Join-Path $target 'wlasny-projekt.js')
        $results.ProductUnregistered = [EasyJavaInstallerProbe]::MsiQueryProductState($product) -eq -1
    }
    $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding UTF8
}
if ($results.UninstallExit -ne 0 -or -not $results.OwnedFilesRemoved -or -not $results.ShortcutRemoved -or -not $results.UserProjectPreserved -or -not $results.ProductUnregistered) { throw 'Nie przeszedł test odinstalowania; szczegóły w results.json.' }
$results | ConvertTo-Json

exit 0
