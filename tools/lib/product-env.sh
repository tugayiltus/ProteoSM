# Environment variable names of the tools: PROTEOSM_* since the ProteoSM rename. A PROTEONE_* variable from before the
# rename still counts while its PROTEOSM_* name is unset: sourcing this file exports PROTEOSM_<X> from PROTEONE_<X> for
# every such variable, so scripts (and the processes they start) read only the new names.
# Source it before reading any PROTEOSM_* variable.
#
# For keys inside an env file (deploy/.env) use product_env_value from tools/lib/env-file.sh.
PRODUCT_ENV_PREFIX="PROTEOSM_"
FORMER_PRODUCT_ENV_PREFIX="PROTEONE_"

product_env_fallback() {
  local former suffix current
  while IFS= read -r former; do
    suffix="${former#"$FORMER_PRODUCT_ENV_PREFIX"}"
    current="$PRODUCT_ENV_PREFIX$suffix"
    if [[ -z "${!current+set}" ]]; then
      export "$current=${!former}"
    fi
  done < <(compgen -e | grep "^$FORMER_PRODUCT_ENV_PREFIX" || true)
}

product_env_fallback
