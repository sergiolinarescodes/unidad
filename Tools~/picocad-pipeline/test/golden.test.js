// golden.test.js — proves the converter reproduces picoCAD 2's own exports.
// For every <model>.txt that has an app-exported <model>.obj next to it, we
// compare structurally (counts, indices) and numerically (float tolerance),
// then byte-for-byte as the strictest check. PNG textures are compared per-pixel
// on RGB (alpha policy is intentionally ours: transparent_color -> alpha 0).
// Usage: node test/golden.test.js [picocad2Dir]

"use strict";

const fs = require("fs");
const path = require("path");
const { loadModel } = require("../lib/parse");
const { buildFlatObj } = require("../lib/obj");
const { decodePng, textureToRgba } = require("../lib/png");

const picoDir = process.argv[2] || path.join(process.env.APPDATA || "", "picocad2");
const TOL = 1e-9;

let failures = 0;
let checks = 0;

function check(cond, label) {
  checks++;
  if (!cond) {
    failures++;
    console.error(`  FAIL: ${label}`);
  }
}

function parseObj(text) {
  const v = [], vt = [], f = [];
  for (const line of text.split("\n")) {
    if (line.startsWith("v ")) v.push(line.trim().split(/\s+/).slice(1).map(Number));
    else if (line.startsWith("vt ")) vt.push(line.trim().split(/\s+/).slice(1).map(Number));
    else if (line.startsWith("f ")) {
      f.push(line.trim().split(/\s+/).slice(1).map((c) => c.split("/").map(Number)));
    }
  }
  return { v, vt, f };
}

function closeVec(a, b) {
  return a.length === b.length && a.every((x, i) => Math.abs(x - b[i]) <= TOL);
}

function testModel(txtPath, objPath) {
  const name = path.basename(txtPath, ".txt");
  console.log(`golden: ${name}`);

  const model = loadModel(txtPath);
  check(model.errors.length === 0, `validation errors: ${model.errors.join("; ")}`);
  if (model.errors.length) return;

  const ours = buildFlatObj(model);
  const theirs = fs.readFileSync(objPath, "utf8");

  const a = parseObj(ours.obj);
  const b = parseObj(theirs);

  check(a.v.length === b.v.length, `vertex count ${a.v.length} != ${b.v.length}`);
  check(a.vt.length === b.vt.length, `vt count ${a.vt.length} != ${b.vt.length}`);
  check(a.f.length === b.f.length, `face count ${a.f.length} != ${b.f.length}`);

  if (a.v.length === b.v.length) {
    for (let i = 0; i < a.v.length; i++) {
      if (!closeVec(a.v[i], b.v[i])) { check(false, `vertex ${i + 1}: [${a.v[i]}] != [${b.v[i]}]`); break; }
    }
  }
  if (a.vt.length === b.vt.length) {
    for (let i = 0; i < a.vt.length; i++) {
      if (!closeVec(a.vt[i], b.vt[i])) { check(false, `vt ${i + 1}: [${a.vt[i]}] != [${b.vt[i]}]`); break; }
    }
  }
  if (a.f.length === b.f.length) {
    for (let i = 0; i < a.f.length; i++) {
      const fa = JSON.stringify(a.f[i]), fb = JSON.stringify(b.f[i]);
      if (fa !== fb) { check(false, `face ${i + 1}: ${fa} != ${fb}`); break; }
    }
  }

  // strictest: byte equality (catches formatting drift; informative, counted as check)
  check(ours.obj === theirs, "OBJ not byte-identical to app export (structure matched — formatting drift)");

  // MTL byte equality
  const mtlPath = objPath.replace(/\.obj$/, ".mtl");
  if (fs.existsSync(mtlPath)) {
    check(ours.mtl === fs.readFileSync(mtlPath, "utf8"), "MTL not byte-identical");
  }

  // PNG: compare RGB of opaque pixels against the app's exported texture
  const pngPath = objPath.replace(/\.obj$/, ".png");
  if (fs.existsSync(pngPath)) {
    try {
      const theirsPng = decodePng(fs.readFileSync(pngPath));
      check(theirsPng.width === 128 && theirsPng.height === 128,
        `app PNG is ${theirsPng.width}x${theirsPng.height}, expected 128x128`);
      if (theirsPng.width === 128 && theirsPng.height === 128) {
        const oursRgba = textureToRgba(model);
        let mismatches = 0;
        for (let i = 0; i < 128 * 128; i++) {
          for (let c = 0; c < 3; c++) {
            if (Math.abs(oursRgba[i * 4 + c] - theirsPng.rgba[i * 4 + c]) > 1) { mismatches++; break; }
          }
        }
        check(mismatches === 0, `texture RGB mismatch on ${mismatches} pixels`);
      }
    } catch (e) {
      check(false, `app PNG decode failed: ${e.message}`);
    }
  }
}

// discover golden pairs
const pairs = [];
for (const f of fs.readdirSync(picoDir)) {
  if (f.endsWith(".txt")) {
    const objPath = path.join(picoDir, f.replace(/\.txt$/, ".obj"));
    if (fs.existsSync(objPath)) pairs.push([path.join(picoDir, f), objPath]);
  }
}

if (pairs.length === 0) {
  console.error(`no golden pairs (<name>.txt + <name>.obj) found in ${picoDir}`);
  process.exit(1);
}

for (const [txt, obj] of pairs) testModel(txt, obj);

console.log(`\n${checks - failures}/${checks} checks passed across ${pairs.length} golden pair(s)`);
process.exit(failures ? 1 : 0);
