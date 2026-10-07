# Dependabot approval and merging

`Merge Dependabot updates` uses the built-in `GITHUB_TOKEN` to approve and
squash-merge verified Dependabot minor/patch updates. It needs no GitHub App or
personal access token. It is disabled until the repository variable
`DEPENDABOT_AUTO_MERGE_ENABLED` is set to the string `true`.

The automation permits one merge per successfully published `main` revision.
Further approvals wait for that merge's CDP release to finish. A release failure
or a merge whose publication was never dispatched pauses the queue.

## Enabling

1. Merge these changes into `main`. Retain the repository setting that allows
   GitHub Actions to create and approve pull requests. This setting was already
   enabled when the proposal was inspected.
2. In the `main` ruleset, require `Run Pull Request Checks` and `Run journey
   tests`, both from GitHub Actions, and enable **Require branches to be up to
   date before merging**. Retain approvals, signed commits, resolved review
   conversations and stale-approval dismissal. Give the bot no bypass.
3. Confirm the current `main` revision has a successful `CDP-publish-workflow`
   job and no later failed/cancelled build or publishing attempt. Include the shared publishing
   lock in any branch used for **Publish Hot Fix** before running that workflow.
4. Set `DEPENDABOT_AUTO_MERGE_ENABLED=true`. Run **Merge Dependabot updates** on
   `main` with no PR number to scan existing PRs, or let CI/release completion
   and the scheduled scan pick them up.

The inspected ruleset required the two jobs but had the up-to-date policy
disabled. The script checks this policy itself before approving. Setting the
variable alone therefore cannot enable merging yet.

A PR's tested base SHA must match current `main`. Dependabot must rebase and
complete fresh CI whenever `main` changes. The workflow does not update PR
branches or queue merges against outdated checks.

SonarCloud is optional for PR merging and publication. The exact Sonar workflow
job and `SonarCloud Code Analysis` check are excluded from the approval gate.
Main and hotfix publishing depend on their build job; Sonar runs independently.
A release workflow can therefore be red solely because of Sonar even when CDP
published successfully. The release gate checks the actual jobs in that case;
any failed build or publishing job still pauses approvals. The queue waits for
the entire release workflow to finish, so a running Sonar scan can still delay
the next approval even though its result is optional.

## Eligibility and trust

The privileged job checks out the workflow's trusted `main` commit, with Git
credentials disabled. It never checks out or runs a PR's code or downloads its
artifacts. PR CI runs separately with its existing permissions and secrets.

An eligible PR is open, non-draft, authored by `dependabot[bot]`, hosted in this
repository and targeting `main`. Every commit must have a verified signature,
Dependabot authorship and one canonical metadata block containing only
`version-update:semver-minor` or `version-update:semver-patch`. A major update in
a group keeps the whole PR manual. Missing or unfamiliar metadata also keeps
the PR manual; the current AWS CLI `latest` digest updates have no update type.

Only the latest PR CI run and attempt for the current head are eligible. Both
required jobs must exist and succeed. Missing, pending, failed, cancelled,
neutral or skipped non-Sonar jobs/checks block approval. Other non-Sonar check
runs and commit statuses must also succeed. The overall PR CI conclusion is
not the gate because an optional Sonar failure can make it red.

Approval attaches to the tested head SHA. The merge API receives that SHA and
obeys GitHub's rules. GitHub rejects an intervening head change or outdated
base; the workflow uses no admin override.

## Approval frequency and CDP versioning

Every CI completion, main/hotfix release completion and 15-minute scheduled
fallback scans the same open Dependabot queue, oldest first. Each scan reads at
most 500 open PRs and considers at most 50 Dependabot candidates; an omitted PR
can be retried by its number. It skips ineligible PRs and stops after at most one approval/merge attempt. A repository-wide
`dependabot-auto-merge` job lock covers verification, approval, merge and the
publication dispatch. Competing events cannot approve different PRs in parallel.

Immediately before approving, the gate requires:

- no non-completed **Publish** or **Publish Hot Fix** run anywhere in the
  repository, including queued, requested, pending or waiting runs;
- actual successful publication of the current `main` SHA, with no later
  unresolved failed/cancelled hotfix release;
- fresh PR CI tested against that same `main` SHA.

