---
key: TS-028
title: TLS foundations and the "before" picture
type: Spike
status: To Do
priority: High
points: 2
assignee: yeasir118
reporter: claude
epic: TS-027
labels: [m1.5, tls, networking, spike]
depends_on: []
created: 2026-11-19
---

# TS-028: TLS foundations and the "before" picture

## Summary
Before building anything, see the problem and the tools. First, capture TinyShop's own traffic as it is today (`phase-1-baseline`) and read order data and SQL straight off the wire. Then take a real TLS connection apart with `openssl`: the certificate chain, the handshake, the negotiated protocol and key exchange. You end with a written threat model, and the capture that later tickets compare against.

## Background
- Epic TS-027, decisions D26 and D38. No code changes: a spike produces knowledge.
- The tools are already in the lab: `nicolaka/netshoot` (used by the reachability matrix) contains `tcpdump`, `tshark` and `openssl`.

## Scope
**In scope**
- A packet capture of plain HTTP, gRPC (h2c) and Postgres traffic between TinyShop's containers, with what's readable in it
- `openssl s_client` against a public website: its chain, protocol, cipher, key exchange, ALPN
- Reading certificates with `openssl x509 -text`: every field that matters
- A threat model for TinyShop in `notes/`

**Out of scope**
- Any change to TinyShop

## Acceptance criteria
- [ ] AC1: A capture from inside `backend-network` (or `order-network`) shows, in plain text, at least: one order's JSON, one SQL statement or its result, and one HTTP header. The commands and the readable lines are in `notes/m1-5.md`.
- [ ] AC2: For one public website, `notes/m1-5.md` records its certificate chain (each certificate's subject, issuer, validity, key type), the negotiated TLS version, cipher suite, key-exchange group and ALPN protocol, with the `openssl` commands.
- [ ] AC3: The same notes explain, in your own words, each step of a TLS 1.3 handshake and what each certificate field you met is for (SAN, key usage, extended key usage, basic constraints, validity, issuer and subject).
- [ ] AC4: A threat model for TinyShop: who could read or change traffic where, what TLS fixes, what it doesn't, and which hops matter most.

## Technical notes
- **Capturing between containers:** start netshoot inside another container's network namespace, as the matrix script does: `docker run --rm -it --network container:<container> nicolaka/netshoot:v0.16`. Then `tcpdump -i any -A port 8080` (`-A` prints the payload as text), or `tshark` for decoded protocols. Generate traffic with the shop or the load generator.
- **Postgres in a capture:** port 5432, from inside a service's container. The password exchange is SCRAM (not the password itself), but queries and results are plain.
- **A public handshake:** `openssl s_client -connect <host>:443 -servername <host> -showcerts -alpn h2,http/1.1`. Look for `Protocol`, `Cipher`, `Negotiated TLS1.3 group` (OpenSSL 3.5 prints the key-exchange group), `ALPN protocol`, and the chain. `openssl x509 -noout -text` reads one certificate.
- **The post-quantum hybrid:** with OpenSSL 3.5 on both sides, the group is often `X25519MLKEM768`. Note which sites negotiate it and which don't.

## Learning objectives
- What "plaintext on the wire" means concretely, in your own system
- Certificates, chains and trust anchors: who vouches for whom
- The TLS 1.3 handshake: key exchange, server authentication, what's encrypted from when
- The difference between encryption, integrity and authentication
- What a threat model is, and why it comes before the solution

## Questions to answer
Write the answers in `notes/m1-5.md`.
1. Docker networks already isolate TinyShop's services. Why isn't that enough? Give a concrete way traffic on a Docker network could be read or changed.
2. What does each of these guarantee: encryption, integrity, server authentication, client authentication? Which does plain TLS give you, and which needs mTLS?
3. Why is a certificate chain needed at all? Why doesn't a website just send a certificate signed directly by a root?
4. What's a "harvest now, decrypt later" attack, and what does X25519MLKEM768 do about it?
5. Which TinyShop hop would you encrypt first if you could only do one, and why?

## Definition of done
- [ ] All acceptance criteria met
- [ ] Questions answered in `notes/`
- [ ] The shared definition of done in [README.md](README.md#definition-of-done-applies-to-every-ticket) is met (review, merge)

## Activity log
- 2026-11-19: Created with epic TS-027, refined (first ticket of the epic)
