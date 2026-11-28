#!/usr/bin/env bash
# Commit-back step test (Roadmap ZP-002). The publish job's "Commit the stamped version back to main-static" step
# runs only on a push to main-static, after the packages are on nuget.org, so a defect in it is found after a release.
# This runs it, and the "Stamp the version" step before it, as .github/workflows/ci.yml has them (taken out by step
# name, so the scripts tested are the ones that ship) against a scratch bare origin, through the cases Phase 11h
# checked by hand (D82, ZA-195, ZA-233, ZA-234), and checks what each run leaves on the origin's main-static and
# what it prints.
#
# Every repository it uses is made in a new temp folder and removed at the end; the steps run in a clone whose origin
# is the scratch origin, checked before each step, so they never reach a real remote. The version step's outputs are
# stubbed (each case says which version that step would give), as are the GITHUB_* values the runner sets.
#
# Needs bash 4+, git 2.32+ and awk (gawk or mawk). Runs on the CI runner and in Git Bash. From the repository root:
#   bash tests/commit-back-test.sh [path to a workflow; default .github/workflows/ci.yml]
# Prints one line per check and "N checks, M failed"; exits 1 when a check fails.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workflow="${1:-${root}/.github/workflows/ci.yml}"
# BuildBaselineTests ZP002 checks that these are step names in the publish job
stamp_step='Stamp the version'
commit_back_step='Commit the stamped version back to main-static'
stamped=(
  Directory.Build.props
  ZauberCMS.Template/ZauberCMS.Template.csproj
  ZauberCMS.Template/template/ZauberCMSTemplate.Site/ZauberCMSTemplate.Site.csproj
)

die() { echo "::error::commit-back test: $*" >&2; exit 1; }

[[ -f "${workflow}" ]] || die "no workflow at ${workflow}"
for tool in git awk sed; do command -v "${tool}" > /dev/null || die "${tool} is not installed"; done

tmp="$(mktemp -d)"
trap 'rm -rf "${tmp}"' EXIT

# The person's own git settings (signing, hooks, autocrlf, identity) stay out; the test's commits are "a person's"
while read -r var; do unset "${var}"; done < <(git rev-parse --local-env-vars)
export GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="${tmp}/gitconfig"
printf '[user]\n\tname = A Person\n\temail = person@example.com\n' > "${GIT_CONFIG_GLOBAL}"

# Prints one part of the step named `name`: part=shell its shell, part=env its env entries as KEY<TAB>VALUE,
# part=run its `run: |` script without the indentation. Exit 2: no step, or more than one, has the name; exit 3: the
# step has no shell, or not exactly one `run: |` block.
step_awk="$(cat << 'AWK'
function indent(s) { match(s, /^ */); return RLENGTH }
function unquote(s,  q) {
  q = substr(s, 1, 1)
  if (length(s) > 1 && (q == "'" || q == "\"") && substr(s, length(s), 1) == q) s = substr(s, 2, length(s) - 2)
  return s
}
{ sub(/\r$/, "") }
# A step runs from its "- name:" line to the next line that is indented no deeper than its dash
in_step && $0 !~ /^ *$/ && indent($0) <= dash { in_step = 0; mode = "" }
!in_step && /^ *- name:/ {
  value = $0; sub(/^ *- name: */, "", value); sub(/ +$/, "", value)
  if (unquote(value) == name) { in_step = 1; dash = indent($0); key = dash + 2; found++; next }
}
!in_step { next }
# Blank lines are held back, so the ones at the end of the block are dropped as YAML does
mode == "run" {
  if ($0 ~ /^ *$/) { blanks++; next }
  if (!body && indent($0) > key) body = indent($0)
  if (body && indent($0) >= body) {
    while (blanks > 0) { if (part == "run") print ""; blanks-- }
    if (part == "run") print substr($0, body + 1)
    next
  }
  mode = ""
}
mode == "env" {
  if ($0 ~ /^ *$/ || $0 ~ /^ *#/) next
  if (indent($0) > key) {
    entry = $0; sub(/^ */, "", entry); sub(/ +$/, "", entry)
    k = entry; sub(/ *:.*$/, "", k)
    v = entry; sub(/^[^:]*: */, "", v)
    if (part == "env") print k "\t" unquote(v)
    next
  }
  mode = ""
}
indent($0) == key && /^ *run:/ {
  runs++
  if ($0 ~ /^ *run: *[|][-+]? *$/) { mode = "run"; body = 0; blanks = 0 } else bad = 1
  next
}
indent($0) == key && /^ *env: *$/ { mode = "env"; next }
indent($0) == key && /^ *shell:/ {
  shell = $0; sub(/^ *shell: */, "", shell); sub(/ +$/, "", shell); shell = unquote(shell)
}
END {
  if (found != 1) exit 2
  if (part == "run" && (bad || runs != 1)) exit 3
  if (part == "shell") { if (shell == "") exit 3; print shell }
}
AWK
)"

