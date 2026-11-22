# OpenLDAP stand-in for Active Directory in the directory contract test (tools/directory-contract-test.sh).
# Built locally from a pinned Alpine base: it runs natively on Apple Silicon and amd64, and the whole setup
# (schema, access rules, TLS) stays in this folder instead of inside a third-party image's bootstrap scripts.
FROM alpine:3.20.3

RUN apk add --no-cache openldap openldap-back-mdb openldap-clients openssl

COPY ad-lite.schema /etc/openldap/schema/ad-lite.schema
COPY slapd.conf /etc/openldap/slapd.conf
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
COPY gen-test-ca.sh /usr/local/bin/gen-test-ca.sh
RUN chmod 0755 /usr/local/bin/entrypoint.sh /usr/local/bin/gen-test-ca.sh

EXPOSE 389 636
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
