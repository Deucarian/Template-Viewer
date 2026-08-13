import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../harness.js", import.meta.url), "utf8");
const hostSource = await readFile(
  new URL("../deucarian-command-host.js", import.meta.url),
  "utf8");

test("harness exercises the canonical generic viewer commands", () => {
  for (const command of [
    "initialize_viewer",
    "select_elements",
    "clear_selection",
    "dispose_viewer"
  ]) {
    assert.match(source, new RegExp(`\\"${command}\\"`));
  }
});

test("harness uses an exact same-origin iframe endpoint and disposes", () => {
  assert.match(source, /targetOrigin:\s*window\.location\.origin/);
  assert.match(source, /allowedOrigins:\s*\[window\.location\.origin\]/);
  assert.match(source, /beforeunload/);
  assert.match(source, /host\.dispose\(\)/);
  assert.doesNotMatch(source, /targetOrigin:\s*["']\*["']/);
});

test("harness command host is self-contained", () => {
  assert.match(hostSource, /export class DeucarianCommandHost/);
  assert.match(hostSource, /export function createLegacyUnityDirectSender/);
  assert.doesNotMatch(hostSource, /(?:from|import)\s*["']\.\.\//);
});
