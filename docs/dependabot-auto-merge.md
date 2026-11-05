# Dependabot auto-merge and CDP publication

Dependabot selects updates and opens pull requests using [dependabot.yml](../.github/dependabot.yml). The scheduled [merge workflow](../.github/workflows/dependabot-auto-merge.yml) checks existing PRs; it does not select packages or create updates. It is disabled unless the repository variable `DEPENDABOT_AUTO_MERGE_ENABLED` is `true`.

## Eligibility

The PR must target `main` from this repository, be open and non-draft, and be authored by Dependabot. Every commit must be authored by Dependabot, have a verified signature and include its signed metadata. Every dependency in every commit must be classified as a minor or patch update. A major, missing, unknown or mixed classification stays manual. This applies equally to NuGet, Actions, Docker and npm metadata; an ecosystem must be configured in Dependabot before PRs can exist. Digest-only changes without a version classification remain manual.

The current PR head and current `main` must match the latest completed PR workflow's head and tested base. The Sonar scan waits for its own quality gate, and the SonarCloud Code Analysis check must exist and succeed. Every job, check run and commit status on that head must have succeeded; skipped, pending, neutral and failed results block automation. GitHub must also enforce strict up-to-date required Actions checks for `Dependency security review`, `Run Pull Request Checks` and `Run journey tests`. The automation approves the tested head and supplies its SHA to the merge API. GitHub rules, including reviews and conversation resolution, still apply.

Dependabot controls its own update schedules, grouping and exclusions. Its PR metadata is not a security guarantee: CI and dependency vulnerability scanning cannot detect every compromised package. PR checks execute dependencies and test code with CI credentials; their scope and access remain a trust boundary. The dependency security review blocks newly introduced high and critical known vulnerabilities and the image scan blocks high and critical findings. See [automation dependency pinning](automation-dependency-pinning.md) for other boundaries.

## Pacing and recovery

The scheduled scan runs every 15 minutes, and a manual dispatch may scan or retry a specific PR. Each run attempts at most one merge. A shared concurrency group serializes the queue's approval, merge and dispatch operations. Before each attempt, the current `main` must have a successful CDP publication, no later failed release, and no active or queued main or hotfix release. After a merge, the new `main` is unpublished, so the next PR waits for publication and fresh CI against that head.

GitHub does not start push workflows for a merge performed using `GITHUB_TOKEN`. After a successful bot merge, the workflow dispatches `Publish` on `main`. If the dispatch fails after the merge, manually dispatch `Merge Dependabot updates` with the merged PR number to recover; only a verified Dependabot PR merged by the Actions bot is accepted. Alternatively, dispatch `Publish` on `main` after inspecting any partial release. A failed or cancelled release stops the queue until it is inspected and successfully recovered.

Main and hotfix publishing jobs share `cdp-release-publish`, and the main workflow uses `publish-main`; both use `queue: max` and do not cancel running work. A superseded main run requests publication of current `main`. Human merges, hotfixes on older refs without this lock, and other release writers are not atomically reserved by the Dependabot queue. Coordinate those separately. GitHub does not guarantee schedule or queue timing. The scan is bounded to 50 candidates in the first 500 open PRs; retry an omitted PR explicitly. A PR whose merge GitHub rejects ends that scan and may delay later candidates.

## Activation and verification

Before enabling the variable, configure the strict required checks above, confirm Actions can approve PRs, configure `SONAR_TOKEN` as a Dependabot secret, and confirm that the CDP credentials and test secrets have appropriate scope. Ensure hotfix refs contain the shared publishing lock. Take a representative minor or patch PR through approval, squash merge and CDP publication, then confirm a second waits for publication and fresh CI. Confirm a major or unclassified update remains manual. These live checks are needed because local tests cannot prove GitHub rules, token permissions, event delivery or CDP behaviour.

The Node tests exercise signed grouped metadata, CI and base freshness, failed checks, merge serialization, release failures and recovery through substituted GitHub API boundaries. They do not certify package content or replace a live rollout test.
