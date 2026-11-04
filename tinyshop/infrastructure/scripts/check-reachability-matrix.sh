#!/usr/bin/env bash
# Proves the reachability matrix of architecture.md §4 against the running stack (TS-014, AC7).
#
# The expected matrix is read from architecture.md itself, not copied here, so the document and the
# test can't drift apart. Every cell is tested, then the matrix that was actually observed is printed
# in the document's own Markdown format and compared line by line. Any difference fails the script:
# either compose.yaml or the document is wrong, and whichever it is gets fixed.
#
# How each cell is tested (TS-014, decision 11):
#   - From a container: a nicolaka/netshoot container started with --network container:<row>, so it
#     shares that container's network namespace: its interfaces, routes and DNS, exactly. Each target
#     is tried twice, and only "both reachable" or "both blocked" is a valid result:
#       by name  Docker's DNS must resolve it, and a TCP connection to that address must open.
#       by IP    a TCP connection to any of the target's addresses (one per network it's on) must
#                open. This is the stronger test of ✗: a name that doesn't resolve only shows that
#                DNS hides the target; an IP that can't be reached shows there's no route to it.
#     ✓ and ◐ both mean "a connection opens" (◐ is reachable but unused); the script can't tell
#     them apart, so an open connection keeps whichever of the two the document says.
#   - From the browser (host): only through published ports, as a browser on another machine would
#     (TS-014, question 3). On a Linux host, the host can also reach container IPs on non-internal
#     networks directly; that route isn't what the matrix's browser row means, so it isn't tested.
#
# Ports: 80 for the frontend, 5432 for the databases, 8080 for everything else. A TCP connection is
# enough: reachability is a network question, not a protocol one.
#
# Until TS-015 adds the real frontend, a stand-in container plays it: on edge-network only, with the
# DNS alias "frontend", listening on 80 and publishing FRONTEND_HOST_PORT like the real one will. It
# is removed when the script ends. Once a "frontend" service runs in the project, that is used instead.
#
# Requires the default stack, running and healthy, without the debug profile:
#   cd tinyshop/infrastructure/compose && docker compose up -d --wait
#   tinyshop/infrastructure/scripts/check-reachability-matrix.sh
# Environment:
#   COMPOSE_PROJECT_NAME  the Compose project to test (default: tinyshop)
#   FRONTEND_HOST_PORT    the stand-in frontend's published port (default: 8000, as architecture.md §2)
set -euo pipefail

# Exact version: a small third-party image (architecture.md §2, image pinning).
readonly NETSHOOT_IMAGE="nicolaka/netshoot:v0.16"
readonly CONNECT_TIMEOUT_SECONDS=3

project="${COMPOSE_PROJECT_NAME:-tinyshop}"
frontend_host_port="${FRONTEND_HOST_PORT:-8000}"
tinyshop_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
document="$tinyshop_dir/docs/architecture.md"
standin="$project-frontend-standin"

# Git Bash on Windows rewrites arguments that look like Unix paths ("/tmp/..." inside the probe
# script below) into Windows paths before Docker sees them. No effect on Linux.
export MSYS_NO_PATHCONV=1

# --- The expected matrix, from the document ----------------------------------------------------

