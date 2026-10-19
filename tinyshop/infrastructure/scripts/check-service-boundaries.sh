#!/usr/bin/env bash
# Fails if any .NET project references a project that belongs to a different service.
#
# Services must depend on each other only through their contracts (HTTP, gRPC, proto files),
# never through code. A ProjectReference from one service into another compiles fine, but
# quietly turns separate services into a distributed monolith, so CI rejects it.
#
# Usage (from anywhere): tinyshop/infrastructure/scripts/check-service-boundaries.sh
set -euo pipefail

tinyshop_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
services_dir="$tinyshop_dir/services"
violations=0

while IFS= read -r project; do
  project_dir="$(dirname "$project")"
  # The service a project belongs to is the first folder below services/.
  relative="${project#"$services_dir"/}"
  service="${relative%%/*}"

  # Each ProjectReference Include="..." path, with Windows separators turned into "/".
  while IFS= read -r reference; do
    [[ -z "$reference" ]] && continue
    target="$(realpath -m "$project_dir/${reference//\\//}")"

    if [[ "$target" != "$services_dir/$service/"* ]]; then
      echo "::error file=${project#"$tinyshop_dir"/}::'$service' references a project outside its own service: ${target#"$tinyshop_dir"/}"
      violations=$((violations + 1))
    fi
  done < <(grep -o '<ProjectReference Include="[^"]*"' "$project" | sed 's/.*Include="\(.*\)"/\1/' || true)
done < <(find "$services_dir" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sort)

if (( violations > 0 )); then
  echo "Found $violations cross-service project reference(s)."
  exit 1
fi

echo "OK: no project references cross a service boundary."
