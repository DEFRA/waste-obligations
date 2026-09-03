# ADR-0005: Page and search compliance declarations with a bounded public contract

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#65](https://github.com/DEFRA/waste-obligations/pull/65), [#68](https://github.com/DEFRA/waste-obligations/pull/68), [#119](https://github.com/DEFRA/waste-obligations/pull/119), [#170](https://github.com/DEFRA/waste-obligations/pull/170), [#171](https://github.com/DEFRA/waste-obligations/pull/171)

## Decision

Declaration list endpoints use page-number paging with a total, bounded page sizes, explicit Mongo ordering and validated filter/sort contracts. Regulator search is a bounded, case-insensitive partial match over declaration-time organisation fields.

## Current definition

`page` defaults to `1`; `pageSize` defaults to `20` and is constrained to `1`–`100`. Organisation/year reads order by `updated` descending then ID; sorting happens in Mongo before paging. The regulator route accepts optional obligation year, comma-separated status and registration type filters, `search`, and a priority-ordered `sort` list.

`search` is limited to 100 characters, regex-escaped, case-insensitive, and matches organisation name, compliance-scheme name, scheme-operator name, or reference number. Supported sort fields and directions are explicitly parsed, not passed through to Mongo.

## Consequences

- Page boundaries and totals have database, rather than process-memory, semantics.
- Query values outside the advertised JSON enum vocabulary are rejected instead of accepting numeric enum values.
- New public filters/sort fields require a deliberate contract, index and source-data review.

## Evidence

PRs #65 and #68 established the search endpoint. PR #119 moved ordering and paging into the Mongo query to avoid an unstable/incomplete page. PR #170 added the controlled sort grammar; PR #171 added the escaped organisation identifier/name search. `AGENTS.md` from PR #151 corroborates the string-bind, exact-JSON-enum validation pattern used by public query parameters.
