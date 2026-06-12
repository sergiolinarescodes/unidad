// obj.js — flat OBJ/MTL writer reproducing picoCAD 2's own export byte conventions.
// This output exists as a parity artifact (golden-tested against the app's export);
// Unity ingests the glTF, not this.
//
// App export conventions (reverse-engineered from anime_pistol.obj et al.):
//   - header: "# picocad model" / "mtllib <name>.mtl" / "o <name>"
//   - vertices: world-baked (parent positions accumulated), Z negated (handedness flip)
//   - uvs: one vt per face corner in source corner order, V flipped (1 - v)
//   - faces: corners emitted in REVERSED order, "f v/vt/F " with F = 1-based face index
//     (the exporter references per-face normals it never writes) and a trailing space
//   - "usemtl <name>.mtl" + "s off" before the face block; blank line between
//     sections and one trailing blank line at EOF

"use strict";

const { walkMeshNodes } = require("./parse");

function num(n) {
  // The app formats with Lua's %.14g: round to 14 significant digits, which
  // collapses double-addition artifacts (0.1+0.2 -> "0.3"). Lua prints signed
  // zero as "-0" (arises from Z negation), so preserve it.
  if (Object.is(n, -0)) return "-0";
  return String(parseFloat(n.toPrecision(14)));
}

function buildFlatObj(model) {
  const vLines = [];
  const vtLines = [];
  const fLines = [];
  let vertexBase = 0; // global 1-based offset for the current node's vertices
  let vtIndex = 0;
  let faceIndex = 0;

  walkMeshNodes(model.root, (node, offset) => {
    const mesh = node.mesh;
    for (const v of mesh.vertices) {
      vLines.push(`v ${num(v.x + offset.x)} ${num(v.y + offset.y)} ${num(-(v.z + offset.z))}`);
    }
    for (const face of mesh.faces) {
      const corners = face.vertexIds.length;
      // vt entries in source corner order, V flipped
      for (let c = 0; c < corners; c++) {
        vtLines.push(`vt ${num(face.uvs[c * 2])} ${num(1 - face.uvs[c * 2 + 1])}`);
      }
      faceIndex++;
      const firstVt = vtIndex + 1;
      // face corners reversed; vertex_ids[c] pairs with vt (firstVt + c)
      const parts = [];
      for (let c = corners - 1; c >= 0; c--) {
        parts.push(`${vertexBase + face.vertexIds[c]}/${firstVt + c}/${faceIndex}`);
      }
      fLines.push(`f ${parts.join(" ")} `);
      vtIndex += corners;
    }
    vertexBase += mesh.vertices.length;
  });

  const obj =
    `# picocad model\n` +
    `mtllib ${model.name}.mtl\n` +
    `o ${model.name}\n` +
    vLines.join("\n") + "\n" +
    "\n" +
    vtLines.join("\n") + "\n" +
    "\n" +
    `usemtl ${model.name}.mtl\n` +
    `s off\n` +
    fLines.join("\n") + "\n" +
    "\n";

  const mtl =
    `# picocad material\n` +
    `newmtl ${model.name}.mtl\n` +
    `ka 1.000000 1.000000 1.000000\n` +
    `kd 1.000000 1.000000 1.000000\n` +
    `ks 0.000000 0.000000 0.000000\n` +
    `tr 1.000000\n` +
    `illum 1\n` +
    `ns 0.000000\n` +
    `map_kd ${model.name}.png\n`;

  return { obj, mtl };
}

module.exports = { buildFlatObj };
