import assert from "node:assert/strict";
import test from "node:test";
import { createHarnessServer } from "../local-server.mjs";

test("local server hosts the harness, generated catalog, and mock iframe", async () => {
  const server = createHarnessServer();
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  const origin = `http://127.0.0.1:${address.port}`;
  try {
    const configuration = await fetch(
      origin + "/harness-config.generated.json");
    assert.equal(configuration.status, 200);
    assert.deepEqual(await configuration.json(), {
      viewer_path: "/mock-viewer.html"
    });

    for (const path of [
      "/harness.html",
      "/harness.js",
      "/commands.generated.json",
      "/mock-viewer.html"
    ]) {
      const response = await fetch(origin + path);
      assert.equal(response.status, 200, path);
      assert.equal(response.headers.get("cache-control"), "no-store");
    }

    const traversal = await fetch(origin + "/..%2Fpackage.json");
    assert.equal(traversal.status, 404);
  } finally {
    await new Promise(resolve => server.close(resolve));
  }
});
