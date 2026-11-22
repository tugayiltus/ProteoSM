#!/bin/sh
# Loads seed.ldif into an empty database, then runs slapd with plain LDAP (for StartTLS) and LDAPS.
# The database lives in the container, so every `compose up` after `down` starts from the same seed.
set -eu

readonly CONFIG=/etc/openldap/slapd.conf
readonly SEED=/seed/seed.ldif
readonly DATA_DIR=/var/lib/openldap/openldap-data
readonly RUN_DIR=/run/openldap
readonly LISTEN="ldap:/// ldaps:///"
# 256 = connections and operations (stats); enough to see binds and searches in `docker compose logs`.
readonly DEBUG_LEVEL="${SLAPD_DEBUG_LEVEL:-256}"

mkdir -p "$DATA_DIR" "$RUN_DIR"
if [ ! -f "$DATA_DIR/data.mdb" ]; then
  slapadd -f "$CONFIG" -l "$SEED"
fi
chown -R ldap:ldap "$DATA_DIR" "$RUN_DIR"

exec slapd -d "$DEBUG_LEVEL" -u ldap -g ldap -f "$CONFIG" -h "$LISTEN"
