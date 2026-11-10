import { readUpdates } from './dependabot-metadata.mjs';

// Only infer a version when a single signed update agrees with a single image change.
export function classifyImageUpdate(message, files) {
  const updates = readUpdates(message);
  if (updates?.length !== 1 || updates[0].types.length !== 0 || updates[0].versions.length !== 1
    || files.length !== 1 || files[0].status !== 'modified' || !files[0].filename.endsWith('.yml')
    || !files[0].patch) return undefined;

  const { name, versions: [version] } = updates[0];
  const title = /^Bump (\S+) from (v?\d+\.\d+\.\d+) to (v?\d+\.\d+\.\d+) in \S+$/m.exec(message);
  if (!title || title[1] !== name || title[3] !== version) return undefined;
  const oldVersion = title[2].replace(/^v/, '').split('.').map(Number);
  const newVersion = version.replace(/^v/, '').split('.').map(Number);
  if (oldVersion[0] !== 0 || newVersion[0] !== 0) return undefined;

  const changes = files[0].patch.split('\n').filter(x => /^[+-]/.test(x) && !/^(---|\+\+\+)/.test(x));
  const imageLine = (prefix, tag) => `${prefix}    image: ${name}:${tag}@sha256:`;
  if (changes.length !== 2 || !changes[0].startsWith(imageLine('-', title[2]))
    || !changes[1].startsWith(imageLine('+', version))
    || !changes.every(x => /@sha256:[a-f0-9]{64}$/.test(x))) return undefined;

  if (newVersion[1] > oldVersion[1]) return 'minor';
  if (newVersion[1] === oldVersion[1] && newVersion[2] > oldVersion[2]) return 'patch';
  return undefined;
}
