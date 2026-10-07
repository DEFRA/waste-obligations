const allowedUpdates = new Set(['version-update:semver-minor', 'version-update:semver-patch']);
const minimumReleaseAgeDays = 7;
const minimumReleaseAge = minimumReleaseAgeDays * 24 * 60 * 60 * 1000;
const earliestPublicationYear = 2010;
const earliestPublication = Date.UTC(earliestPublicationYear, 0, 1);
const updateTypePrefix = '  update-type: ';
const dependencyVersionPrefix = '  dependency-version: ';
const stableVersion = /^\d+\.\d+\.\d+(?:\.\d+)?$/;

// Expanding this list is a reviewed policy change. Build/test tooling stays manual.
const runtimePackages = new Set([
  'AWSSDK.Extensions.NETCore.Setup',
  'AWSSDK.SecurityToken',
  'AWSSDK.SimpleNotificationService',
  'AWSSDK.SQS',
  'Microsoft.AspNetCore.Authentication.JwtBearer',
  'Microsoft.AspNetCore.HeaderPropagation',
  'Microsoft.Extensions.Configuration.Abstractions',
  'Microsoft.Extensions.Configuration.Binder',
  'Microsoft.Extensions.Configuration.EnvironmentVariables',
  'Microsoft.Extensions.Configuration.Json',
  'Microsoft.Extensions.DependencyInjection.Abstractions',
  'Microsoft.Extensions.Hosting.Abstractions',
  'Microsoft.Extensions.Http.Resilience',
  'Microsoft.Extensions.Logging.Abstractions',
  'Microsoft.Extensions.Options.ConfigurationExtensions',
  'Microsoft.Extensions.Options.DataAnnotations',
  'MongoDB.Driver',
  'MongoDB.Driver.Authentication.AWS'
].map(x => x.toLowerCase()));
const projectFiles = new Set([
  'src/Api/Api.csproj',
  'src/AuditEvents/AuditEvents.csproj',
  'tests/Api.Tests/Api.Tests.csproj',
  'tests/Api.IntegrationTests/Api.IntegrationTests.csproj',
  'tests/Testing/Testing.csproj',
  'tools/healthcheck/HealthCheck.csproj',
  'tools/healthcheck/tests/HealthCheck.Tests/HealthCheck.Tests.csproj'
]);

// Read only canonical signed metadata. Duplicate dependency entries are normal
// when the same NuGet reference occurs in multiple projects.
function readDependencyAttribute(line, dependency) {
  if (line.startsWith(updateTypePrefix) && dependency) {
    dependency.types.push(line.slice(updateTypePrefix.length));
  } else if (line.startsWith(dependencyVersionPrefix) && dependency) {
    dependency.versions.push(line.slice(dependencyVersionPrefix.length));
  } else {
    return /^ {2}[a-z-]+: .+$/.test(line) || /^dependency-group: .+$/.test(line);
  }

  return true;
}

function readDependencyLines(lines) {
  const updates = [];
  let dependency;
  for (const line of lines) {
    const name = /^- dependency-name: (\S.*)$/.exec(line);
    if (name) {
      dependency = { name: name[1], types: [], versions: [] };
      updates.push(dependency);
    } else {
      if (!readDependencyAttribute(line, dependency)) {
        return undefined;
      }
    }
  }

  return updates;
}

export function readMinorOrPatchUpdates(message) {
  const blocks = [...message.matchAll(/^---\r?\n([\s\S]*?)^\.\.\.\r?$/gm)];
  if (blocks.length !== 1) {
    return undefined;
  }

  const lines = blocks[0][1].trimEnd().split(/\r?\n/);
  if (lines.shift() !== 'updated-dependencies:') {
    return undefined;
  }

  const updates = readDependencyLines(lines);
  if (!updates?.length || updates.some(x => x.types.length !== 1 || !allowedUpdates.has(x.types[0]))) {
    return undefined;
  }

  return updates;
}

export function getRuntimeTargets(commits) {
  const targets = new Map();
  for (const commit of commits) {
    const updates = readMinorOrPatchUpdates(commit.commit.message);
    if (!updates || updates.some(x => !runtimePackages.has(x.name.toLowerCase())
      || x.versions.length !== 1 || !stableVersion.test(x.versions[0]))) {
      return undefined;
    }
    for (const update of updates) {
      targets.set(update.name.toLowerCase(), update.versions[0]);
    }
  }

  return targets.size ? targets : undefined;
}

function numericVersion(version) {
  return version.split('.').map(Number).concat([0]).slice(0, 4);
}

function isRoutineIncrease(before, after) {
  if (!stableVersion.test(before) || !stableVersion.test(after)) {
    return false;
  }

  const a = numericVersion(before);
  const b = numericVersion(after);
  const difference = a.findIndex((x, i) => x !== b[i]);

  return a[0] === b[0] && difference > 0 && b[difference] > a[difference];
}

