# Tickets

This folder is our lightweight issue tracker. Each ticket is one Markdown file, following the usual Jira conventions.

## Board

| Key | Type | Title | Status | Points | Depends on |
|---|---|---|---|---|---|
| [TS-001](TS-001-epic-m1-naked-system.md) | Epic | M1: Naked TinyShop system | In Progress | – | – |
| [TS-002](TS-002-repository-foundation.md) | Task | Repository foundation | Done | 1 | – |
| [TS-003](TS-003-system-architecture-document.md) | Task | System architecture document | Done | 3 | TS-002 |
| [TS-004](TS-004-http-api-contracts.md) | Task | HTTP API contracts | Done | 3 | TS-003 |
| [TS-005](TS-005-payment-grpc-contract.md) | Task | Payment gRPC contract | Done | 2 | TS-004 |
| [TS-006](TS-006-ci-pipeline-foundation.md) | Task | CI pipeline foundation (GitHub Actions) | In Progress | 3 | TS-005 |
| [TS-007](TS-007-product-database-in-compose.md) | Task | Product database and debug proxy in Compose | In Progress | 2 | TS-006 |
| [TS-008](TS-008-product-service-on-host.md) | Story | Product service on the host with EF Core | To Do | 5 | TS-007 |
| [TS-009](TS-009-product-service-integration-tests.md) | Task | Product service integration tests | To Do | 3 | TS-008 |
| [TS-010](TS-010-containerize-product-service.md) | Story | Containerize the product service | To Do | 5 | TS-009 |
| [TS-011](TS-011-user-service.md) | Story | User service | To Do | 5 | TS-010 |
| [TS-012](TS-012-payment-service.md) | Story | Payment service (gRPC) | To Do | 5 | TS-010 |
| [TS-013](TS-013-order-service.md) | Story | Order service and the order flow | To Do | 8 | TS-011, TS-012 |
| [TS-014](TS-014-gateway-and-network-segmentation.md) | Story | Gateway and network segmentation | To Do | 5 | TS-013 |
| [TS-015](TS-015-frontend-container.md) | Task | Frontend container and nginx reverse proxy | To Do | 3 | TS-014 |
| [TS-016](TS-016-fault-injection-switches.md) | Story | Fault injection switches | To Do | 3 | TS-015 |
| [TS-017](TS-017-load-generator.md) | Task | Load generator (k6) | To Do | 2 | TS-015 |
| [TS-018](TS-018-end-to-end-smoke-test-and-baseline.md) | Task | End-to-end smoke test in CI and `phase-1-baseline` tag | To Do | 3 | TS-016, TS-017 |
| [TS-019](TS-019-failure-exercise-1.md) | Spike | Failure Exercise 1: application failure without observability | To Do | 2 | TS-018 |

Keep this table in sync with each ticket's front matter.

## Conventions

### Keys and file names
- The key is `TS-<number>` (TS = TinyShop), numbered in sequence. Epics share the same sequence.
- The file name is `<KEY>-<kebab-case-title>.md`, for example `TS-002-repository-foundation.md`.
- A key is never reused, even if its ticket is cancelled.

### Issue types
| Type | Use for |
|---|---|
| Epic | A milestone made up of several tickets (M1, M1.5, M2, …) |
| Story | Something a user can see or do ("shopper can view products") |
| Task | Technical or documentation work with no user-visible behaviour |
| Spike | A time-boxed investigation that produces knowledge, not production code |
| Bug | Behaviour that differs from what a ticket or contract specifies |

### Status flow
```
To Do → In Progress → In Review → Done
                ↑           │
                └───────────┘  (changes requested)
```
`Blocked` and `Cancelled` can happen at any point. Write the reason in the ticket's Activity log.

### Priority
`Highest`, `High`, `Medium`, `Low`.

### Story points
Points measure relative effort and uncertainty, not hours, on a Fibonacci scale: 1, 2, 3, 5, 8. Anything bigger than 8 should be split.

### Refinement (definition of ready)
Tickets that are further out are written with less certainty and carry the label `needs-refinement`. Before you pick one up, we refine it together: check its assumptions against what earlier tickets actually built, adjust the acceptance criteria and points, then remove the label. A ticket is **ready** when it has no `needs-refinement` label and its dependencies are Done.

## Continuous integration policy

CI exists from the first ticket that produces something checkable (TS-006), and **grows with the code rather than being designed up front**:

- **Every ticket that adds something buildable, testable or lintable also adds it to CI in the same pull request.** A new service brings its build and tests into the workflow; a new Dockerfile brings an image build; and so on. A ticket isn't done if its new code isn't checked by CI.
- **`master` is protected.** Changes arrive only through pull requests, and a pull request can only be merged when the required CI checks pass.
- **Keep CI fast and deterministic.** If a job becomes slow or flaky, fixing it is a ticket of its own, not something to work around.
- **Deployment (CD) is out of scope.** TinyShop has no deployment target, so CI ends at build, test and verify.

## Definition of done (applies to every ticket)
In addition to each ticket's own acceptance criteria:
- [ ] CI covers everything new in this ticket that can be built, tested or linted, and CI is green
- [ ] Documentation affected by the change (`tinyshop/docs/`, READMEs, `.env.example`) is updated
- [ ] Reviewed and approved
- [ ] Merged into `master` with a merge commit, branch deleted, board and ticket status updated

## Workflow for each ticket

1. **Pick up.** Set `status: In Progress` and add a line to the Activity log.
2. **Branch** from `master`, named after the ticket: `TS-002-repository-foundation`.
3. **Commit** with the key as a prefix: `TS-002: Add root .gitignore`. Every commit ends up on `master`, so make each one a meaningful step with a message saying what changed and why. Before opening the pull request, tidy up "fix typo" or "wip" commits by folding them into the commit they belong to.
4. **Write notes.** Answer the ticket's *Questions to answer* in `notes/`, and record every mistake that cost you more than 15 minutes.
5. **Request review.** Push the branch, open a pull request on GitHub titled `TS-xxx: <ticket title>`, set `status: In Review`, and tell Claude the branch name. The review happens locally with `git diff master...<branch>`; CI runs on the pull request.
6. **Address feedback** by pushing new commits on the same branch. Don't force-push during a review.
7. **Merge** once approved and CI is green, with a **merge commit**, so every commit on the branch is kept and the merge commit marks where the ticket landed on `master`. Set `status: Done`, update the board, and delete the branch (locally and on GitHub).

## Template
New tickets start from [_TEMPLATE.md](_TEMPLATE.md).
