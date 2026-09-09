# Retrospective architecture decision records

This directory records enduring decisions reconstructed from the repository as it stood on `main` at `45e58665f5e1ac70f6a65928661150a30a41bd06` (26 August 2026).

They are retrospective ADRs: they document the current, evidenced decision rather than claiming to reproduce a contemporaneous decision meeting. The linked pull requests, retained code, tests, schemas and documentation are the evidence. A PR description is treated as explicit rationale; conclusions from code or the sequence of changes are identified as inferences.

## Reading an ADR

- **Status** is `Accepted (retrospective)` when the decision is present on `main`.
- **Confidence** is `High` when an implementation and PR description agree, or `Medium` when the implementation is clear but the rationale is inferred.
- **Source PRs** are the most relevant evidence, not an exhaustive changelog. PR links refer to GitHub's permanent PR record.
- “Current definition” describes observable behaviour at the reviewed `main` commit. It does not make undocumented future behaviour contractual.

## Decision catalogue

| ADR | Decision |
| --- | --- |
| [0001](0001-keep-presentation-and-organisation-data-at-service-boundaries.md) | Keep presentation and organisation enrichment outside the obligations response boundary. |
| [0002](0002-persist-compliance-declarations-in-mongo.md) | Persist compliance declarations in MongoDB with orderable identifiers, UTC storage and query-aligned indexes. |
| [0003](0003-store-submission-snapshots.md) | Store the organisation and submitter facts supplied at declaration submission as a declaration snapshot. |
| [0004](0004-model-declaration-lifecycle-and-concurrency.md) | Model declaration status as a controlled lifecycle with embedded business audit history and optimistic concurrency. |
| [0005](0005-page-and-search-compliance-declarations.md) | Provide bounded, deterministic paging and regulator search for compliance declarations. |
| [0006](0006-version-persisted-contracts-and-migrate-forward.md) | Version persisted/analytics declaration contracts and evolve them through forward migrations. |
| [0007](0007-publish-analytics-through-a-transactional-outbox.md) | Record analytics changes in a transactional Mongo outbox and publish asynchronously with leases and retries. |
| [0008](0008-send-best-effort-notifications.md) | Send best-effort, recipient-specific Gov.uk Notify messages after declaration state changes commit. |
| [0009](0009-adapt-prn-common-backend-behind-an-organisation-api.md) | Expose PRNs through an organisation-scoped adapter over PRN Common Backend. |
| [0010](0010-retry-bounded-mongo-transactions.md) | Retry only retryable Mongo transaction failures within a bounded transaction budget. |
| [0011](0011-route-and-protect-outbound-token-requests.md) | Route outbound service/token traffic through CDP proxy support and protect token acquisition with a dedicated resilience pipeline. |
| [0012](0012-operate-with-layered-health-and-observability.md) | Separate readiness from diagnostic health and instrument key external operations. |
| [0013](0013-restrict-operational-deletion-by-client.md) | Keep destructive declaration deletion hidden and allow-listed per client. |
| [0014](0014-store-obligation-coverage-as-a-derived-value.md) | Persist a capped, whole-number obligation coverage percentage at submission and backfill it. |

## Review coverage

The review considered all 71 merged non-Dependabot PRs available in GitHub: 68 whose base branch was `main`, and PRs 100, 102 and 104 which merged into the analytics feature branch before that branch was merged to `main` in PR 99. Each PR is assigned below to its primary retrospective outcome; an ADR can cite a PR in more than one context where warranted.

