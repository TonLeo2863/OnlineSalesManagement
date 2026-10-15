import { readFile, readdir, access } from 'node:fs/promises';
import { join, relative } from 'node:path';
import assert from 'node:assert/strict';

// Structural audit only: does not build C#, run MVC, or connect to SQL Server.
export async function checkSource(repositoryRoot) {
  const app = join(repositoryRoot, 'OnlineSalesManagement');
  const project = await readFile(join(app, 'OnlineSalesManagement.csproj'), 'utf8');
  const entries = [...project.matchAll(/<(Compile|Content)\s+Include="([^"]+)"/g)]
    .map(m => ({ kind: m[1], file: m[2].replace(/\\/g, '/') }));
  const keys = entries.map(e => e.kind + ':' + e.file);
  assert.equal(new Set(keys).size, keys.length, 'Duplicate Compile/Content entries');
  for (const entry of entries) await access(join(app, entry.file));
  const files = [];
  async function walk(directory) {
    for (const item of await readdir(directory, { withFileTypes: true })) {
      if (item.isDirectory()) {
        if (!['bin', 'obj', '.vs'].includes(item.name.toLowerCase())) await walk(join(directory, item.name));
      } else if (/\.(cs|cshtml)$/.test(item.name)) files.push(join(directory, item.name));
    }
  }
  await walk(app);
  const decoder = new TextDecoder('utf-8', { fatal: true });
  for (const file of files) {
    const name = relative(app, file).replace(/\\/g, '/');
    const kind = name.endsWith('.cshtml') ? 'Content' : 'Compile';
    assert(entries.some(e => e.kind === kind && e.file === name), 'Source not registered in project: ' + name);
    decoder.decode(await readFile(file));
  }
  const controllers = files.filter(f => /Controllers[\\/][^\\/]+Controller\.cs$/.test(f));
  let posts = 0;
  for (const file of controllers) {
    const source = await readFile(file, 'utf8');
    for (const match of source.matchAll(/((?:\s*\[[^\]]+\])+)[\s]*public\s+(?:async\s+)?(?:ActionResult|Task<ActionResult>)\s+(\w+)\s*\(/g)) {
      if (/\bHttpPost\b/.test(match[1])) {
        posts++;
        assert(/\bValidateAntiForgeryToken\b/.test(match[1]), 'POST lacks AntiForgeryToken: ' + relative(app, file) + ':' + match[2]);
      }
    }
  }
  return { sourceFiles: files.length, projectEntries: entries.length, checkedPostActions: posts, utf8: 'valid', kind: 'structural-only' };
}
