// gltf-check.js — structural validity checks for converter-emitted glTF files.
// Usage: node test/gltf-check.js <file.gltf> [...more]

"use strict";

const fs = require("fs");
const path = require("path");

const COMPONENT_SIZE = { 5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4 };
const TYPE_COUNT = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16 };

let failures = 0;
function check(cond, label) {
  if (!cond) { failures++; console.error(`  FAIL: ${label}`); }
}

function validate(gltfPath) {
  console.log(`gltf-check: ${path.basename(gltfPath)}`);
  const g = JSON.parse(fs.readFileSync(gltfPath, "utf8"));
  const dir = path.dirname(gltfPath);

  check(g.asset && g.asset.version === "2.0", "asset.version 2.0");

  // buffer matches bin file
  for (const buf of g.buffers || []) {
    const binPath = path.join(dir, buf.uri);
    check(fs.existsSync(binPath), `bin exists: ${buf.uri}`);
    if (fs.existsSync(binPath)) {
      check(fs.statSync(binPath).size === buf.byteLength, `bin size ${fs.statSync(binPath).size} == declared ${buf.byteLength}`);
    }
  }

  // bufferViews inside buffer
  (g.bufferViews || []).forEach((bv, i) => {
    const buf = g.buffers[bv.buffer];
    check(bv.byteOffset + bv.byteLength <= buf.byteLength, `bufferView[${i}] within buffer`);
    check(bv.byteOffset % 4 === 0, `bufferView[${i}] 4-byte aligned`);
  });

  // accessors fit their views
  (g.accessors || []).forEach((a, i) => {
    const bv = g.bufferViews[a.bufferView];
    const size = COMPONENT_SIZE[a.componentType] * TYPE_COUNT[a.type] * a.count;
    check((a.byteOffset || 0) + size <= bv.byteLength, `accessor[${i}] fits bufferView (${size} <= ${bv.byteLength})`);
  });

  // meshes: indices within POSITION count, material valid
  const bin = g.buffers && g.buffers[0] ? fs.readFileSync(path.join(dir, g.buffers[0].uri)) : null;
  (g.meshes || []).forEach((m, mi) => {
    m.primitives.forEach((p) => {
      const posAcc = g.accessors[p.attributes.POSITION];
      const uvAcc = g.accessors[p.attributes.TEXCOORD_0];
      const idxAcc = g.accessors[p.indices];
      check(uvAcc.count === posAcc.count, `mesh[${mi}] uv count == pos count`);
      check(idxAcc.count % 3 === 0, `mesh[${mi}] index count divisible by 3`);
      check(Array.isArray(posAcc.min) && Array.isArray(posAcc.max), `mesh[${mi}] POSITION has min/max`);
      if (bin) {
        const bv = g.bufferViews[idxAcc.bufferView];
        const wide = idxAcc.componentType === 5125;
        let maxIdx = 0;
        for (let i = 0; i < idxAcc.count; i++) {
          const v = wide ? bin.readUInt32LE(bv.byteOffset + i * 4) : bin.readUInt16LE(bv.byteOffset + i * 2);
          if (v > maxIdx) maxIdx = v;
        }
        check(maxIdx < posAcc.count, `mesh[${mi}] max index ${maxIdx} < vertex count ${posAcc.count}`);
      }
    });
  });

  // nodes: children valid, mesh refs valid
  (g.nodes || []).forEach((n, i) => {
    (n.children || []).forEach((c) => check(c >= 0 && c < g.nodes.length, `node[${i}] child ${c} valid`));
    if (n.mesh !== undefined) check(n.mesh < g.meshes.length, `node[${i}] mesh ref valid`);
    if (n.rotation) {
      const len = Math.hypot(...n.rotation);
      check(Math.abs(len - 1) < 1e-6, `node[${i}] rotation quaternion normalized (|q|=${len})`);
    }
  });

  // animations
  (g.animations || []).forEach((anim, ai) => {
    anim.samplers.forEach((s, si) => {
      const input = g.accessors[s.input];
      const output = g.accessors[s.output];
      check(input.type === "SCALAR" && Array.isArray(input.min), `anim[${ai}].sampler[${si}] input scalar with min/max`);
      check(output.count === input.count, `anim[${ai}].sampler[${si}] output count == input count`);
    });
    anim.channels.forEach((c, ci) => {
      check(c.target.node < g.nodes.length, `anim[${ai}].channel[${ci}] node valid`);
      const output = g.accessors[anim.samplers[c.sampler].output];
      const expected = c.target.path === "rotation" ? "VEC4" : "VEC3";
      check(output.type === expected, `anim[${ai}].channel[${ci}] ${c.target.path} output ${output.type} == ${expected}`);
      if (c.target.path === "rotation" && bin) {
        const bv = g.bufferViews[output.bufferView];
        for (let i = 0; i < output.count; i++) {
          const q = [0, 1, 2, 3].map((k) => bin.readFloatLE(bv.byteOffset + (i * 4 + k) * 4));
          if (Math.abs(Math.hypot(...q) - 1) > 1e-4) {
            check(false, `anim[${ai}].channel[${ci}] key ${i} quaternion not normalized`);
            break;
          }
        }
      }
    });
  });

  // image exists
  (g.images || []).forEach((img) => {
    check(fs.existsSync(path.join(dir, img.uri)), `texture exists: ${img.uri}`);
  });
}

const files = process.argv.slice(2);
if (!files.length) {
  console.error("usage: node test/gltf-check.js <file.gltf> [...]");
  process.exit(1);
}
files.forEach(validate);
console.log(failures ? `\n${failures} failure(s)` : "\nall glTF checks passed");
process.exit(failures ? 1 : 0);
