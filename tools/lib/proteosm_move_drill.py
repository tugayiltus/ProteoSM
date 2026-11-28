"""HTTP checks for tools/proteosm-move-drill.sh (move of a Proteone stack into a ProteoSM stack). Standard library only.

extra <base-url> <state.json>
      On the old stack, after proteosm_drill.py seed: a custom email template, saved SMTP, LDAP and Entra secrets
      (each tested once, the result recorded), and a monitoring source whose key signs one event. Adds to state.json.
verify <base-url> <state.json>
      On the new stack after the restore: prints one JSON line per check; exits 1 when any check failed.
fresh <base-url> <admin-email> <admin-temporary-password>
      On a fresh stack: the first admin signs in, the default email templates exist, the health check answers.
"""

import hashlib
import hmac
import json
import sys
import time
import urllib.error
import urllib.request
import uuid

import proteosm_drill as drill

LOCAL_UNREACHABLE_HOST = "127.0.0.1"
# Nothing listens there inside the API container: the test connects and fails fast, after the secret was decrypted.
CLOSED_PORT = 1
SMTP_SECRET = "drill-smtp-secret"
LDAP_SECRET = "drill-ldap-secret"
ENTRA_SECRET = "drill-entra-secret"
CUSTOM_SUBJECT_PREFIX = "Drill custom "
# Migration ProteoSMEmailDefaults: a template still equal to the shipped default gets the new name and this footer.
FORMER_PRODUCT_NAME = "Proteone"
PRODUCT_NAME = "ProteoSM"
DEFAULT_TEMPLATE_FOOTER = "\n\nProteoSM by ProteONE"
DRILL_DOMAIN = "example.com"
SIGNED_PARTS_SEPARATOR = "."
HTTP_OK = 200
HTTP_UNAUTHORIZED = 401


def signed_event(client, source_id, secret, tag, tamper=False):
    body = json.dumps({"eventId": f"drill-{tag}-{uuid.uuid4().hex[:8]}", "alertKey": "disk", "state": "firing",
                       "title": f"Drill {tag}", "description": "move drill"}).encode()
    timestamp = str(int(time.time()))
    signature = hmac.new(secret.encode(), (timestamp + SIGNED_PARTS_SEPARATOR).encode() + body, hashlib.sha256).hexdigest()
    if tamper:
        signature = ("1" if signature[0] == "0" else "0") + signature[1:]
    # The header names are the webhook contract (ADR-0041); they keep the former product name on purpose.
    headers = {"X-Proteone-Timestamp": timestamp, "X-Proteone-Signature": signature}
    status, _ = client.raw_post(f"/monitoring/{source_id}/events", body, headers)
    return status


class Anonymous(drill.Client):
    """A caller without cookies or CSRF token, as a monitoring tool calls the webhook."""

    def raw_post(self, path, body, headers):
        request = urllib.request.Request(self.base + path, data=body, method="POST",
                                         headers={"Content-Type": "application/json", **headers})
        try:
            with self.opener.open(request, timeout=drill.HTTP_TIMEOUT_SECONDS) as response:
                return response.status, response.read()
        except urllib.error.HTTPError as error:
            return error.code, error.read()


def default_body_after_move(body):
    """The body the ProteoSMEmailDefaults migration writes for a template still equal to its shipped default."""
    return body.replace(FORMER_PRODUCT_NAME, PRODUCT_NAME) + DEFAULT_TEMPLATE_FOOTER


def test_result(client, path, body=None):
    status, payload = client.request("POST", path, body)
    if status != HTTP_OK:
        return f"HTTP {status}"
    result = json.loads(payload)
    return f"ok={result.get('ok')} code={result.get('errorCode')}"


