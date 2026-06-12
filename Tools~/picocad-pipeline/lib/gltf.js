// gltf.js — glTF 2.0 writer: the primary Unity ingestion artifact.
// Follows the app's own glTF export conventions (verified against src/io/gltf.lua):
//   - per-corner unique vertices (faces own their UVs), Z negated (handedness),
//     UVs passed through unflipped (glTF origin = top-left, same as picoCAD),
//     fan triangulation with reversed winding (v1, v_{i+1}, v_i),
//     double-sided faces duplicated reversed, NEAREST samplers.
// And adds what the app's export lacks (the whole reason this writer exists):
//   - named node hierarchy with LOCAL transforms (correct pivots for animation),
//   - TRS animation channels sampled from motion tracks (lib/motion.js),
//   - alphaMode MASK when the texture uses the transparent color.
// Rotation mapping under the Z flip: (θx, θy, θz) -> (-θx, -θy, θz), composed
// as R = Rx·Ry·Rz (the app's node composition order: T·Rx·Ry·Rz·S).

"use strict";

const fs = require("fs");
const { usesTransparency } = require("./parse");
const { sampleNodeTracks } = require("./motion");

const F32 = 5126, U16 = 5123, U32 = 5125;
const ARRAY_BUFFER = 34962, ELEMENT_ARRAY_BUFFER = 34963;

// ---------- quaternions (x, y, z, w) ----------
function quatAxisAngle(axis, angle) {
  const h = angle / 2, s = Math.sin(h);
  return [axis[0] * s, axis[1] * s, axis[2] * s, Math.cos(h)];
}
function quatMul(a, b) {
  const [ax, ay, az, aw] = a, [bx, by, bz, bw] = b;
  return [
    aw * bx + ax * bw + ay * bz - az * by,
    aw * by - ax * bz + ay * bw + az * bx,
    aw * bz + ax * by - ay * bx + az * bw,
    aw * bw - ax * bx - ay * by - az * bz,
  ];
}
/** picoCAD euler (radians, R = Rx·Ry·Rz) + Z-flip -> glTF quaternion. */
function eulerToGltfQuat(ex, ey, ez) {
  const qx = quatAxisAngle([1, 0, 0], -ex);
  const qy = quatAxisAngle([0, 1, 0], -ey);
  const qz = quatAxisAngle([0, 0, 1], ez);
  return quatMul(quatMul(qx, qy), qz);
}

// ---------- binary buffer builder ----------
class BinBuilder {
  constructor() { this.chunks = []; this.offset = 0; }
  /** Append a typed chunk, 4-byte aligned. Returns { byteOffset, byteLength }. */
  add(buf) {
    const view = { byteOffset: this.offset, byteLength: buf.length };
    this.chunks.push(buf);
    const rem = buf.length % 4;
    if (rem) this.chunks.push(Buffer.alloc(4 - rem));
    this.offset += buf.length + (rem ? 4 - rem : 0);
    return view;
  }
  concat() { return Buffer.concat(this.chunks); }
}

function f32Buffer(arr) {
  const buf = Buffer.alloc(arr.length * 4);
  arr.forEach((v, i) => buf.writeFloatLE(v, i * 4));
  return buf;
}

function minMax(arr, stride) {
  const min = new Array(stride).fill(Infinity);
  const max = new Array(stride).fill(-Infinity);
  for (let i = 0; i < arr.length; i += stride) {
    for (let c = 0; c < stride; c++) {
      if (arr[i + c] < min[c]) min[c] = arr[i + c];
      if (arr[i + c] > max[c]) max[c] = arr[i + c];
    }
  }
  return { min, max };
}

/** Build glTF mesh data from a picoCAD mesh: per-corner verts, reversed-fan
 *  triangulation, dbl duplication, Z negation, scale applied. */
function buildMeshData(mesh, scale) {
  const positions = [];
  const uvs = [];
  const indices = [];

  function emitTriangle(face, c0, c1, c2) {
    for (const c of [c0, c1, c2]) {
      const v = mesh.vertices[face.vertexIds[c] - 1];
      indices.push(positions.length / 3);
      positions.push(v.x * scale, v.y * scale, -v.z * scale);
      uvs.push(face.uvs[c * 2], face.uvs[c * 2 + 1]);
    }
  }

  for (const face of mesh.faces) {
    const n = face.vertexIds.length;
    // app's fan: (v1, v_{i+1}, v_i) — reversed winding for the Z flip
    for (let i = 1; i < n - 1; i++) emitTriangle(face, 0, i + 1, i);
    if (face.doubleSided) {
      for (let i = 1; i < n - 1; i++) emitTriangle(face, 0, i, i + 1);
    }
  }
  return { positions, uvs, indices };
}

/**
 * Write <base>.gltf + <base>.bin next to the texture <model.name>.png.
 * opts: { scale = 1, fps = 30 }
 * Returns { animations: [clipName...], nodeCount, triangleCount, warnings: [...] }
 */
