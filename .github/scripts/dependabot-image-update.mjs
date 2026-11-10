import { readUpdates } from './dependabot-metadata.mjs';

function readImageMetadata(message) {
  const updates = readUpdates(message);
  if (updates?.length !== 1 || updates[0].types.length !== 0 || updates[0].versions.length !== 1) {
    return undefined;
  }
  const { name, versions: [version] } = updates[0];
  const title = /^Bump (\S+) from (v?\d+\.\d+\.\d+) to (v?\d+\.\d+\.\d+) in \S+$/m.exec(message);
  if (title?.[1] !== name || title?.[3] !== version) {
    return undefined;
  }
  return { name, oldTag: title[2], newTag: version };
}

function matchesImageChange(files, update) {
  if (files.length !== 1 || files[0].status !== 'modified' || !files[0].filename.endsWith('.yml') || !files[0].patch) {
    return false;
  }
  const changes = files[0].patch.split('\n').filter(x => /^[+-]/.test(x) && !/^(---|\+\+\+)/.test(x));
  const imageLine = (prefix, tag) => `${prefix}    image: ${update.name}:${tag}@sha256:`;
  return changes.length === 2 && changes[0].startsWith(imageLine('-', update.oldTag))
    && changes[1].startsWith(imageLine('+', update.newTag))
    && changes.every(x => /@sha256:[a-f0-9]{64}$/.test(x));
}

function classifyVersions(oldTag, newTag) {
  const oldVersion = oldTag.replace(/^v/, '').split('.').map(Number);
  const newVersion = newTag.replace(/^v/, '').split('.').map(Number);
  if (oldVersion[0] !== 0 || newVersion[0] !== 0) {
    return undefined;
  }
  if (newVersion[1] > oldVersion[1]) {
    return 'minor';
  }
  if (newVersion[1] === oldVersion[1] && newVersion[2] > oldVersion[2]) {
    return 'patch';
  }
  return undefined;
}

// Only infer a version when a single signed update agrees with a single image change.
export function classifyImageUpdate(message, files) {
  const update = readImageMetadata(message);
  if (!update || !matchesImageChange(files, update)) {
    return undefined;
  }
  return classifyVersions(update.oldTag, update.newTag);
}
