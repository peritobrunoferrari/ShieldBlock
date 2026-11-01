@echo off
REM ============================================================
REM  ShieldBlock v6.5 - Script de Compilacao
REM  1) Tenta MSBuild do Visual Studio
REM  2) Fallback automatico: csc.exe do .NET Framework 4.7.2
REM ============================================================

cd /d %~dp0
title ShieldBlock v6.5 - Build
color 0B

echo.
echo ============================================================
echo   SHIELDBLOCK v6.5 - Compilando...
echo ============================================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [AVISO] Voce nao esta como Administrador.
    echo         Build funciona, mas o ShieldBlock precisa rodar como Admin.
    echo.
)

set MSBUILD_VS=
for %%V in (
    "C:\Program Files\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
) do (
    if exist %%V (
        set MSBUILD_VS=%%V
        goto :vs_found
    )
)

echo [INFO] Visual Studio nao encontrado.
echo [INFO] Usando fallback: csc.exe direto do .NET Framework.
goto :try_csc

:vs_found
echo [OK]  Visual Studio MSBuild encontrado:
echo       %MSBUILD_VS%
echo [...] Compilando via MSBuild...
echo.
%MSBUILD_VS% ShieldBlock.csproj /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
if %errorLevel% neq 0 (
    echo.
    echo [ERRO] MSBuild falhou. Tentando fallback csc.exe...
    goto :try_csc
)
goto :success

:try_csc
echo.
echo ============================================================
echo   Modo fallback: csc.exe direto
echo ============================================================

set CSC=
for %%C in (
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) do (
    if exist %%C (
        set CSC=%%C
        goto :csc_found
    )
)
echo [ERRO] csc.exe nao encontrado. Instale .NET Framework 4.7.2 SDK.
pause
exit /b 1

:csc_found
echo [OK]  csc.exe encontrado: %CSC%
echo.

if not exist "bin\Release" mkdir "bin\Release"
if not exist "bin\Release\Config" mkdir "bin\Release\Config"
if not exist "bin\Release\Logs" mkdir "bin\Release\Logs"
if not exist "bin\Release\Resources" mkdir "bin\Release\Resources"

echo [...] Compilando 36 arquivos .cs...
echo.

%CSC% /nologo /target:winexe /out:bin\Release\ShieldBlock.exe ^
    /win32manifest:app.manifest ^
    /win32icon:Resources\shield.ico ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.IO.Compression.dll ^
    /reference:System.Management.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Security.dll ^
    /reference:System.Xml.dll ^
    /optimize+ ^
    /langversion:5 ^
    src\Program.cs ^
    src\Core\Theme.cs ^
    src\Core\PeritoConfig.cs ^
    src\Core\ShieldBlockEngine.cs ^
    src\Core\DriveDetector.cs ^
    src\Core\PartitionAnalyzer.cs ^
    src\Core\LogSession.cs ^
    src\Core\AutoLogger.cs ^
    src\Core\SessionRegistry.cs ^
    src\Core\UsbWatcher.cs ^
    src\Core\RegistryTamperGuard.cs ^
    src\Core\FileSystemMonitor.cs ^
    src\Core\ProcessAuditor.cs ^
    src\Core\HeartbeatService.cs ^
    src\Core\ForensicToolWatcher.cs ^
    src\Icp\IcpBrasilCertificate.cs ^
    src\Icp\IcpBrasilSigner.cs ^
    src\Icp\IcpTimestampClient.cs ^
    src\Icp\IcpBrasilConfig.cs ^
    src\Utils\HashUtility.cs ^
    src\Utils\Iso8601.cs ^
    src\Utils\MachineFingerprint.cs ^
    src\Export\ExportManager.cs ^
    src\Export\PdfExporter.cs ^
    src\Export\HtmlExporter.cs ^
    src\Export\JsonExporter.cs ^
    src\Export\XmlDfrwsExporter.cs ^
    src\Export\LogOnlyExporter.cs ^
    src\UI\MainForm.cs ^
    src\UI\StatusCard.cs ^
    src\UI\UiHelpers.cs ^
    src\UI\PeritoEditDialog.cs ^
    src\UI\HistoryPanel.cs ^
    src\UI\IcpBrasilDialog.cs ^
    src\UI\ShieldLogo.cs ^
    src\UI\SplashAboutForm.cs

if %errorLevel% neq 0 (
    echo.
    echo ============================================================
    echo   [ERRO] Build falhou. Cole o erro acima para diagnostico.
    echo ============================================================
    pause
    exit /b 1
)

if exist "Config\perito.json" copy "Config\perito.json" "bin\Release\Config\" >nul
if exist "Resources\Shieldpericias.svg" copy "Resources\Shieldpericias.svg" "bin\Release\Resources\" >nul
if exist "Resources\shield_logo_large.png" copy "Resources\shield_logo_large.png" "bin\Release\Resources\" >nul
if exist "Resources\shield_logo_small.png" copy "Resources\shield_logo_small.png" "bin\Release\Resources\" >nul
if exist "Resources\shield.ico" copy "Resources\shield.ico" "bin\Release\Resources\" >nul
if exist "Resources\shield_logo_black.png" copy "Resources\shield_logo_black.png" "bin\Release\Resources\" >nul

:success
echo.
echo ============================================================
echo   [OK] Build concluido com sucesso!
echo   Executavel: bin\Release\ShieldBlock.exe
echo ============================================================
echo.

if not exist "bin\Release\Config" mkdir "bin\Release\Config"
if not exist "bin\Release\Logs"   mkdir "bin\Release\Logs"
if not exist "bin\Release\Config\perito.json" (
    if exist "Config\perito.json" copy "Config\perito.json" "bin\Release\Config\" >nul
)

set /p OPEN=Deseja executar o ShieldBlock agora? (S/N):
if /I "%OPEN%"=="S" (
    echo Iniciando ShieldBlock...
    start "" "bin\Release\ShieldBlock.exe"
)

pause
