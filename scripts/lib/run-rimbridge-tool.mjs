#!/usr/bin/env node
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, copyFileSync, unlinkSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { setTimeout as sleep } from "node:timers/promises";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, "../..");

function usage() {
  console.error(`usage: run-rimbridge-tool.mjs [options]

Options:
  --game <id>             GABS game id. Default: rimworld-direct
  --tool <name>           Tool to call. Default: zeflammenwerfer/render_tank_pipe_pose_sweep
  --suite <name>          Evidence suite name. Default: tank-pipe-16-aims-sdk
  --run-id <id>           Run id. Default: UTC timestamp
  --out <dir>             Output root. Default: artifacts/rimbridge-evidence
  --save-name <name>      RimWorld save name. Default: zeflammenwerfer walkthrough
  --pawn-id <id>          Pawn id. Default: Thing_Human776
  --cell-x <x>            Screenshot target cell x. Default: 117
  --cell-z <z>            Screenshot target cell z. Default: 114
  --padding-cells <n>     screenshot_cell_rect padding. Default: 2
  --root-size <n>         Camera root size. Default: 2.5
  --pose-ticks <n>        Ticks to advance after each pose. Default: 1
  --connect-wait <sec>    Seconds to wait for mirrored game tools. Default: 90
  --force-takeover        Allow this GABS process to take runtime ownership.
`);
}

function readArg(args, name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (index + 1 >= args.length) throw new Error(`${name} requires a value`);
  return args[index + 1];
}

function hasArg(args, name) {
  return args.includes(name);
}

function timestampId() {
  return new Date().toISOString().replace(/[:.]/g, "-");
}

function defaultGabsBin() {
  if (process.env.GABS_BIN) return process.env.GABS_BIN;
  const codexBin = join(process.env.HOME || "", ".codex/mcp-servers/gabs/gabs");
  if (existsSync(codexBin)) return codexBin;
  return "gabs";
}

function collectPngPaths(value, into = new Set()) {
  if (typeof value === "string") {
    if (value.endsWith(".png") && existsSync(value)) into.add(value);
    return into;
  }
  if (Array.isArray(value)) {
    for (const item of value) collectPngPaths(item, into);
    return into;
  }
  if (value && typeof value === "object") {
    for (const item of Object.values(value)) collectPngPaths(item, into);
  }
  return into;
}

function collectEvidencePngPaths(result) {
  if (Array.isArray(result?.captures)) {
    const capturePaths = new Set();
    for (const capture of result.captures) {
      const screenshot = capture?.screenshot;
      const sourcePath = screenshot?.sourcePath;
      const path = screenshot?.path;
      if (typeof sourcePath === "string" && sourcePath.endsWith(".png") && existsSync(sourcePath)) {
        capturePaths.add(sourcePath);
      } else if (typeof path === "string" && path.endsWith(".png") && existsSync(path)) {
        capturePaths.add(path);
      }
    }
    if (capturePaths.size > 0) return capturePaths;
  }

  return collectPngPaths(result);
}

function collectDerivedScreenshotPaths(result, filePrefix) {
  const paths = new Set();
  if (Array.isArray(result?.captures) === false) return paths;

  for (const capture of result.captures) {
    const screenshot = capture?.screenshot;
    const sourcePath = screenshot?.sourcePath;
    const path = screenshot?.path;
    if (typeof sourcePath !== "string" || typeof path !== "string") continue;
    if (sourcePath === path || path.endsWith(".png") === false || existsSync(path) === false) continue;
    const name = basename(path);
    if (name.startsWith(filePrefix) && name.includes("__cell_rect")) paths.add(path);
  }
  return paths;
}

async function waitForGameTool(client, gameId, toolName, timeoutSeconds, forceTakeover) {
  const deadline = Date.now() + timeoutSeconds * 1000;
  let lastStatus = null;
  while (Date.now() < deadline) {
    const names = await client.tool("games_tool_names", {
      gameId,
      query: toolName,
      brief: true,
      limit: 20
    });
    if ((names.tools || []).some(tool => tool.gabpName === toolName || tool.localName === toolName)) return names;
    lastStatus = await client.tool("games_status", { gameId });
    if (lastStatus.status === "shared-running" || lastStatus.status === "running") {
      await client.tool("games_connect", { gameId, timeout: 30, forceTakeover });
    }
    await sleep(1000);
  }
  throw new Error(`Timed out waiting for ${toolName} on ${gameId}. Last status: ${JSON.stringify(lastStatus, null, 2)}`);
}

function makeLineReader(stream, onLine) {
  let buffer = "";
  stream.setEncoding("utf8");
  stream.on("data", chunk => {
    buffer += chunk;
    for (;;) {
      const index = buffer.indexOf("\n");
      if (index < 0) break;
      const line = buffer.slice(0, index);
      buffer = buffer.slice(index + 1);
      if (line.trim().length > 0) onLine(line);
    }
  });
}

class McpClient {
  constructor(command, args) {
    this.proc = spawn(command, args, {
      cwd: repoRoot,
      stdio: ["pipe", "pipe", "inherit"]
    });
    this.nextId = 1;
    this.pending = new Map();
    makeLineReader(this.proc.stdout, line => this.handleLine(line));
  }