# read_step <step name> <file prefix>: <prefix>.shell, <prefix>.env and <prefix>.run, as ci.yml has them
read_step() {
  local part code
  for part in shell env run; do
    code=0
    awk -v name="$1" -v part="${part}" "${step_awk}" "${workflow}" > "$2.${part}" || code=$?
    case "${code}" in
      0) ;;
      2) die "${workflow} has no step named '$1', or more than one" ;;
      3) die "step '$1' in ${workflow} has no ${part} this test can run (it needs 'shell: bash', one 'run: |')" ;;
      *) die "reading step '$1' from ${workflow} failed (awk exit ${code})" ;;
    esac
  done
  [[ "$(cat "$2.shell")" == bash ]] || die "step '$1' runs in '$(cat "$2.shell")', not bash"
  # GitHub fills in ${{ }} expressions before the script runs; this test does not
  # shellcheck disable=SC2016
  if grep -qF '${{' "$2.run"; then die "step '$1' has a \${{ }} expression in its script; pass it in env: instead"; fi
}
read_step "${stamp_step}" "${tmp}/stamp"
read_step "${commit_back_step}" "${tmp}/commit-back"

# An env entry the steps may have, and the version step's outputs it can name
output_re='^[$][{][{] *steps[.]version[.]outputs[.]([A-Za-z_]+) *[}][}]$'
declare -A outputs=()

# The steps push to `origin`: anything but this case's scratch origin stops the test
scratch_origin_only() {
  local fetch push
  fetch="$(git -C "$1" remote get-url origin)"
  push="$(git -C "$1" remote get-url --push origin)"
  [[ "${fetch}" == "${origin_url}" && "${push}" == "${origin_url}" ]] ||
    die "refusing to run the steps in $1: its origin is ${fetch} (push: ${push}), not the scratch origin ${origin_url}"
}

# run_step <file prefix> <clone>: runs the step as the runner does (`bash --noprofile --norc -eo pipefail`, in the
# workspace, with the step's env), appending its output to the job's log
run_step() {
  local key value
  local -a vars=(CI=true GITHUB_ACTIONS=true "GITHUB_WORKSPACE=$2" "GITHUB_OUTPUT=$2.github-output"
    "GITHUB_ENV=$2.github-env" "GITHUB_STEP_SUMMARY=$2.github-summary")
  while IFS=$'\t' read -r key value; do
    [[ "${value}" =~ ${output_re} && -n "${outputs[${BASH_REMATCH[1]}]+set}" ]] ||
      die "a step's env has ${key}: ${value}; this test gives a step the version step's version, tag and rerun only"
    vars+=("${key}=${outputs[${BASH_REMATCH[1]}]}")
  done < "$1.env"
  scratch_origin_only "$2"
  (cd "$2" && env "${vars[@]}" bash --noprofile --norc -eo pipefail "$1.run") >> "${log}" 2>&1
}

