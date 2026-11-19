---
key: TS-019
title: "Failure Exercise 1: application failure without observability"
type: Spike
status: Done
priority: High
points: 2
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, exercise, observability]
depends_on: [TS-018]
created: 2026-10-18
---

# TS-019: Failure Exercise 1, application failure without observability

## Summary
Switch on product-service's error fault, generate traffic, and try to investigate the failures using **only** what the M1 system offers: plain console logs through `docker compose logs`, the browser, `curl` and k6's output. Write down what you could and couldn't find out, how sure you were, and what was missing. That record is the baseline every later phase is measured against: M2 (structured logs), M3 (metrics) and the traces come back to the same questions.

## Background
- Outline §12. This exercise is "an important part of the assignment".
- The point isn't to succeed. It's to find out exactly where investigation becomes hard or impossible without observability tooling.
- `phase-1-baseline` (TS-018) marks the system this exercise runs against.

## Rules
- **Allowed:** `docker compose logs`, `docker compose ps`, the browser (including its developer tools), `curl`, k6's output and report; the documents a real on-call engineer would have: `contracts.md` and `architecture.md`.
- **Not allowed during the investigation:** a debugger, reading source code, adding logging, and `faults.md` (it describes exactly what each fault does, which would answer the questions for you).
- **A time box:** 45 minutes for the investigation (phase 3). Where you are when it runs out is a result too.

## Decisions (made in refinement, 2026-11-18)
| # | Decision | Why |
|---|---|---|
| 1 | **The fault:** `PRODUCT_SERVICE_FAULT_ERROR_RATE=0.3` with `PRODUCT_SERVICE_FAULT_PRODUCT_ID=1`. | The settings faults.md lists for this exercise. Limited to one product, only some requests fail and the shop page keeps working: harder and more realistic than "everything is broken". |
| 2 | **It isn't blind, and the write-up says so.** You set the fault, and product-service logs "Fault injection is ON" at startup. In the write-up, separate **what the system showed you** from **what you know because you set it**. | A truly blind exercise would need faults hidden from the logs, which TS-016 rejected on purpose (decision 8). The honest split keeps the exercise's value: could you have found it from the evidence alone? |
| 3 | **Predictions first** (phase 1): before switching the fault on, write down what you expect to see and how you'd look for it, and answer TS-004's question 3 for the first time (it was never answered). | Comparing the prediction with what really happened is where the learning is; afterwards, hindsight makes everything look obvious. |
| 4 | **A normal picture first:** the same traffic with every fault off, and a look at its logs and k6 summary, before the incident. | You can only see what's abnormal if you know what normal looks like. Real on-call engineers have that from experience; here it takes ten minutes. |
| 5 | **Traffic:** the load generator for 2 minutes (`LOAD_GENERATOR_DURATION=2m`, default rate), plus a few orders by hand in the browser, for product 1 and for others. | k6 gives a steady stream and its own view of the failures; the browser shows what a user sees. |
| 6 | **Evidence is copied, not summarised:** every claim in the write-up comes with the command and the relevant lines of its output. | So the review can tell "I saw it" from "I concluded it", and so M2/M3 can compare like with like. |
| 7 | **Claude doesn't help during the investigation** (phases 2-4); Claude prepares, reviews and discusses afterwards, then closes the epic. | Help during the investigation would hide exactly the gaps the exercise is meant to find. |

## Phases
1. **Preparation (assignee; brief from Claude):** `notes/exercise-1.md` started; TS-004 question 3 answered; predictions written; the normal picture (faults off, traffic, logs and k6 summary looked at and noted).
2. **The incident (assignee):** the fault on (decision 1), product-service recreated, traffic (decision 5). Start the clock when the traffic starts.
3. **The investigation (assignee, 45 minutes):** the five questions, with evidence (decision 6), confidence and what was missing.
4. **Write-up (assignee):** the time taken; TS-004 question 3 revisited; predictions compared with what happened; the fault switched off again.
5. **Review (Claude with the assignee):** discussion of the write-up; then the closing bookkeeping: TS-019 Done, epic TS-001 closed, the roadmap's M1 marked done.

