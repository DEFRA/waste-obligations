import { readMinorOrPatchUpdates } from './dependabot-metadata.mjs';
import { queueOutcome, scopedCandidateCore, writeQueueSummary } from './dependabot-queue-reporting.mjs';

const requiredJobs = ['Dependency security review', 'Run Pull Request Checks', 'Run journey tests'];
const sonarJob = 'CDP SonarCloud Scan / CDP SonarCloud coverage scan';
const publishJob = 'CDP-publish-workflow';
const publishWorkflow = 'publish.yml';
const mainBranch = 'main';
const maxQueueCandidates = 50;
const maxQueuePages = 5;
const githubActionsIntegrationId = 15368;
const githubActionsBot = 'github-actions[bot]';
const methodNotAllowedStatus = 405;
const conflictStatus = 409;
const blockedMergeStatuses = new Set([methodNotAllowedStatus, conflictStatus]);
const rebaseMarker = '<!-- dependabot-queue-rebase -->';
const rebaseCooldownMs = 24 * 60 * 60 * 1000;
const recoveryRequested = 'recovery-requested';

// Accept the canonical signed Dependabot metadata format; unfamiliar formats stay manual.
export function hasOnlyMinorOrPatchUpdates(message) {
  return readMinorOrPatchUpdates(message) !== undefined;
}

function isDependabotPullRequest(pr, repository) {
  return pr.user?.login === 'dependabot[bot]' && pr.base.ref === mainBranch
    && pr.base.repo.full_name === repository && pr.head.repo?.full_name === repository;
}

function skip(core, reason) {
  core.notice(reason);
}

export async function resolvePullRequest({ github, context, core }) {
  const input = context.eventName === 'workflow_dispatch'
    ? context.payload.inputs?.['pull-request-number'] : undefined;
  if (!['workflow_run', 'workflow_dispatch', 'schedule'].includes(context.eventName)) {
    return;
  }

  if (!input) {
    core.setOutput('scan_queue', 'true');

    return;
  }
  if (!/^[1-9]\d*$/.test(input)) {
    throw new Error('Enter a valid pull request number.');
  }

  const number = Number(input);
  const { data: pr } = await github.rest.pulls.get({ ...context.repo, pull_number: number });
  if (!isDependabotPullRequest(pr, `${context.repo.owner}/${context.repo.repo}`)) {
    return;
  }

  core.setOutput('pull_number', number);
}

function compareRunRecency(a, b) {
  return (Date.parse(a.updated_at ?? '') || 0) - (Date.parse(b.updated_at ?? '') || 0) || a.id - b.id;
}

function isSuccessfulReleaseRun(run) {
  return run?.status === 'completed' && run.conclusion === 'success';
}

async function hasActiveRelease(github, repository) {
  const statuses = ['in_progress', 'queued', 'requested', 'waiting', 'pending'];
  for (const status of statuses) {
    const activeRuns = await github.paginate(github.rest.actions.listWorkflowRunsForRepo, {
      ...repository, status, per_page: 100
    });
    if (activeRuns.some(x => ['.github/workflows/publish.yml', '.github/workflows/publish-hotfix.yml'].includes(x.path))) {
      return true;
    }
  }

  return false;
}

async function hasMainPublication(github, repository, mainRuns) {
  for (const run of mainRuns) {
    if (await hasPublished(github, repository, run)) {
      return true;
    }
  }

  return false;
}

async function hasRecoveredHotfixes(github, repository, latestMainRun, core) {
  const hotfixRuns = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...repository, workflow_id: 'publish-hotfix.yml', status: 'completed', per_page: 100
  });
  for (const hotfix of hotfixRuns.sort((a, b) => compareRunRecency(b, a))) {
    // A later successful main validation acknowledges inspected recovery.
    if (compareRunRecency(hotfix, latestMainRun) <= 0) {
      return true;
    }
    if (!isSuccessfulReleaseRun(hotfix)) {
      skip(core, 'A hotfix release failed after main publication; inspect and recover the release before approving.');

      return false;
    }
    if (await hasPublished(github, repository, hotfix, 'CDP-publish-hotfix-workflow')) {
      return true;
    }
  }

  return true;
}

