---
key: TS-025
title: Remove repetition from compose.yaml with templates
type: Task
status: To Do
priority: Medium
points: 2
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, compose, refactoring, yaml]
depends_on: [TS-024]
created: 2026-10-27
---

# TS-025: Remove repetition from compose.yaml with templates

## Summary
Collapse the near-identical blocks in `compose.yaml` (three Postgres databases, three migration jobs, three socat proxies, the .NET services' health checks) into shared templates, without changing what Compose actually runs.

## Background
- Each slice added in TS-010, TS-021 and TS-023 copied the previous one. The databases differ only in name, password variables, role prefix, privileges, volume and network; the proxies only in target and host port; the migration jobs only in image, Dockerfile, connection string and network.
- Compose already uses top-level `x-` fields with YAML anchors for the connection strings. This ticket extends that to whole blocks.
- Industry practice: Compose files are commonly deduplicated (anchors and merge keys, `extends`, `include`); Dockerfiles usually stay explicit per service, because each one is reviewed, scanned and updated on its own. Dockerfiles are out of scope here for that reason.
- Done after TS-024 (one concern per change): TS-024 also edits the Dockerfiles and Compose is used to verify it.

## Scope
**In scope**
- Templates (`x-` fields with anchors) for: the Postgres database service, the migration job, the debug proxy, the .NET service health check (and the parts of the .NET service definition that are truly identical)
- Each service keeps its own values written out where they differ: `environment` entries, `networks`, ports, names
- Comments: explain the template mechanism once, where the templates are defined

**Out of scope**
- Dockerfiles (stay explicit; see Background)
- Splitting the file with `include`, or `extends` from other files (one file stays the rule, D15)
- Any change to what runs: images, networks, ports, health checks, dependencies

## Acceptance criteria
- [ ] AC1: `docker compose --profile '*' config` produces the same output before and after (the fully expanded model is identical; save both and diff them).
- [ ] AC2: The file is shorter, and every service can still be understood without reading more than its own block and the template it names.
- [ ] AC3: `docker compose up` on empty volumes brings every service up healthy, as before. CI's `compose` job passes.

## Technical notes
- **The merge key `<<: *template` is shallow.** It merges top-level keys only: if a service declares its own `environment:` (or `networks:`, `healthcheck:`), that replaces the template's whole block; the entries aren't combined. This is the most common way templated Compose files lose settings silently, and why AC1 compares the expanded output.
- Mappings inside a template can be shared with their own anchors (for example a `healthcheck` anchor used by every .NET service), which avoids the shallow-merge problem for that block.
- `extends` (within the same file) is an alternative to merge keys and merges `environment` and some other keys more deeply. Compare the two before choosing.
- `docker compose config` is the tool: it shows what Compose really sees after interpolation, anchors and merges.

## Learning objectives
- YAML anchors, aliases and merge keys, and their shallow-merge semantics
- The difference between merge keys and Compose's `extends`
- Why configuration (Compose, Kubernetes manifests) is commonly templated while Dockerfiles usually aren't

## Questions to answer
Write the answers in `notes/`.
1. Give a concrete example from this file where a merge key would silently drop a setting, and how you avoided it.
2. Merge key or `extends`: which did you choose, and why?
3. What do Helm and Kustomize do for Kubernetes manifests that anchors do for Compose, and what can they do that anchors can't?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (CI, docs, review, merge)

## Activity log
- 2026-10-27: Created, from the discussion before TS-024 (Compose repetition; Dockerfiles deliberately left explicit)