def extra(base, state_path):
    with open(state_path, encoding="utf-8") as handle:
        state = json.load(handle)
    admin = drill.Client(base)
    admin.sign_in(state["adminEmail"], drill.ADMIN_PASSWORD)
    me = admin.json("GET", "/auth/me")

    templates = admin.json("GET", "/admin/email-templates")
    template = templates[0]
    custom_subject = CUSTOM_SUBJECT_PREFIX + template["eventType"]
    admin.json("PUT", f"/admin/email-templates/{template['eventType']}",
               {"version": template["version"], "subject": custom_subject, "body": template["body"]})

    # Every template as the old stack holds it, to compare after the move.
    snapshot = {t["eventType"]: {"subject": t["subject"], "body": t["body"]}
                for t in admin.json("GET", "/admin/email-templates")}

    mail = admin.json("GET", "/admin/email-settings")
    admin.json("PUT", "/admin/email-settings", {
        "provider": "smtp", "senderName": "Drill", "senderAddress": f"drill@{DRILL_DOMAIN}", "host": LOCAL_UNREACHABLE_HOST,
        "port": CLOSED_PORT, "security": "start_tls", "userName": "drill", "password": SMTP_SECRET, "version": mail["version"]})
    smtp_test = test_result(admin, "/admin/email-settings/test")

    auth = admin.json("GET", "/admin/auth-settings")
    admin.json("PUT", "/admin/auth-settings/ldap", {
        "host": LOCAL_UNREACHABLE_HOST, "port": CLOSED_PORT, "security": "ldaps", "baseDn": "DC=drill,DC=local",
        "bindDn": "CN=svc,DC=drill,DC=local", "bindPassword": LDAP_SECRET, "userFilter": None, "version": auth["version"]})
    auth = admin.json("GET", "/admin/auth-settings")
    admin.json("PUT", "/admin/auth-settings/entra", {
        "tenantId": str(uuid.uuid4()), "clientId": str(uuid.uuid4()), "clientSecret": ENTRA_SECRET,
        "allowedDomains": [DRILL_DOMAIN], "version": auth["version"]})
    ldap_test = test_result(admin, "/admin/auth-settings/test", {"method": "ldap"})
    entra_test = test_result(admin, "/admin/auth-settings/test", {"method": "entra"})

    team = admin.json("GET", "/admin/teams")
    team_id = (team["items"] if isinstance(team, dict) else team)[0]["id"]
    created = admin.json("POST", "/admin/monitoring-sources", {
        "name": f"Drill {uuid.uuid4().hex[:6]}", "teamId": team_id, "requesterId": me["id"], "serviceId": None,
        "categoryId": None, "impact": "low", "urgency": "low", "isActive": True})
    source_id, secret = created["source"]["id"], created["secret"]
    before = signed_event(Anonymous(base), source_id, secret, "before")
    if before != HTTP_OK:
        raise SystemExit(f"monitoring event on the old stack -> {before}")

    state.update({"customTemplate": {"eventType": template["eventType"], "subject": custom_subject},
                  "templates": snapshot,
                  "smtpTest": smtp_test, "ldapTest": ldap_test, "entraTest": entra_test,
                  "monitoringSourceId": source_id, "monitoringSecret": secret})
    with open(state_path, "w", encoding="utf-8") as handle:
        json.dump(state, handle)
    print(f"extra: template {template['eventType']}, smtp {smtp_test}, ldap {ldap_test}, entra {entra_test}, monitoring source {source_id}")


