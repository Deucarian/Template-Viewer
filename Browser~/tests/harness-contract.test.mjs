import assert from "node:assert/strict";
import test from "node:test";

class FakeWindow extends EventTarget {
  constructor() {
    super();
    this.location = { origin: "https://harness.example" };
    this.posts = [];
  }

  postMessage(message, targetOrigin) {
    this.posts.push({ message, targetOrigin });
  }
}

class FakeIframe extends EventTarget {
  constructor(contentWindow) {
    super();
    this.contentWindow = contentWindow;
  }
}

function messageEvent(source, origin, data) {
  const event = new Event("message");
  Object.defineProperties(event, {
    source: { value: source },
    origin: { value: origin },
    data: { value: data }
  });
  return event;
}

test("the real harness drives every command through the canonical host", async () => {
  const browserWindow = new FakeWindow();
  const viewerWindow = new FakeWindow();
  const iframe = new FakeIframe(viewerWindow);
  const output = { textContent: "" };
  const buttons = Object.fromEntries([
    "initialize", "red", "green-blue", "clear", "invalid", "stale", "dispose"
  ].map(id => [id, {}]));

  globalThis.window = browserWindow;
  globalThis.CustomEvent = class extends Event {
    constructor(type, options = {}) {
      super(type);
      this.detail = options.detail;
    }
  };
  globalThis.document = {
    querySelector(selector) {
      if (selector === "#viewer") return iframe;
      if (selector === "#events") return output;
      return buttons[selector.slice(1)];
    }
  };

  await import(`../harness.js?test=${Date.now()}`);
  const probe = viewerWindow.posts.at(-1).message;
  assert.equal(probe.type, "deucarian-command-probe");
  viewerWindow.posts.length = 0;

  browserWindow.dispatchEvent(messageEvent(viewerWindow, browserWindow.location.origin, {
    source: "deucarian-command-transport",
    type: "deucarian-command-ready",
    transport_id: "web-viewer",
    connection_generation: 1,
    host_session: probe.host_session
  }));
  assert.match(output.textContent, /transport ready/);

  for (const id of [
    "initialize", "red", "green-blue", "clear", "invalid", "stale", "dispose"
  ]) {
    assert.equal(typeof buttons[id].onclick, "function", `${id} is wired`);
    buttons[id].onclick();
  }

  assert.deepEqual(
    viewerWindow.posts.map(entry => entry.message.message.command),
    [
      "initialize_viewer",
      "select_elements",
      "select_elements",
      "clear_selection",
      "select_elements",
      "select_elements",
      "dispose_viewer"
    ]);
  assert.ok(viewerWindow.posts.every(entry =>
    entry.targetOrigin === browserWindow.location.origin));
  assert.deepEqual(
    viewerWindow.posts[1].message.message.payload.element_ids,
    ["red"]);
  assert.deepEqual(
    viewerWindow.posts[2].message.message.payload.element_ids,
    ["green", "blue"]);
  assert.ok(
    viewerWindow.posts[5].message.message.payload.revision <
      viewerWindow.posts[4].message.message.payload.revision,
    "the stale action sends an older revision");

  browserWindow.dispatchEvent(messageEvent(viewerWindow, browserWindow.location.origin, {
    source: "deucarian-command-transport",
    type: "deucarian-command-event",
    transport_id: "web-viewer",
    connection_generation: 1,
    host_session: probe.host_session,
    message: { event: "selection_applied" }
  }));
  assert.match(output.textContent, /selection_applied/);

  browserWindow.dispatchEvent(new Event("beforeunload"));
  assert.throws(() => buttons.initialize.onclick(), /disposed/);
});
