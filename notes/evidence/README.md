# Evidence

Raw material behind the notes: packet captures, test reports, logs, and anything else a ticket or exercise produces and that's worth looking at again later. The notes explain it; this folder keeps the original.

## Layout
One folder per round, named after the ticket that produced it: `<ticket>-<short-name>/`, for example `TS-028-before-tls/`. Within a round, file names say what they are (`before.pcap`, `after-gateway-order.pcap`, ...).

## Rounds
| Folder | Ticket | Contents |
|---|---|---|
| [TS-028-before-tls/](TS-028-before-tls/) | TS-028 | `before.pcap`: order-service's traffic before TLS (HTTP, gRPC over h2c, Postgres), one order (id 1008). Read in [m1_5.md](../m1_5.md) |

## Reading a capture
From the repository's root (Git Bash):
```bash
MSYS_NO_PATHCONV=1 docker run --rm -it -m 2g \
  -v "$(pwd -W)/notes/evidence:/evidence:ro" \
  nicolaka/netshoot:v0.16 \
  tshark -r /evidence/TS-028-before-tls/before.pcap
```
Or open the file in Wireshark directly.

## A word of caution
This is a lab with test data, so captures are committed. In a real system, a capture holds customers' data and secrets (cookies, tokens, personal data), and is kept out of the repository and deleted when it's no longer needed.
