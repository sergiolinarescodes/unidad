// motion.js — exact port of picoCAD 2's motion evaluation
// (src/3d/motion.lua + skrovet/tweening.lua + src/helpers.lua pingpong,
// extracted from the app's fused Love2D source, v2.1.0 build 96).
//
// Semantics:
//  - Each node has 4 tracks; each track holds clips
//    { prop: "pos"|"rot"|"scale"|"visible", axises: ["x"|"y"|"z"], start, stop (seconds),
//      delta, curve?, pingpong?, times? }.
//  - rot deltas are stored in TURNS in the save file; the app converts to radians
//    on load (delta * 2π) and HALVES the amplitude in the oscillate branch.
//  - times => oscillation: value = base + amp * sin(times * 2π * (t-start)/duration),
//    frozen outside [start, stop] (t clamped to stop; before start returns base).
//    Lua truthiness: any non-nil/false `times` counts, including 0.
//  - otherwise => one-shot tween: value = base + delta * easing(min(t-start, duration), 0, 1, duration);
//    every easing clamps t<0 to 0 internally.
//  - visible: node hidden during [start, stop).
//  - Clips for the same prop+axis chain in start-order: value = clip(value, t).
//  - Playback evaluates at t = time % motion_duration (seconds); static pose is t = -1.

"use strict";

const TWO_PI = Math.PI * 2;

function clamp0(t) { return t < 0 ? 0 : t; }

const EASE_LINEAR = (t, b, c, d) => { t = clamp0(t); return (c * t) / d + b; };
const EASE_IN_QUINT = (t, b, c, d) => { t = clamp0(t) / d; return c * t ** 5 + b; };
const EASE_OUT_QUINT = (t, b, c, d) => { t = clamp0(t) / d - 1; return c * (t ** 5 + 1) + b; };
const EASE_IN_OUT_QUAD = (t, b, c, d) => {
  t = (clamp0(t) / d) * 2;
  if (t < 1) return (c / 2) * t * t + b;
  return (-c / 2) * ((t - 1) * (t - 3) - 1) + b;
};
const EASE_OUT_BACK = (t, b, c, d) => {
  const s = 1.70158;
  t = clamp0(t) / d - 1;
  return c * (t * t * ((s + 1) * t + s) + 1) + b;
};
const EASE_OUT_BOUNCE = (t, b, c, d) => {
  t = clamp0(t) / d;
  if (t < 1 / 2.75) return c * (7.5625 * t * t) + b;
  if (t < 2 / 2.75) { t -= 1.5 / 2.75; return c * (7.5625 * t * t + 0.75) + b; }
  if (t < 2.5 / 2.75) { t -= 2.25 / 2.75; return c * (7.5625 * t * t + 0.9375) + b; }
  t -= 2.625 / 2.75;
  return c * (7.5625 * t * t + 0.984375) + b;
};
const EASE_INSTANT = (t, b, c, d) => (t < 0 ? b : c + b);

function pingpong(f) {
  return (t, b, c, d) => {
    if (t > d / 2) t = d - t;
    return f(t * 2, b, c, d);
  };
}

const EASINGS = {
  linear: EASE_LINEAR,
  "ease in": EASE_IN_QUINT,
  "ease out": EASE_OUT_QUINT,
  soft: EASE_IN_OUT_QUAD,
  elastic: EASE_OUT_BACK,
  bounce: EASE_OUT_BOUNCE,
  instant: EASE_INSTANT,
  pinch: pingpong(EASE_IN_OUT_QUAD),
};

function makeClip(data) {
  return {
    prop: data.prop,
    start: data.start ?? 0,
    stop: data.stop ?? 28 / 19.2, // 28 / PIXELS_PER_SECOND default (rarely hit: saves carry stop)
    axises: data.axises || [],
    delta: data.prop === "rot" ? TWO_PI * data.delta : data.delta, // turns -> radians
    curve: data.curve,
    pingpong: data.pingpong,
    times: data.times,
  };
}

