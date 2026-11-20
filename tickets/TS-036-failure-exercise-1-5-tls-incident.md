---
key: TS-036
title: "Failure Exercise 1.5: a TLS incident (blind), and the phase-1.5-tls tag"
type: Spike
status: To Do
priority: High
points: 3
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, exercise, tls, needs-refinement]
depends_on: [TS-035]
created: 2026-11-19
---

# TS-036: Failure Exercise 1.5: a TLS incident (blind), and the phase-1.5-tls tag

## Summary
As in TS-019, Claude breaks the system blind: a TLS failure you don't know in advance (for example an expired or not-yet-valid certificate, a wrong name, a missing intermediate in the chain, a client identity not on an allow-list, or a clock out of step). You investigate with logs, `openssl s_client`, `curl -v` and the TLS matrix, and write down what you found and how. Then the epic's criteria are checked and `master` is tagged `phase-1.5-tls`.

## Background
- Epic TS-027. The format of TS-019 (blind, evidence handed over, settings revealed after the review), which worked well.
- TLS failures are notoriously hard to read: the client often sees only "connection reset" or "handshake failure", and the reason is on the other side.

## Scope
**In scope**
- The blind incident, the investigation and its write-up (`notes/exercise-1-5.md`)
- The epic's acceptance criteria checked; the tag

## Acceptance criteria (draft)
- [ ] AC1: The write-up names the failing hop, the cause and the evidence, with confidence ratings.
- [ ] AC2: Every epic criterion is checked.
- [ ] AC3: `master` is tagged `phase-1.5-tls` (annotated) and the tag is pushed.

## Learning objectives
- Reading TLS failures from both ends
- Which TLS problems the system makes visible today, and which it hides (input for M2)

## Activity log
- 2026-11-19: Created with epic TS-027
