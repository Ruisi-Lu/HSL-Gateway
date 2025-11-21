#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BACKGROUND_PIDS=()

cleanup_processes() {
  if [[ ${#BACKGROUND_PIDS[@]} -eq 0 ]]; then
    return
  fi

  echo
  echo "🔻 停止背景程序..."
  for pid in "${BACKGROUND_PIDS[@]}"; do
    if ps -p "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
      wait "$pid" >/dev/null 2>&1 || true
    fi
  done
  BACKGROUND_PIDS=()
}

trap cleanup_processes EXIT INT TERM

require_command() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "❌ 缺少指令: $cmd" >&2
    exit 1
  fi
}

build_projects() {
  if [[ $# -eq 0 ]]; then
    return
  fi

  echo "📦 編譯專案..."
  for project in "$@"; do
    echo "  • $project"
    (cd "$REPO_ROOT" && dotnet build "$project" --no-restore -v minimal >/dev/null)
  done
}

start_background() {
  local label="$1"
  shift

  (
    cd "$REPO_ROOT"
    "$@"
  ) </dev/null &
  local pid=$!
  BACKGROUND_PIDS+=("$pid")
  echo "  • $label (pid $pid)"
}

print_separator() {
  printf '\n═══════════════════════════════════════════════\n'
  printf "  %s\n" "$1"
  printf '═══════════════════════════════════════════════\n\n'
}
