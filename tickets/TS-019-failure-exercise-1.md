---
key: TS-019
title: "Failure Exercise 1: application failure without observability"
type: Spike
status: To Do
priority: High
points: 2
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, exercise, observability, needs-refinement]
depends_on: [TS-018]
created: 2026-10-18
---

# TS-019: Failure Exercise 1, application failure without observability

## Summary
Switch on product-service's error fault, generate traffic, and try to investigate the failures using **only** what the M1 system offers: plain console logs through `docker compose logs`. Write down what you could and couldn't find out. That record is the baseline every later phase is measured against.

## Background
- Outline §12. This exercise is "an important part of the assignment".
- The point isn't to succeed. It's to find out exactly where investigation becomes hard or impossible without observability tooling.

## Rules
- No debugger, no reading source code during the investigation, no new logging added.
- Allowed: `docker compose logs`, `docker compose ps`, the browser, `curl`, and k6 output.

## Acceptance criteria
- [ ] AC1: `notes/exercise-1.md` answers the outline's questions with evidence (commands and output):
  - Which service failed?
  - Why did the request fail?
  - Which request caused the failure?
  - Did the gateway fail, or did the product service?
  - How would you investigate this if the system ran on another machine with no debugger attached?
- [ ] AC2: For each question, rate how confident you are in the answer (certain, guessed, or impossible), and say what information was missing.
- [ ] AC3: Note how long the investigation took.
- [ ] AC4: Revisit your answer to TS-004 question 3 ("From a 502 alone, can the client tell which service failed?") in the light of this exercise.

## Definition of done
- [ ] All acceptance criteria met
- [ ] Reviewed and discussed with Claude
- [ ] Epic TS-001 closed

## Activity log
- 2026-10-18: Created
