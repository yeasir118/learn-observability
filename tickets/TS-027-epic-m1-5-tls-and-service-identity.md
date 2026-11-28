---
key: TS-027
title: "M1.5: Encryption and identity in transit (TLS, a private PKI, mutual TLS)"
type: Epic
status: In Progress
priority: High
points: null
assignee: yeasir118
reporter: claude
epic: null
labels: [m1.5, tls, pki, mtls, security, networking]
depends_on: [TS-001]
created: 2026-11-19
---

# TS-027: M1.5, encryption and identity in transit

## Summary
Every connection in TinyShop that crosses a network becomes **encrypted** and **authenticated**: the browser to nginx, nginx to the gateway, the gateway to the services, order-service to its dependencies, every service to its database, and k6 to the frontend. Between the services, both sides prove who they are (**mutual TLS**), and each service decides which callers it accepts by their identity, not by their network address.

To get there, you run your own **private certificate authority**: first by hand with OpenSSL, so that every field of a certificate is something you chose, then automated, with short-lived certificates that renew themselves, as the industry does today. The milestone ends with a blind TLS incident and the tag `phase-1.5-tls`.

## Why: what M1 leaves open
M1's security boundary is **the network**: Docker networks decide who can reach whom, and the reachability matrix proves it (TS-014). That's real, but it's only half of what the industry expects:

| M1 today | The gap | What this epic adds |
|---|---|---|
| Every hop is plain HTTP, gRPC without TLS (h2c), or Postgres without TLS | Anything that can see the traffic can read it: order data, queries, results. Anything that can write to it can change it | **Encryption and integrity** on every hop (TLS 1.3) |
| A service trusts any caller that can reach its port | "Can reach the port" is the only check. One compromised container on `backend-network` can call every service on it, and impersonate any of them | **Authentication**: servers prove their identity to clients (TLS), clients to servers (mTLS) |
| The call graph is enforced only by network membership | `backend-network` is a flat trust zone (architecture.md §3): product-service can call user-service, though nothing should | **Authorization by identity**: each service accepts only the callers it should have |
| The browser talks plain HTTP | A shopper on café Wi-Fi sends everything in the clear | **HTTPS at the edge**, the way every public site works |

This is the **zero trust** idea (NIST SP 800-207): don't trust a request because of *where* it comes from (inside the network), trust it because of *who* it provably is. The networks stay: segmentation and identity together are **defence in depth**, and each covers the other's failure.

What TLS does **not** protect against, so nobody expects it to: a compromised service itself (it holds its own key), bugs in the application, data at rest (disks, backups), and anyone with root on the Docker host.

## What "industry standard" means here (researched 2026-11-19)
The design follows where the industry is, and where it's moving:

