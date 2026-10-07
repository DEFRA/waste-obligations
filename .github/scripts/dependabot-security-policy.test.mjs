import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getNugetRelease, getRuntimeTargets, isRuntimeVersionOnlyChange } from './dependabot-security-policy.mjs';

const targets = new Map([['mongodb.driver', '3.10.0']]);
const before = '<Project>\n<PackageReference Include="MongoDB.Driver" Version="3.9.0" />\n</Project>';
const after = '<Project>\n<PackageReference Include="MongoDB.Driver" Version="3.10.0" />\n</Project>';

test('an allowlisted reference may change its version without changing other XML', () => {
  const changed = new Set();

  assert.equal(isRuntimeVersionOnlyChange(before, after, targets, changed), true);
  assert.deepEqual([...changed], ['mongodb.driver']);
});

for (const [name, candidate] of [
  ['an executable build target', after.replace('</Project>', '<Target Name="Build"><Exec Command="curl example.com" /></Target></Project>')],
  ['a newly added reference', after.replace('</Project>', '<PackageReference Include="Other.Package" Version="1.0.0" /></Project>')],
  ['a changed reference identity', after.replace('MongoDB.Driver', 'Microsoft.Build')],
  ['changed package asset controls', after.replace('/>', 'ExcludeAssets="build" />')],
  ['a removed reference', '<Project></Project>'],
  ['a version different from signed metadata', after.replace('3.10.0', '3.11.0')],
  ['a prerelease target', after.replace('3.10.0', '3.10.0-preview.1')]
]) {
  test(`${name} is rejected`, () => {
    assert.equal(isRuntimeVersionOnlyChange(before, candidate, targets, new Set()), false);
  });
}

for (const version of ['4.0.0', '3.8.0', '3.9.0']) {
  test(`a major, downgrade or unchanged ${version} target is rejected`, () => {
    assert.equal(isRuntimeVersionOnlyChange(before, after.replace('3.10.0', version),
      new Map([['mongodb.driver', version]]), new Set()), false);
  });
}

function commit(entries) {
  return { commit: { message: `Bump packages\n\n---\nupdated-dependencies:\n${entries.map(x =>
    `- dependency-name: ${x.name}\n  dependency-version: ${x.version}\n  update-type: version-update:semver-patch`
  ).join('\n')}\n...\n` } };
}

test('duplicate signed entries for shared references produce one target', () => {
  const entry = { name: 'AWSSDK.SQS', version: '4.0.100.15' };

  assert.deepEqual([...getRuntimeTargets([commit([entry, entry])])], [['awssdk.sqs', '4.0.100.15']]);
});

test('an added Dependabot commit replaces the old target version', () => {
  assert.deepEqual([...getRuntimeTargets([
    commit([{ name: 'MongoDB.Driver', version: '3.9.1' }]),
    commit([{ name: 'MongoDB.Driver', version: '3.9.2' }])
  ])], [['mongodb.driver', '3.9.2']]);
});

for (const name of ['actions/checkout', 'SonarAnalyzer.CSharp', 'xunit.v3', 'dotnet/sdk', 'New.Runtime.Package']) {
  test(`${name} is outside the runtime allowlist`, () => {
    assert.equal(getRuntimeTargets([commit([{ name, version: '3.10.0' }])]), undefined);
  });
}

test('a mixed runtime/tooling group stays manual', () => {
  assert.equal(getRuntimeTargets([commit([
    { name: 'MongoDB.Driver', version: '3.9.1' }, { name: 'csharpier', version: '1.3.1' }
  ])]), undefined);
});

function registry(leafOverrides = {}, catalogOverrides = {}) {
  const catalogEntry = 'https://api.nuget.org/v3/catalog0/data/2026.09.29/mongodb.driver.3.10.0.json';
  const calls = [];
  const fetchJson = async (url, options) => {
    calls.push({ url, options });
    const body = calls.length === 1 ? { catalogEntry, listed: true, ...leafOverrides }
      : { id: 'MongoDB.Driver', version: '3.10.0', published: '2026-09-29T12:00:00Z', listed: true, ...catalogOverrides };

    return { ok: true, json: async () => body };
  };

  return { calls, fetchJson };
}

test('NuGet identity, listing and publication date are read without following redirects', async () => {
  const r = registry();

  assert.deepEqual(await getNugetRelease('mongodb.driver', '3.10.0', r.fetchJson),
    { listed: true, published: '2026-09-29T12:00:00Z' });
  assert.equal(r.calls[0].url, 'https://api.nuget.org/v3/registration5-gz-semver2/mongodb.driver/3.10.0.json');
  assert.ok(r.calls.every(x => x.options.redirect === 'error' && x.options.signal instanceof AbortSignal));
});

test('an unlisted catalog or registration leaf cannot establish a listed release', async () => {
  for (const r of [registry({ listed: false }), registry({}, { listed: false })]) {
    assert.equal((await getNugetRelease('mongodb.driver', '3.10.0', r.fetchJson)).listed, false);
  }
});

test('an unexpected catalog host is rejected before a second request', async () => {
  const r = registry({ catalogEntry: 'https://attacker.example/package.json' });

  await assert.rejects(getNugetRelease('mongodb.driver', '3.10.0', r.fetchJson), /catalog location/);
  assert.equal(r.calls.length, 1);
});

test('registry package identity/version mismatches are rejected', async () => {
  for (const overrides of [{ id: 'Other.Package' }, { version: '3.11.0' }]) {
    const r = registry({}, overrides);

    await assert.rejects(getNugetRelease('mongodb.driver', '3.10.0', r.fetchJson), /identity/);
  }
});

test('a registry error cannot establish release age', async () => {
  await assert.rejects(getNugetRelease('mongodb.driver', '3.10.0', async () => ({ ok: false, status: 503 })), /HTTP 503/);
});
