---
key: TS-037
title: "Spike: HTTP/1.0 to HTTP/3 and gRPC, seen in TinyShop"
type: Spike
status: To Do
priority: Low
points: 3
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, http, http2, http3, grpc, networking, spike, needs-refinement]
depends_on: [TS-030, TS-032]
created: 2026-11-28
---

# TS-037: Spike: HTTP/1.0 to HTTP/3 and gRPC, seen in TinyShop

## Summary
See how HTTP evolved by capturing each version in TinyShop itself:
- HTTP/1.0: a new connection for each request
- HTTP/1.1: keep-alive, and head-of-line blocking
- HTTP/2: binary frames, multiplexed streams, HPACK header compression
- gRPC: HTTP/2, plus protobuf, plus trailers
- optionally HTTP/3: QUIC over UDP, with TLS built into the transport

## Background
- Asked for during TS-028. `notes/evidence/TS-028-before-tls/before.pcap` already holds HTTP/1.1 and h2c side by side, and phase 1 met HPACK and the gRPC framing.
- Browsers speak HTTP/2 only over TLS, and the version is chosen by ALPN during the handshake. So this spike comes after the edge (TS-030) and payment-service (TS-032) speak TLS.
- Decryption for the captures: an `SSLKEYLOGFILE` from the client (curl, the browser) lets Wireshark decrypt our own TLS traffic.

## Scope (draft)
**In scope**
- The same request in HTTP/1.0, 1.1 and 2 against the gateway or frontend (`curl --http1.0`, `--http1.1`, `--http2`), captured and compared: connections, round trips, bytes
- HTTP/2 framing: streams, `SETTINGS`, `HEADERS`, `DATA`, HPACK's dynamic table; head-of-line blocking at the TCP level
- gRPC on the wire: the 5-byte message prefix, `grpc-status` in trailers, deadlines (`grpc-timeout`)
- Optional: HTTP/3 at nginx (QUIC, UDP 443, `Alt-Svc`), and what changes for TLS

**Out of scope**
- Changing TinyShop's protocols beyond what's needed to demonstrate them

## Acceptance criteria (draft)
- [ ] AC1: Notes compare HTTP/1.0, 1.1 and 2 for the same request, using captures.
- [ ] AC2: One gRPC call is explained frame by frame, trailers included.

## Learning objectives
- Why each HTTP version exists: the problem it solved, and the one it left
- How ALPN, TLS and HTTP/2 depend on each other

## Activity log
- 2026-11-28: Created while closing TS-028, at the assignee's request; parked until TS-030 and TS-032 are done
