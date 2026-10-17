---
key: TS-002
title: Repository foundation
type: Task
status: To Do
priority: Highest
points: 1
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, repo, git]
depends_on: []
created: 2026-10-18
---

# TS-002: Repository foundation

## Summary
Set up the repository layout and the git hygiene files that every later ticket relies on.

## Background
The repo currently contains `README.md`, `resources/outline.md`, `tickets/` and an empty `tinyshop/` folder. Git doesn't track empty folders, so `tinyshop/` isn't in the repo yet. You're on Windows, but every service will run in Linux containers, so line endings matter.

## Scope
**In scope**
- Root `.gitignore`
- Root `.gitattributes`
- Folder skeleton for the app
- `notes/` folder for the learning journal
- Root `README.md` explaining the repo layout

**Out of scope**
- Any .NET solution, project or code (that comes in step 1)
- Any Docker or Compose files

## Acceptance criteria
- [ ] AC1: The root `.gitignore` ignores .NET build output (`bin/`, `obj/`), `node_modules/`, IDE and OS clutter, and `.env` files. It must **not** ignore `.env.example`.
- [ ] AC2: The root `.gitattributes` makes shell scripts (`*.sh`) always use LF line endings, and sets a sensible default for other text files.
- [ ] AC3: The repo has this layout, using `.gitkeep` files where a folder would otherwise be empty:
  ```text
  /
  ├── README.md
  ├── resources/outline.md
  ├── tickets/
  ├── notes/
  │   └── m1.md
  └── tinyshop/
      ├── docs/
      ├── proto/
      └── src/
  ```
- [ ] AC4: The root `README.md` says what the repo is, explains each top-level folder in one line, and links to `tickets/README.md`.
- [ ] AC5: `notes/m1.md` exists with a heading per ticket. Your answers go there.
- [ ] AC6: `git check-ignore -v` shows that `.env` is ignored and `.env.example` is not. Paste the output into your notes.

## Technical notes
- The .NET SDK can generate a standard `.gitignore` for you, but read what it generates before you commit it. You should be able to explain every section you keep.
- `.gitattributes` decides how files are stored in the repo and how they're checked out. `core.autocrlf` is a per-machine setting, while `.gitattributes` travels with the repo. Find out which one wins.
- Make the `.env` / `.env.example` split now: real values stay on your machine, and a template with the variable names is committed.

## Learning objectives
- How git ignore rules and negation (`!`) work.
- Why line endings break Linux containers built on Windows.
- Why secrets and environment-specific config stay out of git.

## Questions to answer
1. What happens if a `*.sh` file with CRLF line endings is used as a container's entrypoint? What error do you get, and why is it so confusing?
2. What is the difference between `core.autocrlf` and `.gitattributes`, and which takes precedence?
3. If you accidentally commit a `.env` file containing a real secret and then delete it in the next commit, is the secret safe? Why or why not?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] Reviewed and approved
- [ ] Squash-merged into `master`, branch deleted, board updated

## Activity log
- 2026-10-18: Created
