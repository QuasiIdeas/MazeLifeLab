@echo off
REM run_codex.bat - helper to run codex CLI with telemetry, console log and screenshots
REM Usage: run_codex.bat [telemetry.csv] [console.txt] [screenshot.png]

setlocal
set CODex=codex
if "%1"=="" (
  set TELEMETRY=TelemetryLogs\telemetry_latest.csv
) else (
  set TELEMETRY=%~1
)
if "%2"=="" (
  set CONSOLE_LOG=Logs\console.txt
) else (
  set CONSOLE_LOG=%~2
)
if "%3"=="" (
  set SCREEN1=Screenshots\Screenshot 2025-08-28 194241.png
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
"%CODex%" chat --model gpt-4o-mini --prompt-file "%PROMPT_FILE%" --files "%TELEMETRY%","%CONSOLE_LOG%","%SCREEN1%" >> %OUTPUT% 2>&1

if errorlevel 1 (
  echo Codex returned an error. See %OUTPUT% for details.
) else (
  echo Codex finished. Output written to %OUTPUT%
)

endlocal
pause