- **TLS 1.3 is the baseline.** BCP 195 (RFC 9325) requires TLS 1.2 support for the public internet and recommends 1.3; RFC 9852 (2026) goes further: *new* protocols using TLS must require 1.3. Inside TinyShop we own both ends of every connection, so internally it's 1.3 only.
- **Certificates are getting short-lived, and renewal must be automatic.** The CA/Browser Forum (ballot SC-081) cut the maximum lifetime of public certificates to **200 days from March 2026**, 100 days from March 2027 and **47 days from March 2029**. Let's Encrypt is moving to 45 days and offers 6-day certificates. A certificate renewed by hand once a year is now an anti-pattern; ACME (RFC 8555) is the standard automation protocol.
- **Short lifetimes replace revocation.** Revocation checking never worked well; Let's Encrypt stopped OCSP in 2025. A certificate that expires in hours doesn't need to be revoked.
- **Mutual TLS needs a private CA.** Browser root programs (Chrome's policy, followed by the big public CAs in 2026-2027) remove client authentication from public TLS certificates. Public certificates are for websites; workload identity comes from your own PKI.
- **Workload identity has a standard: SPIFFE** (CNCF). A service's identity is a URI in its certificate, `spiffe://<trust-domain>/<workload>`, which services authorize on. Service meshes (Istio, Linkerd) and SPIRE issue these automatically; we do it by hand first, to see what they automate.
- **A two-tier PKI:** an offline root that signs only intermediates, and an issuing intermediate that signs everything else, constrained to the names it may issue for (RFC 5280 name constraints).
- **Private names have a reserved home:** ICANN reserved `.internal` for private use in July 2024, the DNS counterpart of the private IP ranges.
- **Post-quantum key exchange is already on by default.** OpenSSL 3.5 (in all three of our base images: nginx, Postgres, .NET on Alpine all ship 3.5.9) and Go 1.24+ (k6 uses Go 1.27) negotiate the hybrid **X25519MLKEM768** by default: "harvest now, decrypt later" protection, which you'll see in your own handshakes without configuring anything.
- **Configuration guides:** Mozilla's Server Side TLS guidelines ("intermediate" and "modern" profiles) for the edge, NIST SP 800-52 Rev. 2 for TLS in general, the OWASP Transport Layer Security Cheat Sheet.

## Decisions
Numbered on from M1's decisions (D1-D25, epic TS-001). Final unless a ticket reopens one.

| # | Decision | Why |
|---|---|---|
| D26 | **Every connection that crosses a network is encrypted, authenticates the server, and, between services, authenticates the client.** The Docker networks and the reachability matrix stay as they are. | Zero trust and defence in depth: identity on top of segmentation, not instead of it. |
| D27 | **Learn by hand, then automate.** The PKI is built with plain OpenSSL first (TS-029), with every certificate field written out; automation (step-ca and ACME, short-lived certificates) comes last (TS-035). | Automation tools hide exactly the parts worth understanding. Once you've built it by hand, the tool is a convenience, not magic. |
| D28 | **A two-tier private PKI.** An offline **root** (ECDSA P-384, 10 years, its key encrypted, used only to sign intermediates) and an **issuing intermediate** (ECDSA P-384, `pathlen:0`, name-constrained). Leaf certificates use ECDSA P-256 and live 30 days by hand, then 24 hours once automated. | The industry pattern: the root key is used almost never, so it can stay offline; a compromised intermediate can be replaced without touching the root that everyone trusts. ECDSA: smaller and faster than RSA at equal strength, and supported everywhere we need it. |
| D29 | **Names live under `tinyshop.internal`.** Each service is addressed as `<service>.tinyshop.internal` (Compose network aliases), and that's the name in its certificate. The intermediate may issue only for `tinyshop.internal`, `localhost`, `127.0.0.1` and `::1`. | A reserved private TLD (ICANN, 2024). Name constraints make a leaked intermediate useless for anything else: important because the root goes into your Windows trust store (TS-030). |
| D30 | **Trust is explicit, narrow, and never switched off.** Every internal client trusts **only** the TinyShop root, not the operating system's public roots, and always checks the hostname. No "accept any certificate", anywhere, not even in development: no validation callback returning `true`, no `sslmode=require` without verification, no `proxy_ssl_verify off`. | "Verification off" is the most common TLS bug in real code: encrypted, but to whoever answers. Trusting only our root means no public CA can vouch for an internal name. |
| D31 | **Protocols:** TLS **1.3 only** between our own components; at the edge, Mozilla's **intermediate** profile (TLS 1.2 and 1.3). Key exchange and cipher suites: the libraries' defaults, which already include the post-quantum hybrid. | We own both ends inside, so we can require the best; the edge shows the compromise every public site makes for older clients. Overriding good defaults is how configurations rot. |
| D32 | **Private keys:** never in git (`.gitignore` already excludes `*.key`, `*.pem`); each container gets **only its own** key, read-only, readable only by its runtime user; CI creates a throwaway PKI for each run; the root key stays on your machine, encrypted. From TS-035, keys are generated inside the container that uses them and never leave it. | Least privilege for secrets. A key that never moves can't leak in transit. |
| D33 | **Ports:** services listen for TLS on **8443**; their plaintext 8080 is retired, so no plaintext listener is left on any shared network. The edge serves **HTTPS** and keeps an HTTP port only to redirect to HTTPS (exact host ports decided in TS-030). | A plaintext port that "nobody uses" is a downgrade path. |
| D34 | **Service identity is a SPIFFE ID** in each workload certificate, `spiffe://tinyshop.internal/<service>`, next to its DNS name. Each server keeps an **allow-list of callers**, mirroring the call graph (payment-service accepts only order-service; product- and user-service accept the gateway and order-service; ...). | The CNCF standard for workload identity, used by Istio, Linkerd and SPIRE. It turns the "reachable but unused" cells of the reachability matrix (◐) into refused calls. |
| D35 | **Health checks are not a back door.** Docker's health checks keep working without weakening mutual TLS; the mechanism (most likely a management listener on `127.0.0.1` inside the container) is decided in TS-034. | A health endpoint that skips client authentication on the service port would be a hole in the allow-list. |
| D36 | **Postgres requires TLS** for every network connection (`hostssl` only; plaintext is rejected in `pg_hba.conf`), and clients verify the server fully (`VerifyFull`). The application roles also authenticate with a client certificate, on top of their password (TS-034). | Database traffic carries the most sensitive data. The password proves *what* is connecting; the certificate proves *which workload*. |
| D37 | **Short lifetimes instead of revocation;** every component reloads a renewed certificate without downtime, or by a rolling restart, documented per component. | The industry's direction (OCSP retired, 47-day public certificates). Revocation is explained and practised once as a concept, not built as infrastructure. |
| D38 | **Proof, like the reachability matrix:** a **TLS matrix** for every hop, with positive cases (a verified TLS 1.3 handshake, the expected identity) and negative ones (plaintext refused; an untrusted CA, a wrong name, an expired certificate refused; a missing or wrong client identity refused). A packet capture before and after. Every check runs in CI. | A security property that isn't tested regularly decays without anyone noticing. |
| D39 | **One document:** `tinyshop/docs/tls.md`: the threat model, the PKI, a certificate inventory, who trusts what, and runbooks (renew, rotate, respond to a leaked key, install and remove the root on Windows). | The knowledge has to survive the tickets. |

## Child tickets
| Step | Key | Type | Title | Points | Depends on |
|---|---|---|---|---|---|
| 1 | TS-028 | Spike | TLS foundations and the "before" picture | 2 | – |
| 2 | TS-029 | Task | A private PKI by hand (OpenSSL) | 5 | TS-028 |
| 3 | TS-030 | Story | HTTPS at the edge | 5 | TS-029 |
| 4 | TS-031 | Story | TLS inside: nginx, the gateway and the HTTP services | 5 | TS-030 |
| 4 | TS-032 | Story | gRPC over TLS: order-service to payment-service | 3 | TS-031 |
| 5 | TS-033 | Story | TLS to Postgres | 5 | TS-031 |
| 6 | TS-034 | Story | Mutual TLS and service identity | 8 | TS-032, TS-033 |
| 7 | TS-035 | Story | Automated certificate lifecycle: step-ca, ACME, short-lived certificates | 8 | TS-034 |
| 8 | TS-036 | Spike | Failure Exercise 1.5: a TLS incident (blind), and the `phase-1.5-tls` tag | 3 | TS-035 |
| (any) | TS-037 | Spike | HTTP/1.0 to HTTP/3 and gRPC, seen in TinyShop (optional, added 2026-11-28) | 3 | TS-030, TS-032 |

47 points (44 without the optional TS-037). TS-029 onwards carry `needs-refinement` and are refined just before they're picked up, as in M1. As in M1, each ticket adds its own checks to CI in the same pull request (the CI policy).

## How the concepts build on each other
```text
TS-028  why encrypt; what a certificate, a chain and a handshake are     (watch: openssl, tcpdump)
TS-029  be a CA: keys, CSRs, extensions, chains, name constraints        (build: openssl)
TS-030  server authentication, the way the web does it                   (browser, nginx, k6)
TS-031  server authentication between your own services                  (Kestrel, HttpClient, nginx as a client)
TS-032  the same for gRPC: TLS and ALPN instead of h2c                   (gRPC)
TS-033  the same for a database protocol                                 (Postgres, Npgsql, psql)
TS-034  client authentication and authorization: identity, not location  (mTLS, SPIFFE IDs)
TS-035  operations: issuance, renewal, rotation, without a human        (step-ca, ACME)
TS-036  when it breaks: diagnosing TLS failures                          (blind incident)
```

## Acceptance criteria
- [ ] Every connection that crosses a Docker network or the host boundary uses TLS, with the server verified against the TinyShop root; internally TLS 1.3 only.
- [ ] The browser shows `https://` with a valid certificate; plain HTTP only redirects.
- [ ] Every service-to-service call and every database connection authenticates both sides; each service accepts only the callers on its allow-list.
- [ ] No component accepts plaintext on a shared network, and no client anywhere disables certificate verification.
- [ ] The TLS matrix (positive and negative cases, every hop) passes in CI, and the reachability matrix still does.
- [ ] Certificates are short-lived and renew automatically, without downtime.
- [ ] No private key is in git; CI uses a throwaway PKI.
- [ ] `tinyshop/docs/tls.md` describes the threat model, the PKI, the certificate inventory and the runbooks.
- [ ] Failure Exercise 1.5 is written up.
- [ ] `master` is tagged `phase-1.5-tls`.

## Open questions (decided in the tickets named)
- **Host development** (`dotnet run`, architecture.md §8): the same PKI with `localhost` certificates, or plaintext on the host only? (TS-031)
- **Edge ports on Windows:** 443/80, or 8443/8080 if something on your machine already uses 443? (TS-030)
- **HSTS for `localhost`:** an HSTS header on `localhost` can affect every other local development server in that browser. Send it only for a `tinyshop.internal` name, or with a short `max-age`? (TS-030)
- **nginx terminates TLS and opens a new TLS connection to the gateway** (termination and re-encryption), rather than passing the encrypted stream through: the standard for a proxy that routes by path. Confirmed in TS-031.
- **Integration tests** (Testcontainers Postgres): with TLS, like production, or plaintext for speed? (TS-033)

## Out of scope (parked)
- **A service mesh** (Linkerd, Istio): automatic mTLS, compared with this hand-made version: on the roadmap's list, deliberately after this epic.
- **SPIRE** (workload attestation: proving a container is what it claims *before* it gets a certificate), HSMs and cloud KMS for the root key.
- **Public CAs and Let's Encrypt:** they need a public domain; ACME against our own CA (TS-035) teaches the same protocol.
- **Encryption at rest** (disks, Postgres data files, backups).
- **User authentication and tokens** (OAuth 2.0, OIDC): a roadmap expansion idea, which needs this epic first.
- **Kubernetes cert-manager:** the Kubernetes equivalent of TS-035.

## References
- RFC 8446 (TLS 1.3), RFC 5280 (X.509 certificates and name constraints), RFC 8555 (ACME), RFC 6797 (HSTS), RFC 7301 (ALPN)
- RFC 9325 / BCP 195 (recommendations for TLS), RFC 9852 (new protocols must require TLS 1.3)
- NIST SP 800-52 Rev. 2 (TLS guidelines), NIST SP 800-207 (zero trust architecture), NIST SP 800-57 (key management)
- CA/Browser Forum ballot SC-081 (shrinking certificate lifetimes); the Chrome Root Program policy (client authentication leaving public TLS certificates)
- SPIFFE specification (X.509-SVID); Mozilla Server Side TLS guidelines and its configuration generator; OWASP Transport Layer Security Cheat Sheet
- OpenSSL 3.5 release notes (default hybrid post-quantum key exchange)

## Activity log
- 2026-11-19: Created after M1 closed. Designed from current industry practice (researched the same day: SC-081 lifetimes, the end of OCSP at Let's Encrypt, client authentication leaving public certificates, SPIFFE, `.internal`, RFC 9325/9852, OpenSSL 3.5's post-quantum default; checked locally: OpenSSL 3.5.9 in the nginx, Postgres and .NET images, Go 1.27 in k6, BusyBox `wget` with `ssl_client`). Decisions D26-D39; child tickets TS-028 to TS-036
- 2026-11-20: Started with TS-028 (branch `TS-028-before-tls-situation`)
- 2026-11-28: TS-028 done: the "before" capture, three public handshakes, the TLS 1.3 handshake and certificate fields, and the threat model (`notes/m1_5.md`, evidence in `notes/evidence/`). Optional spike TS-037 (HTTP versions) added at the assignee's request. Next: TS-029