# publish <commit> <version> [<rerun>] [race]: one run of the publish job for a push of <commit>, the version step
# having given <version> (rerun "true": it found its own tag). The workspace is what actions/checkout leaves with
# fetch-depth 0: main-static and origin/main-static at the commit pushed. "race": a person's push lands between the
# commit-back's first fetch and its push. Sets `status` (the failing step's exit code, else 0) and `log`.
publish() {
  local work
  job=$((job + 1))
  work="${case_dir}/job${job}"
  log="${work}.log"
  : > "${log}"
  outputs=([version]="$2" [tag]="v$2" [rerun]="${3:-false}")
  git clone --quiet "${origin_url}" "${work}"
  git -C "${work}" checkout --quiet -B main-static "$1"
  git -C "${work}" update-ref refs/remotes/origin/main-static "$1"
  if [[ "${4:-}" == race ]]; then
    local dev_q marker_q
    printf -v dev_q '%q' "${dev}"
    printf -v marker_q '%q' "${work}.raced"
    cat > "${work}/.git/hooks/pre-push" << EOF
#!/usr/bin/env bash
set -euo pipefail
[[ -e ${marker_q} ]] && exit 0
: > ${marker_q}
while read -r var; do unset "\${var}"; done < <(git rev-parse --local-env-vars)
git -C ${dev_q} fetch --quiet origin
git -C ${dev_q} reset --quiet --hard origin/main-static
echo race >> ${dev_q}/app.txt
git -C ${dev_q} commit --quiet -am "A push that lands first"
git -C ${dev_q} push --quiet origin HEAD:main-static
EOF
    chmod +x "${work}/.git/hooks/pre-push"
  fi
  status=0
  run_step "${tmp}/stamp" "${work}" || status=$?
  if [[ "${status}" == 0 ]]; then run_step "${tmp}/commit-back" "${work}" || status=$?; fi
}

# new_case <name>: a scratch origin whose main-static holds the three stamped files (from this repository) at 1.0.0,
# and a person's clone of it, `dev`; `seed` is that first commit
new_case() {
  local file
  case_name="$1"
  cases=$((cases + 1))
  case_dir="${tmp}/case${cases}"
  job=0
  log=""
  origin="${case_dir}/origin.git"
  dev="${case_dir}/dev"
  mkdir -p "${case_dir}"
  git init --quiet --bare --initial-branch=main-static "${origin}"
  origin_url="$(git -C "${origin}" rev-parse --absolute-git-dir)"
  git init --quiet --initial-branch=main-static "${dev}"
  git -C "${dev}" remote add origin "${origin_url}"
  for file in "${stamped[@]}"; do
    mkdir -p "$(dirname "${dev}/${file}")"
    tr -d '\r' < "${root}/${file}" > "${dev}/${file}"
  done
  echo app > "${dev}/app.txt"
  (cd "${dev}" && all_versions 1.0.0)
  git -C "${dev}" add --all
  git -C "${dev}" commit --quiet -m "Seed"
  git -C "${dev}" push --quiet origin main-static
  seed="$(git -C "${dev}" rev-parse HEAD)"
}

# person <subject> <edit...>: a person's commit on the newest main-static, pushed; sets `sha`
person() {
  git -C "${dev}" fetch --quiet origin
  git -C "${dev}" reset --quiet --hard origin/main-static
  (cd "${dev}" && "${@:2}")
  git -C "${dev}" commit --quiet -am "$1"
  git -C "${dev}" push --quiet origin HEAD:main-static
  sha="$(git -C "${dev}" rev-parse HEAD)"
}
code_change() { echo change >> app.txt; }
props_version() { sed -i "s|<Version>[^<]*</Version>|<Version>$1</Version>|" Directory.Build.props; }
props_edit() { echo '<!-- a person edits the props -->' >> Directory.Build.props; }
all_versions() { # as release.ps1 sets them
  props_version "$1"
  sed -i "s|<PackageVersion>[^<]*</PackageVersion>|<PackageVersion>$1</PackageVersion>|" "${stamped[1]}"
  sed -i "s|\(<PackageReference Include=\"ZauberCMS\" Version=\"\)[^\"]*|\1$1|" "${stamped[2]}"
}

