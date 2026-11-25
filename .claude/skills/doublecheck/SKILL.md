---
name: doublecheck
description: "Thorough review and verification of all code changes. Use when the user says /doublecheck, 'double check', 'review your work', 'verify changes', or asks to make sure nothing was missed after a set of code changes. Traces through all modifications, validates correctness, checks for bugs, and builds the solution."
---

# Double Check

Thoroughly review and verify all code changes made in this session.

## Process

1. **Trace through every change**: Re-read each modified file. Walk through the logic path from entry point to exit for every change. Confirm each change matches the original request.

2. **Verify completeness**: Compare what was requested against what was implemented. Flag anything missing or incomplete.

3. **Check for bugs and issues**:
   - Thread safety problems (shared mutable state, race conditions)
   - Memory leaks (undisposed resources, event handler leaks, missing `using` statements)
   - Async issues (missing `await`, deadlocks, fire-and-forget without error handling)
   - Performance issues (N+1 queries, unnecessary allocations, blocking calls in async paths)
   - Null reference risks
   - Off-by-one errors
   - Error handling gaps

4. **Check for linter errors and warnings**: Review code style, unused imports, unused variables, naming conventions, and any compiler warnings. Fix any found issues whether pre-existing or newly introduced.

5. **Check against the project rules**: walk the Definition of Done in `AGENTS.md` and the relevant sections of
   `Docs/Standards.md` (public API / persisted type names, soft-delete filters, `SaveChangesAndLog`, three migrations,
   render modes, cancellation tokens). For every member, parameter or default you changed, grep all callers.

6. **Docs**: if behaviour, an extension point or a documented fact changed, update `AGENTS.md` / `Docs/*.md` in the
   same change.

7. **Build the solution**: `dotnet build ZauberCMS.sln -c Release` once all changes are finalized. Fix any errors or
   new warnings.
