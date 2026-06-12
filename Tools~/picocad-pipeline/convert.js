#!/usr/bin/env node
// convert.js — picoCAD 2 save (.txt) -> Unity-ready assets.
// Usage: node convert.js <model.txt> --out <dir> [--scale N] [--name <override>]
// Emits: <name>.gltf + <name>.bin + <name>.png   (primary — Unity ingests via glTFast)
//        <name>.obj + <name>.mtl                 (parity artifact, byte-matches the app's export)
//        <name>.manifest.json                    (pipeline sidecar: palette, flags, clips)
// Pure Node, no external dependencies.

"use strict";

const fs = require("fs");
const path = require("path");
const { loadModel, usesTransparency } = require("./lib/parse");
const { buildFlatObj } = require("./lib/obj");
const { encodePng, textureToRgba } = require("./lib/png");

function fail(msg) {
  console.error(`[convert] ERROR: ${msg}`);
  process.exit(1);
}

function parseArgs(argv) {
  const args = { scale: 1 };
  const rest = [];
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--out") args.out = argv[++i];
    else if (a === "--scale") args.scale = parseFloat(argv[++i]);
    else if (a === "--name") args.name = argv[++i];
    else rest.push(a);
  }
  args.input = rest[0];
  return args;
}

function main() {
  const args = parseArgs(process.argv);
  if (!args.input) fail("usage: node convert.js <model.txt> --out <dir> [--scale N] [--name <override>]");
  if (!fs.existsSync(args.input)) fail(`input not found: ${args.input}`);

  const model = loadModel(args.input);
  if (model.errors.length) {
    model.errors.forEach((e) => console.error(`[convert] validation error: ${e}`));
    process.exit(1);
  }
  model.warnings.forEach((w) => console.warn(`[convert] warning: ${w}`));
  if (args.name) model.name = args.name;

  const outDir = args.out || path.dirname(model.sourceFile);
  fs.mkdirSync(outDir, { recursive: true });
  const base = path.join(outDir, model.name);
  const written = [];

  // --- parity OBJ/MTL (app-export-compatible) ---
  const { obj, mtl } = buildFlatObj(model);
  fs.writeFileSync(`${base}.obj`, obj);
  fs.writeFileSync(`${base}.mtl`, mtl);
  written.push(`${base}.obj`, `${base}.mtl`);

  // --- texture PNG (RGBA, transparent_color -> alpha 0) ---
  const rgba = textureToRgba(model);
  fs.writeFileSync(`${base}.png`, encodePng(rgba, 128, 128));
  written.push(`${base}.png`);

  // --- glTF (primary Unity artifact) ---
  let gltfInfo = null;
  let gltfModule = null;
  try {
    gltfModule = require("./lib/gltf");
  } catch {
    console.warn("[convert] lib/gltf.js not present yet — skipping glTF emission");
  }
  if (gltfModule) {
    gltfInfo = gltfModule.writeGltf(model, base, { scale: args.scale });
    written.push(`${base}.gltf`, `${base}.bin`);
  }

  // --- manifest sidecar ---
  const manifest = {
    schema: 1,
    name: model.name,
    sourceFile: model.sourceFile,
    picoCadVersion: model.version,
    generatedAt: null, // stamped by the orchestrator, not here (determinism)
    scale: args.scale,
    palette: model.palette.map(([r, g, b]) =>
      "#" + [r, g, b].map((v) => Math.round(v * 255).toString(16).padStart(2, "0")).join("")
    ),
    transparentColor: model.transparentColor,
    usesTransparency: usesTransparency(model),
    meshCount: model.meshCount,
    faceCount: model.faceCount,
    motionDuration: model.motionDuration,
    animations: gltfInfo ? gltfInfo.animations : [],
    files: {
      gltf: gltfInfo ? `${model.name}.gltf` : null,
      texture: `${model.name}.png`,
      obj: `${model.name}.obj`,
    },
    warnings: model.warnings,
  };
  fs.writeFileSync(`${base}.manifest.json`, JSON.stringify(manifest, null, 2));
  written.push(`${base}.manifest.json`);

  console.log(`[convert] OK ${model.name}: ${model.meshCount} meshes, ${model.faceCount} faces` +
    (gltfInfo ? `, ${gltfInfo.animations.length} animation(s)` : ""));
  written.forEach((f) => console.log(`[convert]   ${f}`));
}

main();
