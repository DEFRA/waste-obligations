import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  dispatchPublish,
  hasOnlyMinorOrPatchUpdates,
  mergeDependabotPullRequest,
  processDependabotQueue,
  resolvePullRequest,
  shouldPublish
} from './dependabot-auto-merge.mjs';

const headSha = 'a'.repeat(40);
const baseSha = 'b'.repeat(40);
const mergeSha = 'c'.repeat(40);
const otherSha = 'd'.repeat(40);
const repository = { owner: 'DEFRA', repo: 'waste-obligations' };
const repositoryName = 'DEFRA/waste-obligations';

function metadata(types = ['version-update:semver-minor']) {
  return `Bump dependencies\n\n---\nupdated-dependencies:\n${types.map((type, i) =>
    `- dependency-name: ${i === 0 ? 'MongoDB.Driver' : 'AWSSDK.SQS'}\n  dependency-version: 3.10.0\n  dependency-type: direct:production${type ? `\n  update-type: ${type}` : ''}`
  ).join('\n')}\ndependency-group: nuget-runtime\n...\n\nSigned-off-by: dependabot[bot]`;
}

function harness() {
  const pr = {
    number: 123, state: 'open', draft: false, merged: false,
    user: { login: 'dependabot[bot]' },
    head: { sha: headSha, repo: { full_name: repositoryName } },
    base: { ref: 'main', repo: { full_name: repositoryName } }
  };
  const run = {
    id: 10, run_attempt: 1, event: 'pull_request', status: 'completed', conclusion: 'success',
    path: '.github/workflows/check-pull-request.yml', head_sha: headSha,
    head_repository: { full_name: repositoryName },
    pull_requests: [{ number: 123, head: { sha: headSha }, base: { ref: 'main', sha: baseSha } }]
  };
  const successfulJob = name => ({ name, status: 'completed', conclusion: 'success' });
  const state = {
    pr, run, ciRuns: [run], ciJobs: {}, mainSha: baseSha, reviews: [], checks: [], statuses: [],
    jobs: [successfulJob('Run Pull Request Checks'), successfulJob('Run journey tests'),
      successfulJob('CDP SonarCloud Scan / CDP SonarCloud coverage scan')],
    commits: [{ sha: headSha, author: { login: 'dependabot[bot]' },
      commit: { verification: { verified: true }, message: metadata() } }],
    rules: [{ type: 'required_status_checks', parameters: {
      strict_required_status_checks_policy: true,
      required_status_checks: [{ context: 'Run Pull Request Checks', integration_id: 15368 },
        { context: 'Run journey tests', integration_id: 15368 }]
    } }],
    publishRuns: [{ id: 40, status: 'completed', conclusion: 'success', head_sha: baseSha, head_branch: 'main' }],
    publishJobs: { 40: [successfulJob('CDP-publish-workflow')] },
    activeReleaseRuns: [], hotfixRuns: [], comparison: 'ahead', prReads: 0, openPrs: [pr], candidates: {}
  };
  const effects = { reviews: [], merges: [], dispatches: [] };
  const output = {};
  const core = { notice() {}, setOutput(name, value) { output[name] = value; } };
  const context = {
    repo: repository, ref: 'refs/heads/main', sha: baseSha, runId: 99,
    eventName: 'workflow_run', payload: { workflow_run: run }
  };
  const github = {
    async request() { return { data: state.rules }; },
    async paginate(method, input) {
      const { data } = await method(input);

      return Array.isArray(data) ? data : data.workflow_runs ?? data.jobs ?? data.check_runs;
    },
    rest: {
      pulls: {
        async listFiles() { return { data: state.files ?? [{ filename: 'src/Api/Api.csproj', status: 'modified' }] }; },
        async list(input) { return { data: state.openPrs.slice((input.page - 1) * input.per_page, input.page * input.per_page) }; },
        async get(input) {
          if (state.candidates[input.pull_number]) return { data: state.candidates[input.pull_number].pr };
          state.prReads++;

          return { data: state.prReads > 1 && state.changedPr ? state.changedPr : state.pr };
        },
        async listCommits(input) { return { data: state.candidates[input.pull_number]?.commits ?? state.commits }; },
        async listReviews() { return { data: state.reviews }; },
        async createReview(input) { effects.reviews.push(input); },
        async merge(input) {
          effects.merges.push(input);
          if (state.mergeError) throw state.mergeError;

          state.mainSha = mergeSha;

          return { data: state.mergeResult ?? { merged: true, sha: mergeSha } };
        }
      },
      repos: {
        async getContent(input) {
          const version = input.ref === headSha ? '3.10.0' : '3.9.0';

          return { data: { type: 'file', encoding: 'base64', size: 200,
            content: Buffer.from((input.ref === headSha ? state.after : state.before)
              ?? `<Project><PackageReference Include="MongoDB.Driver" Version="${version}" /></Project>`).toString('base64') } };
        },
        async getBranch() { return { data: { commit: { sha: state.mainSha } } }; },
        async getCombinedStatusForRef() { return { data: { statuses: state.statuses } }; },
        async compareCommitsWithBasehead() { return { data: { status: state.comparison } }; },
        async listPullRequestsAssociatedWithCommit() { return { data: [state.pr] }; }
      },
      checks: { async listForRef() { return { data: { check_runs: state.checks } }; } },
      actions: {
        async listWorkflowRuns(input) {
          const runs = input.workflow_id === 'publish.yml' ? state.publishRuns
            : input.workflow_id === 'publish-hotfix.yml' ? state.hotfixRuns : state.ciRuns;

          return { data: { workflow_runs: runs.filter(x => !input.head_sha || !x.head_sha || x.head_sha === input.head_sha) } };
        },
        async listWorkflowRunsForRepo(input) {
          return { data: { workflow_runs: state.activeReleaseRuns.filter(x => x.status === input.status) } };
        },
        async getWorkflowRun(input) { return { data: state.ciRuns.find(x => x.id === input.run_id) }; },
        async listJobsForWorkflowRun(input) {
          const jobs = input.run_id === 10 ? state.jobs : state.ciJobs[input.run_id] ?? state.publishJobs[input.run_id] ?? [];
          const attempt = input.run_id === 10 ? state.run.run_attempt
            : [...state.ciRuns, ...state.publishRuns, ...state.hotfixRuns].find(x => x.id === input.run_id)?.run_attempt ?? 1;

          return { data: { jobs: input.filter === 'all' ? jobs : jobs.filter(x => (x.run_attempt ?? 1) === attempt) } };
        },
        async createWorkflowDispatch(input) {
          if (state.dispatchError) throw state.dispatchError;

          effects.dispatches.push(input);
        }
      }
    }
  };

  return { github, context, core, state, effects, output,
    now: Date.parse('2026-10-07T12:00:00Z'),
    releaseLookup: async () => ({ published: '2026-09-29T12:00:00Z', listed: true }) };
}

