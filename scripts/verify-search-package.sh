#!/usr/bin/env bash
# Verifies the restored DashyNMS.Agent.Search package (#596) before anything builds
# against it or loads it. Run it straight after a restore and before any build, test
# or publish: CI restores, runs this, then builds or tests.
#
# It checks the package is the exact file DashyNMS/mcp's "Search package" workflow
# built and attested from the pinned commit on main, that the files NuGet extracted
# (which builds and tests actually use) are the ones inside that file, and that the
# package carries nothing MSBuild would run (build/, buildTransitive/, analyzers/,
# tools/). Pins live here only; move them with the PackageReference.
#
# Needs: gh (authenticated, attestations: read), unzip, and sha256sum or shasum.
set -euo pipefail

# Pins: the version in DashyNMS.Mobile.Shared.csproj, the SHA-256 the publish run
# attested, the DashyNMS/mcp commit it was built from, and the workflow that built it
# (run 43216437071).
SEARCH_VERSION="0.1.0-ci.43216437071.1"
SEARCH_SHA256="9b1f20ee38156a2947f57db10ca25d81f55c456adc376259e24a699b93bfb02a"
SEARCH_COMMIT="57e09ca472783055c7909612ffbc1e99af5d18ab"
SEARCH_REPO="DashyNMS/mcp"
SEARCH_WORKFLOW="$SEARCH_REPO/.github/workflows/search-package.yml"

fail() { echo "::error::Search package: $*" >&2; exit 1; }

sha256() {
  if command -v sha256sum >/dev/null; then sha256sum | cut -d' ' -f1; else shasum -a 256 | cut -d' ' -f1; fi
}

size() { wc -c < "$1" | tr -d ' '; }

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CSPROJ="$ROOT/src/DashyNMS.Mobile.Shared/DashyNMS.Mobile.Shared.csproj"
grep -q "Include=\"DashyNMS.Agent.Search\" Version=\"$SEARCH_VERSION\"" "$CSPROJ" \
  || fail "DashyNMS.Mobile.Shared.csproj doesn't reference $SEARCH_VERSION; move the pins with it"

PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
DIR="$PACKAGES/dashynms.agent.search/$SEARCH_VERSION"
NUPKG="$DIR/dashynms.agent.search.$SEARCH_VERSION.nupkg"
[ -f "$NUPKG" ] || fail "not restored at $NUPKG; restore first"

# 1. The exact bytes the publish run attested.
actual=$(sha256 < "$NUPKG")
[ "$actual" = "$SEARCH_SHA256" ] || fail "SHA-256 is $actual, expected $SEARCH_SHA256"

# 2. Provenance bound to the repository, the publishing workflow and its commit on main,
#    on a GitHub-hosted runner.
gh attestation verify "$NUPKG" \
  --repo "$SEARCH_REPO" \
  --signer-workflow "$SEARCH_WORKFLOW" \
  --signer-digest "$SEARCH_COMMIT" \
  --source-ref refs/heads/main \
  --source-digest "$SEARCH_COMMIT" \
  --deny-self-hosted-runners \
  --predicate-type https://slsa.dev/provenance/v1 >/dev/null \
  || fail "attestation doesn't verify against $SEARCH_WORKFLOW at $SEARCH_COMMIT"

ENTRIES=$(unzip -Z1 "$NUPKG")

# 3. The nuspec inside the verified file names the same repository and commit.
NUSPEC_ENTRY=$(grep -E '^[^/]+\.nuspec$' <<<"$ENTRIES" | head -1)
[ -n "$NUSPEC_ENTRY" ] || fail "no nuspec"
NUSPEC=$(unzip -p "$NUPKG" "$NUSPEC_ENTRY")
grep -q "commit=\"$SEARCH_COMMIT\"" <<<"$NUSPEC" || fail "nuspec names another commit"
grep -q "url=\"https://github.com/$SEARCH_REPO\"" <<<"$NUSPEC" || fail "nuspec names another repository"

# 4. Nothing MSBuild or the IDE would run.
if grep -E '^(build|buildTransitive|buildMultiTargeting|analyzers|tools)/' <<<"$ENTRIES"; then
  fail "contains build-time assets"
fi

# 5. What builds and tests load is the extracted copy, not the .nupkg: every lib/ file
#    and the LICENSE must match the verified file byte for byte, and nothing extra may sit
#    beside them.
while IFS= read -r entry; do
  case "$entry" in lib/*[!/]|LICENSE) ;; *) continue ;; esac
  [ -f "$DIR/$entry" ] || fail "$entry wasn't extracted"
  [ "$(unzip -p "$NUPKG" "$entry" | sha256)" = "$(sha256 < "$DIR/$entry")" ] || fail "extracted $entry differs from the verified package"
done <<<"$ENTRIES"
if [ -d "$DIR/lib" ]; then
  while IFS= read -r file; do
    grep -qxF "${file#"$DIR/"}" <<<"$ENTRIES" || fail "unexpected extracted file ${file#"$DIR/"}"
  done < <(find "$DIR/lib" -type f)
fi
for extra in build buildTransitive analyzers tools; do
  [ ! -e "$DIR/$extra" ] || fail "unexpected extracted folder $extra"
done

DLL="$DIR/lib/net10.0/DashyNMS.Agent.Search.dll"
[ -f "$DLL" ] || fail "no net10.0 assembly"
echo "DashyNMS.Agent.Search $SEARCH_VERSION verified: SHA-256, attestation ($SEARCH_WORKFLOW @ $SEARCH_COMMIT, refs/heads/main), nuspec, extracted files."

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Shared Search package"
    echo "- Version: \`$SEARCH_VERSION\` from $SEARCH_REPO \`$SEARCH_COMMIT\`"
    echo "- Attestation: signed by \`$SEARCH_WORKFLOW\` at that commit on \`refs/heads/main\`, GitHub-hosted runner"
    echo "- nupkg: $(size "$NUPKG") bytes, SHA-256 verified; extracted lib/ and LICENSE match it; no build-time assets"
    echo "- DashyNMS.Agent.Search.dll: $(size "$DLL") bytes"
    echo "- Dependencies: $(grep -o '<dependency [^>]*>' <<<"$NUSPEC" | tr '\n' ' ')"
  } >> "$GITHUB_STEP_SUMMARY"
fi
