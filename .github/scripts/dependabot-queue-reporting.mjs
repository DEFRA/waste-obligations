export function queueOutcome(result) {
  if (result === true) {
    return 'Merged';
  }
  if (result === 'recovery-requested') {
    return 'Recovery requested';
  }
  return 'Skipped';
}

export function scopedCandidateCore(core, repository, number) {
  const scoped = Object.create(core);
  scoped.messages = [];
  scoped.notice = message => {
    scoped.messages.push(message);
    core.notice(`PR #${number} (https://github.com/${repository.owner}/${repository.repo}/pull/${number}): ${message}`);
  };
  return scoped;
}

export async function writeQueueSummary(core, repository, mainSha, results, message) {
  const counts = ['Merged', 'Recovery requested', 'Skipped', 'Error'].map(outcome =>
    `${outcome}: ${results.filter(result => result.outcome === outcome).length}`).join(', ');
  const suffix = message ? ' ' + message : '';
  core.notice(`Queue summary for main ${mainSha}: ${results.length} considered; ${counts}.${suffix}`);
  if (!core.summary) {
    return;
  }

  try {
    core.summary.addHeading('Dependabot queue').addRaw(`Main: \`${mainSha}\`  \n${counts}\n\n`);
    if (message) {
      core.summary.addRaw(`${message}\n\n`);
    }
    if (results.length) {
      core.summary.addTable([
        [{ data: 'PR', header: true }, { data: 'Head', header: true }, { data: 'Decision', header: true }, { data: 'Details', header: true }],
        ...results.map(result => [`<a href="https://github.com/${repository.owner}/${repository.repo}/pull/${result.number}">#${result.number}</a>`, result.head, result.outcome, result.details.join(' ')])
      ]);
    }
    await core.summary.write();
  } catch {
    core.notice('Could not write the GitHub job summary; decisions are available in the job log.');
  }
}
