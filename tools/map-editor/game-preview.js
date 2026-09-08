import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

/**
 * In-game map rendering uses one atlas: MasterWallTexture (Global.TESTWALL).
 * Floor quads sample lines 21–22 (floorTexCoord); wall faces use lines 13–20.
 */
const ATLAS_SIZE = 512;
const CELL_PX = 16;
const TILE_WORLD = 1;

let atlasMeta = { width: ATLAS_SIZE, height: ATLAS_SIZE, cellSize: CELL_PX };

function parseIntSafe(v, fallback = 0) {
  const n = Number.parseInt(String(v ?? ""), 10);
  return Number.isFinite(n) ? n : fallback;
}

function tileLinesFromEditor(gx, gy, ed) {
  return ed.tileLinesAt(gx, gy);
}

function atlasRect(lines, xIndex, yIndex) {
  return {
    x: parseIntSafe(lines[xIndex]),
    y: parseIntSafe(lines[yIndex]),
  };
}

function uvRect(coord) {
  const u0 = (coord.x * CELL_PX) / atlasMeta.width;
  const v1 = 1 - (coord.y * CELL_PX) / atlasMeta.height;
  const u1 = u0 + CELL_PX / atlasMeta.width;
  const v0 = v1 - CELL_PX / atlasMeta.height;
  return { u0, v0, u1, v1 };
}

function pushQuad(positions, uvs, indices, px, pz, coord, y0, y1, edge) {
  const { u0, v0, u1, v1 } = uvRect(coord);
  const base = positions.length / 3;
  let verts;

  if (edge === "floor") {
    verts = [
      [px, y0, pz, u0, v1],
      [px + TILE_WORLD, y0, pz, u1, v1],
      [px + TILE_WORLD, y0, pz + TILE_WORLD, u1, v0],
      [px, y0, pz + TILE_WORLD, u0, v0],
    ];
  } else if (edge === "right") {
    const x = px + TILE_WORLD;
    verts = [
      [x, y0, pz, u0, v1],
      [x, y0, pz + TILE_WORLD, u1, v1],
      [x, y1, pz + TILE_WORLD, u1, v0],
      [x, y1, pz, u0, v0],
    ];
  } else if (edge === "bottom") {
    const z = pz + TILE_WORLD;
    verts = [
      [px, y0, z, u0, v1],
      [px + TILE_WORLD, y0, z, u1, v1],
      [px + TILE_WORLD, y1, z, u1, v0],
      [px, y1, z, u0, v0],
    ];
  } else if (edge === "left") {
    const x = px;
    verts = [
      [x, y0, pz + TILE_WORLD, u0, v1],
      [x, y0, pz, u1, v1],
      [x, y1, pz, u1, v0],
      [x, y1, pz + TILE_WORLD, u0, v0],
    ];
  } else {
    const z = pz;
    verts = [
      [px + TILE_WORLD, y0, z, u0, v1],
      [px, y0, z, u1, v1],
      [px, y1, z, u1, v0],
      [px + TILE_WORLD, y1, z, u0, v0],
    ];
  }

  for (const [x, y, z, u, v] of verts) {
    positions.push(x, y, z);
    uvs.push(u, v);
  }
  indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
}

/** Approximate vertical profile from tile data (hedge > building > prop > flat). */
function wallHeightForTile(lines, hs) {
  const floor = atlasRect(lines, 20, 21);
  const [r, t, l, b] = lines.slice(0, 4).map(v => v === "1");
  const overlay = lines.slice(4, 20).filter(v => v !== "0").length;
  const isStore = lines[20] === "3" && lines[21] === "6";

  if (floor.y >= 31 || (t && b && !r && !l)) return 0.85 * hs;
  if (isStore) return 0.35 * hs;
  if (floor.y === 6 || floor.y === 8 || floor.y === 7) return 0.55 * hs;
  if (floor.y === 5 || floor.y === 4) return 0.48 * hs;
  if (overlay >= 4 && floor.y <= 3) return 0.42 * hs;
  if (r || t || l || b) return 0.38 * hs;
  return 0.32 * hs;
}