// Successful publication is the pacing boundary, including the merge-to-dispatch gap.
async function hasReleaseCapacity(github, repository, mainSha, core) {
  if (await hasActiveRelease(github, repository)) {
    skip(core, 'A main or hotfix release is active or queued; leave further approvals until it finishes.');

    return false;
  }

  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...repository, workflow_id: publishWorkflow, head_sha: mainSha, per_page: 100
  });
  const mainRuns = runs.filter(x => x.head_branch === mainBranch).sort((a, b) => compareRunRecency(b, a));
  // A later failed/cancelled attempt must not inherit an earlier successful release.
  if (!isSuccessfulReleaseRun(mainRuns[0])
    || !await hasMainPublication(github, repository, mainRuns)) {
    skip(core, 'Current main has no successful publication, or a later publication failed. Recover that release first.');

    return false;
  }

  return hasRecoveredHotfixes(github, repository, mainRuns[0], core);
}

export async function processDependabotQueue({ github, context, core, pullNumber }) {
  if (pullNumber) {
    // Explicit recovery of an already merged PR must work while main is unpublished.
    const candidateCore = scopedCandidateCore(core, context.repo, pullNumber);
    await mergeDependabotPullRequest({ github, context, core: candidateCore, pullNumber });

    return;
  }

  const { data: main } = await github.rest.repos.getBranch({ ...context.repo, branch: mainBranch });
  core.notice(`Scanning Dependabot updates against main ${main.commit.sha}.`);
  if (!await hasReleaseCapacity(github, context.repo, main.commit.sha, core)) {
    await writeQueueSummary(core, context.repo, main.commit.sha, [], 'Waiting for publication capacity.');
    return;
  }

  const candidates = await listCandidates(github, context.repo, core);
  await scanCandidates({ github, context, core, candidates, mainSha: main.commit.sha });
}

async function listCandidates(github, repository, core) {
  const candidates = [];
  for (let page = 1; page <= maxQueuePages; page++) {
    const { data: prs } = await github.rest.pulls.list({
      ...repository, state: 'open', base: mainBranch, sort: 'created', direction: 'asc', per_page: 100, page
    });
    candidates.push(...prs.filter(x => isDependabotPullRequest(x, `${repository.owner}/${repository.repo}`)));
    if (candidates.length >= maxQueueCandidates || prs.length < 100) {
      break;
    }
    if (page === maxQueuePages) {
      core.notice('Queue scan reached 500 open PRs; retry an omitted PR by number.');
    }
  }
  if (candidates.length >= maxQueueCandidates) {
    core.notice('Queue scan is limited to 50 Dependabot candidates; retry an omitted PR by number.');
  }

  return candidates;
}

async function scanCandidates({ github, context, core, candidates, mainSha }) {
  const results = [];
  try {
    for (const pr of candidates.slice(0, maxQueueCandidates)) {
      const candidateCore = scopedCandidateCore(core, context.repo, pr.number);
      candidateCore.notice(`Inspecting head ${pr.head.sha}.`);
      let result;
      try {
        result = await mergeDependabotPullRequest({ github, context, core: candidateCore, pullNumber: pr.number });
      } catch (error) {
        candidateCore.notice('Processing failed; see the workflow error for details.');
        results.push({ number: pr.number, head: pr.head.sha, outcome: 'Error', details: candidateCore.messages.slice(1) });
        throw error;
      }
      const outcome = queueOutcome(result);
      candidateCore.notice(`Decision: ${outcome}.`);
      results.push({ number: pr.number, head: pr.head.sha, outcome, details: candidateCore.messages.slice(1, -1) });
      if (result) {
        break;
      }
    }
  } finally {
    await writeQueueSummary(core, context.repo, mainSha, results);
  }
}

function matchesRunAttempt(latest, runId, runAttempt) {
  return latest !== undefined && (!runId || (latest.id === runId && latest.run_attempt === runAttempt));
}

function recordsCurrentPullRequest(run, pr) {
  const recordedPr = run.pull_requests?.find(x => x.number === pr.number);

  return recordedPr?.base.ref === mainBranch && recordedPr.head.sha === pr.head.sha && Boolean(recordedPr.base.sha);
}

function isCompletedPullRequestRun(run, latest, repository, pr) {
  const expectedWorkflow = run.event === 'pull_request'
    && run.path === '.github/workflows/check-pull-request.yml'
    && run.head_repository?.full_name === `${repository.owner}/${repository.repo}`;

  return expectedWorkflow && run.head_sha === pr.head.sha
    && run.status === 'completed'
    && run.run_attempt === latest.run_attempt;
}

function hasSuccessfulRequiredJobs(jobs) {
  return [...requiredJobs, sonarJob].every(name => jobs.filter(x => x.name === name).length === 1)
    && jobs.every(x => x.status === 'completed' && x.conclusion === 'success');
}

