---
name: audit
description: "Deep ZauberCMS audit. Reads every file by area, sweeps the whole codebase for async/concurrency bugs, memory and resource leaks, query/cache/render performance, correctness invariants, security, Blazor render-mode problems, public API/extensibility/packaging and docs drift, walks the admin in a browser for editor UX, and compares each section with Umbraco. Writes defects (ZA-NNN) and improvement proposals (ZP-NNN) to .audit/report.md and history to .audit/history.md. Use when the user says /audit, 'run the audit', 'audit the codebase', before a release, or at the end of a Roadmap phase. Args: scope = changed (default) | full | lens letters A-H | unit ids U1-U16; mode = report (default) | fix."
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
| [product.md](product.md) | product agents: scratch site, walkthrough script, lens G (UX), lens H (Umbraco), concept map |

Outputs: **`.audit/report.md`**, the current state (open findings, open proposals, checks, coverage), rewritten each
run, and **`.audit/history.md`**, append-only (fixed, resolved, closed, declined; newest first). History is never
rewritten, so the report stays short enough to read at the start of every run.

## Arguments and depth

`scope`: `changed` (default) · `full` · lens letters (`A,D`, `G,H`) · unit ids (`U4,U12`).
`mode`: `report` (default, no code edits) · `fix` (apply fixes that meet §6).

| | `changed` | `full` | lens letters | unit ids |
| --- | --- | --- | --- | --- |
| Code read end to end | changed files + their direct callers and callees, grouped by unit into ≤ 4 agents | every unit in areas.md, one agent each | — | those units |
| Diff reviewer | 1 fresh agent on the whole diff | — | — | — |
| Sweeps (S-A, S-B, S-D, S-E) | none (unit agents apply all lenses) | 4 agents, whole codebase | one agent per named code lens, whole codebase | none |
| Product review (G, H) | 1 agent, if a screen changed | P1–P4 | P1–P4 when G or H is named | the product group of those units |
| Browser walkthrough | the changed screens | the full script | when G is named | if the unit has screens |
| Deterministic checks (§2) | build, tests, pattern counts | all rows | build + the rows for that lens | build, tests |
| Typical agent count | 2–6 | 25–30 | 1–5 | units + verifiers |

`full` is deliberately large: it is the deep audit. Say the agent count in one line before fanning out, then go on
without asking. A Roadmap phase ends with `/audit changed`; a release or a quarter ends with `/audit full`.

## 0. Prepare

1. Run `git status --short` and note edits that are not yours (another session may be working in this tree: check
   `ListAgents` if you have it). Never stage those. `git pull` when the tree allows it; otherwise say you audited the
   local `HEAD`. Note `HEAD`.
2. Read `.audit/report.md` in full. Note the *Audited commit*, the next free `ZA`/`ZP` IDs, the open findings and
   proposals, and the Coverage section's "Not covered" list.
3. Read `Docs/Roadmap.md`: Status, Owner decisions, Backlog, Open questions. Each brief carries the D-numbers that
   bind its area: a D-decision is not a finding, unless the code does something other than what it says.
4. Resolve the scope. For `changed`, take `git diff --name-only <audited>..HEAD` plus uncommitted files and map them
   to units with areas.md. If the report has no audited commit, or more than ~150 files changed, use `full`.
5. Make a run folder outside the repo: `<scratchpad>/audit-<sha7>/`, or a temp folder if there is no scratchpad.
   Write `progress.md` there: scope, the plan (every agent and its status), reserved IDs, and where the findings
   files are. Update it as agents return. It is the checkpoint: after a context compaction, read it before anything else.
6. For `full`, the previous run's "Not covered" items become mandatory. Give each one to the agent that owns its area.

## 1. Reconcile open items (always, before new work)

For each open finding and proposal (all of them for `full`; for `changed`, those whose cited files changed), re-read
the cited code and mark it **resolved** (cite the commit), **still open** (update `path:line`), or
**regressed/changed**. Scan `git log <audited>..HEAD` for changes that flip a default or a parameter an open finding
depends on: regressions hide there. A proposal that was built, or that the owner declined (in a D-decision or an
answer in the Roadmap), moves to history with the reason. With more than 20 items, give this to one agent and ask it
for the status changes only.

