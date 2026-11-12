# BIM Insight read-capability fork

Maintained in `7zgfpgfg65-bit/rvt-mcp`, branch `feature/category-id-statistics`.
Base: upstream master `e49c7961441a306490e1da1dc99ea179c3f31035`.
This is a development fork, not an upstream release or an installed/validated Revit deployment.
Apache-2.0 and existing copyright/attribution remain in place. Changed source files carry
modification notices; new files identify the fork author. No upstream NOTICE file was present.

## Compatible statistics extension

`revit_analyze_model_statistics(maxElements)` keeps the existing input and legacy output
fields. The old `categories` array remains display-name-grouped for existing clients.
New clients must use the following additive fields:

| Field | Meaning |
|---|---|
| categoryIdCountsSchemaVersion | 1 |
| categoryIdCounts | Sorted rows: categoryId (64-bit), category (display name), count |
| categorizedElementsCounted | Sum of all categoryIdCounts.count values |
| uncategorizedElementsCounted | Count of elements whose Category is null, kept separately |
| categoryIdCountsComplete | False whenever the element enumeration cap stopped collection |
| categoryIdCountsScope | Host document, non-type categorized instances, model/annotation categories, no linked contents |
| document | documentId, path, title, revitVersion for the document actually enumerated |

`elementsCounted = categorizedElementsCounted + uncategorizedElementsCounted`.
Different category IDs with identical labels stay separate. A real category named
`Uncategorized` cannot merge with null categories in the new array. Negative and 64-bit IDs
are preserved. Unknown/missing category names for non-null categories fail instead of
publishing invented labels. Empty input is complete with an empty array.

The existing element cap remains: clients must reject truncated/incomplete counts. This
does not introduce cursor paging. Oversized responses remain subject to existing response
guards; a guard error must not be treated as a partial successful report. Separate MCP calls
are not an atomic snapshot; keep the model unchanged during collection.

## Read-only document identity

The fork base already includes `revit_get_document_state(scope="active"|"all")`, exposed in
the meta toolset and annotated read-only. It returns `documents[].path`, `document_id`,
`is_active`, `host_year`, state/version information and warnings. Reuse this existing tool;
do not add another competing identity command or invoke open_model just to discover a path.
Unsaved models may have an empty path; consumers requiring absolute file identity must reject
that case. Runtime document tokens change across reopen and are not persistent file IDs.

## Development verification

Revit 2027 shell build uses the real Revit API reference packages, with deployment disabled:

```powershell
dotnet build src/plugin-r27/RvtMcp.Plugin.R27.csproj -c Release -p:RvtMcpSkipDeploy=true
dotnet test tests/RvtMcp.Tests/RvtMcp.Tests.csproj -c Release -p:RvtMcpSkipDeploy=true --filter 'FullyQualifiedName~CategoryStatistics|FullyQualifiedName~GetDocumentStateHandlerTests|FullyQualifiedName~DocumentToolSurfaceTests|FullyQualifiedName~DocumentToolShellTests'
```

Observed: Revit 2027 build passed, 0 warnings/errors; 27 focused cases passed. Production
handler tests use an explicitly fake Revit boundary to check same-name category separation,
annotation inclusion, type/link-content exclusion, cap behavior and identity fields. Pure
aggregation tests cover empty input, exact cap, overflow cap, >1000 elements, 64-bit IDs and
inconsistent names. Document state tests verify path/token fields and existing read-only
tool exposure. These do not establish live Revit behavior.

The full upstream suite has environment/baseline failures on this machine (including symlink
privilege, Thai-calendar dates and culture-sensitive string assertions). The untouched base
was tested separately: 3639 passed, 28 failed. Do not claim the whole upstream suite is green.
Final fork comparison is recorded in the validation update below.

Worker contract tests are in the separate RevitWorkerCodex repository. The worker recognizes
only the explicit new schema/scope/completeness fields; legacy responses still fail the full
category requirement. Partial reports remain available while deployment is pending.

## Deployment boundary and next live checks

No installer, client configuration or installed add-in was changed by this development task.
Before deploying, identify matching server/plugin artifacts, record hashes, preview exact
destinations/backups, close Revit normally and obtain the separate installation approval
required by AGENTS.md. Do not load both fork and upstream add-ins into the same Revit session.
Use a fork-specific informational version (for example 1.2.2-biminsight.1) on BOTH components.

After approved deployment, rediscover tools and verify health/version alignment; use the
read-only document-state tool to bind the supplied RVT path to its active token. Compare
category totals, door/room lists and selected measurements with independently checked schedules.
Test a same-name-category fixture and caps; confirm no model save/edit and record limitations.
Do not declare full Phase 1 acceptance based on build/mock tests alone.

## Final local validation — 2026-11-12

- Fork full suite: 3650 passed, 28 failed, total 3678.
- Untouched upstream baseline: 3639 passed, 28 failed, total 3667.
- Compared TRX failed-test names: all 28 identical, zero differences. Added 11 tests pass.
- Focused category/document tool suite: 27 passed.
- Server and Revit 2027 plugin Release builds: 0 warnings, 0 errors, using
  `-p:RvtMcpSkipDeploy=true -p:Version=1.2.2-biminsight.1`.
- Built server initialized over stdio and tools/list completed in read-only query,meta mode.
  `revit_get_document_state` and `revit_analyze_model_statistics` are available. No Revit
  tools were invoked by this metadata check. Evidence is local artifacts/fork-tools.json.
- Worker: 54 tests passed; new category-contract tests are synthetic until live fork deployment.

Full-suite failures remain an explicit baseline limitation; this does not certify the fork
for production. Installed server/plugin/client configuration are unchanged. Built assemblies
are under src/server/bin/Release/net8.0 and src/plugin-r27/bin/Release/net10.0-windows7.0.