| Primary outcome | PRs reviewed |
| --- | --- |
| ADR 0001 | [#3](https://github.com/DEFRA/waste-obligations/pull/3), [#9](https://github.com/DEFRA/waste-obligations/pull/9), [#43](https://github.com/DEFRA/waste-obligations/pull/43) |
| ADR 0002 | [#12](https://github.com/DEFRA/waste-obligations/pull/12), [#18](https://github.com/DEFRA/waste-obligations/pull/18), [#20](https://github.com/DEFRA/waste-obligations/pull/20), [#25](https://github.com/DEFRA/waste-obligations/pull/25), [#35](https://github.com/DEFRA/waste-obligations/pull/35), [#39](https://github.com/DEFRA/waste-obligations/pull/39), [#56](https://github.com/DEFRA/waste-obligations/pull/56) |
| ADR 0003 | [#33](https://github.com/DEFRA/waste-obligations/pull/33), [#70](https://github.com/DEFRA/waste-obligations/pull/70), [#93](https://github.com/DEFRA/waste-obligations/pull/93), [#97](https://github.com/DEFRA/waste-obligations/pull/97) |
| ADR 0004 | [#34](https://github.com/DEFRA/waste-obligations/pull/34), [#50](https://github.com/DEFRA/waste-obligations/pull/50), [#59](https://github.com/DEFRA/waste-obligations/pull/59), [#63](https://github.com/DEFRA/waste-obligations/pull/63), [#69](https://github.com/DEFRA/waste-obligations/pull/69), [#154](https://github.com/DEFRA/waste-obligations/pull/154) |
| ADR 0005 | [#65](https://github.com/DEFRA/waste-obligations/pull/65), [#68](https://github.com/DEFRA/waste-obligations/pull/68), [#119](https://github.com/DEFRA/waste-obligations/pull/119), [#170](https://github.com/DEFRA/waste-obligations/pull/170), [#171](https://github.com/DEFRA/waste-obligations/pull/171) |
| ADR 0006 | [#134](https://github.com/DEFRA/waste-obligations/pull/134), [#142](https://github.com/DEFRA/waste-obligations/pull/142), [#149](https://github.com/DEFRA/waste-obligations/pull/149) |
| ADR 0007 | [#99](https://github.com/DEFRA/waste-obligations/pull/99), [#100](https://github.com/DEFRA/waste-obligations/pull/100), [#102](https://github.com/DEFRA/waste-obligations/pull/102), [#104](https://github.com/DEFRA/waste-obligations/pull/104), [#106](https://github.com/DEFRA/waste-obligations/pull/106), [#113](https://github.com/DEFRA/waste-obligations/pull/113) |
| ADR 0008 | [#52](https://github.com/DEFRA/waste-obligations/pull/52), [#98](https://github.com/DEFRA/waste-obligations/pull/98), [#108](https://github.com/DEFRA/waste-obligations/pull/108), [#112](https://github.com/DEFRA/waste-obligations/pull/112), [#144](https://github.com/DEFRA/waste-obligations/pull/144), [#155](https://github.com/DEFRA/waste-obligations/pull/155), [#159](https://github.com/DEFRA/waste-obligations/pull/159), [#178](https://github.com/DEFRA/waste-obligations/pull/178), [#179](https://github.com/DEFRA/waste-obligations/pull/179) |
| ADR 0009 | [#5](https://github.com/DEFRA/waste-obligations/pull/5), [#140](https://github.com/DEFRA/waste-obligations/pull/140), [#148](https://github.com/DEFRA/waste-obligations/pull/148), [#151](https://github.com/DEFRA/waste-obligations/pull/151), [#158](https://github.com/DEFRA/waste-obligations/pull/158) |
| ADR 0010 | [#107](https://github.com/DEFRA/waste-obligations/pull/107), [#160](https://github.com/DEFRA/waste-obligations/pull/160), [#161](https://github.com/DEFRA/waste-obligations/pull/161) |
| ADR 0011 | [#6](https://github.com/DEFRA/waste-obligations/pull/6), [#7](https://github.com/DEFRA/waste-obligations/pull/7), [#8](https://github.com/DEFRA/waste-obligations/pull/8), [#180](https://github.com/DEFRA/waste-obligations/pull/180) |
| ADR 0012 | [#1](https://github.com/DEFRA/waste-obligations/pull/1), [#2](https://github.com/DEFRA/waste-obligations/pull/2), [#37](https://github.com/DEFRA/waste-obligations/pull/37), [#38](https://github.com/DEFRA/waste-obligations/pull/38), [#156](https://github.com/DEFRA/waste-obligations/pull/156), [#157](https://github.com/DEFRA/waste-obligations/pull/157), [#172](https://github.com/DEFRA/waste-obligations/pull/172) |
| ADR 0013 | [#83](https://github.com/DEFRA/waste-obligations/pull/83) |
| ADR 0014 | [#141](https://github.com/DEFRA/waste-obligations/pull/141), [#150](https://github.com/DEFRA/waste-obligations/pull/150), [#152](https://github.com/DEFRA/waste-obligations/pull/152) |
| Reviewed; no enduring architecture decision | [#4](https://github.com/DEFRA/waste-obligations/pull/4) documentation correction; [#13](https://github.com/DEFRA/waste-obligations/pull/13) Dependabot grouping experiment; [#101](https://github.com/DEFRA/waste-obligations/pull/101) workflow trigger; [#117](https://github.com/DEFRA/waste-obligations/pull/117) dependency advisory update; [#118](https://github.com/DEFRA/waste-obligations/pull/118) documentation addition. |

The catalogue intentionally does not turn every endpoint, implementation refactor or routine dependency change into an ADR. It records a decision only where later code depends on a stable constraint, boundary, definition or operational policy.

## `AGENTS.md` correlation

`AGENTS.md` began in [PR #99](https://github.com/DEFRA/waste-obligations/pull/99) and was changed in PRs [#108](https://github.com/DEFRA/waste-obligations/pull/108), [#140](https://github.com/DEFRA/waste-obligations/pull/140), [#142](https://github.com/DEFRA/waste-obligations/pull/142), [#148](https://github.com/DEFRA/waste-obligations/pull/148), [#149](https://github.com/DEFRA/waste-obligations/pull/149), [#151](https://github.com/DEFRA/waste-obligations/pull/151), and [#156](https://github.com/DEFRA/waste-obligations/pull/156). Its historical changes corroborate, rather than replace, the source evidence:

| Formalised guidance | Correlated ADRs | Interpretation |
| --- | --- | --- |
| Mongo migrations need a distributed lease, need rollout compatibility, and must retain historical audit events. Added with PR #99. | 0006, 0007, 0010 | Confirms that deployment and historical-event compatibility are deliberate constraints, not incidental implementation details. |
| A persisted shape change must be classified, versioned major/minor, migrated with raw BSON, tested across source states, and documented. Added in PR #142 and expanded with an immutable-schema changelog step in PR #149. | 0006, 0014 | Direct corroboration of the schema-evolution decision. |
| Integration clients return their own models; endpoint/application code maps them to public DTOs. Added in PR #140. | 0001, 0009 | Direct corroboration of the anti-coupling integration boundary. |
| Public single-enum query values are bound as strings and validated against the JSON values; list inputs use a dedicated comma-separated validator. Added in PR #151 after the PRN search work. | 0005, 0009 | Confirms that public query contracts must not accidentally accept numeric enum values. |
| PR #148 additionally requires comparison with the nearest existing endpoint/request/validation/OpenAPI pattern before introducing a one-off variant. | 0005, 0009 | Explains the consistent endpoint and validation patterns seen in the later search/PRN work. |

The remaining recorded `AGENTS.md` changes in PRs #108 and #156 are coding/test-style rules. They are useful maintenance guidance but are not evidence of a separate architectural decision, so they are not promoted to ADRs.
