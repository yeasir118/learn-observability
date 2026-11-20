---
key: TS-029
title: A private PKI by hand (OpenSSL)
type: Task
status: To Do
priority: High
points: 5
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, pki, tls, needs-refinement]
depends_on: [TS-028]
created: 2026-11-19
---

# TS-029: A private PKI by hand (OpenSSL)

## Summary
Become a certificate authority: create TinyShop's offline root and its name-constrained issuing intermediate with plain OpenSSL, and a script that issues leaf certificates for every service, with each extension written out and explained. CI gets the same PKI, freshly generated for each run.

## Background
- Epic TS-027, decisions D27 (by hand first), D28 (two tiers, ECDSA, lifetimes), D29 (`tinyshop.internal`, name constraints), D32 (keys).

## Scope
**In scope**
- `tinyshop/infrastructure/pki/`: OpenSSL configuration files (one per certificate profile) and scripts; generated output in a git-ignored folder
- The root: ECDSA P-384, 10 years, CA, key encrypted with a passphrase
- The intermediate: ECDSA P-384, `pathlen:0`, name constraints (`tinyshop.internal`, `localhost`, `127.0.0.1`, `::1`)
- Leaf issuance from a key and a CSR (certificate signing request), with SANs, key usage and extended key usage per profile (server; server and client)
- A certificate inventory: which certificate exists for which component, with which names
- CI: the PKI generated in the `system` job (no secrets stored anywhere)
- `tinyshop/docs/tls.md` started: the PKI section

**Out of scope**
- Using the certificates anywhere (TS-030 onwards); automation (TS-035)

## Acceptance criteria (draft)
- [ ] AC1: One command creates the root and the intermediate; another issues a leaf for a given component and names.
- [ ] AC2: `openssl verify` accepts every leaf against the root through the intermediate; a leaf for a name outside the constraints (e.g. `example.com`) is rejected by verification.
- [ ] AC3: Every extension in every profile is explained in `tls.md`.
- [ ] AC4: No key or certificate under `pki/` is tracked by git; CI generates its own.

## Learning objectives
- What a CA actually does: sign a public key and names, under rules
- Keys, CSRs, certificates; basic constraints, path length, key usage, EKU, SAN, name constraints
- Why the root stays offline and what the intermediate is for

## Questions to answer (draft)
1. What would an attacker who stole the intermediate's key be able to do? And the root's? How do name constraints change the answer?
2. Why does a leaf's key never need to be sent to the CA?
3. What does `pathlen:0` prevent?

## Activity log
- 2026-11-19: Created with epic TS-027
