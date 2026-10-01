const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Hearthstone Deck Tracker/Importing/FirestoneExport.js'), 'utf8');

async function run({ missingDb = false, missingTable = false, readFailure = false } = {}) {
  let closed = false, aborted = false, copied, error;
  const records = [{ reviewId: 'first', playerDeckName: 'Моя колода' }, { reviewId: 'second' }];
  const context = {
    indexedDB: {
      open(name) {
        assert.equal(name, 'FirestoneDB');
        const request = { transaction: { abort() { aborted = true; } } };
        queueMicrotask(() => {
          if (missingDb) return request.onupgradeneeded();
          request.result = {
            objectStoreNames: { contains(name) { assert.equal(name, 'matchHistory'); return !missingTable; } },
            transaction(name, mode) {
              assert.equal(name, 'matchHistory');
              assert.equal(mode, 'readonly');
              return { objectStore(name) {
                assert.equal(name, 'matchHistory');
                return { getAll() {
                  const read = {};
                  queueMicrotask(() => {
                    if (readFailure) { read.error = new Error('read failure'); read.onerror(); }
                    else { read.result = records; read.onsuccess(); }
                  });
                  return read;
                } };
              } };
            },
            close() { closed = true; }
          };
          request.onsuccess();
        });
        return request;
      }
    },
    copy(value) { copied = JSON.parse(value); },
    console: { info() {}, error(label, message) { error = message; } }
  };
  await vm.runInNewContext(source, context);
  if (missingDb) { assert.ok(aborted); assert.match(error, /not found/); }
  else if (missingTable) { assert.ok(closed); assert.match(error, /not found/); }
  else if (readFailure) { assert.ok(closed); assert.equal(error, 'read failure'); }
  else { assert.ok(closed); assert.deepEqual(copied.stats, records); }
  if (missingDb || missingTable || readFailure) assert.equal(copied, undefined);
}

(async () => {
  await run();
  await run({ missingDb: true });
  await run({ missingTable: true });
  await run({ readFailure: true });
  console.log('PASS: complete Firestone export, read-only transaction, missing database/table and read failure');
})().catch(error => { console.error(error); process.exitCode = 1; });
