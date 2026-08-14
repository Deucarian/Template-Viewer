import { DeucarianCommandHost } from "./deucarian-command-host.js";

const output = document.querySelector("#events");
const iframe = document.querySelector("#viewer");
let revision = 0;
const host = new DeucarianCommandHost({
  iframe,
  targetOrigin: window.location.origin,
  transportId: "web-viewer"
});

host.on("deucarian-command-ready", () => write("transport ready"));
host.on("deucarian-command-response", value =>
  write("response " + JSON.stringify(value)));
host.on("deucarian-command-event", value =>
  write("event " + JSON.stringify(value)));
host.on("deucarian-command-error", value =>
  write("error " + JSON.stringify(value)));
host.start();

function send(command, payload = {}) {
  revision += 1;
  host.sendCommand({
    protocol_version: 1,
    command_id: `harness-${revision}`,
    command,
    payload: { revision, ...payload },
    metadata: { source: "local-harness" }
  });
}

function write(value) {
  output.textContent = `${new Date().toISOString()} ${value}\n${output.textContent}`;
}

document.querySelector("#initialize").onclick = () => send("initialize_viewer");
document.querySelector("#red").onclick = () => send("select_elements", { element_ids: ["red"] });
document.querySelector("#green-blue").onclick = () => send("select_elements", { element_ids: ["green", "blue"] });
document.querySelector("#clear").onclick = () => send("clear_selection");
document.querySelector("#invalid").onclick = () => send("select_elements", { element_ids: ["missing"] });
document.querySelector("#stale").onclick = () => {
  host.sendCommand({
    protocol_version: 1,
    command_id: "harness-stale",
    command: "select_elements",
    payload: { revision: Math.max(0, revision - 1), element_ids: ["blue"] },
    metadata: { source: "local-harness" }
  });
};
document.querySelector("#dispose").onclick = () => send("dispose_viewer");
window.addEventListener("beforeunload", () => host.dispose(), { once: true });
