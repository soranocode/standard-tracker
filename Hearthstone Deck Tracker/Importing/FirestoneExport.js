// Run in the Firestone Overwolf window's DevTools console.
// Reads the existing database; never creates, upgrades or writes it.
(async () => {
  const request = indexedDB.open('FirestoneDB');
  const db = await new Promise((resolve, reject) => {
    request.onupgradeneeded = () => {
      request.transaction.abort();
      reject(new Error('FirestoneDB not found. Open the console of the Firestone window.'));
    };
    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
  });
  try {
    if (!db.objectStoreNames.contains('matchHistory')) {
      throw new Error('matchHistory not found. Open the console of the Firestone window.');
    }
    const transaction = db.transaction('matchHistory', 'readonly');
    const rows = await new Promise((resolve, reject) => {
      const read = transaction.objectStore('matchHistory').getAll();
      read.onsuccess = () => resolve(read.result);
      read.onerror = () => reject(read.error);
      transaction.onabort = () => reject(transaction.error);
    });
    copy(JSON.stringify({ stats: rows }, null, 2));
    console.info(`Copied ${rows.length} matches. Paste into Notepad and save as firestone-history.json (UTF-8).`);
  } finally {
    db.close();
  }
})().catch(error => console.error('Firestone export failed:', error.message));