async function merge(h) {
  await mergeDependabotPullRequest({ ...h, pullNumber: 123, runId: 10, runAttempt: 1 });
}

function assertUntouched(h) {
  assert.deepEqual(h.effects, { reviews: [], merges: [], dispatches: [] });
  assert.equal(h.output.merge_commit_sha, undefined);
}

test('recognised grouped minor and patch metadata is eligible', () => {
  assert.equal(hasOnlyMinorOrPatchUpdates(metadata(['version-update:semver-minor', 'version-update:semver-patch'])), true);
});

for (const [name, message] of [
  ['major update in a group', metadata(['version-update:semver-patch', 'version-update:semver-major'])],
  ['unclassified latest-image update', metadata([''])],
  ['unknown update in a group', metadata(['version-update:semver-minor', ''])],
  ['duplicate update type', metadata().replace('  update-type:', '  update-type: version-update:semver-patch\n  update-type:')],
  ['missing canonical metadata', 'Bump Package from 1.2.0 to 1.3.0'],
  ['two metadata blocks', `${metadata()}\n${metadata()}`]
]) {
  test(`${name} stays manual`, () => {
    assert.equal(hasOnlyMinorOrPatchUpdates(message), false);
  });
}

test('a passing update approves and merges the exact tested SHA', async () => {
  const h = harness();
  await merge(h);

  assert.equal(h.effects.reviews.length, 1);
  assert.equal(h.effects.reviews[0].commit_id, headSha);
  assert.equal(h.effects.merges.length, 1);
  assert.equal(h.effects.merges[0].sha, headSha);
  assert.equal(h.effects.merges[0].merge_method, 'squash');
  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('an optional Sonar failure does not block the two successful required jobs', async () => {
  const h = harness();
  h.state.run.conclusion = 'failure';
  h.state.jobs[2].conclusion = 'failure';
  h.state.checks.push({ name: 'SonarCloud Code Analysis', status: 'completed', conclusion: 'failure' });
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

for (const [name, change] of [
  ['runtime release younger than seven days', h => { h.releaseLookup = async () => ({ listed: true, published: '2026-10-01T12:00:00Z' }); }],
  ['unlisted runtime release', h => { h.releaseLookup = async () => ({ listed: false, published: '2026-09-29T12:00:00Z' }); }],
  ['future release timestamp', h => { h.releaseLookup = async () => ({ listed: true, published: '2026-10-08T12:00:00Z' }); }],
  ['unknown release timestamp', h => { h.releaseLookup = async () => ({ listed: true, published: 'unknown' }); }],
  ['NuGet unlisted sentinel timestamp', h => { h.releaseLookup = async () => ({ listed: true, published: '1900-01-01T00:00:00Z' }); }],
  ['registry unavailable', h => { h.releaseLookup = async () => { throw new Error('NuGet unavailable'); }; }],
  ['an Actions update', h => { h.state.commits[0].commit.message = metadata().replace('MongoDB.Driver', 'actions/checkout'); }],
  ['missing target version metadata', h => { h.state.commits[0].commit.message = metadata().replace('  dependency-version: 3.10.0\n', ''); }],
  ['a workflow edit alongside a dependency update', h => { h.state.files = [{ filename: '.github/workflows/publish.yml', status: 'modified' }]; }],
  ['an added project', h => { h.state.files = [{ filename: 'src/Api/Api.csproj', status: 'added' }]; }],
  ['an executable build target alongside a version update', h => { h.state.after = '<Project><PackageReference Include="MongoDB.Driver" Version="3.10.0" /><Target Name="Build"><Exec Command="attack" /></Target></Project>'; }]
]) {
  test(`${name} causes no approval, merge or dispatch`, async () => {
    const h = harness();
    change(h);
    await merge(h);

    assertUntouched(h);
  });
}

test('a release exactly seven days old is eligible regardless of PR creation/rebase date', async () => {
  const h = harness();
  h.state.pr.created_at = '2026-10-07T11:00:00Z';
  h.releaseLookup = async () => ({ listed: true, published: '2026-09-30T12:00:00Z' });
  await merge(h);

  assert.equal(h.effects.merges.length, 1);
});

for (const [name, change] of [
  ['failed PR build', h => { h.state.jobs[0].conclusion = 'failure'; }],
  ['missing journey job', h => { h.state.jobs.splice(1, 1); }],
  ['skipped journey job', h => { h.state.jobs[1].conclusion = 'skipped'; }],
  ['pending extra check', h => { h.state.checks.push({ name: 'Security', status: 'in_progress', conclusion: null }); }],
  ['failed commit status', h => { h.state.statuses.push({ context: 'External checks', state: 'failure' }); }],
  ['major commit', h => { h.state.commits[0].commit.message = metadata(['version-update:semver-major']); }],
  ['unverified signature', h => { h.state.commits[0].commit.verification.verified = false; }],
  ['human commit', h => { h.state.commits[0].author.login = 'developer'; }],
  ['fork PR', h => { h.state.pr.head.repo.full_name = 'someone/waste-obligations'; }],
  ['draft PR', h => { h.state.pr.draft = true; }],
  ['outdated main', h => { h.state.mainSha = otherSha; }],
  ['head changed before approval', h => { h.state.changedPr = { ...h.state.pr, head: { ...h.state.pr.head, sha: otherSha } }; }],
  ['target changed before approval', h => { h.state.changedPr = { ...h.state.pr, base: { ...h.state.pr.base, ref: 'release' } }; }],
  ['newer CI run', h => { h.state.ciRuns.push({ ...h.state.run, id: 11 }); }],
  ['newer rerun attempt', h => { h.state.run.run_attempt = 2; }],
  ['no strict base protection', h => { h.state.rules[0].parameters.strict_required_status_checks_policy = false; }],
  ['journey check not protected', h => { h.state.rules[0].parameters.required_status_checks.pop(); }]
]) {
  test(`${name} causes no approval, merge or dispatch`, async () => {
    const h = harness();
    change(h);
    await merge(h);

    assertUntouched(h);
  });
}

test('a previous current-head bot approval is not repeated on retry', async () => {
  const h = harness();
  h.state.reviews.push({ user: { login: 'github-actions[bot]' }, commit_id: headSha, state: 'APPROVED' });
  await merge(h);

  assert.deepEqual(h.effects.reviews, []);
  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('a failed prior attempt does not block a successful latest CI attempt', async () => {
  const h = harness();
  h.state.run.run_attempt = 2;
  h.state.jobs.forEach(x => { x.run_attempt = 2; });
  h.state.jobs.push({ name: 'Run Pull Request Checks', status: 'completed', conclusion: 'failure', run_attempt: 1 });
  await mergeDependabotPullRequest({ ...h, pullNumber: 123, runId: 10, runAttempt: 2 });

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('GitHub rejecting an intervening push leaves no confirmed merge to publish', async () => {
  const h = harness();
  h.state.mergeError = Object.assign(new Error('Head changed'), { status: 409 });
  await merge(h);

  assert.equal(h.output.merge_commit_sha, undefined);
  assert.deepEqual(h.effects.dispatches, []);
});

test('an uncertain merge response fails instead of dispatching', async () => {
  const h = harness();
  h.state.mergeResult = { merged: false };

  await assert.rejects(merge(h), /did not confirm/);
  assert.equal(h.output.merge_commit_sha, undefined);
});

test('retrying an already bot-merged PR recovers its merge SHA without another review or merge', async () => {
  const h = harness();
  Object.assign(h.state.pr, { state: 'closed', merged: true, merged_by: { login: 'github-actions[bot]' }, merge_commit_sha: mergeSha });
  h.state.ciRuns = [];
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
  assert.deepEqual(h.effects, { reviews: [], merges: [], dispatches: [] });
});

test('a human merge is not used for automated recovery', async () => {
  const h = harness();
  Object.assign(h.state.pr, { state: 'closed', merged: true, merged_by: { login: 'developer' }, merge_commit_sha: mergeSha });
  await merge(h);

  assertUntouched(h);
});

test('a confirmed merge dispatches Publish on main', async () => {
  const h = harness();
  h.state.mainSha = mergeSha;
  await dispatchPublish({ ...h, mergeCommitSha: mergeSha });

  assert.deepEqual(h.effects.dispatches, [{ ...repository, workflow_id: 'publish.yml', ref: 'main' }]);
});

test('a merge absent from main cannot dispatch Publish', async () => {
  const h = harness();
  h.state.comparison = 'diverged';

  await assert.rejects(dispatchPublish({ ...h, mergeCommitSha: mergeSha }), /not on main/);
  assert.deepEqual(h.effects.dispatches, []);
});

test('an active Publish run prevents duplicate dispatch and can be retried after failure', async () => {
  const h = harness();
  h.state.publishRuns = [{ id: 50, status: 'in_progress' }];
  await dispatchPublish({ ...h, mergeCommitSha: mergeSha });
  assert.deepEqual(h.effects.dispatches, []);

  h.state.publishRuns[0].status = 'completed';
  h.state.publishJobs[50] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'failure' }];
  await dispatchPublish({ ...h, mergeCommitSha: mergeSha });

  assert.equal(h.effects.dispatches.length, 1);
});

test('a successful publish job prevents dispatch; a green workflow with a skipped job does not', async () => {
  const h = harness();
  h.state.publishRuns = [{ id: 50, status: 'completed', conclusion: 'success' }];
  h.state.publishJobs[50] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'success' }];
  await dispatchPublish({ ...h, mergeCommitSha: mergeSha });
  assert.deepEqual(h.effects.dispatches, []);

  h.state.publishJobs[50][0].conclusion = 'skipped';
  await dispatchPublish({ ...h, mergeCommitSha: mergeSha });

  assert.equal(h.effects.dispatches.length, 1);
});

test('publication validation rejects a non-main dispatch', async () => {
  const h = harness();
  h.context.ref = 'refs/heads/dependabot/updates';

  await assert.rejects(shouldPublish(h), /must run on main/);
});

test('a superseded queued revision hands off publication of current main', async () => {
  const h = harness();
  h.state.mainSha = otherSha;
  await shouldPublish(h);
  assert.equal(h.output.should_publish, 'false');
  assert.equal(h.output.superseded, 'true');

  await dispatchPublish({ ...h, mergeCommitSha: h.context.sha });

  assert.deepEqual(h.effects.dispatches, [{ ...repository, workflow_id: 'publish.yml', ref: 'main' }]);
});

test('publication validation skips a previously published SHA', async () => {
  const h = harness();
  h.state.publishRuns = [{ id: 50, status: 'completed' }];
  h.state.publishJobs[50] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'success' }];
  await shouldPublish(h);

  assert.equal(h.output.should_publish, 'false');
});

test('rerunning a previously successful Publish run does not publish again', async () => {
  const h = harness();
  h.state.publishRuns = [{ id: h.context.runId, status: 'in_progress', run_attempt: 2 }];
  h.state.publishJobs[h.context.runId] = [
    { name: 'CDP-publish-workflow', status: 'completed', conclusion: 'success', run_attempt: 1 }
  ];
  await shouldPublish(h);

  assert.equal(h.output.should_publish, 'false');
});

test('publication validation waits for concurrency rather than skipping because of an active other run', async () => {
  const h = harness();
  h.state.publishRuns = [{ id: 50, status: 'in_progress' }];
  await shouldPublish(h);

  assert.equal(h.output.should_publish, 'true');
});

test('a completed workflow scans the queue without relying on PR associations', async () => {
  const h = harness();
  h.context.payload.workflow_run = { ...h.state.run, pull_requests: [] };
  await resolvePullRequest(h);

  assert.equal(h.output.scan_queue, 'true');
  assert.equal(h.output.pull_number, undefined);
  assertUntouched(h);
});

test('manual recovery accepts a PR number and rejects shell-shaped input', async () => {
  const h = harness();
  h.context.eventName = 'workflow_dispatch';
  h.context.payload = { inputs: { 'pull-request-number': '123' } };
  await resolvePullRequest(h);
  assert.equal(h.output.pull_number, 123);

  h.context.payload.inputs['pull-request-number'] = '123; echo unsafe';

  await assert.rejects(resolvePullRequest(h), /valid pull request number/);
});

for (const path of ['.github/workflows/publish.yml', '.github/workflows/publish-hotfix.yml']) {
  for (const status of ['in_progress', 'queued', 'requested', 'waiting', 'pending']) {
    test(`${path} ${status} on another revision blocks further approval and merge`, async () => {
      const h = harness();
      h.state.activeReleaseRuns.push({ path, status, head_sha: otherSha, head_branch: 'hotfix/example' });
      await merge(h);

      assertUntouched(h);
    });
  }
}

for (const [name, runs, jobs] of [
  ['no publication', [], {}],
  ['failed publication', [{ id: 50, status: 'completed', conclusion: 'failure', head_branch: 'main' }],
    { 50: [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'failure' }] }],
  ['cancelled publication', [{ id: 50, status: 'completed', conclusion: 'cancelled', head_branch: 'main' }], {}],
  ['successful workflow with skipped publication', [{ id: 50, status: 'completed', conclusion: 'success', head_branch: 'main' }],
    { 50: [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'skipped' }] }],
  ['publication on another branch', [{ id: 50, status: 'completed', conclusion: 'success', head_branch: 'other' }],
    { 50: [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'success' }] }]
]) {
  test(`${name} leaves the Dependabot queue untouched`, async () => {
    const h = harness();
    h.state.publishRuns = runs;
    h.state.publishJobs = jobs;
    await processDependabotQueue(h);

    assertUntouched(h);
  });
}