function hasSuccessfulChecks(checks, statuses) {
  return checks.some(x => x.name === 'SonarCloud Code Analysis' && x.app?.slug === 'sonarqubecloud'
      && x.status === 'completed' && x.conclusion === 'success')
    && checks.every(x => x.status === 'completed' && x.conclusion === 'success')
    && statuses.statuses.every(x => x.state === 'success');
}

async function getCurrentRun(github, repository, pr, runId, runAttempt, core) {
  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...repository, workflow_id: 'check-pull-request.yml', event: 'pull_request',
    head_sha: pr.head.sha, per_page: 100
  });
  const latest = runs.sort((a, b) => compareRunRecency(b, a))[0];
  if (!latest) {
    skip(core, 'No PR CI run exists for this head.');
    return undefined;
  }
  if (!matchesRunAttempt(latest, runId, runAttempt)) {
    skip(core, 'A newer CI run or attempt replaced this event.');

    return undefined;
  }

  const { data: run } = await github.rest.actions.getWorkflowRun({ ...repository, run_id: latest.id });
  core.notice(`CI run ${run.id}, attempt ${run.run_attempt}: ${run.status}, conclusion ${run.conclusion ?? 'pending'}.`);
  if (!isCompletedPullRequestRun(run, latest, repository, pr) || !recordsCurrentPullRequest(run, pr)) {
    skip(core, 'CI is pending or its workflow, head or PR association does not match.');

    return undefined;
  }

  core.notice(`CI tested main ${run.pull_requests.find(x => x.number === pr.number).base.sha}.`);
  return run;
}

async function hasGreenChecks(github, repository, pr, run, core) {
  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
    ...repository, run_id: run.id, filter: 'latest', per_page: 100
  });
  if (!hasSuccessfulRequiredJobs(jobs)) {
    skip(core, 'Every required job must succeed; missing, skipped or failed jobs stay manual.');

    return false;
  }

  const checks = await github.paginate(github.rest.checks.listForRef, {
    ...repository, ref: pr.head.sha, filter: 'latest', per_page: 100
  });
  const { data: statuses } = await github.rest.repos.getCombinedStatusForRef({ ...repository, ref: pr.head.sha });
  if (!hasSuccessfulChecks(checks, statuses)) {
    skip(core, 'Another check or commit status is pending or unsuccessful.');

    return false;
  }

  return true;
}

async function verifyCommits(github, repository, pr, core) {
  const commits = await github.paginate(github.rest.pulls.listCommits, {
    ...repository, pull_number: pr.number, per_page: 100
  });
  if (!commits.length || commits.at(-1).sha !== pr.head.sha) {
    skip(core, 'Commit list is empty or does not match the current head.');
    return undefined;
  }
  if (commits.some(x => x.author?.login !== 'dependabot[bot]')) {
    skip(core, 'A commit is not authored by Dependabot.');
    return undefined;
  }
  if (commits.some(x => x.commit.verification?.verified !== true)) {
    skip(core, 'A commit does not have a verified signature.');
    return undefined;
  }
  if (commits.some(x => !hasOnlyMinorOrPatchUpdates(x.commit.message))) {
    const major = commits.some(x => /^ {2}update-type: version-update:semver-major\r?$/m.test(x.commit.message));
    skip(core, major ? 'Update metadata includes a major version; manual review is required.'
      : 'Update metadata is missing, malformed, mixed or not classified as minor or patch.');

    return undefined;
  }

  return commits;
}

async function hasStrictMergeRules(github, repository, core) {
  const { data: rules } = await github.request('GET /repos/{owner}/{repo}/rules/branches/{branch}', {
    ...repository, branch: mainBranch
  });
  const protectedChecks = rules.filter(x => x.type === 'required_status_checks');
  if (!protectedChecks.some(x => x.parameters.strict_required_status_checks_policy === true
    && requiredJobs.every(name => x.parameters.required_status_checks.some(y => y.context === name && y.integration_id === githubActionsIntegrationId)))) {
    skip(core, 'Enable strict up-to-date checks for every required GitHub Actions job on main first.');

    return false;
  }

  return true;
}

function isCurrentDependabotPullRequest(currentPr, pr, repository) {
  return isDependabotPullRequest(currentPr, `${repository.owner}/${repository.repo}`)
    && currentPr.state === 'open' && !currentPr.draft && currentPr.head.sha === pr.head.sha;
}

function isFreshPullRequest(currentPr, pr, repository, run, main) {
  return isCurrentDependabotPullRequest(currentPr, pr, repository)
    && run.pull_requests.find(x => x.number === pr.number).base.sha === main.commit.sha;
}