## Acceptance criteria
- [x] AC1: `notes/exercise-1.md` answers the outline's questions with evidence (commands and output):
  - Which service failed?
  - Why did the request fail?
  - Which request caused the failure?
  - Did the gateway fail, or did the product service?
  - How would you investigate this if the system ran on another machine with no debugger attached?
- [x] AC2: For each question, rate how confident you are in the answer (certain, guessed, or impossible), and say what information was missing.
- [x] AC3: Note how long the investigation took.
- [x] AC4: Answer TS-004 question 3 ("From a 502 alone, can the client tell which service failed? Should it be able to?") before the exercise, and revisit it after.
- [x] AC5: The predictions (phase 1) are compared with what actually happened.
- [x] AC6: Every fault is off again afterwards.

## Learning objectives
- What "investigating a failure" means in practice, and how far plain console logs get you
- The difference between evidence and inference, and how to rate your confidence
- Why a normal baseline matters before an incident
- What information is missing, concretely: the list that motivates M2 and M3

## Definition of done
- [x] All acceptance criteria met
- [x] Reviewed and discussed with Claude
- [x] Epic TS-001 closed

## Activity log
- 2026-10-18: Created
- 2026-11-18: Refined. Rules (allowed documents, the time box), decisions 1-7, phases, AC4 rewritten (TS-004 question 3 was never answered: answered before, revisited after), AC5 and AC6 added; `needs-refinement` removed. The assignee does the exercise; Claude prepares and reviews
- 2026-11-18: Started (branch `TS-019-failure-exercise-1`)
- 2026-11-19: Phase 1: `notes/exercise-1.md` started; TS-004 question 3 answered by the assignee, then checked against contracts.md §2 (the `code` tells where and what kind, not which service); the normal picture observed by the assignee and completed by Claude from a separate run (`ts019`, `.env.example`, every fault off): every container logs every request, nginx one line each, no shared request ID in any log. The predictions are left to the assignee: written by Claude, they'd carry the answers
- 2026-11-19: Predictions written by the assignee (tidied, not corrected). Phase 2 changed at the assignee's request, replacing decisions 1, 2 and 5: **Claude ran the incident blind**, so the assignee doesn't know which fault is on. Settings chosen by Claude within the exercise's theme (an application failure), deliberately not decision 1's, and recorded here only after the review. Separate Compose project `ts019-incident` (`.env.example` plus the settings in the shell, so the assignee's `.env` reveals nothing), traffic from 11:26:06 UTC: the load generator for 2 minutes at 10 per second, plus 10 hand-placed orders through the published port. Evidence in `~/exercise-1-evidence/` (outside the repository); the stack left running for live investigation
- 2026-11-19: Phases 3-4 (assignee): the failure followed from k6's summary and the shopper's 502 through nginx, the gateway and order-service to product-service's `InjectedFaultException`, with `grep` on timestamp, status and container. Phase 5, review (Claude): the write-up structured into the five questions with copied evidence and confidence; findings added: the failure followed was one of k6's (11:26:14), not the shopper's (nginx 11:26:47.223, recorded by the shopper as 11:26:46, when k6 had another failure at 11:26:46.009; only the client IP separates them); two seconds held two failures each; all 29 failures were `GET /products/4` (29 of 63 calls); the 502's `traceId` appears in no log; `instance` leaks the internal path `/orders`. **The settings, revealed:** `PRODUCT_SERVICE_FAULT_ERROR_RATE=0.4`, `PRODUCT_SERVICE_FAULT_PRODUCT_ID=4` (both found by the assignee in the logs). contracts.md: §2.1's example and `instance` corrected, §8 gains two gaps (internal paths in error bodies, the unusable `traceId`). `ts019-incident` removed with its volumes; the assignee's `.env` sets no fault: AC6 met. AC1, AC2, AC4, AC5 met; AC3 waits for the assignee's time
- 2026-11-19: The investigation took about 40 minutes (assignee): AC3 met. Epic TS-001 closed. Done