function solidHeightForTile(lines, hs) {
  const floor = atlasRect(lines, 20, 21);
  const [r, t, l, b] = lines.slice(0, 4).map(v => v === "1");
  const walled = r || t || l || b;
  const isStore = lines[20] === "3" && lines[21] === "6";
  const overlay = lines.slice(4, 20).filter(v => v !== "0").length;

  if (floor.y >= 31 && t && b) return 1.35 * hs;
  if (isStore) return 0;
  if ((floor.y === 6 || floor.y === 8) && t && b) return 0.65 * hs;
  if (floor.y === 5 && t && b) return 0.55 * hs;
  if (floor.y === 4 && overlay >= 3 && t) return 0.75 * hs;
  if (overlay >= 5 && floor.y === 1) return 0.55 * hs;
  if (walled && floor.y === 3 && overlay >= 2) return 0.35 * hs;
  return 0;
}

function pushBox(positions, uvs, indices, px, pz, coord, y0, height) {
  const { u0, v0, u1, v1 } = uvRect(coord);
  const y1 = y0 + height;
  const faces = [
    [[px, y0, pz, u0, v1], [px + 1, y0, pz, u1, v1], [px + 1, y1, pz, u1, v0], [px, y1, pz, u0, v0]],
    [[px + 1, y0, pz + 1, u0, v1], [px, y0, pz + 1, u1, v1], [px, y1, pz + 1, u1, v0], [px + 1, y1, pz + 1, u0, v0]],
    [[px, y0, pz + 1, u0, v1], [px, y0, pz, u1, v1], [px, y1, pz, u1, v0], [px, y1, pz + 1, u0, v0]],
    [[px + 1, y0, pz, u0, v1], [px + 1, y0, pz + 1, u1, v1], [px + 1, y1, pz + 1, u1, v0], [px + 1, y1, pz, u0, v0]],
  ];
  for (const face of faces) {
    const base = positions.length / 3;
    for (const [x, y, z, u, v] of face) {
      positions.push(x, y, z);
      uvs.push(u, v);
    }
    indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
  }
  const base = positions.length / 3;
  const top = [
    [px, y1, pz, u0, v1],
    [px + 1, y1, pz, u1, v1],
    [px + 1, y1, pz + 1, u1, v0],
    [px, y1, pz + 1, u0, v0],
  ];
  for (const [x, y, z, u, v] of top) {
    positions.push(x, y, z);
    uvs.push(u, v);
  }
  indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
}