function writeGltf(model, base, opts = {}) {
  const scale = opts.scale || 1;
  const fps = opts.fps || 30;
  const warnings = [];

  const gltf = {
    asset: { version: "2.0", generator: "picocad-pipeline" },
    scene: 0,
    scenes: [{ nodes: [0] }],
    nodes: [],
    meshes: [],
    materials: [],
    samplers: [{ magFilter: 9728, minFilter: 9728, wrapS: 33071, wrapT: 33071 }],
    textures: [{ source: 0, sampler: 0 }],
    images: [{ uri: `${model.name}.png` }],
    accessors: [],
    bufferViews: [],
    buffers: [],
  };

  const transparent = usesTransparency(model);
  gltf.materials.push({
    name: model.name,
    pbrMetallicRoughness: {
      baseColorTexture: { index: 0 },
      metallicFactor: 0,
      roughnessFactor: 1,
    },
    ...(transparent ? { alphaMode: "MASK", alphaCutoff: 0.5 } : {}),
  });

  const bin = new BinBuilder();
  const usedNames = new Set();
  let triangleCount = 0;
  const animationSamplers = [];
  const animationChannels = [];

  function uniqueName(raw) {
    let name = (raw || "node").replace(/[/\\.]/g, "_");
    let candidate = name, i = 2;
    while (usedNames.has(candidate)) candidate = `${name}_${i++}`;
    usedNames.add(candidate);
    return candidate;
  }

  function addAccessor(view, componentType, count, type, withMinMax, srcArray, stride) {
    const acc = { bufferView: gltf.bufferViews.length, byteOffset: 0, componentType, count, type };
    if (withMinMax) {
      const { min, max } = minMax(srcArray, stride);
      acc.min = min;
      acc.max = max;
    }
    gltf.bufferViews.push({
      buffer: 0,
      byteOffset: view.byteOffset,
      byteLength: view.byteLength,
      ...(view.target ? { target: view.target } : {}),
    });
    gltf.accessors.push(acc);
    return gltf.accessors.length - 1;
  }

  function buildNode(node) {
    const idx = gltf.nodes.length;
    const g = { name: uniqueName(node.name || model.name) };
    gltf.nodes.push(g);

    const t = node.transform;
    const tx = t.pos.x * scale, ty = t.pos.y * scale, tz = -t.pos.z * scale;
    if (tx || ty || tz) g.translation = [tx, ty, tz];
    if (t.rot.x || t.rot.y || t.rot.z) g.rotation = eulerToGltfQuat(t.rot.x, t.rot.y, t.rot.z);
    if (t.scale.x !== 1 || t.scale.y !== 1 || t.scale.z !== 1) g.scale = [t.scale.x, t.scale.y, t.scale.z];

    if (node.mesh && node.mesh.faces.length) {
      const md = buildMeshData(node.mesh, scale);
      triangleCount += md.indices.length / 3;

      const posView = bin.add(f32Buffer(md.positions));
      const posAcc = addAccessor({ ...posView, target: ARRAY_BUFFER }, F32, md.positions.length / 3, "VEC3", true, md.positions, 3);

      const uvView = bin.add(f32Buffer(md.uvs));
      const uvAcc = addAccessor({ ...uvView, target: ARRAY_BUFFER }, F32, md.uvs.length / 2, "VEC2", false);

      const wide = md.positions.length / 3 > 65535;
      const idxBuf = Buffer.alloc(md.indices.length * (wide ? 4 : 2));
      md.indices.forEach((v, i) => (wide ? idxBuf.writeUInt32LE(v, i * 4) : idxBuf.writeUInt16LE(v, i * 2)));
      const idxView = bin.add(idxBuf);
      const idxAcc = addAccessor({ ...idxView, target: ELEMENT_ARRAY_BUFFER }, wide ? U32 : U16, md.indices.length, "SCALAR", false);

      g.mesh = gltf.meshes.length;
      gltf.meshes.push({
        name: g.name,
        primitives: [{ attributes: { POSITION: posAcc, TEXCOORD_0: uvAcc }, indices: idxAcc, material: 0 }],
      });
    }

    // animation channels
    const sampled = sampleNodeTracks(node, model.motionDuration, fps);
    if (sampled) {
      if (sampled.visibleFolded) {
        warnings.push(`node "${node.name}": visibility animation folded into scale-0 (glTF has no visibility channel)`);
      }
      const timeView = bin.add(f32Buffer(sampled.times));
      const timeAcc = addAccessor(timeView, F32, sampled.times.length, "SCALAR", true, sampled.times, 1);

      function addChannel(path, flat, type) {
        const view = bin.add(f32Buffer(flat));
        const stride = type === "VEC3" ? 3 : 4;
        const outAcc = addAccessor(view, F32, flat.length / stride, type, false);
        animationSamplers.push({ input: timeAcc, output: outAcc, interpolation: "LINEAR" });
        animationChannels.push({
          sampler: animationSamplers.length - 1,
          target: { node: idx, path },
        });
      }

      if (sampled.pos) {
        addChannel("translation", sampled.pos.flatMap((p) => [p.x * scale, p.y * scale, -p.z * scale]), "VEC3");
      }
      if (sampled.rotEuler) {
        addChannel("rotation", sampled.rotEuler.flatMap((e) => eulerToGltfQuat(e.x, e.y, e.z)), "VEC4");
      }
      if (sampled.scale) {
        addChannel("scale", sampled.scale.flatMap((s) => [s.x, s.y, s.z]), "VEC3");
      }
    }

    const childIndices = node.children.filter((c) => c.visible).map((c) => buildNode(c));
    if (childIndices.length) g.children = childIndices;
    return idx;
  }

  buildNode({ ...model.root, name: model.name });

  const animations = [];
  if (animationChannels.length) {
    gltf.animations = [{ name: "Motion", samplers: animationSamplers, channels: animationChannels }];
    animations.push("Motion");
  }

  const binData = bin.concat();
  const path = require("path");
  gltf.buffers.push({ uri: `${path.basename(base)}.bin`, byteLength: binData.length });

  fs.writeFileSync(`${base}.bin`, binData);
  fs.writeFileSync(`${base}.gltf`, JSON.stringify(gltf, null, 1));

  return { animations, nodeCount: gltf.nodes.length, triangleCount, warnings };
}

module.exports = { writeGltf, eulerToGltfQuat };