test('a failed latest release cannot inherit publication success from an earlier attempt or run', async () => {
  const h = harness();
  h.state.publishRuns.push({ id: 50, run_attempt: 2, status: 'completed', conclusion: 'failure', head_branch: 'main' });
  h.state.publishJobs[50] = [
    { name: 'CDP-publish-workflow', status: 'completed', conclusion: 'success', run_attempt: 1 },
    { name: 'CDP-publish-workflow', status: 'completed', conclusion: 'failure', run_attempt: 2 }
  ];
  await merge(h);

  assertUntouched(h);
});

test('a duplicate skipped Publish run does not erase an actual successful publication', async () => {
  const h = harness();
  h.state.publishRuns.push({ id: 50, status: 'completed', conclusion: 'success', head_branch: 'main' });
  h.state.publishJobs[50] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'skipped' }];
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('one eligible merge consumes a published head until the next head is published and retested', async () => {
  const h = harness();
  h.state.openPrs.push({ ...h.state.pr, number: 124 });
  await processDependabotQueue(h);

  assert.equal(h.effects.merges.length, 1);
  assert.equal(h.effects.merges[0].pull_number, 123);
  assert.equal(h.output.merge_commit_sha, mergeSha);
  delete h.output.merge_commit_sha;
  await processDependabotQueue(h);

  assert.equal(h.effects.merges.length, 1);
  assert.equal(h.output.merge_commit_sha, undefined);
  h.state.pr.number = 124;
  h.state.openPrs = [h.state.pr];
  h.state.run.pull_requests[0].number = 124;
  h.state.run.pull_requests[0].base.sha = mergeSha;
  h.state.publishRuns.push({ id: 50, status: 'completed', conclusion: 'success', head_sha: mergeSha, head_branch: 'main' });
  h.state.publishJobs[50] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'success' }];
  await processDependabotQueue(h);

  assert.equal(h.effects.merges.length, 2);
  assert.equal(h.effects.merges[1].pull_number, 124);
});