function evaluateClip(clip, startValue, t) {
  // oscillate (Lua truthy check: present and not false)
  if (clip.times !== undefined && clip.times !== null && clip.times !== false) {
    if (t >= clip.start) {
      t = Math.min(t, clip.stop);
      const duration = clip.stop - clip.start;
      const amp = clip.prop === "rot" ? clip.delta / 2 : clip.delta;
      return startValue + amp * Math.sin((clip.times * TWO_PI * (t - clip.start)) / duration);
    }
    return startValue;
  }

  if (clip.prop === "visible") {
    return t >= clip.start && t < clip.stop ? false : startValue;
  }

  let easing = EASINGS[clip.curve] || EASE_LINEAR;
  if (clip.pingpong) easing = pingpong(easing);
  const duration = clip.stop - clip.start;
  const ts = Math.min(t - clip.start, duration);
  return startValue + clip.delta * easing(ts, 0, 1, duration);
}

/** Collect a node's clips grouped by prop+axis, start-sorted, from all 4 tracks. */
function collectClips(node) {
  const byProp = {}; // "pos.x" -> [clip...]
  let visibleClips = [];
  for (const track of node.tracks) {
    for (const data of track) {
      const clip = makeClip(data);
      if (clip.prop === "visible") {
        visibleClips.push(clip);
        continue;
      }
      for (const axis of clip.axises) {
        const key = `${clip.prop}.${axis}`;
        (byProp[key] = byProp[key] || []).push(clip);
      }
    }
  }
  for (const key of Object.keys(byProp)) byProp[key].sort((a, b) => a.start - b.start);
  visibleClips.sort((a, b) => a.start - b.start);
  return { byProp, visibleClips };
}

function evalProp(clips, baseValue, t) {
  let value = baseValue;
  for (const clip of clips) value = evaluateClip(clip, value, t);
  return value;
}

/**
 * Sample a node's animated channels over [0, motionDuration] at `fps`.
 * Returns null when the node has no clips, else:
 * { times: [s...], pos: [{x,y,z}...]|null, rotEuler: [{x,y,z} radians...]|null,
 *   scale: [{x,y,z}...]|null, visibleFolded: bool }
 * Keys evaluate at (t % motionDuration), matching the app's looping playback —
 * the final key (t = duration) therefore equals the first, closing the loop.
 * Static-visibility=false during a window is folded into scale 0 (glTF cannot
 * animate visibility); callers should surface the warning.
 */
function sampleNodeTracks(node, motionDuration, fps) {
  const { byProp, visibleClips } = collectClips(node);
  const hasAny = Object.keys(byProp).length > 0 || visibleClips.length > 0;
  if (!hasAny || !motionDuration) return null;

  const frameCount = Math.max(1, Math.round(motionDuration * fps)) + 1; // inclusive end key
  const times = [];
  const props = { pos: null, rot: null, scale: null };
  const animated = {
    pos: ["pos.x", "pos.y", "pos.z"].some((k) => byProp[k]),
    rot: ["rot.x", "rot.y", "rot.z"].some((k) => byProp[k]),
    scale: ["scale.x", "scale.y", "scale.z"].some((k) => byProp[k]) || visibleClips.length > 0,
  };
  for (const p of Object.keys(animated)) if (animated[p]) props[p] = [];

  const base = node.transform;
  for (let i = 0; i < frameCount; i++) {
    const tKey = i / fps;
    const t = motionDuration > 0 ? tKey % motionDuration : 0;
    times.push(tKey);
    for (const prop of ["pos", "rot", "scale"]) {
      if (!props[prop]) continue;
      const sample = {};
      for (const axis of ["x", "y", "z"]) {
        const clips = byProp[`${prop}.${axis}`];
        const baseValue = base[prop][axis];
        sample[axis] = clips ? evalProp(clips, baseValue, t) : baseValue;
      }
      if (prop === "scale" && visibleClips.length) {
        const visible = evalProp(visibleClips, true, t);
        if (!visible) { sample.x = 0; sample.y = 0; sample.z = 0; }
      }
      props[prop].push(sample);
    }
  }

  return {
    times,
    pos: props.pos,
    rotEuler: props.rot,
    scale: props.scale,
    visibleFolded: visibleClips.length > 0,
  };
}

module.exports = { sampleNodeTracks, evaluateClip, makeClip, collectClips, EASINGS };
