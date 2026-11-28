# TS-028 phase 2: public TLS handshakes taken apart

The raw output behind [m1_5.md, phase 2](../../m1_5.md#phase-2-a-real-tls-connection-taken-apart-ac2). Run with the local OpenSSL 3.5.9 (MSYS2 / Git Bash) on 2026-11-21 to 2026-11-23, from this folder.

Three websites: **www.google.com** (the main one), **github.com** (no post-quantum key exchange), **www.amazon.com** (an RSA certificate; its folder is called `aws`).

## Files and the commands that made them

### `<site>/<site>-handshake.txt`: the full handshake output
```bash
echo | openssl s_client -connect www.google.com:443 -servername www.google.com -showcerts -alpn h2,http/1.1 > google/google-handshake.txt 2>&1
```
The same for `github.com` and `www.amazon.com`.
- `echo |`: sends an empty line, so `s_client` closes by itself instead of waiting for input.
- `-servername`: sends SNI, the name the client wants; a server hosting many sites picks its certificate by it.
- `-showcerts`: prints every certificate the server sent, not just the leaf.
- `-alpn h2,http/1.1`: offers these application protocols; the server picks one.
- Google's saved file shows `ALPN protocol: http/1.1`, so that run offered only `http/1.1`. With `h2,http/1.1` offered, Google picks `h2` (see `compare.txt`).

The useful lines, without the noise (`-a`: the output contains a few binary bytes):
```bash
grep -aE "^depth=|^ [0-9] s:|^   i:|^   v:|^Protocol|Cipher is|Negotiated TLS1.3 group|Peer Temp Key|Peer signature type|ALPN protocol|Verify return code" google/google-handshake.txt
```

### `<site>/leaf_cert.crt`, `intermediate_cert_1.crt`, `intermediate_cert_2.crt`: each certificate the server sent
Each `-----BEGIN CERTIFICATE-----` ... `-----END CERTIFICATE-----` block from the handshake file, saved in order: 0 = leaf, 1 = intermediate, 2 = a cross-signed copy of the root. (So the name `intermediate_cert_2` isn't quite right, see the notes.) Copied by hand; an equivalent command:
```bash
awk '/BEGIN CERTIFICATE/{n++; f=(n==1 ? "leaf_cert" : "intermediate_cert_" (n-1)) ".crt"} f{print > ("google/" f)} /END CERTIFICATE/{f=""}' google/google-handshake.txt
```
Public certificates aren't secret, so they're committed (`.gitignore` re-includes `notes/evidence/**/*.crt`). The readable text is in `certs.txt`.

### `<site>/certs.txt`: each certificate's fields, readable (committed)
```bash
for s in google github aws; do
for f in leaf_cert intermediate_cert_1 intermediate_cert_2; do
  echo "##### $s/$f.crt"
  openssl x509 -in $s/$f.crt -noout -subject -issuer -dates -ext subjectAltName,keyUsage,extendedKeyUsage,basicConstraints
  openssl x509 -in $s/$f.crt -noout -text | grep -E "Public Key Algorithm|Public-Key|NIST CURVE|Signature Algorithm" | sort -u
done > $s/certs.txt
done
```
- `x509 -noout`: don't print the certificate itself, only what's asked for.
- `-subject -issuer -dates`: who it's for, who signed it, when it's valid.
- `-ext ...`: only these extensions: SAN, Key Usage, Extended Key Usage, Basic Constraints.
- `-text | grep`: the key type and size, and the signature algorithm.
- Full detail of one certificate: `openssl x509 -in google/leaf_cert.crt -noout -text`.

### `compare.txt`: the three websites side by side
```bash
for h in github.com www.amazon.com www.google.com; do
  echo "=== $h"
  echo | openssl s_client -connect $h:443 -servername $h -alpn h2,http/1.1 2>&1 | grep -aE "^depth=|^Protocol|Cipher is|Negotiated TLS1.3 group|Peer Temp Key|Peer signature type|Server public key|ALPN protocol"
done > compare.txt
```
`Negotiated TLS1.3 group` appears when the group is a hybrid (post-quantum); for a classic group, OpenSSL prints `Peer Temp Key` instead.

### `experiments.txt`: four experiments against Google
| Experiment | Command (each piped from `echo |` where it's `s_client`) | Shows |
|---|---|---|
| A. ALPN | `openssl s_client ... -alpn http/1.1`, then without `-alpn` | the server picks from what the client offers; no offer, no ALPN |
| B. Trust nothing | `openssl s_client ... -no-CAfile -no-CApath -no-CAstore` | without a trusted root, verification fails (error 20) |
| B. The trust store | `openssl version -d`, then `grep -c "BEGIN CERTIFICATE" <dir>/cert.pem` and `grep "GTS Root R4" <dir>/cert.pem` | where OpenSSL's roots are, how many, and that Google's root is among them |
| C. TLS 1.2 | `openssl s_client ... -tls1_2` | the older protocol: a different cipher name, a classic key exchange |
| D. Names | `openssl verify -untrusted google/intermediate_cert_1.crt [-verify_hostname <name>] google/leaf_cert.crt` | the chain verifies; the right name passes, a wrong name fails (error 62) |

- `-untrusted`: intermediates that may be used to build the chain, but aren't trusted by themselves.
- `-verify_hostname`: also check that the certificate's SAN covers this name. Without it, `openssl verify` checks only signatures and dates.

The first run of D pointed at `google-0.pem` / `google-1.pem`, which didn't exist (the files here are named differently). D was rerun with this folder's names, and both runs are in the file.