export async function mergeDependabotPullRequest({ github, context, core, pullNumber, runId, runAttempt }) {
  const repository = context.repo;
  const { data: pr } = await github.rest.pulls.get({ ...repository, pull_number: pullNumber });
  core.notice(`Current PR head ${pr.head.sha}.`);
  if (!isDependabotPullRequest(pr, `${repository.owner}/${repository.repo}`) || pr.draft) {
    skip(core, 'PR is a draft or is not a same-repository Dependabot PR targeting main.');
    return undefined;
  }

  const commits = await verifyCommits(github, repository, pr, core);
  if (!commits) {
    return undefined;
  }

  // A duplicate event or manual retry can recover a merge that preceded a failed dispatch.
  if (pr.merged) {
    if (pr.merged_by?.login !== githubActionsBot) {
      return undefined;
    }

    core.setOutput('merge_commit_sha', pr.merge_commit_sha);

    return true;
  }
  if (pr.state !== 'open') {
    skip(core, 'PR is not open.');
    return undefined;
  }

  const eligibility = await isEligibleOpenPullRequest({ github, repository, pr, runId, runAttempt, core });
  if (eligibility === recoveryRequested) {
    return eligibility;
  }
  if (!eligibility) {
    return undefined;
  }

  return approveAndMerge(github, repository, pr, core);
}

async function isEligibleOpenPullRequest({ github, repository, pr, runId, runAttempt, core }) {
  const run = await getCurrentRun(github, repository, pr, runId, runAttempt, core);
  if (!run || !await hasStrictMergeRules(github, repository, core)) {
    return false;
  }

  const { data: currentPr } = await github.rest.pulls.get({ ...repository, pull_number: pr.number });
  const { data: main } = await github.rest.repos.getBranch({ ...repository, branch: mainBranch });
  if (!isFreshPullRequest(currentPr, pr, repository, run, main)) {
    if (await requestStaleRebase(github, repository, pr, currentPr, main.commit.sha, core)) {
      return recoveryRequested;
    }
    skip(core, 'The PR or main changed since CI; wait for a rebase and a fresh CI run.');

    return false;
  }

  if (!await hasReleaseCapacity(github, repository, main.commit.sha, core)) {
    return false;
  }

  if (run.conclusion !== 'success') {
    if (await retryFailedSonar(github, repository, pr, run, main.commit.sha, core)) {
      return recoveryRequested;
    }
    skip(core, 'CI failed or was cancelled and is not eligible for an automatic Sonar retry.');
    return false;
  }

  return hasGreenChecks(github, repository, pr, run, core);
}

async function requestStaleRebase(github, repository, pr, currentPr, mainSha, core) {
  return isCurrentDependabotPullRequest(currentPr, pr, repository)
    && await hasReleaseCapacity(github, repository, mainSha, core)
    && requestRebase(github, repository, currentPr, mainSha, core);
}

function isSonarOnlyFailure(jobs) {
  return [...requiredJobs, sonarJob].every(name => jobs.filter(job => job.name === name).length === 1)
    && jobs.every(job => job.status === 'completed'
      && (job.conclusion === 'success' || (job.name === sonarJob && job.conclusion === 'failure')))
    && jobs.some(job => job.name === sonarJob && job.conclusion === 'failure');
}

async function retryFailedSonar(github, repository, pr, run, mainSha, core) {
  if (run.conclusion !== 'failure' || run.run_attempt !== 1) {
    return false;
  }
  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
    ...repository, run_id: run.id, filter: 'latest', per_page: 100
  });
  if (!isSonarOnlyFailure(jobs)) {
    return false;
  }

  const { data: currentPr } = await github.rest.pulls.get({ ...repository, pull_number: pr.number });
  const { data: main } = await github.rest.repos.getBranch({ ...repository, branch: mainBranch });
  const latest = await getCurrentRun(github, repository, pr, run.id, run.run_attempt, core);
  if (!latest || !isCurrentDependabotPullRequest(currentPr, pr, repository) || main.commit.sha !== mainSha) {
    return false;
  }

  await github.rest.actions.reRunWorkflowFailedJobs({ ...repository, run_id: run.id });
  core.notice('Retried the failed Sonar job once; wait for CI and the quality gate.');
  return true;
}