  handleLine(line) {
    let message;
    try {
      message = JSON.parse(line);
    } catch {
      throw new Error(`Invalid MCP JSON line: ${line}`);
    }
    if (message.id == null) return;
    const pending = this.pending.get(message.id);
    if (!pending) return;
    this.pending.delete(message.id);
    if (message.error) pending.reject(new Error(JSON.stringify(message.error, null, 2)));
    else pending.resolve(message.result);
  }

  request(method, params = undefined) {
    const id = this.nextId++;
    const message = { jsonrpc: "2.0", id, method };
    if (params !== undefined) message.params = params;
    return new Promise((resolvePromise, rejectPromise) => {
      this.pending.set(id, { resolve: resolvePromise, reject: rejectPromise });
      this.proc.stdin.write(`${JSON.stringify(message)}\n`);
    });
  }

  notify(method, params = undefined) {
    const message = { jsonrpc: "2.0", method };
    if (params !== undefined) message.params = params;
    this.proc.stdin.write(`${JSON.stringify(message)}\n`);
  }

  async tool(name, args = {}) {
    const result = await this.request("tools/call", {
      name,
      arguments: args
    });
    if (result?.isError) throw new Error(JSON.stringify(result, null, 2));
    return result.structuredContent ?? result;
  }

  close() {
    this.proc.kill("SIGTERM");
  }
}

const args = process.argv.slice(2);
if (hasArg(args, "--help")) {
  usage();
  process.exit(0);
}

const gameId = readArg(args, "--game", process.env.GABS_GAME_ID || "rimworld-direct");
const toolName = readArg(args, "--tool", "zeflammenwerfer/render_tank_pipe_pose_sweep");
const suite = readArg(args, "--suite", "tank-pipe-16-aims-sdk");
const runId = readArg(args, "--run-id", timestampId());
const outRoot = resolve(repoRoot, readArg(args, "--out", "artifacts/rimbridge-evidence"));
const outDir = join(outRoot, suite, runId);
const forceTakeover = hasArg(args, "--force-takeover") || process.env.GABS_FORCE_TAKEOVER === "1";
const connectWaitSeconds = Number(readArg(args, "--connect-wait", "90"));
mkdirSync(outDir, { recursive: true });

const parameters = {
  loadGame: true,
  runId,
  saveName: readArg(args, "--save-name", "zeflammenwerfer walkthrough"),
  pawnId: readArg(args, "--pawn-id", "Thing_Human776"),
  cellX: Number(readArg(args, "--cell-x", "117")),
  cellZ: Number(readArg(args, "--cell-z", "114")),
  paddingCells: Number(readArg(args, "--padding-cells", "2")),
  rootSize: Number(readArg(args, "--root-size", "2.5")),
  poseTicks: Number(readArg(args, "--pose-ticks", "1")),
  filePrefix: `${suite}-${runId}`
};

const gabsBin = defaultGabsBin();
const client = new McpClient(gabsBin, ["server", "stdio", "--log-level", "error"]);

try {
  await client.request("initialize", {
    protocolVersion: "2024-11-05",
    capabilities: {},
    clientInfo: { name: "zeflammenwerfer-evidence-suite", version: "0.2.0" }
  });
  client.notify("notifications/initialized", {});

  const status = await client.tool("games_status", { gameId });
  const gameIsRunning = status.status === "running" || status.status === "shared-running";
  if (!gameIsRunning) {
    await client.tool("games_start", { gameId, resetEndpoint: true, timeout: 180 });
  } else {
    await client.tool("games_connect", { gameId, timeout: 60, forceTakeover });
  }
  await waitForGameTool(client, gameId, toolName, connectWaitSeconds, forceTakeover);

  const result = await client.tool("games_call_tool", {
    gameId,
    tool: toolName,
    timeout: 300,
    arguments: parameters
  });

  const manifest = {
    suite,
    runId,
    gameId,
    toolName,
    parameters,
    result,
    copiedImages: [],
    deletedDerivedImages: []
  };

  for (const pngPath of collectEvidencePngPaths(result)) {
    const target = join(outDir, basename(pngPath));
    copyFileSync(pngPath, target);
    manifest.copiedImages.push({
      source: pngPath,
      copiedTo: target
    });
  }

  for (const pngPath of collectDerivedScreenshotPaths(result, parameters.filePrefix)) {
    try {
      unlinkSync(pngPath);
      manifest.deletedDerivedImages.push({
        source: pngPath,
        deleted: true
      });
    } catch (error) {
      manifest.deletedDerivedImages.push({
        source: pngPath,
        deleted: false,
        error: String(error?.message || error)
      });
    }
  }

  const manifestPath = join(outDir, "manifest.json");
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

  console.log(`suite=${suite}`);
  console.log(`runId=${runId}`);
  console.log(`manifest=${manifestPath}`);
  console.log(`images=${manifest.copiedImages.length}`);
  if (result.success === false || manifest.copiedImages.length !== 16) process.exitCode = 1;
} finally {
  client.close();
}