test('queue scanning skips a major candidate and considers the next eligible update', async () => {
  const h = harness();
  const majorPr = { ...h.state.pr, number: 122 };
  h.state.openPrs.unshift(majorPr);
  h.state.candidates[122] = { pr: majorPr, commits: [{ ...h.state.commits[0],
    commit: { verification: { verified: true }, message: metadata(['version-update:semver-major']) } }] };
  await processDependabotQueue(h);

  assert.equal(h.effects.merges.length, 1);
  assert.equal(h.effects.merges[0].pull_number, 123);
  assert.equal(h.effects.reviews.length, 1);
  assert.equal(h.effects.reviews[0].pull_number, 123);
});

test('a scheduled scan recovers dropped events using the latest eligible CI run', async () => {
  const h = harness();
  h.context.eventName = 'schedule';
  h.context.payload = {};
  await resolvePullRequest(h);
  await processDependabotQueue(h);

  assert.equal(h.output.scan_queue, 'true');
  assert.equal(h.output.merge_commit_sha, mergeSha);
});

for (const eventName of ['workflow_dispatch', 'workflow_run']) {
  test(`${eventName} without a PR number requests a queue scan`, async () => {
    const h = harness();
    h.context.eventName = eventName;
    h.context.payload = {};
    await resolvePullRequest(h);

    assert.equal(h.output.scan_queue, 'true');
    assertUntouched(h);
  });
}

