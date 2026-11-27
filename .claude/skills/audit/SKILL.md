---
name: audit
description: "Deep ZauberCMS audit. Reads every file by area, sweeps the whole codebase for async/concurrency bugs, memory and resource leaks, query/cache/render performance, correctness invariants, security, Blazor render-mode problems, public API/extensibility/packaging and docs drift; walks the admin in a browser for editor UX; measures queries, memory and upgrades on a running scratch site; compares each section with Umbraco. Writes defects (ZA-NNN) and improvement proposals (ZP-NNN) to .audit/report.md and history to .audit/history.md. Use when the user says /audit, 'run the audit', 'audit the codebase', before a release, or at the end of a Roadmap phase. Args: scope = changed (default) | full | lens letters A-H | unit ids U1-U16; mode = report (default) | fix."
---

# ZauberCMS Audit Runbook

The audit answers two questions about the whole product, not only the code:

1. **What is wrong?** Bugs, async and concurrency errors, leaks, slow paths, security holes, broken contracts, and UX
   that loses work or misleads → **findings `ZA-NNN`**, fixed through the Roadmap.
2. **What would make it better?** Easier editing, closer to how Umbraco does it (ZauberCMS is a scaled-down,
   Blazor-native Umbraco), easier to extend and update → **proposals `ZP-NNN`**, which the owner accepts or declines.

The bar is `AGENTS.md` (above all its core principle: pluggable and updatable), `Docs/Standards.md`, and the owner
decisions (D-numbers) in `Docs/Roadmap.md`. Any agent or tool can follow this runbook.

| File | Read by |
| --- | --- |
| `SKILL.md` (this file) | the orchestrator |
| [areas.md](areas.md) | the orchestrator (to plan); each unit agent (its own unit) |
| [lenses.md](lenses.md) | every agent: "Rules for every agent"; code agents also lenses A–F |
| [product.md](product.md) | the scratch site, product agents (walkthrough, lens G, lens H, the Umbraco map), runtime probes |

Outputs: **`.audit/report.md`**, the current state (open findings, open proposals, checks, coverage), and
**`.audit/history.md`**, append-only (fixed, resolved, closed, declined; newest first). History is never rewritten,
so the report stays short enough to read at the start of every run.

## Arguments and depth

`scope`: `changed` (default) · `full` · lens letters (`A,D`, `G,H`) · unit ids (`U4,U12`).
`mode`: `report` (default, no code edits) · `fix` (apply fixes that meet §7).

| | `changed` | `full` | lens letters | unit ids |
| --- | --- | --- | --- | --- |
| Checks agent K (§2) | build, tests, pattern counts | every row | build + the rows for those lenses | build, tests |
| Code read end to end | changed files + their direct callers and callees, grouped by unit into ≤ 4 agents | every unit in areas.md, one agent each | — | those units |
| Diff reviewer | 1 fresh agent on the whole diff | — | — | — |
| Sweeps S-A, S-B, S-D, S-E | none (unit agents apply all lenses) | all 4, whole codebase | one per named code lens | none |
| Product agents (G, H) | 1, if a screen changed | P1a, P1b, P2, P3, P4 | all five when G or H is named | the groups of those units |
| Runtime probe R1 (product.md) | only if the front-end path or caching changed | yes; R2 (two servers) when Docker is available | when A or B is named | — |
| Merge agent M + verifiers | M optional; verifiers for Critical/High | yes | yes | yes |
| Typical agent count | 2–6 | 30–35 | 2–8 | units + 3 |

`full` is deliberately large: it is the deep audit. Say the agent count in one line before fanning out, then go on
without asking. A Roadmap phase ends with `/audit changed`; a release, or a quarter, ends with `/audit full`.

## 0. Prepare

1. **Run folder.** Make `<run>` = `<scratchpad>/audit-<sha7>/` (a temp folder if there is no scratchpad; never inside
   the repo). Write `<run>/progress.md`: target, scope, the plan (every agent and its status), reserved IDs, where
   the outputs are. Update it as agents return. It is the checkpoint: after a context compaction, read it first.