async function requestRebase(github, repository, pr, mainSha, core) {
  const comments = await github.paginate(github.rest.issues.listComments, {
    ...repository, issue_number: pr.number, per_page: 100
  });
  const recentRequest = comments.some(comment => comment.user?.login === githubActionsBot
    && comment.body?.includes(rebaseMarker)
    && Date.now() - Date.parse(comment.created_at) < rebaseCooldownMs);
  if (recentRequest) {
    skip(core, 'A rebase was requested in the last 24 hours; wait before requesting another.');
    return false;
  }

  const { data: currentPr } = await github.rest.pulls.get({ ...repository, pull_number: pr.number });
  const { data: currentMain } = await github.rest.repos.getBranch({ ...repository, branch: mainBranch });
  if (!isCurrentDependabotPullRequest(currentPr, pr, repository)
    || currentMain.commit.sha !== mainSha) {
    return false;
  }

  await github.rest.issues.createComment({
    ...repository, issue_number: pr.number,
    body: `@dependabot rebase\n\n${rebaseMarker}\nThe last checks used an older main revision. Please rebase so CI can test this update against the current main.`
  });
  core.notice('Requested a Dependabot rebase; wait for a new signed commit and fresh CI.');
  return true;
}

async function approveAndMerge(github, repository, pr, core) {
  const reviews = await github.paginate(github.rest.pulls.listReviews, {
    ...repository, pull_number: pr.number, per_page: 100
  });
  if (!reviews.some(x => x.user?.login === githubActionsBot && x.commit_id === pr.head.sha && x.state === 'APPROVED')) {
    await github.rest.pulls.createReview({
      ...repository, pull_number: pr.number, commit_id: pr.head.sha, event: 'APPROVE',
      body: 'Verified Dependabot minor/patch update. All checks passed for this revision. These controls do not certify upstream code as safe.'
    });
  }

  let result;
  try {
    result = await github.rest.pulls.merge({
      ...repository, pull_number: pr.number, sha: pr.head.sha, merge_method: 'squash'
    });
  } catch (error) {
    if (!blockedMergeStatuses.has(error.status)) {
      throw error;
    }

    skip(core, 'GitHub blocked the merge; continue to the next candidate.');

    return false;
  }
  if (!result.data.merged) {
    throw new Error('GitHub did not confirm the merge; Publish was not dispatched.');
  }

  core.setOutput('merge_commit_sha', result.data.sha);
  core.notice(`Merged head ${pr.head.sha} as ${result.data.sha}; publication will be dispatched.`);

  return true;
}

async function hasPublished(github, repository, run, jobName = publishJob) {
  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
    ...repository, run_id: run.id, filter: 'all', per_page: 100
  });

  return jobs.some(x => x.name === jobName && x.status === 'completed' && x.conclusion === 'success');
}

export async function dispatchPublish({ github, context, core, mergeCommitSha }) {
  if (!/^[0-9a-f]{40}$/.test(mergeCommitSha ?? '')) {
    throw new Error('A confirmed merge commit is required.');
  }

  const repository = context.repo;
  const { data: main } = await github.rest.repos.getBranch({ ...repository, branch: mainBranch });
  const { data: comparison } = await github.rest.repos.compareCommitsWithBasehead({
    ...repository, basehead: `${mergeCommitSha}...${main.commit.sha}`
  });
  if (!['ahead', 'identical'].includes(comparison.status)) {
    throw new Error('The confirmed merge is not on main.');
  }

  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...repository, workflow_id: publishWorkflow, head_sha: main.commit.sha, per_page: 100
  });
  for (const run of runs) {
    if (run.status !== 'completed' || await hasPublished(github, repository, run)) {
      core.notice(`Publish run ${run.id} is active or has published main ${main.commit.sha}.`);

      return;
    }
  }

  await github.rest.actions.createWorkflowDispatch({ ...repository, workflow_id: publishWorkflow, ref: mainBranch });
  core.notice(`Requested Publish on main ${main.commit.sha} after merge ${mergeCommitSha}. Check the result; rerun this workflow with the PR number if dispatch or publishing fails.`);
}

export async function shouldPublish({ github, context, core }) {
  if (context.ref !== 'refs/heads/main') {
    throw new Error('Publish must run on main.');
  }

  const { data: main } = await github.rest.repos.getBranch({ ...context.repo, branch: mainBranch });
  if (context.sha !== main.commit.sha) {
    core.notice('This queued revision was superseded; request publication of current main instead.');
    core.setOutput('should_publish', 'false');
    core.setOutput('superseded', 'true');

    return;
  }

  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...context.repo, workflow_id: publishWorkflow, head_sha: context.sha, per_page: 100
  });
  for (const run of runs) {
    if (await hasPublished(github, context.repo, run)) {
      core.notice('This revision has already been published.');
      core.setOutput('should_publish', 'false');

      return;
    }
  }

  core.setOutput('should_publish', 'true');
}
