@echo off
REM ============================================================
REM Wrapper de build do Servia
REM
REM Motivo: o ambiente Windows desta maquina nao expoe as
REM variaveis ProgramFiles e APPDATA ao processo do .NET quando
REM ele e iniciado pelo git-bash. Sem elas, o NuGet falha com
REM "Value cannot be null (Parameter 'path1')".
REM
REM Este wrapper define as variaveis e repassa o comando ao dotnet.
REM Uso:
REM   build.cmd restore
REM   build.cmd build
REM   build.cmd test
REM ============================================================

set "ProgramFiles=C:\Program Files"
set "ProgramFiles(x86)=C:\Program Files (x86)"
set "ProgramW6432=C:\Program Files"
set "CommonProgramFiles=C:\Program Files\Common Files"
set "ProgramData=C:\ProgramData"
set "APPDATA=%USERPROFILE%\AppData\Roaming"
set "LOCALAPPDATA=%USERPROFILE%\AppData\Local"

dotnet %*
exit /b %ERRORLEVEL%