## 2. Deterministic checks (run directly; keep only summaries)

| Check | Command / method | Becomes a finding when |
| --- | --- | --- |
| Build + warnings | `dotnet build ZauberCMS.sln -c Release` → count by warning code | it fails (High); a warning appears that the last report did not have (Low) |
| Tests | `dotnet test --solution ZauberCMS.sln -c Release`; record whether Docker ran the PostgreSQL/Redis tests and whether `ZAUBER_TEST_SQLSERVER` was set | a test fails (re-run once; fails again → High, passes → flaky, Medium); a `Skip` names a closed finding or no finding |
| Vulnerable packages | `dotnet list ZauberCMS.sln package --vulnerable --include-transitive` | any, at the advisory's severity |
| Outdated packages | `dotnet list ZauberCMS.sln package --outdated`; report majors separately (D4 holds the .NET 11 moves) | a patch or minor we are behind on fixes a bug or a vulnerability that affects us |
| Version alignment | shared packages at the same version in every csproj; the PackageReference chain matches `<Version>` | they differ |
| Migration parity | same migration names in `Data/Migrations/{SqlServer,SqLite,PostgreSql}`; snapshots changed in the same commits; the "no pending model changes" tests pass | they differ |
| Config parity | keys in `ZauberCMS/appsettings.json`, `ZauberCMS.Web/appsettings.json` and the template site's agree (there is a parity test) | a key is missing from one; a real secret |
| Pattern counts | the table below, Grep `output_mode: count` over `ZauberCMS.Core ZauberCMS.Components ZauberCMS.Routing ZauberCMS`, leaving out `Migrations/` and `wwwroot/lib/`; compare with the last report | never by itself: a rise is a lead, and the agent owning each new hit reads it |
| Hot-path budget | DB round-trips to render a front-end page: cold, warm, 404, plus each block list and each media picker. Measure (the scratch site with the Serilog override `Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command=Information`, counting commands in its log per request; or a test with a command interceptor) or count by reading `CatchAll` → `EntryPage` → view; say which | higher than the last report (Medium) |
| Package contents (`full`) | `dotnet pack ZauberCMS.sln -c Release -o <run>/nupkgs`; list each `.nupkg` (a zip) | stray files (npm manifests, tests, `appsettings.Development.json`, secrets); missing static web assets or `build/` targets |
| Template smoke test (`full`) | `dotnet new install ZauberCMS.Template/template --debug:custom-hive <run>/hive`; `dotnet new zaubercms -o <run>/tmpl/AuditSite --debug:custom-hive <run>/hive`; in the new csproj, replace the `ZauberCMS` PackageReference with a ProjectReference to this repo's `ZauberCMS/ZauberCMS.csproj` (the PackageReference pins the published version, so it would test nuget.org, not this commit); `dotnet build -c Release`; start it once and request `/` (expect the installer) | the build fails or the site does not start (High: every new site). If set-up fails, record "not run" and why |
| Docs drift | every backticked path, type and member in `AGENTS.md` and `Docs/*.md` exists (grep); spot-check behaviour claims in the docs for the units in scope | a doc contradicts the code (Low; Medium if it would mislead a plugin author into a break) |
| Repo hygiene | no tracked local files (`*.user`, `settings.local.json`, a `*.db` outside `ZauberCMS.Web`) | a tracked local file |

Pattern counts: one Grep per line (regex after the name; `(multiline)` means `multiline: true`, `(razor)` means
`glob: "*.razor"`). The counts include false positives; what matters is the trend and each new hit.

```text
sync-over-async          \.Result\b|\.Wait\(\)|GetAwaiter\(\)\.GetResult\(\)
async-void               async void
fire-and-forget          _ = [\w.]+Async\(|Task\.Run\(
empty-catch (multiline)  catch(\s*\([^)]*\))?\s*\{\s*\}
unsanitised-markup       MarkupString|Html\.Raw\(
raw-sql                  FromSqlRaw|ExecuteSqlRaw|SqlQueryRaw
new-httpclient           new HttpClient\(
culture-unsafe-compare   \.ToLower\(\)|\.ToUpper\(\)
local-time               DateTime(Offset)?\.Now\b
regex-per-call           new Regex\(|Regex\.(IsMatch|Match|Matches|Replace|Split)\(
count-gt-zero            \.Count\(\)\s*>\s*0
static-mutable           static\s+(readonly\s+)?(Dictionary|List|HashSet|ConcurrentDictionary)<
event-subscribe          \.On[A-Z]\w*\s*\+=
event-unsubscribe        \.On[A-Z]\w*\s*-=          (a file where the two counts differ is a lead)
httpcontext-accessor     IHttpContextAccessor
exception-text (razor)   \.Message\b
suppressed-warnings      #pragma warning disable|<NoWarn>
unfinished               TODO|FIXME|HACK
```

