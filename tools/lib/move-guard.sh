# Guard of tools/deploy.sh for the move from Proteone to ProteoSM (docs/operations.md, "Proteone to ProteoSM move").
# Source tools/lib/env-file.sh first, then:
#   move_guard <env file>   exits 1 when this host holds the data of a Proteone stack (volume proteone_pgdata) and the
#                           ProteoSM database is missing or empty. A refused or failed restore and a plain
#                           "tools/compose.sh up" create the ProteoSM volume, so the volume alone proves nothing.
# Runs tools/compose.sh (postgres only) when both volumes exist; stops postgres again when it did not run before.

# A Proteone stack on this host (Compose project "proteone", before the ProteoSM rename) keeps its data in this volume.
readonly MOVE_GUARD_FORMER_DATA_VOLUME="proteone_pgdata"
readonly MOVE_GUARD_DEFAULT_PROJECT="proteosm"
readonly MOVE_GUARD_READY_TIMEOUT_SECONDS="${MOVE_GUARD_READY_TIMEOUT_SECONDS:-60}"
readonly MOVE_GUARD_DATABASE="proteosm"
readonly MOVE_GUARD_HISTORY_TABLE="__EFMigrationsHistory"

move_guard_refuse() {
  echo "Refused: this host has Proteone data ($MOVE_GUARD_FORMER_DATA_VOLUME) and $1. Move it first" >&2
  echo "(docs/operations.md, \"Proteone to ProteoSM move\"), or pass --fresh-install to start empty on purpose; the old" >&2
  echo "volume is not touched." >&2
  exit 1
}

move_guard_volume_exists() { docker volume inspect "$1" >/dev/null 2>&1; }

move_guard_sql() { tools/compose.sh exec -T postgres psql -U "$MOVE_GUARD_DATABASE" -d "$MOVE_GUARD_DATABASE" -XtAc "$1" | tr -d '[:space:]'; }

# Prints the number of applied migrations in the ProteoSM database, 0 when it holds no migration history table.
move_guard_applied_migrations() {
  local has_history
  has_history="$(move_guard_sql "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename = '$MOVE_GUARD_HISTORY_TABLE'")"
  if [[ "$has_history" == "0" ]]; then echo 0; return 0; fi
  move_guard_sql "SELECT count(*) FROM \"$MOVE_GUARD_HISTORY_TABLE\""
}

move_guard() {
  local env_file="$1" project data_volume was_running applied="" second
  move_guard_volume_exists "$MOVE_GUARD_FORMER_DATA_VOLUME" || return 0
  # Compose takes the project name from the shell first, then from the env file.
  project="${COMPOSE_PROJECT_NAME:-$(env_value "$env_file" COMPOSE_PROJECT_NAME)}"
  data_volume="${project:-$MOVE_GUARD_DEFAULT_PROJECT}_pgdata"
  if ! move_guard_volume_exists "$data_volume"; then move_guard_refuse "no ProteoSM data yet ($data_volume)"; fi
  # Without the env file compose cannot run; tools/deploy.sh stops on the missing file next.
  [[ -f "$env_file" ]] || return 0

  was_running="$(tools/compose.sh ps --services --status running 2>/dev/null | grep -cx postgres || true)"
  tools/compose.sh up -d postgres >/dev/null 2>&1 || true
  for ((second = 0; second < MOVE_GUARD_READY_TIMEOUT_SECONDS; second++)); do
    if tools/compose.sh exec -T postgres pg_isready -q -U "$MOVE_GUARD_DATABASE" -d "$MOVE_GUARD_DATABASE" >/dev/null 2>&1; then
      applied="$(move_guard_applied_migrations 2>/dev/null || true)"
      break
    fi
    sleep 1
  done
  if [[ "$was_running" == "0" ]]; then tools/compose.sh stop postgres >/dev/null 2>&1 || true; fi
  if [[ -z "$applied" ]]; then move_guard_refuse "the ProteoSM database ($data_volume) did not answer"; fi
  if [[ "$applied" == "0" ]]; then
    move_guard_refuse "the ProteoSM database ($data_volume) is empty (a refused or failed restore leaves it so)"
  fi
}