test('manual confirmed-merge recovery remains available with an unpublished main and active hotfix', async () => {
  const h = harness();
  Object.assign(h.state.pr, { state: 'closed', merged: true, merged_by: { login: 'github-actions[bot]' }, merge_commit_sha: mergeSha });
  h.state.mainSha = mergeSha;
  h.state.publishRuns = [];
  h.state.activeReleaseRuns.push({ path: '.github/workflows/publish-hotfix.yml', status: 'in_progress' });
  await processDependabotQueue({ ...h, pullNumber: 123 });
  await dispatchPublish({ ...h, mergeCommitSha: h.output.merge_commit_sha });

  assert.deepEqual(h.effects.reviews, []);
  assert.deepEqual(h.effects.merges, []);
  assert.equal(h.effects.dispatches.length, 1);
});

test('after inspection a successful deduplicated Publish retry can recover a failed duplicate release', async () => {
  const h = harness();
  h.state.publishRuns.push({ id: 50, status: 'completed', conclusion: 'failure', head_branch: 'main' });
  await merge(h);

  assertUntouched(h);
  h.state.publishRuns.push({ id: 51, status: 'completed', conclusion: 'success', head_branch: 'main' });
  h.state.publishJobs[51] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'skipped' }];
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('a recent failed rerun of an older Publish run blocks approval after a newer successful run', async () => {
  const h = harness();
  h.state.publishRuns[0].updated_at = '2026-10-07T10:00:00Z';
  h.state.publishRuns.push({ id: 39, run_attempt: 2, status: 'completed', conclusion: 'failure',
    head_branch: 'main', updated_at: '2026-10-07T11:00:00Z' });
  await merge(h);

  assertUntouched(h);
});

