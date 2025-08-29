#!/usr/bin/env bash
# run_codex.sh - helper to run codex CLI with telemetry, console log and screenshots
# Usage: ./run_codex.sh [telemetry.csv] [console.txt] [screenshot.png]
set -euo pipefail

CODEx=${CODEx:-codex}
PROMPT_FILE=${PROMPT_FILE:-prompt.txt}

# helpers to pick latest file in a dir matching pattern
pick_latest(){
  local dir=$1; shift
  local pattern=$1; shift
  local file
  if [ -d "$dir" ]; then
    file=$(ls -t "$dir"/$pattern 2>/dev/null || true)
    if [ -n "$file" ]; then
      echo "$file" | head -n1
      return 0
    fi
  fi
  return 1
}

# telemetry
if [ $# -ge 1 ] && [ -n "$1" ]; then
  TELEMETRY="$1"
else
  TELEMETRY=$(pick_latest "TelemetryLogs" "*.csv" || true)
  if [ -z "$TELEMETRY" ]; then
    TELEMETRY="TelemetryLogs/telemetry_latest.csv"
  fi
fi

# console
if [ $# -ge 2 ] && [ -n "$2" ]; then
  CONSOLE_LOG="$2"
else
  CONSOLE_LOG=$(pick_latest "Logs" "*.txt" || true)
  if [ -z "$CONSOLE_LOG" ]; then
    CONSOLE_LOG="Logs/console.txt"
  fi
fi

# screenshot
if [ $# -ge 3 ] && [ -n "$3" ]; then
  SCREEN1="$3"
else
  SCREEN1=$(pick_latest "Screenshots" "*.png" || true)
  if [ -z "$SCREEN1" ]; then
    SCREEN1=""
  fi
fi

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
OUTPUT="codex_result_${TIMESTAMP}.txt"

echo "codex CLI: ${CODEx}"
echo "prompt file: ${PROMPT_FILE}"
echo "telemetry: ${TELEMETRY}"
echo "console: ${CONSOLE_LOG}"
echo "screenshot: ${SCREEN1}"
echo "output: ${OUTPUT}"

echo "Running Codex..." > "$OUTPUT"

# build --files argument only including existing files
FILES_ARG=()
if [ -n "$TELEMETRY" ] && [ -f "$TELEMETRY" ]; then FILES_ARG+=("$TELEMETRY"); fi
if [ -n "$CONSOLE_LOG" ] && [ -f "$CONSOLE_LOG" ]; then FILES_ARG+=("$CONSOLE_LOG"); fi
if [ -n "$SCREEN1" ] && [ -f "$SCREEN1" ]; then FILES_ARG+=("$SCREEN1"); fi

if [ ${#FILES_ARG[@]} -gt 0 ]; then
  # join by comma as in Windows script
  IFS=','; FILES_JOINED="${FILES_ARG[*]}"; unset IFS
  "$CODEx" chat --model gpt-5-mini --prompt-file "$PROMPT_FILE" --files "$FILES_JOINED" >> "$OUTPUT" 2>&1 || rc=$?
else
  "$CODEx" chat --model gpt-5-mini --prompt-file "$PROMPT_FILE" >> "$OUTPUT" 2>&1 || rc=$?
fi

rc=${rc:-0}
if [ $rc -ne 0 ]; then
  echo "Codex returned error code $rc. See $OUTPUT for details." >&2
  exit $rc
fi

echo "Codex finished. Output written to $OUTPUT"
cat "$OUTPUT"

