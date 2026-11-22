#!/usr/bin/env bash
# Plan progress: task status from the plan's SDD ledger, plus branch, recent commits and uncommitted changes.
# Usage: tools/progress.sh [plan-file]   (default: newest plan in docs/superpowers/plans/)
# Colors only on a terminal; set NO_COLOR=1 to turn them off.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly PLANS_DIR="docs/superpowers/plans"
readonly SDD_DIR=".superpowers/sdd"
readonly LEDGER_TAIL_LINES=5
readonly RECENT_COMMITS=5
readonly MAX_CHANGED_FILES=30
readonly PERCENT_SCALE=100
readonly BAR_WIDTH=40
readonly TITLE_WIDTH=62
readonly NOTE_WIDTH=90
readonly RULE_WIDTH=72

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
  B=$'\e[1m'; DIM=$'\e[2m'; R=$'\e[0m'
  GREEN=$'\e[32m'; YELLOW=$'\e[33m'; BLUE=$'\e[34m'; CYAN=$'\e[36m'; RED=$'\e[31m'; GREY=$'\e[90m'
else
  B=""; DIM=""; R=""; GREEN=""; YELLOW=""; BLUE=""; CYAN=""; RED=""; GREY=""
fi

repeat() { local i out=""; for ((i = 0; i < $2; i++)); do out+="$1"; done; printf '%s' "$out"; }
cut_to() { local s="$1" w="$2"; if [ "${#s}" -gt "$w" ]; then printf '%s…' "${s:0:$((w - 1))}"; else printf '%s' "$s"; fi; }
section() { echo; printf '%s%s %s %s%s\n' "$B$CYAN" "──" "$1" "$(repeat '─' $((RULE_WIDTH - ${#1} - 4)))" "$R"; }

plan="${1:-$(find "$PLANS_DIR" -maxdepth 1 -name '*.md' | sort | tail -1)}"
ledger="$SDD_DIR/$(basename "$plan" .md)/progress.md"

printf '%s╭%s╮%s\n' "$B$BLUE" "$(repeat '─' $((RULE_WIDTH - 2)))" "$R"
printf '%s│%s %s📋 %s%s\n' "$B$BLUE" "$R" "$B" "$(basename "$plan" .md)" "$R"
printf '%s╰%s╯%s\n' "$B$BLUE" "$(repeat '─' $((RULE_WIDTH - 2)))" "$R"

if [ ! -f "$ledger" ]; then
  echo "${YELLOW}No ledger at $ledger (plan not started, or finished and workspace removed).${R}"
else
  section "Tasks"
  total=0; done_count=0; active_count=0
  while IFS='|' read -r n title; do
    total=$((total + 1))
    last="$(grep -E "^Task $n: " "$ledger" | tail -1 || true)"
    if grep -qE "^Task $n: complete" "$ledger"; then
      done_count=$((done_count + 1))
      printf '  %s✔%s %s%2s%s  %s%s%s\n' "$GREEN" "$R" "$DIM" "$n" "$R" "$GREEN" "$(cut_to "$title" "$TITLE_WIDTH")" "$R"
    elif [ -n "$last" ]; then
      active_count=$((active_count + 1))
      printf '  %s●%s %s%2s%s  %s%s%s\n' "$YELLOW" "$R" "$B" "$n" "$R" "$B$YELLOW" "$(cut_to "$title" "$TITLE_WIDTH")" "$R"
      printf '        %s↳ %s%s\n' "$GREY" "$(cut_to "${last#Task "$n": }" "$NOTE_WIDTH")" "$R"
    else
      printf '  %s○%s %s%2s  %s%s\n' "$GREY" "$R" "$DIM" "$n" "$(cut_to "$title" "$TITLE_WIDTH")" "$R"
    fi
  done < <(grep -E '^### Task [0-9]+:' "$plan" | sed -E 's/^### Task ([0-9]+): (.*)/\1|\2/')

  if [ "$total" -gt 0 ]; then
    percent=$((done_count * PERCENT_SCALE / total))
    filled=$((done_count * BAR_WIDTH / total))
    active_cells=$((active_count * BAR_WIDTH / total))
    [ $((filled + active_cells)) -gt "$BAR_WIDTH" ] && active_cells=$((BAR_WIDTH - filled))
    empty=$((BAR_WIDTH - filled - active_cells))
    echo
    printf '  %s%s%s%s%s%s%s  %s%d%%%s  %s(%d/%d done, %d active, %d todo)%s\n' \
      "$GREEN" "$(repeat '█' "$filled")" "$YELLOW" "$(repeat '▓' "$active_cells")" "$GREY" "$(repeat '░' "$empty")" "$R" \
      "$B" "$percent" "$R" "$DIM" "$done_count" "$total" "$active_count" "$((total - done_count - active_count))" "$R"
  fi

  section "Last $LEDGER_TAIL_LINES ledger lines"
  tail -"$LEDGER_TAIL_LINES" "$ledger" | while IFS= read -r line; do
    printf '  %s│%s %s\n' "$GREY" "$R" "$(cut_to "$line" "$NOTE_WIDTH")"
  done
fi

section "Branch / last commits"
printf '  %s⎇  %s%s\n' "$B$CYAN" "$(git branch --show-current)" "$R"
git log --oneline -"$RECENT_COMMITS" | while read -r sha subject; do
  printf '  %s%s%s %s\n' "$YELLOW" "$sha" "$R" "$subject"
done

section "Uncommitted changes"
changes="$(git status --short)"
if [ -z "$changes" ]; then
  echo "  ${GREEN}✔ clean${R}"
else
  printf '%s\n' "$changes" | head -"$MAX_CHANGED_FILES" | while IFS= read -r line; do
    code="${line:0:2}"; path="${line:3}"
    case "$code" in
      *M*) color="$YELLOW"; mark="~" ;;
      *A*) color="$GREEN"; mark="+" ;;
      *D*) color="$RED"; mark="-" ;;
      "??") color="$GREEN"; mark="?" ;;
      *) color="$GREY"; mark="·" ;;
    esac
    printf '  %s%s%s %s\n' "$color" "$mark" "$R" "$path"
  done
  count="$(printf '%s\n' "$changes" | wc -l | tr -d ' ')"
  [ "$count" -gt "$MAX_CHANGED_FILES" ] && echo "  ${DIM}… and $((count - MAX_CHANGED_FILES)) more${R}"
  printf '  %s%d file(s) changed%s\n' "$DIM" "$count" "$R"
fi
