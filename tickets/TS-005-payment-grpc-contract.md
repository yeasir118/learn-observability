---
key: TS-005
title: Payment gRPC contract
type: Task
status: Done
priority: High
points: 2
assignee: yeasir118
reporter: claude
epic: TS-001
labels: [m1, grpc, protobuf, api-design]
depends_on: [TS-004]
created: 2026-10-18
---

# TS-005: Payment gRPC contract

## Summary
Write `tinyshop/proto/tinyshop/payment/v1/payment.proto`, which defines the `PaymentService.ProcessPayment` RPC, and document its error semantics in `tinyshop/docs/contracts.md`. Check that the proto is valid using a containerized tool, without installing anything on the host.

## Background
- Outline §7: the request carries `order_id`, `user_id` and `amount`, and the response carries `success` and `transaction_id`.
- How errors are split (decided):
  - **Business outcome** (payment declined): gRPC status `OK`, with `success = false` and a `failure_reason` in the response message.
  - **Technical failure:** a non-OK gRPC status, and no response message is returned.

    | gRPC status | When | Order returns (from TS-004) |
    |---|---|---|
    | `INTERNAL` | Payment service bug or crash | 502 |
    | `UNAVAILABLE` | Payment service can't be reached | 502 |
    | `DEADLINE_EXCEEDED` | Call took too long | 504 |
- gRPC calls have no deadline by default. M1 doesn't set one (decision D5). Record this in TS-004's defaults section.

## Scope
**In scope**
- `payment.proto` with the service, request and response messages
- A gRPC section in `contracts.md`: the RPC, its fields, and the status-code mapping
- Linting the proto with a containerized tool

**Out of scope**
- Generating C# code, or any server or client implementation (that's step 4)
- Server reflection setup (step 4)

## Acceptance criteria
- [ ] AC1: `payment.proto` uses `proto3` syntax, declares a versioned package (for example `tinyshop.payment.v1`), and sets `option csharp_namespace`.
- [ ] AC2: It defines `service PaymentService` with `rpc ProcessPayment(ProcessPaymentRequest) returns (ProcessPaymentResponse)`.
- [ ] AC3: The request has the order ID, user ID and amount. The response has `success`, `transaction_id` and `failure_reason`. Field types match your decisions in TS-004 (ID type, money).
- [ ] AC4: Field names and numbering follow the protobuf style guide (`snake_case` fields, `PascalCase` messages, and field numbers starting at 1).
- [ ] AC5: Money is represented without floating point. Document your choice and the reason in a comment in the proto.
- [ ] AC6: The proto lints cleanly with `buf lint`, run through the `bufbuild/buf` Docker image with the repo mounted as a volume. Paste the command you used and its output into your notes.
- [ ] AC7: `contracts.md` has a gRPC section with the RPC, its fields, and the status-code mapping table, and it's consistent with TS-004's dependency-failure translation table.

## Technical notes
- Protobuf has no decimal type. The common options are integer minor units (cents), the `google.type.Money` message (units + nanos), or a string. Each has trade-offs, so pick one and justify it.
- Running `buf` in Docker means mounting a host folder into the container and setting the working directory. Watch out for how Windows paths behave when mounted, depending on whether you use PowerShell or Git Bash.
- `buf lint` may want a `buf.yaml` config file. Keep it minimal, and decide whether it belongs in `tinyshop/proto/`.

## Learning objectives
- How Protocol Buffers work as a contract language, and why field numbers matter more than field names.
- How gRPC status codes differ from HTTP status codes, and how a business outcome differs from a technical error.
- Using Docker to run tools without installing them.

## Questions to answer
1. On the wire, protobuf identifies a field by its number, not its name. What breaks if you renumber an existing field? What's safe to change?
2. An old client gets a response containing a field it doesn't know about. What happens?
3. Why not report a payment decline as a gRPC error such as `FAILED_PRECONDITION`? What would you gain or lose?
4. What does a proto3 `bool` field look like when it isn't set, and why does that matter for `success`?
5. What did you have to do to make the volume mount work in your shell on Windows?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/m1.md`
- [ ] Reviewed and approved
- [ ] Merged into `master`, branch deleted, board updated

## Activity log
- 2026-10-18: Created
- 2026-10-18: Proto path changed from `tinyshop/proto/payment.proto` to `tinyshop/proto/tinyshop/payment/v1/payment.proto`, so the directories mirror the package as buf's `STANDARD` lint rules require (`PACKAGE_DIRECTORY_MATCH`)
- 2026-10-18: Done, merged into master
