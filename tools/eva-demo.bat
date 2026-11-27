@echo off
REM Lanceur rapide pour la demonstration en salle EVA (Windows).
REM Placez GODOT_BIN dans vos variables d'environnement ou lancez depuis le dossier contenant DogeballVR.exe.

setlocal
set MODE=%~1
if "%MODE%"=="" set MODE=host
set MAP=%~2
if "%MAP%"=="" set MAP=croix_neon

if defined GODOT_BIN (
    set EXE="%GODOT_BIN%" --path "%~dp0.." --
) else if exist "%~dp0..\DogeballVR.exe" (
    set EXE="%~dp0..\DogeballVR.exe" --
) else (
    set EXE=godot --path "%~dp0.." --
)

if /I "%MODE%"=="host" (
    echo [EVA] Lancement PC Hote LAN - Arene : %MAP% (F6 Relancer ^| F7 Arene ^| F8 Camera TV ^| F10 Masquer^)
    %EXE% --host --map=%MAP% --eva-demo --eva-end-hold=15
    goto :eof
)
if /I "%MODE%"=="solo" (
    echo [EVA] Lancement Demo Solo - Arene : %MAP%
    %EXE% --map=%MAP% --eva-demo --eva-end-hold=15
    goto :eof
)
if /I "%MODE%"=="spectator" (
    echo [EVA] Lancement Vitrine TV - Arene : %MAP%
    %EXE% --map=%MAP% --pc-autoplay --vista-orbit --eva-demo
    goto :eof
)

echo Usage : eva-demo.bat [host^|solo^|spectator] [croix_neon^|fosse_croisee]
exit /b 1
