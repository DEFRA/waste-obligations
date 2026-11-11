const allowedUpdates = new Set(['version-update:semver-minor', 'version-update:semver-patch']);
const updateTypePrefix = '  update-type: ';
const dependencyVersionPrefix = '  dependency-version: ';

// Read the signed metadata written by Dependabot for every dependency in the group.
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
      continue;
    }
    if (!readDependencyAttribute(line, dependency)) {
      return undefined;
    }
  }

  return updates;
}

export function readUpdates(message) {
  const blocks = [...message.matchAll(/^---\r?\n([\s\S]*?)^\.\.\.\r?$/gm)];
  if (blocks.length !== 1) {
    return undefined;
  }

  const lines = blocks[0][1].trimEnd().split(/\r?\n/);
  if (lines.shift() !== 'updated-dependencies:') {
    return undefined;
  }

  const updates = readDependencyLines(lines);
  if (!updates?.length) {
    return undefined;
  }

  return updates;
}

export function readMinorOrPatchUpdates(message) {
  const updates = readUpdates(message);
  return updates?.every(x => x.types.length === 1 && allowedUpdates.has(x.types[0])) ? updates : undefined;
}

