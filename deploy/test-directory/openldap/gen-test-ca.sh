#!/bin/sh
# Creates a throw-away test CA and a server certificate for the "openldap" service in the shared certs volume.
# The CA key is deleted at once, so nothing else can be signed with it. Skips when a certificate already exists.
set -eu

readonly CERT_DIR=/certs
readonly VALID_DAYS=7
readonly KEY_BITS=2048
readonly CA_NAME="Proteone Test Directory CA"
# The tests reach the server by its compose service name; localhost covers the health check and manual checks.
readonly SERVER_NAME=openldap
readonly SUBJECT_ALT_NAMES="DNS:openldap,DNS:localhost,IP:127.0.0.1"

if [ -f "$CERT_DIR/server.crt" ]; then
  echo "test CA: certificate already present"
  exit 0
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

cat > "$work/ca.cnf" <<EOF
[req]
prompt = no
distinguished_name = dn
x509_extensions = v3_ca
[dn]
CN = $CA_NAME
[v3_ca]
basicConstraints = critical,CA:TRUE
keyUsage = critical,keyCertSign,cRLSign
subjectKeyIdentifier = hash
EOF

cat > "$work/server.ext" <<EOF
basicConstraints = CA:FALSE
keyUsage = critical,digitalSignature,keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = $SUBJECT_ALT_NAMES
authorityKeyIdentifier = keyid
subjectKeyIdentifier = hash
EOF

openssl req -x509 -config "$work/ca.cnf" -newkey "rsa:$KEY_BITS" -nodes -days "$VALID_DAYS" \
  -keyout "$work/ca.key" -out "$CERT_DIR/ca.crt"
openssl req -new -newkey "rsa:$KEY_BITS" -nodes -subj "/CN=$SERVER_NAME" \
  -keyout "$CERT_DIR/server.key" -out "$work/server.csr"
openssl x509 -req -in "$work/server.csr" -CA "$CERT_DIR/ca.crt" -CAkey "$work/ca.key" -CAcreateserial \
  -days "$VALID_DAYS" -extfile "$work/server.ext" -out "$CERT_DIR/server.crt"

# slapd runs as "ldap" and must read its key; everyone may read the certificates.
chown ldap:ldap "$CERT_DIR/server.key"
chmod 0400 "$CERT_DIR/server.key"
chmod 0444 "$CERT_DIR/ca.crt" "$CERT_DIR/server.crt"
echo "test CA: created $CERT_DIR/ca.crt and a server certificate for $SUBJECT_ALT_NAMES"
