# Tickets

This folder is our lightweight issue tracker. Each ticket is one Markdown file, following the usual Jira conventions.

## Board

| Key | Type | Title | Status | Points | Depends on |
|---|---|---|---|---|---|
| [TS-001](TS-001-epic-m1-naked-system.md) | Epic | M1: Naked TinyShop system | In Progress | – | – |
| [TS-002](TS-002-repository-foundation.md) | Task | Repository foundation | To Do | 1 | – |
| [TS-003](TS-003-system-architecture-document.md) | Task | System architecture document | To Do | 3 | TS-002 |
| [TS-004](TS-004-http-api-contracts.md) | Task | HTTP API contracts | To Do | 3 | TS-003 |
| [TS-005](TS-005-payment-grpc-contract.md) | Task | Payment gRPC contract | To Do | 2 | TS-004 |

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

## Workflow for each ticket

1. **Pick up.** Set `status: In Progress` and add a line to the Activity log.
2. **Branch** from `master`, named after the ticket: `TS-002-repository-foundation`.
3. **Commit** with the key as a prefix: `TS-002: Add root .gitignore`.
4. **Write notes.** Answer the ticket's *Questions to answer* in `notes/`, and record every mistake that cost you more than 15 minutes.
5. **Request review.** Push the branch, set `status: In Review`, and tell Claude the branch name. Opening a pull request on GitHub is optional; the review happens locally with `git diff master...<branch>`.
6. **Address feedback** by pushing new commits on the same branch. Don't force-push during a review.
7. **Merge** once approved, as a squash merge into `master`, so each ticket is one commit on `master`. Set `status: Done`, update the board, and delete the branch.

## Template
New tickets start from [_TEMPLATE.md](_TEMPLATE.md).
