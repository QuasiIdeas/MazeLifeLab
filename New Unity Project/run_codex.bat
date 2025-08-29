@echo off
REM run_codex.bat - helper to run codex CLI with telemetry, console log and screenshots
REM Usage: run_codex.bat [telemetry.csv] [console.txt] [screenshot.png]

setlocal
set CODex=codex

REM Determine telemetry file (arg1 overrides automatic detection)
if "%1"=="" (
  set TELEMETRY_DIR=TelemetryLogs
  set TELEMETRY=
  for /f "delims=" %%F in ('dir "%TELEMETRY_DIR%\*.csv" /b /a-d /o:-d 2^>nul') do (
    set TELEMETRY=%%F
    goto :foundTelemetry
  )
  :foundTelemetry
  if defined TELEMETRY ( set TELEMETRY=%TELEMETRY_DIR%\%TELEMETRY% ) else ( set TELEMETRY=TelemetryLogs\telemetry_latest.csv )
) else (
  set TELEMETRY=%~1
)

REM Determine console log (arg2 overrides)
if "%2"=="" (
  set LOG_DIR=Logs
  set CONSOLE_LOG=
  for /f "delims=" %%F in ('dir "%LOG_DIR%\*.txt" /b /a-d /o:-d 2^>nul') do (
    set CONSOLE_LOG=%%F
    goto :foundLog
  )
  :foundLog
  if defined CONSOLE_LOG ( set CONSOLE_LOG=%LOG_DIR%\%CONSOLE_LOG% ) else ( set CONSOLE_LOG=Logs\console.txt )
) else (
  set CONSOLE_LOG=%~2
)

REM Determine screenshot (arg3 overrides)
if "%3"=="" (
  set SNAP_DIR=Screenshots
  set SCREEN1=
  for /f "delims=" %%F in ('dir "%SNAP_DIR%\*.png" /b /a-d /o:-d 2^>nul') do (
    set SCREEN1=%%F
    goto :foundSnap
  )
  :foundSnap
  if defined SCREEN1 ( set SCREEN1=%SNAP_DIR%\%SCREEN1% ) else ( set SCREEN1=Screenshots\Screenshot 2025-08-28 194241.png )
) else (
  set SCREEN1=%~3
)

REM Prompt file in repo root
set PROMPT_FILE=prompt.txt
set OUTPUT=codex_result.txt

echo Using codex CLI: %CODex%
echo Telemetry: %TELEMETRY%
echo Console: %CONSOLE_LOG%
echo Screenshot: %SCREEN1%

echo Running Codex... > %OUTPUT%
"%CODex%" chat --model gpt-5-mini --prompt-file "%PROMPT_FILE%" --files "%TELEMETRY%","%CONSOLE_LOG%","%SCREEN1%" >> %OUTPUT% 2>&1

if errorlevel 1 (
  echo Codex returned an error. See %OUTPUT% for details.
) else (
  echo Codex finished. Output written to %OUTPUT%
)

endlocal
pause