A successful workflow with only a skipped publishing job does not establish
publication. A later failed/cancelled Publish run or attempt blocks the gate
even if that revision was published earlier. A failure solely from optional
Sonar is excluded after verifying all other release jobs succeeded or skipped.
Actual successful publication is still required. A successful duplicate run that
skips because the revision is already published can retain the earlier success.
Release recency uses completion/update time, including reruns of older runs. A
failed hotfix after the latest successful main Publish pauses approvals until a
successful hotfix publication or an inspected, successful main Publish recovery.
A skipped hotfix publishing job cannot clear an earlier failure.

After one bot merge, `main` points to an unpublished SHA. This closes the gap
between merging and dispatching: another event cannot approve the next PR even
if the dispatch is delayed or lost. The next PR waits for actual publication
and fresh CI, rather than an arbitrary delay. The 15-minute fallback is for
missed events; GitHub does not guarantee its timing.

The pinned CDP build action calculates a version, builds images, then writes
the release tag and pushes build stages. The publishing jobs in **Publish** and
**Publish Hot Fix** share the `cdp-release-publish` concurrency group for the
whole composite action. This serialises their version calculation, image writes
and tagging. Main Publish also retains its outer `publish-main` workflow lock.
Hotfix refs must contain this lock; older refs cannot acquire a lock absent
from their workflow definition.

Human merges can still advance `main` during a release. Other tag/image writers
outside these workflows must coordinate separately. The release checks and a
human starting a workflow are separate API operations, so they are not an atomic
reservation. The shared publishing lock still prevents these two CDP publishing
jobs from writing concurrently.

## Publishing and recovery

GitHub suppresses push-triggered workflows when `GITHUB_TOKEN` performs a merge.
After GitHub confirms the merge, this workflow explicitly dispatches **Publish**
on `main`. The run uses its captured `github.sha` consistently for checkout,
Sonar analysis, image labels and release tagging. A later human merge can be
included in that captured revision.

GitHub concurrency keeps one running job and one pending job per group. A new
pending job can replace an older pending job; `cancel-in-progress: false` does
not make a durable FIFO queue. Scanning on all relevant events and on a schedule
recovers lost merge events. A superseded queued main Publish run requests
publication of current `main`. A publishing job displaced by a hotfix can leave
main unpublished; the approval gate then stays closed until publication is
recovered. No fixed cooldown or automatic retry conceals a release failure.

Publication deduplication looks for a successful `CDP-publish-workflow` job,
including earlier attempts of the same run. It avoids rerunning successful
release writes. An already active publication is left to finish; inspect its
result before retrying.

If a timeout, cancellation or GitHub failure occurs between merging and
dispatching, run **Merge Dependabot updates** manually on `main` with the merged
PR's number. Recovery accepts only a verified minor/patch PR merged by
`github-actions[bot]`. It performs no new approval or merge and can dispatch
missing publication while the approval gate is closed. The publishing lock
also applies to this recovery. Alternatively, dispatch **Publish** on `main`.

If Publish fails, inspect its logs and any partial image/tag writes before
rerunning it or using the PR recovery path. Publication is not a transaction:
the upstream action can fail after an image or tag is written. A retry can
therefore leave additional release versions. A successful dispatch alone does
not prove an image was published. A later failed duplicate publication after
a successful one needs investigation: deduplication preserves that successful
release, while the approval gate remains closed for the later failure. After
inspection, dispatch Publish directly: a successful deduplicated run can restore
the gate without writing another release.

Set `DEPENDABOT_AUTO_MERGE_ENABLED=false` to stop new automation runs. Already
running jobs need cancellation separately. Merged PRs are not rolled back.

## Verification and stability

The Node tests exercise the real policy with substituted GitHub API boundaries:
minor/patch and major/unknown groups, signatures/authors, failed or missing PR
checks, optional Sonar failures, stale heads/bases/attempts, strict protection,
blocked merges, all active release states, skipped/failed publications, one
merge per published head, queue scanning and merged-PR recovery. These tests
run in `Run Pull Request Checks` alongside existing Compose-helper tests.
Workflow syntax is checked locally with actionlint.

Before enabling, take one eligible Dependabot PR through the real GitHub path
and confirm its bot approval, squash merge and successful CDP publication. Then
confirm a second PR waits for publication and fresh CI. Check a failing and a
major update receive no approval or merge. Local API-boundary tests do not prove
GitHub token policy, ruleset enforcement, concurrency delivery, OIDC or the
upstream release action's behaviour.

This design pauses on uncertain or failed checks/publication. Its tradeoff is
lower throughput and occasional manual release recovery. Automatic approval is
a policy decision backed by CI; it does not replace a human assessment of a
dependency's behaviour or release notes.