function buildMapGeometry(ed) {
  const positions = [];
  const uvs = [];
  const indices = [];
  const hs = ed.getHeightScale?.() ?? 1;

  for (let gy = 0; gy < ed.GRID; gy++) {
    for (let gx = 0; gx < ed.GRID; gx++) {
      const lines = tileLinesFromEditor(gx, gy, ed);
      const floor = atlasRect(lines, 20, 21);
      const floorY = 0.02;
      pushQuad(positions, uvs, indices, gx, gy, floor, floorY, floorY, "floor");

      const solidH = solidHeightForTile(lines, hs);
      if (solidH > 0.05) {
        pushBox(positions, uvs, indices, gx, gy, floor, floorY + 0.01, solidH);
      }

      const wh = wallHeightForTile(lines, hs);
      const wallEdges = [
        ["right", lines[0] === "1", 12, 13],
        ["top", lines[1] === "1", 14, 15],
        ["left", lines[2] === "1", 16, 17],
        ["bottom", lines[3] === "1", 18, 19],
      ];
      const wallBase = floorY + solidH;
      for (const [edge, on, xi, yi] of wallEdges) {
        if (!on) continue;
        pushQuad(positions, uvs, indices, gx, gy, atlasRect(lines, xi, yi), wallBase, wallBase + wh, edge);
      }
    }
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute("uv", new THREE.Float32BufferAttribute(uvs, 2));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return geometry;
}

export function createGamePreview(canvas) {
  const state = {
    inited: false,
    active: false,
    animId: 0,
    renderer: null,
    scene: null,
    camera: null,
    controls: null,
    root: null,
    mapTex: null,
    statusEl: null,
    lastHeightScale: null,
  };

  async function loadTextures() {
    const loader = new THREE.TextureLoader();
    const [metaRes, mapTex] = await Promise.all([
      fetch("assets/MasterWallTexture.json").then(r => r.ok ? r.json() : atlasMeta),
      loader.loadAsync("assets/MasterWallTexture.png"),
    ]);
    atlasMeta = { ...atlasMeta, ...metaRes };
    mapTex.magFilter = THREE.NearestFilter;
    mapTex.minFilter = THREE.NearestFilter;
    mapTex.colorSpace = THREE.SRGBColorSpace;
    mapTex.wrapS = mapTex.wrapT = THREE.ClampToEdgeWrapping;
    state.mapTex = mapTex;
  }

  function clearRoot() {
    while (state.root.children.length) {
      const child = state.root.children.pop();
      child.geometry?.dispose();
      child.material?.dispose();
    }
  }

  function rebuildScene() {
    const ed = window.mapEditor;
    if (!ed || !state.inited || !ed.mapLoaded || !state.mapTex) return;

    clearRoot();
    const geometry = buildMapGeometry(ed);
    const material = new THREE.MeshLambertMaterial({
      map: state.mapTex,
      transparent: true,
      alphaTest: 0.5,
      side: THREE.DoubleSide,
    });

    state.root.add(new THREE.Mesh(geometry, material));
    state.lastHeightScale = ed.getHeightScale?.() ?? 1;
    ed.setPreview3dDirty(false);
    if (state.statusEl) {
      state.statusEl.textContent = "Game preview · Hedge Maze / Manor-accurate textures + height";
    }
  }

  function initPreview() {
    if (state.inited) return;
    state.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    state.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    state.scene = new THREE.Scene();
    state.scene.background = new THREE.Color(0x1a2418);

    state.camera = new THREE.PerspectiveCamera(48, 1, 0.1, 300);
    state.camera.position.set(16, 32, 38);

    state.controls = new OrbitControls(state.camera, canvas);
    state.controls.target.set(16, 0.2, 16);
    state.controls.enableDamping = true;
    state.controls.maxPolarAngle = Math.PI / 2.05;

    state.scene.add(new THREE.AmbientLight(0xffffff, 0.75));
    const sun = new THREE.DirectionalLight(0xfff6e8, 0.9);
    sun.position.set(24, 40, 12);
    state.scene.add(sun);

    state.root = new THREE.Group();
    state.scene.add(state.root);

    state.inited = true;
    resize();
    window.addEventListener("resize", resize);
  }

  function resize() {
    if (!state.inited) return;
    const parent = canvas.parentElement;
    state.renderer.setSize(parent.clientWidth, parent.clientHeight, false);
    state.camera.aspect = parent.clientWidth / parent.clientHeight;
    state.camera.updateProjectionMatrix();
  }

  function animate() {
    if (!state.active) return;
    state.animId = requestAnimationFrame(animate);
    const ed = window.mapEditor;
    if (ed?.preview3dDirty) rebuildScene();
    const hs = ed?.getHeightScale?.() ?? 1;
    if (state.lastHeightScale !== null && Math.abs(hs - state.lastHeightScale) > 0.01) {
      ed?.setPreview3dDirty(true);
    }
    state.controls.autoRotate = ed?.getSpinEnabled() ?? false;
    state.controls.autoRotateSpeed = 0.25;
    state.controls.update();
    state.renderer.render(state.scene, state.camera);
  }

  return {
    setStatusElement(el) { state.statusEl = el; },
    async show() {
      initPreview();
      state.active = true;
      resize();
      if (!state.mapTex) {
        try {
          await loadTextures();
        } catch (err) {
          if (state.statusEl) {
            state.statusEl.textContent = "Run scripts/extract-xnb-texture.py (needs game/Content/*.xnb)";
          }
          console.error(err);
          return;
        }
      }
      rebuildScene();
      cancelAnimationFrame(state.animId);
      animate();
    },
    rebuild() {
      if (!state.inited) return this.show();
      rebuildScene();
    },
    hide() {
      state.active = false;
      cancelAnimationFrame(state.animId);
    },
  };
}
