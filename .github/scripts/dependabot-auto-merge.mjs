import { getRuntimeTargets, passesSupplyChainPolicy, readMinorOrPatchUpdates } from './dependabot-security-policy.mjs';

const requiredJobs = ['Dependency security review', 'Run Pull Request Checks', 'Run journey tests'];
const optionalSonarChecks = new Set([
  'CDP SonarCloud Scan / CDP SonarCloud coverage scan',
  'SonarCloud Code Analysis'
]);
const publishJob = 'CDP-publish-workflow';
const maxQueueCandidates = 50;
const maxQueuePages = 5;
const failureConclusion = 'failure';
const githubActionsIntegrationId = 15368;
const blockedMergeStatuses = new Set([405, 409]);

// Accept the canonical signed Dependabot metadata format; unfamiliar formats stay manual.
export function hasOnlyMinorOrPatchUpdates(message) {
  return readMinorOrPatchUpdates(message) !== undefined;
}

function isDependabotPullRequest(pr, repository) {
  return pr.user?.login === 'dependabot[bot]' && pr.base.ref === 'main'
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

async function isSuccessfulReleaseRun(github, repository, run) {
  if (!run || run.status !== 'completed') {
    return false;
  }
  if (run.conclusion === 'success') {
    return true;
  }
  if (run.conclusion !== failureConclusion) {
    return false;
  }

  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
    ...repository, run_id: run.id, filter: 'latest', per_page: 100
  });
  const releaseJobs = jobs.filter(x => !optionalSonarChecks.has(x.name));

  return releaseJobs.length > 0
    && releaseJobs.every(x => x.status === 'completed' && ['success', 'skipped'].includes(x.conclusion))
    && jobs.some(x => optionalSonarChecks.has(x.name) && x.conclusion === failureConclusion);
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
    if (!await isSuccessfulReleaseRun(github, repository, hotfix)) {
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
    ...repository, workflow_id: 'publish.yml', head_sha: mainSha, per_page: 100
  });
  const mainRuns = runs.filter(x => x.head_branch === 'main').sort((a, b) => compareRunRecency(b, a));
  // A later failed/cancelled attempt must not inherit an earlier successful release.
  if (!await isSuccessfulReleaseRun(github, repository, mainRuns[0])
    || !await hasMainPublication(github, repository, mainRuns)) {
    skip(core, 'Current main has no successful publication, or a later publication failed. Recover that release first.');

    return false;
  }

  return hasRecoveredHotfixes(github, repository, mainRuns[0], core);
}

