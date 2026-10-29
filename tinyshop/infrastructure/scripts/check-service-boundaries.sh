#!/usr/bin/env bash
# Fails if any .NET project references a project it isn't allowed to.
#
# Services must depend on each other only through their contracts (HTTP, gRPC, proto files),
# never through code. A ProjectReference from one service into another compiles fine, but
# quietly turns separate services into a distributed monolith, so CI rejects it.
#
# The rules (decision D25; tinyshop/shared/README.md):
#   services/<service>/...  may reference  services/<service>/...  and  shared/...
#   shared/...              may reference  shared/...  only (shared code can't depend on any service)
#   shared/TinyShop.ServiceDefaults       only from a service's *.Api project
#   shared/TinyShop.ServiceDefaults.Data  only from a service's *.Infrastructure project
#
# Usage (from anywhere): tinyshop/infrastructure/scripts/check-service-boundaries.sh
set -euo pipefail

tinyshop_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
services_dir="$tinyshop_dir/services"
shared_dir="$tinyshop_dir/shared"
violations=0

# check_references <project> <owner description> <allowed folder>...
# Reports every ProjectReference of <project> that points outside all of the allowed folders.
check_references() {
  local project="$1" owner="$2"
  shift 2
  local project_dir reference target allowed ok
  project_dir="$(dirname "$project")"

  # Each ProjectReference Include="..." path, with Windows separators turned into "/".
  while IFS= read -r reference; do
    [[ -z "$reference" ]] && continue
    target="$(realpath -m "$project_dir/${reference//\\//}")"

    ok=false
    for allowed in "$@"; do
      [[ "$target" == "$allowed/"* ]] && ok=true
    done

    if [[ "$ok" == false ]]; then
      echo "::error file=${project#"$tinyshop_dir"/}::$owner references a project it may not: ${target#"$tinyshop_dir"/}"
      violations=$((violations + 1))
    fi
  done < <(grep -o '<ProjectReference Include="[^"]*"' "$project" | sed 's/.*Include="\(.*\)"/\1/' || true)
}

# check_shared_layer <project> <shared project name> <required project-name suffix>
# Reports a reference to shared/<shared project name> from a service project whose name doesn't end
# in <suffix>: each shared project belongs to one layer (tinyshop/shared/README.md).
check_shared_layer() {
  local project="$1" shared_project="$2" suffix="$3"
  local name
  name="$(basename "$project" .csproj)"
  # [\\/]: the path separator, "\" (as Visual Studio writes it) or "/".
  if grep -q "shared[\\/]${shared_project}[\\/]" "$project" && [[ "$name" != *"$suffix" ]]; then
    echo "::error file=${project#"$tinyshop_dir"/}::$name may not reference $shared_project (only *$suffix projects may)"
    violations=$((violations + 1))
  fi
}

# Service projects: their own service folder, or shared/.
while IFS= read -r project; do
  # The service a project belongs to is the first folder below services/.
  relative="${project#"$services_dir"/}"
  service="${relative%%/*}"
  check_references "$project" "'$service'" "$services_dir/$service" "$shared_dir"

  # And each shared project only from its layer: the web plumbing from Api, the database plumbing
  # from Infrastructure. TinyShop.ServiceDefaults.Abstractions has no dependencies, so any layer may.
  check_shared_layer "$project" "TinyShop.ServiceDefaults" ".Api"
  check_shared_layer "$project" "TinyShop.ServiceDefaults.Data" ".Infrastructure"
done < <(find "$services_dir" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sort)

# Shared projects: shared/ only.
if [[ -d "$shared_dir" ]]; then
  while IFS= read -r project; do
    check_references "$project" "shared project" "$shared_dir"
  done < <(find "$shared_dir" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sort)
fi

if (( violations > 0 )); then
  echo "Found $violations forbidden project reference(s)."
  exit 1
fi

echo "OK: services reference only their own projects and shared/ (each shared project from its layer); shared/ references no service."