# Every table line between the "## 4. Reachability matrix" heading and the next heading of any level.
mapfile -t expected_table < <(awk '
  /^## 4\. Reachability matrix/ { found = 1; next }
  found && /^#/                 { exit }
  found && /^\|/                { print }
' "$document")
if (( ${#expected_table[@]} < 3 )); then
  echo "::error::No reachability matrix found in ${document#"$tinyshop_dir"/} (§4)."
  exit 1
fi

# split_row <line>: the cells of a Markdown table row, trimmed, one per line.
split_row() {
  local line="$1" cell
  local -a cells
  line="${line#|}"
  line="${line%|}"
  IFS='|' read -ra cells <<< "$line"
  for cell in "${cells[@]}"; do
    cell="${cell#"${cell%%[![:space:]]*}"}"
    cell="${cell%"${cell##*[![:space:]]}"}"
    printf '%s\n' "$cell"
  done
}

# Line 0 is the header ("From \ To", then one column per target), line 1 the |---| separator.
mapfile -t header < <(split_row "${expected_table[0]}")
targets=("${header[@]:1}")

port_of() {
  case "$1" in
    frontend) echo 80 ;;
    *-db) echo 5432 ;;
    *) echo 8080 ;;
  esac
}

# --- The containers ----------------------------------------------------------------------------

# Found by Compose's labels, so the script needs neither the Compose file nor a working directory.
container_of() {
  docker ps --quiet \
    --filter "label=com.docker.compose.project=$project" \
    --filter "label=com.docker.compose.service=$1" | head -n 1
}

# The matrix describes the default stack. A debug proxy adds routes into the database networks.
running_services="$(docker ps --filter "label=com.docker.compose.project=$project" \
  --format '{{.Label "com.docker.compose.service"}}')"
if grep -q -- '-db-proxy$' <<< "$running_services"; then
  echo "::error::Debug proxies are running in project '$project'. The matrix describes the default stack: stop them first."
  exit 1
fi

cleanup() {
  docker rm --force "$standin" > /dev/null 2>&1 || true
}
trap cleanup EXIT

declare -A container
for target in "${targets[@]}"; do
  container[$target]="$(container_of "$target")"
done

if [[ -z "${container[frontend]:-}" ]]; then
  edge_network="$(docker network ls --quiet \
    --filter "label=com.docker.compose.project=$project" \
    --filter "label=com.docker.compose.network=edge-network")"
  if [[ -z "$edge_network" ]]; then
    echo "::error::Project '$project' has no edge-network. Start the stack first: docker compose up -d --wait"
    exit 1
  fi
  echo "No frontend service in '$project' yet (TS-015): starting a stand-in on edge-network, published on :$frontend_host_port."
  cleanup
  # socat: accept every connection on 80 and answer one line. Enough for a TCP check.
  container[frontend]="$(docker run --detach --rm --name "$standin" \
    --network "$edge_network" --network-alias frontend \
    --publish "$frontend_host_port:80" \
    "$NETSHOOT_IMAGE" socat TCP-LISTEN:80,fork,reuseaddr SYSTEM:'echo frontend stand-in')"
fi

missing=()
for target in "${targets[@]}"; do
  [[ -z "${container[$target]}" ]] && missing+=("$target")
done
if (( ${#missing[@]} > 0 )); then
  echo "::error::Not running in project '$project': ${missing[*]}. Start the stack first: docker compose up -d --wait"
  exit 1
fi

# Every address of a container, one per network it's on, comma-separated. sort -u: a container
# started with "docker run --network" can list the same network twice.
addresses_of() {
  docker inspect --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' "$1" \
    | grep -v '^$' | sort -u | paste -sd, -
}

# The first host port a container publishes (for <container port>, or any), or nothing.
published_port_of() {
  local port_spec="${2:-}" mapping
  # shellcheck disable=SC2086 # empty port_spec must vanish, not become an empty argument
  mapping="$(docker port "$1" $port_spec | head -n 1)"
  [[ -n "$mapping" ]] && echo "${mapping##*:}"
  return 0
}

# --- Probing -----------------------------------------------------------------------------------

# Runs inside netshoot, in the row container's network namespace. Arguments: the timeout, then one
# "name:port:ip1,ip2" per target. Prints "name<TAB>by-name<TAB>by-IP" per target, each result
# "open:<address>" or "closed:<what was tried>". Targets are probed in parallel: a blocked
# connection only fails after the timeout.
probe_script="$(cat << 'EOF'
timeout_seconds=$1
shift

probe() {
  name=$1 port=$2 addresses=$3

  # dig asks Docker's DNS (127.0.0.11, from the row container's resolv.conf). A name Docker doesn't
  # know is passed on to the host's resolver on a non-internal network, which can take seconds to
  # answer "no such name": one try of 2 seconds, and no answer counts as unresolved.
  resolved=$(dig +short +time=2 +tries=1 "$name" A 2> /dev/null | grep -E '^[0-9]+(\.[0-9]+){3}$' | head -n 1)
  if [ -z "$resolved" ]; then
    by_name="closed:no-dns-answer"
  elif nc -z -w "$timeout_seconds" "$resolved" "$port" 2> /dev/null; then
    by_name="open:$resolved"
  else
    by_name="closed:$resolved"
  fi

  by_ip="closed:$addresses"
  for address in $(echo "$addresses" | tr ',' ' '); do
    if nc -z -w "$timeout_seconds" "$address" "$port" 2> /dev/null; then
      by_ip="open:$address"
      break
    fi
  done

  printf '%s\t%s\t%s\n' "$name" "$by_name" "$by_ip"
}

i=0
for target in "$@"; do
  IFS=: read -r name port addresses << END
$target
END
  probe "$name" "$port" "$addresses" > "/tmp/probe.$i" &
  i=$((i + 1))
done
wait
cat /tmp/probe.*
EOF
)"

# render <expected cell> <by-name> <by-IP>: the cell as observed, in the document's notation.
render() {
  local expected="${1%% *}" by_name="$2" by_ip="$3"
  if [[ "$by_name" == open:* && "$by_ip" == open:* ]]; then
    if [[ "$expected" == "✓" || "$expected" == "◐" ]]; then echo "$expected"; else echo "◐"; fi
  elif [[ "$by_name" == closed:* && "$by_ip" == closed:* ]]; then
    echo "✗"
  else
    echo "?"   # name and IP disagree: always a finding
  fi
}

declare -a observed_table=("${expected_table[0]}" "${expected_table[1]}")
declare -a findings=()

for line in "${expected_table[@]:2}"; do
  mapfile -t cells < <(split_row "$line")
  row="${cells[0]//\*/}"
  expected_cells=("${cells[@]:1}")
  observed_cells=()
  echo
  echo "From $row:"

  if [[ "$row" == "browser (host)" ]]; then
    # The host reaches a container only through a port it publishes.
    for i in "${!targets[@]}"; do
      target="${targets[$i]}" expected="${expected_cells[$i]}"
      if [[ "$target" == frontend ]]; then
        port="$(published_port_of "${container[frontend]}" 80/tcp)"
        if [[ -n "$port" ]] && timeout "$CONNECT_TIMEOUT_SECONDS" bash -c "exec 3<> /dev/tcp/127.0.0.1/$port" 2> /dev/null; then
          observed="✓ (via :$port)" evidence="published :$port, connection opened"
        else
          observed="✗" evidence="published: ${port:-nothing}, no connection"
        fi
      else
        port="$(published_port_of "${container[$target]}")"
        if [[ -n "$port" ]]; then
          observed="✓ (via :$port)" evidence="publishes :$port"
        else
          observed="✗" evidence="publishes no port"
        fi
      fi
      observed_cells+=("$observed")
      printf '  %-16s expected %s, observed %s (%s)\n' "$target" "${expected%% *}" "${observed%% *}" "$evidence"
      [[ "$observed" != "$expected" ]] && findings+=("browser -> $target: expected '$expected', observed '$observed' ($evidence)")
    done
  else
    from="${container[$row]:-}"
    if [[ -z "$from" ]]; then
      echo "::error::Row '$row' isn't a container of project '$project'."
      exit 1
    fi
    probes=()
    for i in "${!targets[@]}"; do
      target="${targets[$i]}"
      [[ "${expected_cells[$i]}" == "–" ]] && continue
      probes+=("$target:$(port_of "$target"):$(addresses_of "${container[$target]}")")
    done

    declare -A by_name=() by_ip=()
    while IFS=$'\t' read -r name name_result ip_result; do
      by_name[$name]="$name_result"
      by_ip[$name]="$ip_result"
    done < <(docker run --rm --network "container:$from" "$NETSHOOT_IMAGE" \
               sh -c "$probe_script" probe "$CONNECT_TIMEOUT_SECONDS" "${probes[@]}")

    for i in "${!targets[@]}"; do
      target="${targets[$i]}" expected="${expected_cells[$i]}"
      if [[ "$expected" == "–" ]]; then
        observed_cells+=("–")
        continue
      fi
      observed="$(render "$expected" "${by_name[$target]:-closed:no-result}" "${by_ip[$target]:-closed:no-result}")"
      observed_cells+=("$observed")
      evidence="by name ${by_name[$target]:-?}, by IP ${by_ip[$target]:-?}"
      printf '  %-16s expected %s, observed %s (%s)\n' "$target" "$expected" "$observed" "$evidence"
      [[ "$observed" != "$expected" ]] && findings+=("$row -> $target: expected $expected, observed $observed ($evidence)")
    done
    unset by_name by_ip
  fi

  rendered="| **$row** |"
  for observed in "${observed_cells[@]}"; do
    rendered+=" $observed |"
  done
  observed_table+=("$rendered")
done

# --- Result ------------------------------------------------------------------------------------

echo
echo "Observed matrix (? = reachable by name or by IP, but not both):"
echo
printf '%s\n' "${observed_table[@]}"
echo

if diff <(printf '%s\n' "${expected_table[@]}") <(printf '%s\n' "${observed_table[@]}") > /dev/null; then
  echo "OK: every cell matches architecture.md §4 (project '$project')."
  exit 0
fi

echo "Differences from architecture.md §4 (< document, > observed):"
diff <(printf '%s\n' "${expected_table[@]}") <(printf '%s\n' "${observed_table[@]}") || true
for finding in "${findings[@]}"; do
  echo "::error::$finding"
done
echo "${#findings[@]} cell(s) differ. Fix compose.yaml or architecture.md, whichever is wrong."
exit 1
