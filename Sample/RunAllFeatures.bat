@echo off
setlocal

set "app=%~dp0..\out\CodexBatch\CodexBatch.exe"
if not exist "%app%" (
    echo Release executable not found: "%app%" 1>&2
    exit /b 1
)

"%app%" "%~dp0AllFeatures.txt"
if errorlevel 1 exit /b 1

set "work=C:\temp\CodexBatchTest"
if not exist "%work%\names (CP932).txt" exit /b 1
if not exist "%work%\names-utf8.txt" exit /b 1
if exist "%work%\empty.txt" exit /b 1
if exist "%work%\cleanup folder" exit /b 1
exit /b 0
