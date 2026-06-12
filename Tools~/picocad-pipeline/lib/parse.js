// parse.js — picoCAD 2 save file (.txt, single-line JSON) -> normalized model object
// Pure Node, no external dependencies.

"use strict";

const fs = require("fs");
const path = require("path");

const TEXTURE_SIZE = 128;
const PIXEL_COUNT = TEXTURE_SIZE * TEXTURE_SIZE; // 16384

/**
 * Load and validate a picoCAD 2 save file.
 * Returns { name, sourceFile, version, palette, transparentColor, backgroundColor,
 *           pixels, motionDuration, exportSettings, root, errors, warnings }
 * `root` is the scene graph root node; every node is normalized to
 * { name, transform: {pos, rot, scale}, mesh: { vertices: [{x,y,z}], faces: [...] } | null,
 *   tracks: [seg[], seg[], seg[], seg[]], children: [...] }
 */
function loadModel(filePath) {
  const raw = fs.readFileSync(filePath, "utf8");
  const data = JSON.parse(raw);

  const errors = [];
  const warnings = [];

  if (!data.texture) errors.push("missing texture block");
  if (!data.metadata) errors.push("missing metadata block");
  if (!data.graph) errors.push("missing graph block");
  if (errors.length) return { errors, warnings };

  const tex = data.texture;
  if (typeof tex.pixels !== "string" || tex.pixels.length !== PIXEL_COUNT) {
    errors.push(`texture.pixels must be ${PIXEL_COUNT} hex chars, got ${tex.pixels ? tex.pixels.length : "none"}`);
  } else if (/[^0-9a-f]/.test(tex.pixels)) {
    errors.push("texture.pixels contains non-hex characters");
  }
  if (!Array.isArray(tex.colors) || tex.colors.length !== 16) {
    errors.push(`texture.colors must have 16 entries, got ${tex.colors ? tex.colors.length : "none"}`);
  } else {
    tex.colors.forEach((c, i) => {
      if (!Array.isArray(c) || c.length !== 3 || c.some((v) => typeof v !== "number" || v < 0 || v > 1)) {
        errors.push(`texture.colors[${i}] must be 3 floats in [0,1]`);
      }
    });
  }

  const name = path.basename(filePath, path.extname(filePath));
  let meshCount = 0;
  let faceCount = 0;

  function normalizeNode(n, nodePath) {
    const t = n.transform || { pos: { x: 0, y: 0, z: 0 }, rot: { x: 0, y: 0, z: 0 }, scale: { x: 1, y: 1, z: 1 } };
    if (t.rot.x || t.rot.y || t.rot.z) warnings.push(`node "${nodePath}" has non-zero rotation (unsupported by the app's own export; carried through to glTF only)`);
    if (t.scale.x !== 1 || t.scale.y !== 1 || t.scale.z !== 1) warnings.push(`node "${nodePath}" has non-identity scale (carried through to glTF only)`);

    let mesh = null;
    if (n.mesh) {
      meshCount++;
      const flat = n.mesh.vertices || [];
      if (flat.length % 3 !== 0) errors.push(`node "${nodePath}": vertices length ${flat.length} not divisible by 3`);
      const vertices = [];
      for (let i = 0; i + 2 < flat.length; i += 3) {
        vertices.push({ x: flat[i], y: flat[i + 1], z: flat[i + 2] });
      }
      const faces = (n.mesh.faces || []).map((f, fi) => {
        faceCount++;
        const ids = f.vertex_ids || [];
        if (ids.length < 3) errors.push(`node "${nodePath}" face ${fi}: fewer than 3 vertices`);
        ids.forEach((id) => {
          if (!Number.isInteger(id) || id < 1 || id > vertices.length) {
            errors.push(`node "${nodePath}" face ${fi}: vertex id ${id} out of range 1..${vertices.length}`);
          }
        });
        const uvs = f.uvs || [];
        if (uvs.length !== ids.length * 2) {
          errors.push(`node "${nodePath}" face ${fi}: expected ${ids.length * 2} uv floats, got ${uvs.length}`);
        }
        if (uvs.some((u) => u < -0.001 || u > 1.001)) {
          warnings.push(`node "${nodePath}" face ${fi}: uv outside [0,1]`);
        }
        if (typeof f.color === "number" && (f.color < 0 || f.color > 15)) {
          errors.push(`node "${nodePath}" face ${fi}: color ${f.color} out of range 0..15`);
        }
        return {
          vertexIds: ids,
          uvs,
          color: f.color | 0,
          doubleSided: !!f.dbl,
          noTexture: !!f.notex,
          noShade: !!f.noshade,
          priority: !!f.prio,
        };
      });
      mesh = { name: n.mesh.name || n.name, vertices, faces };
    }

    const tracks = n.motions && Array.isArray(n.motions.tracks) ? n.motions.tracks : [[], [], [], []];

    return {
      name: n.name || "",
      path: nodePath,
      visible: n.visible !== false, // the app's exporters prune invisible subtrees
      transform: t,
      mesh,
      tracks,
      children: (n.children || []).map((c) => normalizeNode(c, nodePath ? `${nodePath}/${c.name}` : c.name)),
    };
  }

  const root = normalizeNode(data.graph, "");

  if (meshCount === 0) warnings.push("model has no meshes");

  return {
    name,
    sourceFile: path.resolve(filePath),
    version: data.metadata.version,
    palette: tex.colors,
    shadePal1: tex.shade_pal_1,
    shadePal2: tex.shade_pal_2,
    transparentColor: tex.transparent_color,
    backgroundColor: tex.background_color,
    pixels: tex.pixels,
    motionDuration: data.metadata.motion_duration,
    exportSettings: data.metadata.export_settings || {},
    root,
    meshCount,
    faceCount,
    errors,
    warnings,
  };
}

/** Depth-first walk over nodes that have a mesh, with the accumulated parent position offset.
 *  Matches the app's flat-export traversal order (graph children in document order).
 *  Static transforms in picoCAD 2 are position-only in practice; offsets are additive. */
function walkMeshNodes(root, visit) {
  function rec(node, parentOffset) {
    if (!node.visible) return; // app exporters skip invisible subtrees
    const t = node.transform;
    const offset = {
      x: parentOffset.x + (t.pos.x || 0),
      y: parentOffset.y + (t.pos.y || 0),
      z: parentOffset.z + (t.pos.z || 0),
    };
    if (node.mesh) visit(node, offset);
    node.children.forEach((c) => rec(c, offset));
  }
  rec(root, { x: 0, y: 0, z: 0 });
}

/** True if any pixel actually referenced by face UVs (or anywhere — conservative) uses the transparent color. */
function usesTransparency(model) {
  if (typeof model.transparentColor !== "number") return false;
  const hex = model.transparentColor.toString(16);
  return model.pixels.includes(hex);
}

module.exports = { loadModel, walkMeshNodes, usesTransparency, TEXTURE_SIZE, PIXEL_COUNT };
