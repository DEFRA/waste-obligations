# PR coverage and candidate disposition

Assessment date: **23 September 2026**. This is a concise accountability record, not a claim that every historical diff was substantively reviewed.

[PR #188](https://github.com/DEFRA/waste-obligations/pull/188), commit `058c00d`, is open and unmerged. Its stated 71-PR catalogue was reviewed as input, then reconciled with later history through PR #263.

| ADRs | Principal PR evidence |
| --- | --- |
| 0001–0006 | #3, #9, #18, #20, #25, #33, #39, #43, #50, #56, #59, #63, #65, #68, #70, #93, #97, #119, #141, #150, #152, #154, #170, #171 |
| 0007–0010 | #52, #98, #99, #108, #141, #142, #149, #159–#161, #178, #179, #194, #238 |
| 0011–0013 | #5, #9, #140, #148, #151, #158, #180, #183, #263 |
| 0014–0019 | #83, #183–#187, #217, #219, #220, #225, #226, #229–#233, #239, #241, #243 |
| 0020 | #246 |

## Supporting and excluded clusters

- #100, #102, and #104 are analytics child PRs cited by the original candidate catalogue and support ADR 0008.
- #184–#187, #217, #219, #229–#233, and #241 evolve projection/worker operation; they support ADRs 0014–0015 rather than each establishing a separate ADR.
- #243 supports health evolution in ADR 0017. #244 is a narrow cancellation-recipient repair. #245 supports PRN fixture alignment only.
- #247, #249–#251, #256, and #259–#261 are dependency/mechanical changes. Formatting, Sonar, snapshot-only, branch-sync, and logging-only changes are excluded unless cited as supporting evidence.

## Unavailable or unresolved evidence

The supplied material does not establish an explicit CompanyName business rationale, a settled `/health/all` exposure policy, dead-letter remediation, or a rationale for every schema increment. PRs not listed here are not implicitly classified as fully reviewed.