export async function processDependabotQueue({ github, context, core, pullNumber, releaseLookup, now }) {
  if (pullNumber) {
    // Explicit recovery of an already merged PR must work while main is unpublished.
    await mergeDependabotPullRequest({ github, context, core, pullNumber, releaseLookup, now });

    return;
  }

  const { data: main } = await github.rest.repos.getBranch({ ...context.repo, branch: 'main' });
  if (!await hasReleaseCapacity(github, context.repo, main.commit.sha, core)) {
    return;
  }

  const candidates = [];
  for (let page = 1; page <= maxQueuePages; page++) {
    const { data: prs } = await github.rest.pulls.list({
      ...context.repo, state: 'open', base: 'main', sort: 'created', direction: 'asc', per_page: 100, page
    });
    candidates.push(...prs.filter(x => isDependabotPullRequest(x, `${context.repo.owner}/${context.repo.repo}`)));
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

  for (const pr of candidates.slice(0, maxQueueCandidates)) {
    if (await mergeDependabotPullRequest({ github, context, core, pullNumber: pr.number, releaseLookup, now })) {
      return;
    }
  }
}

function matchesRunAttempt(latest, runId, runAttempt) {
  return latest !== undefined && (!runId || (latest.id === runId && latest.run_attempt === runAttempt));
}

function recordsCurrentPullRequest(run, pr) {
  const recordedPr = run.pull_requests?.find(x => x.number === pr.number);

  return recordedPr?.base.ref === 'main' && recordedPr.head.sha === pr.head.sha && Boolean(recordedPr.base.sha);
}

function isCompletedPullRequestRun(run, latest, repository, pr) {
  return run.event === 'pull_request' && run.path === '.github/workflows/check-pull-request.yml'
    && run.head_repository?.full_name === `${repository.owner}/${repository.repo}`
    && run.head_sha === pr.head.sha && run.status === 'completed'
    && run.run_attempt === latest.run_attempt;
}

function hasSuccessfulRequiredJobs(jobs) {
  return requiredJobs.every(name => jobs.filter(x => x.name === name).length === 1)
    && jobs.filter(x => !optionalSonarChecks.has(x.name)).every(x => x.status === 'completed' && x.conclusion === 'success');
}

function hasSuccessfulChecks(checks, statuses) {
  return checks.filter(x => !optionalSonarChecks.has(x.name)).every(x => x.status === 'completed' && x.conclusion === 'success')
    && statuses.statuses.filter(x => !optionalSonarChecks.has(x.context)).every(x => x.state === 'success');
}

async function getEligibleRun(github, repository, pr, runId, runAttempt, core) {
  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...repository, workflow_id: 'check-pull-request.yml', event: 'pull_request',
    head_sha: pr.head.sha, per_page: 100
  });
  const latest = runs.sort((a, b) => compareRunRecency(b, a))[0];
  if (!matchesRunAttempt(latest, runId, runAttempt)) {
    skip(core, 'No current CI run, or a newer run/attempt replaced this event.');

    return undefined;
  }

  const { data: run } = await github.rest.actions.getWorkflowRun({ ...repository, run_id: latest.id });
  if (!isCompletedPullRequestRun(run, latest, repository, pr) || !recordsCurrentPullRequest(run, pr)) {
    skip(core, 'CI has not completed for this PR and revision.');

    return undefined;
  }

  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
    ...repository, run_id: run.id, filter: 'latest', per_page: 100
  });
  if (!hasSuccessfulRequiredJobs(jobs)) {
    skip(core, 'Every required job must succeed; missing, skipped or failed jobs stay manual.');

    return undefined;
  }

  const checks = await github.paginate(github.rest.checks.listForRef, {
    ...repository, ref: pr.head.sha, filter: 'latest', per_page: 100
  });
  const { data: statuses } = await github.rest.repos.getCombinedStatusForRef({ ...repository, ref: pr.head.sha });
  if (!hasSuccessfulChecks(checks, statuses)) {
    skip(core, 'Another check or commit status is pending or unsuccessful.');

    return undefined;
  }

  return run;
}

async function verifyCommits(github, repository, pr, core) {
  const commits = await github.paginate(github.rest.pulls.listCommits, {
    ...repository, pull_number: pr.number, per_page: 100
  });
  if (!commits.length || commits.at(-1).sha !== pr.head.sha || commits.some(x =>
    x.author?.login !== 'dependabot[bot]' || x.commit.verification?.verified !== true
    || !hasOnlyMinorOrPatchUpdates(x.commit.message))) {
    skip(core, 'Every commit must be verified Dependabot metadata containing only minor/patch updates.');

    return undefined;
  }

  return commits;
}

async function hasStrictMergeRules(github, repository, core) {
  const { data: rules } = await github.request('GET /repos/{owner}/{repo}/rules/branches/{branch}', {
    ...repository, branch: 'main'
  });
  const protectedChecks = rules.filter(x => x.type === 'required_status_checks');
  if (!protectedChecks.some(x => x.parameters.strict_required_status_checks_policy === true
    && requiredJobs.every(name => x.parameters.required_status_checks.some(y => y.context === name && y.integration_id === githubActionsIntegrationId)))) {
    skip(core, 'Enable strict up-to-date checks for every required GitHub Actions job on main first.');

    return false;
  }

  return true;
}

function isFreshPullRequest(currentPr, pr, repository, run, main) {
  return isDependabotPullRequest(currentPr, `${repository.owner}/${repository.repo}`)
    && currentPr.state === 'open' && !currentPr.draft && currentPr.head.sha === pr.head.sha
    && run.pull_requests.find(x => x.number === pr.number).base.sha === main.commit.sha;
}

