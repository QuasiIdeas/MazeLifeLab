#!/usr/bin/env bash
# run_codex.sh - helper to run codex CLI with telemetry, console log and screenshots
# Usage: ./run_codex.sh [screenshot.png]
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


# screenshot (optional latest screenshot as first argument)
if [ $# -ge 1 ] && [ -n "$1" ]; then
  SCREEN1="$1"
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
echo "model: ${MODEL:-gpt-5-mini}"
echo "screenshot: ${SCREEN1}"
echo "output: ${OUTPUT}"

echo "Running Codex..." > "$OUTPUT"

# Prepare input files list
# Prepare input files list: only images (screenshots) go via -i
FILES_ARG=()
if [ -n "$SCREEN1" ] && [ -f "$SCREEN1" ]; then
  FILES_ARG+=("$SCREEN1")
fi

# Run codex CLI: build prompt from prompt file, images only for screenshots
MODEL=${MODEL:-gpt-5-mini}
# Base prompt
PROMPT_TEXT=$(<"$PROMPT_FILE")
# Disable telemetry and console log inclusion
if false; then
# Append telemetry data into the prompt text
if [ -n "$TELEMETRY" ] && [ -f "$TELEMETRY" ]; then
  PROMPT_TEXT+=$'\n\n--- Telemetry: '"$TELEMETRY"$' ---\n'
  PROMPT_TEXT+="$(<"$TELEMETRY")"
fi
# Append console log into the prompt text
if [ -n "$CONSOLE_LOG" ] && [ -f "$CONSOLE_LOG" ]; then
  PROMPT_TEXT+=$'\n\n--- Console Log: '"$CONSOLE_LOG"$' ---\n'
  PROMPT_TEXT+="$(<"$CONSOLE_LOG")"
fi
fi  # end disable telemetry/console inclusion
# Build codex CLI command
CMD=("$CODEx" -m "$MODEL")
# Include screenshots as image inputs
for img in "${FILES_ARG[@]}"; do
  CMD+=( -i "$img" )
done
# Finally, the prompt text
CMD+=( "$PROMPT_TEXT" )
"${CMD[@]}" 

rc=${rc:-0}
if [ $rc -ne 0 ]; then
  echo "Codex returned error code $rc. See $OUTPUT for details." >&2
  exit $rc
fi

echo "Codex finished. Output written to $OUTPUT"
cat "$OUTPUT"