def verify(base, state_path):
    with open(state_path, encoding="utf-8") as handle:
        state = json.load(handle)
    failed = 0

    def record(name, expected, actual):
        nonlocal failed
        ok = expected == actual
        failed += 0 if ok else 1
        print(json.dumps({"check": name, "expected": expected, "actual": actual, "ok": ok}, ensure_ascii=False), flush=True)

    def size_or_status(status, body):
        return len(body) if status == HTTP_OK else f"HTTP {status}"

    status, _ = drill.Client(base).request("GET", "/health")
    record("health", HTTP_OK, status)
    old_session = drill.Client(base, drill.jar_from(state["employeeCookies"]))
    status, _ = old_session.request("GET", "/auth/me")
    record("a session from the Proteone stack ends (cookie renamed): users sign in once more", HTTP_UNAUTHORIZED, status)

    admin = drill.Client(base)
    status, _ = admin.request("POST", "/auth/login", {"email": state["adminEmail"], "password": drill.ADMIN_PASSWORD})
    record("the admin signs in with the same password", HTTP_OK, status)
    employee = drill.Client(base)
    status, _ = employee.request("POST", "/auth/login", {"email": state["employeeEmail"], "password": drill.USER_PASSWORD})
    record("the employee signs in with the same password", HTTP_OK, status)
    status, body = employee.request("GET", state["attachmentPath"])
    record("attachment downloads with the same size", state["attachmentSize"], size_or_status(status, body))
    status, body = admin.request("GET", state["exportPath"])
    record("report export file downloads with the same size", state["exportSize"], size_or_status(status, body))
    status, body = drill.Client(base).request("POST", "/auth/login", {"email": state["goneEmail"], "password": state["gonePassword"]})
    record("deactivated user cannot sign in", f"HTTP {drill.HTTP_UNAUTHORIZED} {drill.INVALID_CREDENTIALS}",
           f"HTTP {status} {drill.problem_code(body)}")

    templates = {t["eventType"]: t for t in admin.json("GET", "/admin/email-templates")}
    before = state["templates"]
    custom_type = state["customTemplate"]["eventType"]
    custom = templates.get(custom_type)
    record("custom email template byte-identical (subject and body)", True,
           custom is not None and custom["subject"] == state["customTemplate"]["subject"]
           and before[custom_type]["subject"] == custom["subject"] and before[custom_type]["body"] == custom["body"])
    defaults = [event for event in before if event != custom_type]
    record("every unchanged default gets the ProteoSM wording and footer (same subject)", len(defaults),
           sum(1 for event in defaults if event in templates
               and templates[event]["subject"] == before[event]["subject"]
               and templates[event]["body"] == default_body_after_move(before[event]["body"])))
    record("no unchanged default still names Proteone", 0,
           sum(1 for event in defaults if event in templates and FORMER_PRODUCT_NAME in templates[event]["body"]))

    record("SMTP password decrypts (same test result as before the move)", state["smtpTest"], test_result(admin, "/admin/email-settings/test"))
    record("LDAP bind password decrypts (same test result)", state["ldapTest"], test_result(admin, "/admin/auth-settings/test", {"method": "ldap"}))
    record("Entra client secret decrypts (same test result)", state["entraTest"], test_result(admin, "/admin/auth-settings/test", {"method": "entra"}))
    tool = Anonymous(base)
    record("monitoring key verifies a signed event", HTTP_OK, signed_event(tool, state["monitoringSourceId"], state["monitoringSecret"], "after"))
    record("monitoring key refuses a wrong signature", HTTP_UNAUTHORIZED,
           signed_event(tool, state["monitoringSourceId"], state["monitoringSecret"], "tampered", tamper=True))
    return 0 if failed == 0 else 1


def fresh(base, admin_email, temporary):
    failed = 0

    def record(name, expected, actual):
        nonlocal failed
        ok = expected == actual
        failed += 0 if ok else 1
        print(json.dumps({"check": name, "expected": expected, "actual": actual, "ok": ok}, ensure_ascii=False), flush=True)

    status, _ = drill.Client(base).request("GET", "/health")
    record("fresh: health", HTTP_OK, status)
    admin = drill.Client(base)
    admin.sign_in(admin_email, temporary, drill.ADMIN_PASSWORD)
    status, _ = admin.request("GET", "/auth/me")
    record("fresh: the first admin signs in", HTTP_OK, status)
    cookies = sorted(c.name for c in admin.jar)
    record("fresh: session cookie is __Host-proteosm", True, "__Host-proteosm" in cookies)
    templates = admin.json("GET", "/admin/email-templates")
    record("fresh: default email templates exist", True, len(templates) > 0)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else ""
    if command == "extra" and len(sys.argv) == 4:
        extra(sys.argv[2], sys.argv[3])
    elif command == "verify" and len(sys.argv) == 4:
        sys.exit(verify(sys.argv[2], sys.argv[3]))
    elif command == "fresh" and len(sys.argv) == 5:
        sys.exit(fresh(*sys.argv[2:5]))
    else:
        print(__doc__, file=sys.stderr)
        sys.exit(2)