for (const conclusion of ['failure', 'cancelled']) {
  test(`a ${conclusion} hotfix after main publication blocks approval until inspected main recovery`, async () => {
    const h = harness();
    h.state.hotfixRuns.push({ id: 50, status: 'completed', conclusion });
    await merge(h);

    assertUntouched(h);
    h.state.publishRuns.push({ id: 51, status: 'completed', conclusion: 'success', head_branch: 'main' });
    h.state.publishJobs[51] = [{ name: 'CDP-publish-workflow', status: 'completed', conclusion: 'skipped' }];
    await merge(h);

    assert.equal(h.output.merge_commit_sha, mergeSha);
  });
}

test('a successful hotfix retry resolves its prior failure without a new main publication', async () => {
  const h = harness();
  h.state.hotfixRuns.push({ id: 50, status: 'completed', conclusion: 'success', run_attempt: 2 });
  h.state.publishJobs[50] = [
    { name: 'CDP-publish-hotfix-workflow', status: 'completed', conclusion: 'failure', run_attempt: 1 },
    { name: 'CDP-publish-hotfix-workflow', status: 'completed', conclusion: 'success', run_attempt: 2 }
  ];
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('a skipped hotfix publish cannot conceal a failed prior release after main publication', async () => {
  const h = harness();
  h.state.hotfixRuns.push({ id: 50, status: 'completed', conclusion: 'failure' },
    { id: 51, status: 'completed', conclusion: 'success' });
  h.state.publishJobs[51] = [{ name: 'CDP-publish-hotfix-workflow', status: 'completed', conclusion: 'skipped' }];
  await merge(h);

  assertUntouched(h);
});

test('a failed rerun of an older hotfix is assessed by completion time rather than run number', async () => {
  const h = harness();
  h.state.publishRuns[0].updated_at = '2026-10-07T10:00:00Z';
  h.state.hotfixRuns.push({ id: 20, run_attempt: 2, status: 'completed', conclusion: 'failure',
    updated_at: '2026-10-07T11:00:00Z' });
  await merge(h);

  assertUntouched(h);
});

test('an older failed hotfix does not prevent progress after a later successful main release', async () => {
  const h = harness();
  h.state.hotfixRuns.push({ id: 20, status: 'completed', conclusion: 'failure' });
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('a scan stops at 50 candidates; an omitted PR remains available by explicit number', async () => {
  const h = harness();
  const majors = Array.from({ length: 50 }, (_, i) => ({ ...h.state.pr, number: i + 200 }));
  for (const pr of majors) {
    h.state.candidates[pr.number] = { pr, commits: [{ ...h.state.commits[0],
      commit: { verification: { verified: true }, message: metadata(['version-update:semver-major']) } }] };
  }
  h.state.openPrs = [...majors, h.state.pr];
  await processDependabotQueue(h);

  assertUntouched(h);
  await processDependabotQueue({ ...h, pullNumber: 123 });

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

for (const latestPassed of [false, true]) {
  test(`the latest rerun of an older CI run ${latestPassed ? 'allows approval' : 'blocks approval'} even with a newer run ID`, async () => {
    const h = harness();
    Object.assign(h.state.run, { run_attempt: 2, updated_at: '2026-10-07T11:00:00Z' });
    h.state.jobs.forEach(x => { x.run_attempt = 2; });
    if (!latestPassed) h.state.jobs[0].conclusion = 'failure';
    h.state.ciRuns.push({ ...h.state.run, id: 11, run_attempt: 1, updated_at: '2026-10-07T10:00:00Z' });
    h.state.ciJobs[11] = [
      { name: 'Run Pull Request Checks', status: 'completed', conclusion: latestPassed ? 'failure' : 'success' },
      { name: 'Run journey tests', status: 'completed', conclusion: 'success' }
    ];
    await processDependabotQueue(h);

    if (latestPassed) assert.equal(h.output.merge_commit_sha, mergeSha);
    else assertUntouched(h);
  });
}

test('a failed optional Sonar job does not block a successfully published main revision', async () => {
  const h = harness();
  h.state.publishRuns[0].conclusion = 'failure';
  h.state.publishJobs[40].push(
    { name: 'CDP-build-workflow', status: 'completed', conclusion: 'success' },
    { name: 'CDP SonarCloud Scan / CDP SonarCloud coverage scan', status: 'completed', conclusion: 'failure' }
  );
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});

test('a failed optional Sonar job does not conceal a failed CDP build after an earlier publication', async () => {
  const h = harness();
  h.state.publishRuns.push({ id: 50, status: 'completed', conclusion: 'failure', head_branch: 'main' });
  h.state.publishJobs[50] = [
    { name: 'CDP-build-workflow', status: 'completed', conclusion: 'failure' },
    { name: 'CDP-publish-workflow', status: 'completed', conclusion: 'skipped' },
    { name: 'CDP SonarCloud Scan / CDP SonarCloud coverage scan', status: 'completed', conclusion: 'failure' }
  ];
  await merge(h);

  assertUntouched(h);
});

test('a successfully published hotfix with a failed optional Sonar job allows further approvals', async () => {
  const h = harness();
  h.state.hotfixRuns.push({ id: 50, status: 'completed', conclusion: 'failure' });
  h.state.publishJobs[50] = [
    { name: 'CDP-build-hotfix-workflow', status: 'completed', conclusion: 'success' },
    { name: 'CDP-publish-hotfix-workflow', status: 'completed', conclusion: 'success' },
    { name: 'CDP SonarCloud Scan / CDP SonarCloud coverage scan', status: 'completed', conclusion: 'failure' }
  ];
  await merge(h);

  assert.equal(h.output.merge_commit_sha, mergeSha);
});