export async function mergeDependabotPullRequest({ github, context, core, pullNumber, runId, runAttempt, releaseLookup, now }) {
  const repository = context.repo;
  const { data: pr } = await github.rest.pulls.get({ ...repository, pull_number: pullNumber });
  if (!isDependabotPullRequest(pr, `${repository.owner}/${repository.repo}`) || pr.draft) {
    return undefined;
  }

  const commits = await verifyCommits(github, repository, pr, core);
  if (!commits) {
    return undefined;
  }

  // A duplicate event or manual retry can recover a merge that preceded a failed dispatch.
  if (pr.merged) {
    if (pr.merged_by?.login !== 'github-actions[bot]') {
      return undefined;
    }

    core.setOutput('merge_commit_sha', pr.merge_commit_sha);

    return true;
  }
  if (pr.state !== 'open') {
    return undefined;
  }

  if (!await isEligibleOpenPullRequest({ github, repository, pr, runId, runAttempt, commits, core, releaseLookup, now })) {
    return undefined;
  }

  return approveAndMerge(github, repository, pr, core);
}

async function isEligibleOpenPullRequest({ github, repository, pr, runId, runAttempt, commits, core, releaseLookup, now }) {
  const run = await getEligibleRun(github, repository, pr, runId, runAttempt, core);
  if (!run || !await hasStrictMergeRules(github, repository, core)) {
    return false;
  }

  const { data: currentPr } = await github.rest.pulls.get({ ...repository, pull_number: pr.number });
  const { data: main } = await github.rest.repos.getBranch({ ...repository, branch: 'main' });
  if (!isFreshPullRequest(currentPr, pr, repository, run, main)) {
    skip(core, 'The PR or main changed since CI; wait for a rebase and a fresh CI run.');

    return false;
  }

  if (!await hasReleaseCapacity(github, repository, main.commit.sha, core)) {
    return false;
  }

  if (!await passesSupplyChainPolicy({ github, repository, pr, baseSha: main.commit.sha,
    targets: getRuntimeTargets(commits), core, releaseLookup, now })) {
    return false;
  }

  return true;
}

async function approveAndMerge(github, repository, pr, core) {
  const reviews = await github.paginate(github.rest.pulls.listReviews, {
    ...repository, pull_number: pr.number, per_page: 100
  });
  if (!reviews.some(x => x.user?.login === 'github-actions[bot]' && x.commit_id === pr.head.sha && x.state === 'APPROVED')) {
    await github.rest.pulls.createReview({
      ...repository, pull_number: pr.number, commit_id: pr.head.sha, event: 'APPROVE',
      body: 'Allowlisted runtime NuGet minor/patch update with version-only changes and listed releases at least seven days old. Required checks passed for this revision. These controls do not certify upstream code as safe.'
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

    skip(core, 'GitHub blocked the merge; no Publish run was requested.');

    return true;
  }
  if (!result.data.merged) {
    throw new Error('GitHub did not confirm the merge; Publish was not dispatched.');
  }

  core.setOutput('merge_commit_sha', result.data.sha);

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
  const { data: main } = await github.rest.repos.getBranch({ ...repository, branch: 'main' });
  const { data: comparison } = await github.rest.repos.compareCommitsWithBasehead({
    ...repository, basehead: `${mergeCommitSha}...${main.commit.sha}`
  });
  if (!['ahead', 'identical'].includes(comparison.status)) {
    throw new Error('The confirmed merge is not on main.');
  }

  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...repository, workflow_id: 'publish.yml', head_sha: main.commit.sha, per_page: 100
  });
  for (const run of runs) {
    if (run.status !== 'completed' || await hasPublished(github, repository, run)) {
      core.notice('Publish is already active or has published this main revision.');

      return;
    }
  }

  await github.rest.actions.createWorkflowDispatch({ ...repository, workflow_id: 'publish.yml', ref: 'main' });
  core.notice('Requested Publish on main. Check its result; rerun this workflow with the PR number if dispatch or publishing fails.');
}

export async function shouldPublish({ github, context, core }) {
  if (context.ref !== 'refs/heads/main') {
    throw new Error('Publish must run on main.');
  }

  const { data: main } = await github.rest.repos.getBranch({ ...context.repo, branch: 'main' });
  if (context.sha !== main.commit.sha) {
    core.notice('This queued revision was superseded; request publication of current main instead.');
    core.setOutput('should_publish', 'false');
    core.setOutput('superseded', 'true');

    return;
  }

  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...context.repo, workflow_id: 'publish.yml', head_sha: context.sha, per_page: 100
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