## 3. Plan and fan out

1. Build the plan from areas.md: the units in scope, the sweeps, P1–P4, and for `changed` the diff reviewer. Split
   a unit above ~5k lines by subfolder and merge one below ~1k into a neighbour (areas.md says how to count).
2. **One browser at a time.** P1–P4 (or the single product agent of a `changed` run) share one browser and one
   scratch site, so they run one after another. Start the scratch site (product.md, "Scratch site") before the first
   product agent, which also completes the installer. After the last one, stop the site and clean up. Without a browser tool the P agents work from the markup and code, and say so in coverage.
3. Waves: P1 plus up to 5 unit agents at once; start the next agent as each one finishes. Sweeps can run at any
   point; overlap with unit findings is merged in §5.
4. If the Workflow tool is available and the scope is `full`, steps 3 to 5 can run as one workflow: units and sweeps
   in parallel, P1–P4 in sequence, each Critical or High verified as it arrives. Running `/audit full` is the user's
   opt-in. Load the `workflow-authoring` skill first. The orchestrator starts and stops the scratch site outside it.
   Otherwise use the Agent tool in waves.
5. Each agent writes its whole output to `<run>/<agent-id>.md` and returns at most 10 lines, so the orchestrator's
   context holds summaries, not findings. Record each return in `progress.md`.

Brief for every agent (fill in; the brief is all the agent knows):

```text
You are one agent of a ZauberCMS audit. Repo: <path>. Commit: <sha>. Your job: <unit id + name | sweep S-x | P-n>.
Read first: AGENTS.md; .claude/skills/audit/lenses.md ("Rules for every agent", then lenses <A–F | the named
lens>); <your unit in .claude/skills/audit/areas.md>; <for P agents: .claude/skills/audit/product.md>.
Files: <list or globs>. Read every file end to end; grep hits are leads, not findings. Follow calls out of your
files as far as you need to judge them.
Must cover: <carried-forward "Not covered" items | the changed hunks>.
Open items here (don't re-report them; say whether each is fixed, still open or changed): <ZA/ZP ids + titles>.
Owner decisions that bind here: <D-numbers, one line each>.
Leads from the pattern counts: <new hits in your files>.
<P agents: the scratch site is at http://127.0.0.1:5399; credentials in <run>/site/credentials.txt; you are P-n of
4, the browser is yours until you finish.>
Write to <run>/<agent-id>.md: findings (ZA schema), proposals (ZP schema), then the coverage block. Return at most
10 lines: counts by severity, the Critical/High titles, the number of proposals, anything you could not check.
```

## 4. Verify

- **Findings.** Every Critical and High, every finding a `fix` run will touch, every Low-confidence finding, and about
  a third of the Mediums (the riskiest) go to a fresh agent that did not find them. It gets the claim and the cited
  lines, not the finder's reasoning, and tries to disprove it: is the path reachable, does a caller already guard it,
  is it already an open finding, does a D-decision cover it. It returns confirmed, downgraded or dropped, with the
  reason. A finding that is not reproduced is dropped or downgraded. Low-confidence findings are verified or dropped.
- **Proposals.** Check that the feature does not already exist under another name (grep and the admin), that the
  Umbraco claim is right (a docs.umbraco.com link), and that no D-decision or declined proposal already covers it.
- Batch the verification: one verifier per unit or per lens, not one per finding.

## 5. Merge, calibrate, number

- One root cause is one finding: merge duplicates across units and sweeps and list the other places in it. Match each
  new finding against the open ones: the same root cause keeps the existing ID.