// Compare the complete files, masking only allowlisted PackageReference Version
// values. Added references, targets, sources or any other XML changes fail closed.
export function isRuntimeVersionOnlyChange(before, after, targets, changed) {
  const references = /(<PackageReference\s+Include="([^"]+)"\s+Version=")([^"]+)(")/g;
  const previous = new Map();
  const maskedBefore = before.replace(references, (text, prefix, name, version, suffix) => {
    const key = name.toLowerCase();
    if (!targets.has(key)) {
      return text;
    }
    if (previous.has(key)) {
      return text;
    }
    previous.set(key, version);

    return `${prefix}__VERSION__${suffix}`;
  });
  let valid = true;
  const seen = new Set();
  const maskedAfter = after.replace(references, (text, prefix, name, version, suffix) => {
    const key = name.toLowerCase();
    if (!targets.has(key)) {
      return text;
    }
    if (!previous.has(key) || seen.has(key) || version !== targets.get(key)) {
      valid = false;
    }
    seen.add(key);
    if (previous.get(key) !== version) {
      if (!isRoutineIncrease(previous.get(key) ?? '', version)) {
        valid = false;
      }
      changed.add(key);
    }

    return `${prefix}__VERSION__${suffix}`;
  });

  return valid && before !== after && maskedBefore === maskedAfter;
}

async function readProject(github, repository, path, ref) {
  const { data } = await github.rest.repos.getContent({ ...repository, path, ref });
  if (data.type !== 'file' || data.encoding !== 'base64' || data.size > 65536) {
    return undefined;
  }

  return Buffer.from(data.content, 'base64').toString('utf8');
}

export async function getNugetRelease(name, version, fetchJson = fetch) {
  async function readJson(url) {
    const response = await fetchJson(url, { signal: AbortSignal.timeout(10000), redirect: 'error' });
    if (!response.ok) {
      throw new Error(`NuGet returned HTTP ${response.status}.`);
    }

    return response.json();
  }

  const leaf = await readJson(`https://api.nuget.org/v3/registration5-gz-semver2/${name}/${version}.json`);
  const catalogUrl = new URL(leaf.catalogEntry);
  if (catalogUrl.origin !== 'https://api.nuget.org' || !catalogUrl.pathname.startsWith('/v3/catalog0/data/')) {
    throw new Error('Unrecognised NuGet catalog location.');
  }
  const catalog = await readJson(catalogUrl.href);
  if (catalog.id?.toLowerCase() !== name || !stableVersion.test(catalog.version ?? '')
    || numericVersion(catalog.version).some((x, i) => x !== numericVersion(version)[i])) {
    throw new Error('NuGet catalog identity did not match the signed update.');
  }

  return { published: catalog.published, listed: leaf.listed === true && catalog.listed === true };
}

async function hasOnlyTargetVersionChanges(github, repository, pr, baseSha, targets) {
  const files = await github.paginate(github.rest.pulls.listFiles, {
    ...repository, pull_number: pr.number, per_page: 100
  });
  if (!files.length || files.some(x => x.status !== 'modified' || !projectFiles.has(x.filename))) {
    return false;
  }

  const changed = new Set();
  for (const file of files) {
    const before = await readProject(github, repository, file.filename, baseSha);
    const after = await readProject(github, repository, file.filename, pr.head.sha);
    if (before === undefined || after === undefined || !isRuntimeVersionOnlyChange(before, after, targets, changed)) {
      return false;
    }
  }

  return changed.size === targets.size && [...targets.keys()].every(x => changed.has(x));
}

async function hasMatureListedReleases(targets, releaseLookup, now, core) {
  for (const [name, version] of targets) {
    const release = await releaseLookup(name, version);
    const published = Date.parse(release.published);
    if (release.listed !== true || !Number.isFinite(published)
      || published < earliestPublication || now - published < minimumReleaseAge) {
      core.notice('Every target NuGet release must be listed and at least seven days old; otherwise leave the PR manual.');

      return false;
    }
  }

  return true;
}

export async function passesSupplyChainPolicy({ github, repository, pr, baseSha, targets, core,
  releaseLookup = getNugetRelease, now = Date.now() }) {
  if (!targets) {
    core.notice('Only explicitly allowlisted runtime NuGet updates are eligible; tooling/actions/images stay manual.');

    return false;
  }
  try {
    if (!await hasOnlyTargetVersionChanges(github, repository, pr, baseSha, targets)) {
      return false;
    }

    return await hasMatureListedReleases(targets, releaseLookup, now, core);
  } catch (error) {
    core.notice(`Could not verify dependency contents/release age; no approval: ${error.message}`);

    return false;
  }
}