2. **Target: what is audited.** Run `git status --short` (another session may be working in this tree: check
   `ListAgents` if you have it).
   - Every uncommitted change is yours (the phase you just built) or there are none → audit the working tree in
     place. Target = `<HEAD sha>+wt` (or `<HEAD sha>` when clean). `git pull` first if the tree is clean.
   - Anyone else's uncommitted work is in the tree → don't pull, and don't audit their half-finished work: `git
     worktree add --detach <run>/src HEAD` and run every command and agent in `<run>/src`. Target = `<HEAD sha>`.
     Remove the worktree at the end (`git worktree remove --force <run>/src`).
   - `mode = fix` needs the first case. Otherwise run in report mode and say why.
   `<root>` below means the tree being audited.
3. **The report.** Read `.audit/report.md` in full. Note the header's *Last full run* and *Last changed run* (an old
   header with only *Audited commit* counts as the last full run), the next free `ZA`/`ZP` IDs, the open findings and
   proposals, and Coverage → "Not covered".
4. **The Roadmap.** Read `Docs/Roadmap.md`: Status, Owner decisions, phases, Open questions. Each brief carries the
   D-numbers that bind its area. A D-decision is not a finding, unless the code does something other than it says.
5. **Scope.** For `changed`, the base is the newer of the last full run and the last changed run. If that value ends
   in `+wt`, or there is no last changed run, take the commit that recorded it instead: the first commit after it
   that changed `.audit/report.md` (`git log --reverse --format=%h <sha>..HEAD -- .audit/report.md`, first line), or
   with no value at all, `git log -1 --format=%h -- .audit/report.md`. Files = `git diff --name-only <base>..HEAD`,
   plus uncommitted files when the target is `+wt`. Map them to units with areas.md. With more than ~150 files, or no
   base, use `full`.
6. **Carried items.** For `full`, the previous run's "Not covered" items are mandatory: give each to the agent that
   owns its area. Items listed as "Out of reach" (§8) don't carry over.

## 1. Reconcile open items

- `full`: each unit agent checks the open items in its files (the brief lists them), and its status is the one that
  counts. The orchestrator scans `git log <base>..HEAD` for commits that flip a default or a parameter an open
  finding depends on (regressions hide there) and gives items that no unit owns to agent K.
- `changed`: the diff reviewer checks the open items whose cited files changed.
- Status: **resolved** (cite the commit), **still open** (new `path:line`), or **regressed/changed**. A proposal that
  was built, or that the owner declined (a D-decision or an answer in the Roadmap), moves to history with the reason.

## 2. Deterministic checks (agent K, in `<root>`)

One agent, K, runs these and writes the table to `<run>/K.md`. Keep the orchestrator's context for planning.

| Check | Command / method | Becomes a finding when |
| --- | --- | --- |
| Build + warnings | `dotnet build ZauberCMS.sln -c Release` → count by warning code | it fails (High); a warning the last report did not have (Low) |
| Tests | `dotnet test --solution ZauberCMS.sln -c Release`; record whether Docker ran the PostgreSQL/Redis tests and whether `ZAUBER_TEST_SQLSERVER` was set | a test fails (re-run once; fails again → High, passes → flaky, Medium); a `Skip` names a closed finding or no finding |
| Vulnerable packages | `dotnet package list --project ZauberCMS.sln --vulnerable --include-transitive` (CI fails on any) | any, at the advisory's severity |
| Outdated packages | `dotnet list ZauberCMS.sln package --outdated`; majors separately (D4 holds the .NET 11 moves) | a patch or minor we're behind on fixes a bug or vulnerability that affects us |
| Version alignment | every solution `PackageReference` without a `Version` and listed in `Directory.Packages.props`; one `<Version>` (in `Directory.Build.props`) for the four packages; the template site's `ZauberCMS` reference and the template's `PackageVersion` match it (the `BuildBaselineTests` cover the first two) | they differ |
| Migration parity | the same migrations in `Data/Migrations/{SqlServer,SqLite,PostgreSql}`, compared by the name after the timestamp (timestamps differ per provider); snapshots changed in the same commits; the "no pending model changes" tests pass | they differ |
| Migration safety | every migration since the base: drops or renames a column (EF may emit drop + add, losing data), adds a NOT NULL column without a default, or forces an SQLite table rebuild on a large table | any, unless a data step or the commit explains it (Medium; High on Content, ContentVersion or Media) |
| Config parity | keys in `ZauberCMS/appsettings.json`, `ZauberCMS.Web/appsettings.json` and the template site's agree (a parity test exists) | a key is missing from one; a real secret |
| Pattern counts | the list below | never by itself: a rise is a lead, and the agent owning each new hit reads it |
| Package contents (`full`) | `dotnet pack ZauberCMS.sln -c Release` (no `-o`: each csproj's `CopyNuGetPackage` copies the package to `$(LocalNugetSource)`, so pass `-p:LocalNugetSource=<run>/nupkgs`); list each `.nupkg` (a zip) and its `.nuspec` dependencies | stray files (npm manifests, tests, `appsettings.Development.json`, secrets); missing static web assets, `build/` targets or `build/settings.json`; a dependency that doesn't point at this version |
| API breaks (`full`) | the same pack with `-p:EnablePackageValidation=true -p:PackageValidationBaselineVersion=<last published version>` | each break listed. During the rework breaks are allowed (D18), but each needs a release-notes line: one without is a finding (F1) |
| Template code (`full`) | `dotnet new install <root>/ZauberCMS.Template/template --debug:custom-hive <run>/hive`; `dotnet new zaubercms -o <run>/tmpl/AuditSite --debug:custom-hive <run>/hive`; in the new csproj, replace the `ZauberCMS` PackageReference with a ProjectReference to `<root>/ZauberCMS/ZauberCMS.csproj` (the PackageReference pins the published version); `dotnet build -c Release`. P1a walks its installer later | it doesn't build (High: every new site) |
| Template from packages (`full`, best effort) | packages built with a unique prerelease version into `<run>/nupkgs` (`-p:Version=…`); a second site whose `nuget.config` lists `<run>/nupkgs` first, restored with `--packages <run>/nuget-cache`; build | it doesn't build or start. If the packed dependencies still point at the published version, that is the version-alignment finding: record it and stop here |
| Docs drift | backticked paths, types and members in `AGENTS.md` and `Docs/*.md` exist (grep), except the Roadmap's DONE phases, which name retired things on purpose; spot-check behaviour claims for the units in scope | a doc contradicts the code (Low; Medium if it would mislead a plugin author into a break) |
| Repo hygiene | no tracked local files (`*.user`, `settings.local.json`, a `*.db` outside `ZauberCMS.Web`) | a tracked local file |
| Hot-path budget | measured by R1 (product.md), reported here | higher than the last report (Medium) |

**Pattern counts.** One Grep per line: `path` = `<root>`, `glob` = `*.{cs,razor}` (`*.csproj` too for
suppressed warnings), `output_mode: count`, `head_limit: 0`, and `multiline: true` where marked. Sum the per-file
counts, leaving out files under `Migrations/`, `wwwroot/lib/`, `tests/` and `ZauberCMS.Web/`. The counts include false
positives; what matters is the trend and each new hit.

```text
sync-over-async          \.Result\b|\.Wait\(\)|GetAwaiter\(\)\.GetResult\(\)
async-void               async void
fire-and-forget          _ = [\w.]+Async\(|Task\.Run\(
empty-catch (multiline)  catch(\s*\([^)]*\))?\s*\{\s*(//[^\n]*\s*)*\}
unsanitised-markup       MarkupString|Html\.Raw\(
raw-sql                  FromSqlRaw|ExecuteSqlRaw|SqlQueryRaw
new-httpclient           new HttpClient\(
culture-unsafe-compare   \.ToLower\(\)|\.ToUpper\(\)
local-time               DateTime(Offset)?\.Now\b
regex-per-call           new Regex\(|Regex\.(IsMatch|Match|Matches|Replace|Split)\(
count-gt-zero            \.Count\(\)\s*>\s*0
static-mutable           static\s+(readonly\s+)?(Dictionary|List|HashSet|ConcurrentDictionary)<
event-subscribe          (\.On[A-Z]\w*|\bLocationChanged)\s*\+=
event-unsubscribe        (\.On[A-Z]\w*|\bLocationChanged)\s*-=     (a file where the two counts differ is a lead)
httpcontext-accessor     IHttpContextAccessor
exception-text           \.Message\b                              (razor files only)
suppressed-warnings      #pragma warning disable|<NoWarn>
unfinished               TODO|FIXME|HACK
```

## 3. Plan and fan out

1. Build the plan from areas.md: K, the units in scope, the sweeps, the product agents, R1 (and R2), and for
   `changed` the diff reviewer. Split a unit above ~5k lines by subfolder; merge one below ~1k into a neighbour.
2. **The site and the browser are shared, so their users run in sequence:** after K's build, prepare the scratch
   site (product.md, "Scratch site") → P1a → P1b → P2 → P3 → P4 → R1 → R2. **Units and sweeps run in parallel the
   whole time**: they need neither. Keep the site up until verification (§5) is done, then stop it. Without a
   browser tool, the product agents work from the markup and code, and say so in coverage.
3. Up to 6 agents at once; start the next as each finishes.
4. If the Workflow tool is available and the scope is `full`, steps 3 to 6 can run as one workflow (units and sweeps
   in parallel, the site's users in sequence, M, then verifiers). Running `/audit full` is the user's opt-in. Load
   the `workflow-authoring` skill first. The orchestrator prepares and stops the site outside it. Otherwise use the
   Agent tool.
5. **Return contract.** Each agent writes its full output to `<run>/<agent-id>.md` and one line per item to
   `<run>/<agent-id>.tsv` (lenses.md, "Output files"), and returns at most 10 lines. The orchestrator never needs to
   read the `.md` files: M does.

Brief for every agent (fill in; the brief is all the agent knows):

```text
You are one agent of a ZauberCMS audit. Tree to audit: <root> (target <sha or sha+wt>). Your job: <unit id + name |
sweep S-x | P-n | R-n | K | diff reviewer>. Run folder: <run>.
Read first: <root>/AGENTS.md; .claude/skills/audit/lenses.md ("Rules for every agent", then lenses <A–F | the
named lens>); <your unit in .claude/skills/audit/areas.md>; <P and R agents: .claude/skills/audit/product.md>.
Files: <list or globs>. Read every file end to end; grep hits are leads, not findings. Follow calls out of your
files as far as you need to judge them.
Must cover: <carried-forward "Not covered" items | the changed hunks>.
Open items here (report each one's status; don't re-report it): <ZA/ZP ids + titles>.
Owner decisions that bind here: <D-numbers, one line each>.
Leads from the pattern counts: <new hits in your files>.
<P and R agents: the scratch site is http://127.0.0.1:5399, the credentials are in <run>/site/credentials.txt, the
browser and the site are yours alone until you finish. Write your notes after each task, not at the end.>
Write <run>/<agent-id>.md and <run>/<agent-id>.tsv as lenses.md describes. Return at most 10 lines: counts by
severity, the Critical/High titles, the number of proposals, anything you could not check.
```

## 4. Merge (agent M)

M reads every `<run>/*.tsv` and `.md` and writes `<run>/merged.md`:

- One root cause is one finding: merge duplicates across units, sweeps and product agents (the `.tsv` key is
  `path#member`; the same key, or the same cause at different keys, is one item), listing every place and source.
- Match each item against the open ones: the same root cause keeps the existing ID. Unit agents' statuses for open
  items win over anyone else's.
- Calibrate: the same kind of problem gets the same severity everywhere (lenses.md, "Severity"). Proposals for the
  same thing from several P agents become one, with one value and effort rating.
- Roll-ups: accessibility goes into the accessibility roll-up (ZA-128 while open); Known deviations from
  `Docs/Standards.md` once; small performance items into the open performance roll-up (ZA-115 while open), or start one.
- Build the Umbraco alignment table from the P agents' rows (product.md, column P).
- Write `<run>/verify-list.md` (§5) and return the counts. The orchestrator reads `merged.md`'s summary and the
  verify list, not the agents' files.

## 5. Verify

- **Findings.** Every Critical and High, every finding a `fix` run will touch, every Low-confidence finding, and about
  a third of the Mediums (the riskiest) go to a fresh agent that did not find them, batched by unit or lens. It gets
  the claim and the cited lines, not the finder's reasoning, and tries to disprove it: is the path reachable, does a
  caller already guard it, does an open finding or a D-decision cover it. It may reproduce on the scratch site (still
  running) or with a throwaway test in a worktree: `<run>/src`, or `git worktree add --detach <run>/repro HEAD` plus
  the uncommitted diff when the target is `+wt`. Never in the repo itself. It returns confirmed, downgraded or dropped,
  with the reason. Not reproduced → dropped or downgraded; Low confidence → verified or dropped.
- **Proposals.** The feature doesn't already exist under another name (grep and the admin), the Umbraco claim is right
  (a docs.umbraco.com link), and no D-decision or declined proposal covers it.
- Then stop the scratch site (product.md).

## 6. Number and draft

M (continued with the verdicts) drops and downgrades as verified, numbers new items from the report's next free `ZA`
and `ZP` IDs (IDs are never reused), compares the trends with the last report, and drafts the report (§8) and this
run's history section in `<run>/`. The orchestrator reads the drafts, checks them against `progress.md`, and writes
the files.

## 7. Fix (mode = fix only)

Fix only when **all** of these hold: confidence High; severity Critical/High, or Medium with a concrete trigger; the
change is local (about 5 files or fewer); no public API, persisted format, schema or migration, auth, identity,
cookie or antiforgery configuration, new dependency, or user-visible workflow change; a security fix has a concrete
attack scenario. Proposals are never built in an audit.

For each fix: change the minimum → **grep every caller of any member, parameter or default you changed** → add the
regression test (`Docs/Standards.md` §9; remove a waiting `Skip`) → build → run the tests. If the build or tests fail
and can't be repaired in scope, undo your own edit (reverse-apply your diff of that file; never `git checkout` a file)
and mark the finding `not fixed — build/tests broke`. Never edit migrations. Everything else stays open with a "why not
fixed" line.

## 8. Report

A `full` run rewrites `.audit/report.md` in this shape. A `changed` run updates it in place: the header's last
changed run and status line, the items it opened, changed or closed, and one line under Coverage. It keeps "Not
covered" as it is. Write in the plain, direct English of the existing report; one finding = a few lines.

```markdown
# ZauberCMS Audit Report

Last full run: <sha> (<date>, <target>) · Last changed run: <sha or sha+wt> (<date>, <what: e.g. Roadmap Phase 10e>)
Build: PASS/FAIL · Warnings: N (prev M) · Tests: N (pass/fail/skip; providers run) · Vulnerable: N · Outdated majors: N
Next free IDs: ZA-NNN (findings) · ZP-NNN (proposals) · Umbraco reference: <version compared>

## Summary
| Severity | Open | New this run | Fixed this run | Resolved since last run |
Proposals: N open (N high value) · Trend: <pattern counts and hot-path budget, changes only>
**Work order and owner decisions: Docs/Roadmap.md.** <where the new items were assigned>

## Open findings
<by severity, then ID>
### ZA-NNN [Severity/Confidence] Title
- Where: `path:line` · Category: A–H · Unit: Un · Since: <date> · Roadmap: <phase>
- Issue / Trigger: …
- Fix: … · Test: … · Blocked by: <public API | schema | owner decision | …>

## Improvement proposals
<grouped by section: Content, Media, Structure, Users and access, Settings, Front end, Installer, Developer experience>
### ZP-NNN [Value/Effort] Title
- Section: … · Lens: G | H | F · Since: <date> · Needs: <owner decision | design | nothing>
- Today: … · Proposal: … · Why: … (Umbraco: <docs link>)

## Umbraco alignment
| Area | Aligned | Different on purpose | Gaps (ZP ids) |

## Fixed this run / Resolved since last run
- ZA-NNN Title: what changed (commit). <this run only; history.md keeps every run>

## Deterministic checks
<the §2 table, one line each, with the previous value>

## Coverage
| Unit / agent | Files read fully | Skimmed | Not read (why) |
- Walkthrough and runtime probes: tasks done, and tasks not done with the reason; the environment (Production, SQLite).
- Not covered (carried to the next full run): …
- Out of reach (no step of this runbook can do it; not carried): …
```

## 9. History and hand-off

1. **`.audit/history.md`**: add this run's section at the top of "Fixed, resolved, closed and declined" (items fixed
   or resolved, with how each was verified; proposals built or declined, with the owner's reason), and a line at the
   top of the run log. Never rewrite older entries.
2. **`Docs/Roadmap.md`**: run `git diff Docs/Roadmap.md` first (the owner and other sessions edit it too). Update the
   Status line's open counts. Put each new finding in the phase whose theme fits (there is no Backlog: owner, 2026-11-27; a finding that needs
   an owner decision goes in a phase and in "Open questions for the owner").
   Add the high-value proposals to "Open questions for the owner", one line each with a recommendation; the full
   list stays in the report. Don't start fixing (except in `fix` mode).
3. **Declined proposals**: when the owner declines one, add a row to product.md "Different on purpose" so later runs
   don't raise it again.
4. Commit only as `AGENTS.md`'s working agreements allow, and only the audit's own files. When another session has
   uncommitted edits in the same files, stage only your own hunks, or agree the order with that session first.
5. Clean up: the scratch site is stopped; remove `<run>/src` and `<run>/repro` (`git worktree remove --force`), any
   containers R2 started, and the run folder once the report is committed.
6. Tell the user: counts by severity and the trend since the last run, the top 5 open findings, the top 5 proposals,
   what was not covered, and the decisions needed from them.