- Calibrate: the same kind of problem gets the same severity across agents (lenses.md, "Severity").
- Roll-ups: accessibility goes into the accessibility roll-up (ZA-128 while it is open), Known deviations from
  `Docs/Standards.md` are listed once, and small performance items join the current performance roll-up.
- Number new items from the report's next free `ZA` and `ZP` IDs. IDs are never reused.
- Trends: compare the §2 numbers with the last report.

## 6. Fix (mode = fix only)

Fix only when **all** of these hold: confidence High; severity Critical/High, or Medium with a concrete trigger; the
change is local (about 5 files or fewer); no public API, persisted format, schema or migration, auth, identity,
cookie or antiforgery configuration, new dependency, or user-visible workflow change; a security fix has a concrete
attack scenario. Proposals are never built in an audit.

For each fix: change the minimum → **grep every caller of any member, parameter or default you changed** → add the
regression test (`Docs/Standards.md` §9; remove a waiting `Skip`) → build → run the tests. If the build or the tests
fail and can't be repaired in scope, revert and mark the finding `not fixed — build/tests broke`. Never edit
migrations. Never run the app against the repository's `app.db` (the scratch site in product.md is fine). Everything
else stays open with a "why not fixed" line.

## 7. Report

Write `.audit/report.md` in this shape, in the plain, direct English of the existing report (one finding = a few lines):

```markdown
# ZauberCMS Audit Report

Audited commit: <sha> · Date: <UTC> · Scope: <…> · Mode: <…> · Umbraco reference: <version compared>
Build: PASS/FAIL · Warnings: N (prev M) · Tests: N (pass/fail/skip; providers run) · Vulnerable: N · Outdated majors: N
Next free IDs: ZA-NNN (findings) · ZP-NNN (proposals)
<after a later `/audit changed`: one "After Roadmap Phase X (<date>): …" line; the older ones are in history.md>

## Summary
| Severity | Open | New this run | Fixed this run | Resolved since last run |
Proposals: N open (N high value) · Trend: <pattern counts and hot-path budget, changes only>
**Work order and owner decisions: Docs/Roadmap.md.** <where the new items were assigned>

## Open findings
<by severity, then ID>
### ZA-NNN [Severity/Confidence] Title
- Where: `path:line` · Category: A–H · Unit: Un · Since: <date> · Roadmap: <phase | Backlog>
- Issue / Trigger: …
- Fix: … · Test: … · Blocked by: <public API | schema | owner decision | …>

## Improvement proposals
<grouped by section: Content, Media, Structure, Users and access, Settings, Front end, Installer, Developer experience>
### ZP-NNN [Value/Effort] Title
- Section: … · Lens: G | H · Since: <date> · Needs: <owner decision | design | nothing>
- Today: … · Proposal: … · Why: … (Umbraco: <docs link>)

## Umbraco alignment
| Area | Aligned | Different on purpose | Gaps (ZP ids) |

## Fixed this run / Resolved since last run
- ZA-NNN Title: what changed (commit). <this run only; history.md keeps every run>

## Deterministic checks
<the §2 table, one line each, with the previous value>

## Coverage
| Unit / agent | Files read fully | Skimmed | Not read (why) |
- Walkthrough: tasks done, and tasks not done with the reason.
- Not covered (carried to the next full run): …
```

## 8. History and hand-off

1. **`.audit/history.md`**: add a section at the top of "Fixed, resolved, closed and declined" for this run (items fixed
   or resolved, with how each was verified; proposals built or declined, with the owner's reason), and a line at the
   top of the run log. Never rewrite older entries.
2. **`Docs/Roadmap.md`**: run `git diff Docs/Roadmap.md` first (the owner edits it too). Update the Status line's open
   counts. Put each new finding in the phase whose theme fits, or in the Backlog with the reason. Add the
   high-value proposals to "Open questions for the owner", one line each with a recommendation; the full list stays
   in the report. Don't start fixing (except in `fix` mode).
3. **Declined proposals**: when the owner declines one, add a row to product.md "Different on purpose" so later runs
   don't raise it again.
4. Commit only as `AGENTS.md`'s working agreements allow, and only the audit's own files. Remove the scratch site
   and the run folder once the report is written.
5. Tell the user: counts by severity and the trend since the last run, the top 5 open findings, the top 5 proposals,
   what was not covered, and the decisions needed from them.
