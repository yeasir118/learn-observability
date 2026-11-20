---
key: TS-035
title: "Automated certificate lifecycle: step-ca, ACME, short-lived certificates"
type: Story
status: To Do
priority: Medium
points: 8
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, pki, acme, automation, operations, needs-refinement]
depends_on: [TS-034]
created: 2026-11-19
---

# TS-035: Automated certificate lifecycle: step-ca, ACME, short-lived certificates

## Summary
Replace the hand-issued 30-day certificates with what the industry runs: an online issuing CA (smallstep's `step-ca`, its intermediate signed by our offline root), certificates that live for hours, issued and renewed automatically, with each private key generated inside the container that uses it. Every component picks up its renewed certificate without downtime.

## Background
- Epic TS-027, decisions D27 (automate second), D28 (24-hour leaves), D32 (keys born in the container), D37 (short lifetimes instead of revocation).
- Industry context: public certificates go to 200 days (2026), 100 (2027) and 47 (2029); Let's Encrypt offers 6-day certificates; renewal by hand doesn't scale to that.

## Scope
**In scope**
- `step-ca` in Compose, on its own network, with an intermediate signed by the TS-029 root (the root key stays offline)
- Issuance and renewal: ACME (RFC 8555) where it fits, step-ca's own provisioners where ACME doesn't (internal names and client certificates; decided in refinement)
- Per component: how the certificate is fetched at start and renewed (an init step and a renewal process), and how it's reloaded: nginx (`reload`), Kestrel (a certificate selector or a file watch), Postgres (`pg_reload_conf()`); measured: no failed request during a renewal under load
- Expiry visible: each certificate's remaining lifetime is easy to read (a preparation for metrics and alerts in M3)
- The runbooks in `tls.md`: renewal, rotating the intermediate, responding to a leaked key

## Acceptance criteria (draft)
- [ ] AC1: Every certificate in the running stack comes from step-ca, lives at most 24 hours, and renews automatically.
- [ ] AC2: The smoke test passes across a forced renewal of every certificate, with no failed request.
- [ ] AC3: No private key leaves the container that generated it.

## Learning objectives
- ACME: accounts, orders, challenges, and why automation replaced manual issuance
- Certificate rotation without downtime
- Why short lifetimes make revocation unnecessary

## Activity log
- 2026-11-19: Created with epic TS-027
