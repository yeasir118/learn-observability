# TS-028 phase 1: the "before" capture

The raw capture behind [m1_5.md, phase 1](../../m1_5.md#phase-1-the-before-capture-ac1): order-service's traffic before TLS, 2026-11-20, 80 packets in 20 seconds, one complete order (id 1008).

## `before.pcap`: how it was captured
```bash
MSYS_NO_PATHCONV=1 docker run --rm -it -m 2g \
  --network container:tinyshop-order-service-1 \
  -v "C:/Users/YeasirKhandaker/tls-captures:/captures" \
  nicolaka/netshoot:v0.16 \
  tcpdump -i any -w /captures/before.pcap 'port 8080 or port 5432'
```
- `--network container:...`: join order-service's network namespace, so its traffic is visible (on a bridge network a container sees only its own traffic).
- `-m 2g`: a memory limit for the tool container.
- `-i any`: every interface, including the loopback, where Docker's health check arrives.
- `-w`: write raw packets to a file instead of printing them.
- `'port 8080 or port 5432'`: application traffic and Postgres only.
- Stopped with **Ctrl+C**, so tcpdump writes out what it still holds and prints the count.

It was captured into a folder outside the repository, then moved here. Next time, mount this folder directly.

## How to read it
From the repository's root, start a shell with this folder mounted (no `--network` needed):
```bash
MSYS_NO_PATHCONV=1 docker run --rm -it -m 2g -v "$(pwd -W)/notes/evidence/TS-028-before-tls:/captures:ro" -w /captures nicolaka/netshoot:v0.16
```
Inside it:
```bash
tshark -r before.pcap -q -z conv,tcp                         # who talked to whom
tshark -r before.pcap -Y "http or pgsql"                     # one filter expression (two -Y options don't combine)
tshark -r before.pcap -Y http -V                             # every detail of each HTTP packet
tshark -r before.pcap -Y pgsql.query -T fields -e pgsql.query   # the SQL text
tshark -r before.pcap -Y 'pgsql.type == "Bind"' -T fields -e pgsql.val.data   # the SQL parameter values, in hex
tshark -r before.pcap -d tcp.port==8080,http2 -Y grpc -V     # "decode as": port 8080 also carries HTTP/2 (gRPC)
termshark -r before.pcap                                     # interactive; keyboard only (a mouse click makes it leak memory)
echo 4b6579626f617264 | xxd -r -p                            # hex -> text ("Keyboard")
```