# What the origin's main-static holds, and what the last job did
tip() { git -C "${origin}" rev-parse main-static; }
versions() {
  local show=(git -C "${origin}" show)
  printf '%s %s %s' \
    "$("${show[@]}" "main-static:${stamped[0]}" | sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' | head -n 1)" \
    "$("${show[@]}" "main-static:${stamped[1]}" | sed -n 's:.*<PackageVersion>\([^<]*\)</PackageVersion>.*:\1:p')" \
    "$("${show[@]}" "main-static:${stamped[2]}" | sed -n 's:.*Include="ZauberCMS" Version="\([^"]*\)".*:\1:p')"
}
versions_are() { [[ "$(versions)" == "$1 $1 $1" ]]; }
tip_is() { [[ "$(tip)" == "$1" ]]; }
# main-static is one [skip ci] commit on top of <commit> that sets the three files to <version>
bump_on() {
  [[ "$(git -C "${origin}" rev-parse main-static^ 2> /dev/null)" == "$1" ]] &&
    [[ "$(git -C "${origin}" log -1 --format=%s main-static)" == *"[skip ci]"* ]] &&
    versions_are "$2"
}
exits() { [[ "${status}" == "$1" ]]; }
says() { grep -qF -- "$1" "${log}"; }
never_says() { ! grep -qF -- "$1" "${log}"; }
# (no `grep -q` after a pipe: under pipefail, the writer it stops early would fail the check)
warns_naming() { grep -F '::warning::' "${log}" | grep -F -- "$1" > /dev/null; }
no_warning() { never_says '::warning::' && never_says '::error::'; }
keeps_props_edit() { git -C "${origin}" show "main-static:${stamped[0]}" | grep -F 'a person edits the props' > /dev/null; }

checks=0
failed=0
cases=0
check() {
  checks=$((checks + 1))
  if "${@:2}"; then
    echo "ok   - ${case_name}: $1"
  else
    failed=$((failed + 1))
    echo "FAIL - ${case_name}: $1"
    git -C "${origin}" log -4 --format='       main-static: %h %s (%an)' main-static
    if [[ -s "${log}" ]]; then sed 's/^/       | /' "${log}"; fi
  fi
}

new_case "two pushes in flight"
person "First change" code_change; first="${sha}"
person "Second change" code_change; second="${sha}"
publish "${first}" 1.0.1
check "the first push's job exits 0" exits 0
check "it commits 1.0.1 back on top of the second push" bump_on "${second}" 1.0.1
first_bump="$(tip)"
publish "${second}" 1.0.2
check "the second push's job exits 0" exits 0
check "it commits 1.0.2 back on top of the first job's bump" bump_on "${first_bump}" 1.0.2
check "it says it committed" says "Committed"
check "it warns of nothing" no_warning

new_case "a single push"
person "A change" code_change; a="${sha}"
publish "${a}" 1.0.1
check "exits 0" exits 0
check "commits 1.0.1 back on top of the commit built" bump_on "${a}" 1.0.1
check "says it committed" says "Committed"
check "tries once" never_says "(attempt 1)"
check "warns of nothing" no_warning

new_case "a person's later version"
person "A change" code_change; a="${sha}"
person "Version 1.1.0" props_version 1.1.0; p="${sha}"
publish "${a}" 1.0.1
check "exits 0" exits 0
check "keeps main-static at the person's 1.1.0" tip_is "${p}"
check "does not say it committed" never_says "Committed"
publish "${p}" 1.1.0
check "the person's own run commits 1.1.0 back" bump_on "${p}" 1.1.0

new_case "a person's later edit of the props"
person "A change" code_change; a="${sha}"
person "Edit the props" props_edit; p="${sha}"
publish "${a}" 1.0.1
check "exits 0" exits 0
check "keeps main-static at the person's commit" tip_is "${p}"
check "warns, naming the person's commit" warns_naming "Edit the props"
check "does not say it committed" never_says "Committed"
publish "${p}" 1.0.2
check "the person's own run commits 1.0.2 back" bump_on "${p}" 1.0.2
check "and keeps the person's edit" keeps_props_edit

new_case "a rerun of an older release"
person "First change" code_change; a="${sha}"
publish "${a}" 1.0.1
person "Second change" code_change; b="${sha}"
publish "${b}" 1.0.2
check "1.0.1 and then 1.0.2 are committed back" bump_on "${b}" 1.0.2
newest="$(tip)"
publish "${a}" 1.0.1 true
check "the rerun of 1.0.1 exits 0" exits 0
check "keeps main-static at the 1.0.2 bump" tip_is "${newest}"
check "does not say it committed" never_says "Committed"
check "warns of nothing" no_warning

new_case "a push that loses a race"
person "A change" code_change; a="${sha}"
publish "${a}" 1.0.1 false race
racer="$(git -C "${dev}" rev-parse HEAD)"
check "exits 0" exits 0
check "says the first push was refused" says "(attempt 1)"
check "pushes again once" never_says "(attempt 2)"
check "commits 1.0.1 back on top of the push that landed first" bump_on "${racer}" 1.0.1
check "says it committed" says "Committed"

new_case "a rerun of the newest release"
person "A change" code_change; a="${sha}"
publish "${a}" 1.0.1
check "1.0.1 is committed back" bump_on "${a}" 1.0.1
person "A later change" code_change; later="${sha}"
publish "${a}" 1.0.1 true
check "the rerun exits 0" exits 0
check "keeps main-static at the later commit" tip_is "${later}"
check "does not say it committed (the replay is empty)" never_says "Committed"
check "warns of nothing" no_warning

new_case "a prerelease and its release"
person "Version 1.1.0-beta.1" props_version 1.1.0-beta.1; beta="${sha}"
publish "${beta}" 1.1.0-beta.1
check "the prerelease's run commits 1.1.0-beta.1 back" bump_on "${beta}" 1.1.0-beta.1
person "Version 1.1.0" props_version 1.1.0; release="${sha}"
publish "${beta}" 1.1.0-beta.1 true
check "a rerun of the prerelease after the release's push exits 0" exits 0
check "keeps main-static at the release's commit (1.1.0 is above 1.1.0-beta.1)" tip_is "${release}"
check "does not say it committed" never_says "Committed"
publish "${release}" 1.1.0
check "the release's run commits 1.1.0 back over the prerelease's bump" bump_on "${release}" 1.1.0

new_case "a rollback of main-static past the commit built"
person "A change" code_change; a="${sha}"
git -C "${dev}" push --quiet --force origin "${seed}:refs/heads/main-static"
person "A change after the rollback" code_change; after="${sha}"
publish "${a}" 1.0.1
check "exits 0" exits 0
check "warns that main-static no longer has the commit built" warns_naming "1.0.1"
check "keeps main-static at the commit after the rollback (the rolled-back commit stays out)" tip_is "${after}"
check "does not say it committed" never_says "Committed"

new_case "a push the origin refuses every time"
person "A change" code_change; a="${sha}"
printf '#!/bin/sh\necho "main-static is protected" >&2\nexit 1\n' > "${origin}/hooks/pre-receive"
chmod +x "${origin}/hooks/pre-receive"
publish "${a}" 1.0.1
check "exits 1" exits 1
check "tries five times" says "(attempt 5)"
check "does not say it fetches again after the last attempt (ZA-250)" never_says "(attempt 5), usually"
check "stops with an error" says "::error::"
check "does not say main-static moved (it did not)" never_says "moved"
check "does not say it committed" never_says "Committed"
check "leaves main-static as it was" tip_is "${a}"

new_case "a commit that has the version already (release.ps1)"
person "Release 1.2.0" all_versions 1.2.0; p="${sha}"
publish "${p}" 1.2.0
check "exits 0" exits 0
check "commits nothing" tip_is "${p}"
check "does not say it committed" never_says "Committed"

echo "${checks} checks, ${failed} failed"
if [[ "${failed}" != 0 ]]; then exit 1; fi
