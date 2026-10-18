---
key: TS-006
title: CI pipeline foundation (GitHub Actions)
type: Task
status: In Progress
priority: Highest
points: 3
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, ci, github-actions, protobuf]
depends_on: [TS-005]
created: 2026-10-18
---

# TS-006: CI pipeline foundation (GitHub Actions)

## Summary
Create the repository's first CI workflow. It checks the protobuf contract (format, lint, breaking changes) on every pull request and push to `master`, and `master` is protected so that nothing merges unless CI passes.

## Background
- **CI starts as soon as there's something to check.** The proto from TS-005 is the first such thing. Starting now, while the pipeline is tiny, is much easier than retrofitting CI onto a finished system.
- **From this ticket on, CI grows with the code.** Every ticket that adds something buildable, testable or lintable also adds it to CI (see the [CI policy](README.md#continuous-integration-policy)). This ticket builds the skeleton those later tickets extend.
- You already run the buf checks locally in Docker ([contracts.md §9.5](../tinyshop/docs/contracts.md#95-tooling)). CI runs the same checks on GitHub's machines.

## Scope
**In scope**
- `.github/workflows/ci.yml` with one job that runs the buf checks
- Dependabot configuration that keeps the GitHub Actions versions up to date
- Branch protection for `master` that requires the CI check to pass

**Out of scope**
- Building or testing .NET code (no .NET code exists yet; TS-008 and TS-009 add those jobs)
- Docker image builds (TS-010)
- Deployment

## Acceptance criteria
- [ ] AC1: `.github/workflows/ci.yml` runs on pull requests targeting `master`, on pushes to `master`, and on manual trigger (`workflow_dispatch`).
- [ ] AC2: A job named `proto` checks `tinyshop/proto` with buf: format check, lint, and breaking-change detection against `master`. On pushes to `master` itself, it compares against the previous commit, or skips the breaking check.
- [ ] AC3: The workflow grants itself only the permissions it needs (`permissions: contents: read` at the top level).
- [ ] AC4: When a newer commit is pushed to the same branch, the previous, now outdated, run is cancelled (`concurrency` with `cancel-in-progress`).
- [ ] AC5: Every third-party action is pinned to a version. Record in your notes whether you pinned by tag or by full commit SHA, and why.
- [ ] AC6: The workflow never tries to log in or push to the Buf Schema Registry.
- [ ] AC7: `.github/dependabot.yml` checks the `github-actions` ecosystem weekly.
- [ ] AC8: `master` is protected with a GitHub ruleset or branch protection rule: changes must come through a pull request, the `proto` check must pass, and force pushes and deletion are blocked.
- [ ] AC9: **Proof, part 1:** a throwaway pull request that renumbers a field in `payment.proto` shows a failing `proto` check, and GitHub blocks the merge. Close the pull request without merging. Put a screenshot or link in your notes.
- [ ] AC10: **Proof, part 2:** the pull request for this ticket shows a green `proto` check.

## Technical notes
- Buf publishes an official action, `bufbuild/buf-action`. It can run format, lint and breaking checks, and works out the base branch of a pull request by itself. Read its inputs carefully. In particular, find out how to point it at a subdirectory, and how to make sure it never pushes to the BSR. Running the `bufbuild/buf` Docker image in a plain `run:` step, as you do locally, is also acceptable.
- **Pinning:** a tag such as `@v4` can be moved by the action's owner to point at different code. A full commit SHA can't. Pinning by SHA, with Dependabot proposing updates, is the safest practice in the industry, but either is acceptable here as long as you can explain the trade-off.
- **Required checks and path filters don't mix.** If a workflow only runs when certain files change (`paths:`), a pull request that doesn't touch those files never reports the required check, and GitHub waits for it forever. While the pipeline is small, run every job on every pull request.
- **Required checks are matched by name.** The check that appears in the ruleset is the job's name (`proto`). Renaming a job later silently breaks the rule.
- Allow only merge commits (no squash, no rebase) under the repository's Settings → General → Pull Requests, and turn on "Automatically delete head branches".

## Learning objectives
- The structure of a GitHub Actions workflow: triggers, jobs, steps, runners, permissions, concurrency.
- Why CI is introduced early and grows ticket by ticket.
- Supply-chain hygiene for CI: least-privilege tokens, pinned actions, automated updates.
- How branch protection turns "CI exists" into "CI is enforced".

## Questions to answer
1. What's the difference between the `pull_request` and `push` triggers? Which commit does each one test?
2. What can a workflow do with the default `GITHUB_TOKEN` if you don't restrict `permissions`, and why does that matter?
3. A required check is configured, but a pull request only changes a Markdown file and the workflow has a `paths:` filter for `tinyshop/proto/**`. What happens to the pull request?
4. Why is pinning an action to a tag weaker than pinning it to a commit SHA?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-18: Created
- 2026-10-18: In progress. Workflow and Dependabot config written; merge strategy changed to merge commits (decision D20)
