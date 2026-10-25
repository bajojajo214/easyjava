# Budowanie instalatora EasyJava

Narzędzia: 64-bitowy Windows, .NET Framework z csc.exe, makecab.exe i Windows Installer. Zewnętrzny kompilator instalatorów nie jest wymagany.

1. Zbuduj aplikację przez `src/Build-EasyJava.ps1`.
2. Uruchom `installer/Build-Installer.ps1 -PayloadDirectory <folder-z-easyjava.exe-i-easyjava.ico> -OutputDirectory <wynik>`.
3. Wynik to `EasyJava-Setup.exe`, z osadzonym pakietem MSI i weryfikacją jego SHA-256.

Folder payload może zawierać instrukcję, raporty i folder Przyklady. Gdy przykładów brakuje, skrypt generuje je z aplikacji. Źródła i archiwa nie są kopiowane do payload.

`Test-Installer.ps1 -Installer <EXE> -TestDirectory <nowy-folder>` wykonuje rzeczywisty cykl instalacji/odinstalowania w kontekście bieżącego użytkownika. Test odmawia działania, jeśli ten ProductCode lub skrót EasyJava już istnieje. Sprawdza zarejestrowaną lokalizację przed odinstalowaniem i zachowuje osobny projekt testowy. Nie wymaga potwierdzenia instalacji do Program Files.

Normalny instalator używa ALLUSERS=1 i ProgramFiles64Folder/EasyJava. Windows obsługuje uprawnienia i odinstalowanie. Nie przejmujemy skojarzenia .js, nie wyłączamy zabezpieczeń i nie usuwamy folderów użytkownika.

Przy kolejnej wersji zmień ProductCode i Version w BuildMsi.cs, zachowując UpgradeCode. Źródła pakietu wykorzystują standardowe tabele i akcje Windows Installer.

Dokumentacja: [kontekst instalacji](https://learn.microsoft.com/en-us/windows/win32/msi/installation-context), [tabele bazy MSI](https://learn.microsoft.com/en-us/windows/win32/msi/database-tables), [aktualizacje](https://learn.microsoft.com/en-us/windows/win32/msi/upgrade-table).
