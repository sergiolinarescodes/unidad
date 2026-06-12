// png.js — minimal PNG encode/decode for the pipeline. Pure Node (built-in zlib only).
// Encoder: 8-bit RGBA, filter 0, single IDAT — used for the model texture
// (transparent_color pixels get alpha 0 so Unity's MASK alpha clipping works).
// Decoder: supports the common non-interlaced 8-bit RGB/RGBA/palette cases —
// used by golden tests to compare against the app's own exported PNG.

"use strict";

const zlib = require("zlib");

// ---------- CRC32 ----------
const CRC_TABLE = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, "ascii");
  data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
}

/** Encode RGBA pixel buffer (width*height*4, row-major top-down) to PNG. */
function encodePng(rgba, width, height) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // color type RGBA
  ihdr[10] = 0; // compression
  ihdr[11] = 0; // filter
  ihdr[12] = 0; // interlace

  const stride = width * 4;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0; // filter type 0
    rgba.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride);
  }
  const idat = zlib.deflateSync(raw, { level: 9 });

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr),
    chunk("IDAT", idat),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

/** Build the RGBA texture buffer from a parsed model (see parse.js). */
function textureToRgba(model) {
  const { pixels, palette, transparentColor } = model;
  const size = 128;
  const rgba = Buffer.alloc(size * size * 4);
  for (let i = 0; i < size * size; i++) {
    const idx = parseInt(pixels[i], 16);
    const [r, g, b] = palette[idx];
    rgba[i * 4] = Math.round(r * 255);
    rgba[i * 4 + 1] = Math.round(g * 255);
    rgba[i * 4 + 2] = Math.round(b * 255);
    rgba[i * 4 + 3] = idx === transparentColor ? 0 : 255;
  }
  return rgba;
}

// ---------- Decoder (tests/parity only) ----------

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
}

/** Decode a PNG into { width, height, rgba } (always expanded to RGBA). */
function decodePng(buf) {
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error("not a PNG");
  let pos = 8;
  let ihdr = null;
  const idat = [];
  let plte = null;
  let trns = null;
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString("ascii", pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === "IHDR") {
      ihdr = {
        width: data.readUInt32BE(0),
        height: data.readUInt32BE(4),
        bitDepth: data[8],
        colorType: data[9],
        interlace: data[12],
      };
    } else if (type === "IDAT") idat.push(data);
    else if (type === "PLTE") plte = Buffer.from(data);
    else if (type === "tRNS") trns = Buffer.from(data);
    else if (type === "IEND") break;
    pos += 12 + len;
  }
  if (!ihdr) throw new Error("PNG missing IHDR");
  if (ihdr.interlace !== 0) throw new Error("interlaced PNG unsupported");
  const channels = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[ihdr.colorType];
  if (!channels) throw new Error(`unsupported PNG color type ${ihdr.colorType}`);
  if (ihdr.bitDepth !== 8 && !(ihdr.colorType === 3 && [1, 2, 4].includes(ihdr.bitDepth))) {
    throw new Error(`unsupported PNG (bitDepth=${ihdr.bitDepth} colorType=${ihdr.colorType})`);
  }

  const raw = zlib.inflateSync(Buffer.concat(idat));
  const bitsPerPixel = channels * ihdr.bitDepth;
  const byteStride = Math.ceil((ihdr.width * bitsPerPixel) / 8);
  const bpp = Math.max(1, bitsPerPixel >> 3); // filter byte distance
  const unfiltered = Buffer.alloc(byteStride * ihdr.height);
  for (let y = 0; y < ihdr.height; y++) {
    const filter = raw[y * (byteStride + 1)];
    const row = raw.subarray(y * (byteStride + 1) + 1, (y + 1) * (byteStride + 1));
    const prev = y > 0 ? unfiltered.subarray((y - 1) * byteStride, y * byteStride) : null;
    const cur = unfiltered.subarray(y * byteStride, (y + 1) * byteStride);
    for (let x = 0; x < byteStride; x++) {
      const a = x >= bpp ? cur[x - bpp] : 0;
      const b = prev ? prev[x] : 0;
      const c = x >= bpp && prev ? prev[x - bpp] : 0;
      let v = row[x];
      if (filter === 1) v += a;
      else if (filter === 2) v += b;
      else if (filter === 3) v += (a + b) >> 1;
      else if (filter === 4) v += paeth(a, b, c);
      else if (filter !== 0) throw new Error(`unsupported PNG filter ${filter}`);
      cur[x] = v & 0xff;
    }
  }

  // expand sub-byte palette indices to one byte per sample
  let out;
  if (ihdr.bitDepth === 8) {
    out = unfiltered;
  } else {
    out = Buffer.alloc(ihdr.width * ihdr.height);
    const perByte = 8 / ihdr.bitDepth;
    const mask = (1 << ihdr.bitDepth) - 1;
    for (let y = 0; y < ihdr.height; y++) {
      for (let x = 0; x < ihdr.width; x++) {
        const byte = unfiltered[y * byteStride + Math.floor(x / perByte)];
        const shift = 8 - ihdr.bitDepth * ((x % perByte) + 1);
        out[y * ihdr.width + x] = (byte >> shift) & mask;
      }
    }
  }

  // expand to RGBA
  const rgba = Buffer.alloc(ihdr.width * ihdr.height * 4);
  for (let i = 0; i < ihdr.width * ihdr.height; i++) {
    if (ihdr.colorType === 6) {
      out.copy(rgba, i * 4, i * 4, i * 4 + 4);
    } else if (ihdr.colorType === 2) {
      rgba[i * 4] = out[i * 3];
      rgba[i * 4 + 1] = out[i * 3 + 1];
      rgba[i * 4 + 2] = out[i * 3 + 2];
      rgba[i * 4 + 3] = 255;
    } else if (ihdr.colorType === 3) {
      const idx = out[i];
      rgba[i * 4] = plte[idx * 3];
      rgba[i * 4 + 1] = plte[idx * 3 + 1];
      rgba[i * 4 + 2] = plte[idx * 3 + 2];
      rgba[i * 4 + 3] = trns && idx < trns.length ? trns[idx] : 255;
    } else if (ihdr.colorType === 0) {
      rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = out[i];
      rgba[i * 4 + 3] = 255;
    } else if (ihdr.colorType === 4) {
      rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = out[i * 2];
      rgba[i * 4 + 3] = out[i * 2 + 1];
    }
  }
  return { width: ihdr.width, height: ihdr.height, rgba };
}

module.exports = { encodePng, decodePng, textureToRgba };
