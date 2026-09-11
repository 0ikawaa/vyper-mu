/* Visor 3D de modelos BMD (three.js r128, vendorizado en vendor/). Un solo renderer que se
   re-monta en el contenedor que le pase el editor. MU usa Z hacia arriba; se rota el grupo. */
'use strict';

const Viewer = (() => {
  let renderer, scene, camera, group, container, raf;
  let yaw = 0.6, pitch = 0.35, dist = 200, target = new THREE.Vector3();
  let autoRotate = true, wireframe = false, dragging = false, last = null;
  let currentIndex = null, loadedFor = null;
  const cache = new Map();
  const texLoader = new THREE.TextureLoader();
  const onInfo = { cb: null };

  function ensure() {
    if (renderer) return;
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.outputEncoding = THREE.sRGBEncoding;
    scene = new THREE.Scene();
    camera = new THREE.PerspectiveCamera(35, 1, 1, 20000);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x334455, 0.9));
    const key = new THREE.DirectionalLight(0xffffff, 0.8); key.position.set(1, 2, 1.5); scene.add(key);
    const fill = new THREE.DirectionalLight(0xffe0b0, 0.35); fill.position.set(-2, -1, -1); scene.add(fill);
    group = new THREE.Group();
    group.rotation.x = -Math.PI / 2; // Z arriba -> Y arriba
    scene.add(group);

    const el = renderer.domElement;
    el.style.display = 'block';
    el.style.cursor = 'grab';
    el.addEventListener('pointerdown', (e) => { dragging = true; last = [e.clientX, e.clientY]; el.setPointerCapture(e.pointerId); el.style.cursor = 'grabbing'; });
    el.addEventListener('pointerup', (e) => { dragging = false; el.releasePointerCapture(e.pointerId); el.style.cursor = 'grab'; });
    el.addEventListener('pointermove', (e) => {
      if (!dragging) return;
      yaw -= (e.clientX - last[0]) * 0.01;
      pitch = Math.max(-1.4, Math.min(1.4, pitch + (e.clientY - last[1]) * 0.01));
      last = [e.clientX, e.clientY];
      autoRotate = false;
    });
    el.addEventListener('wheel', (e) => { e.preventDefault(); dist *= e.deltaY > 0 ? 1.12 : 0.89; dist = Math.max(5, Math.min(5000, dist)); }, { passive: false });
    el.addEventListener('dblclick', () => { fit(); });
    window.addEventListener('resize', resize);
    loop();
  }

  function resize() {
    if (!container || !renderer) return;
    const w = container.clientWidth, h = container.clientHeight;
    if (w === 0 || h === 0) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }

  function loop() {
    raf = requestAnimationFrame(loop);
    if (!container || !container.isConnected) return;
    if (autoRotate) yaw += 0.006;
    camera.position.set(
      target.x + dist * Math.cos(pitch) * Math.sin(yaw),
      target.y + dist * Math.sin(pitch),
      target.z + dist * Math.cos(pitch) * Math.cos(yaw));
    camera.lookAt(target);
    renderer.render(scene, camera);
  }

  function clear() {
    while (group.children.length) {
      const m = group.children.pop();
      m.geometry.dispose();
      if (m.material.map) m.material.map.dispose();
      m.material.dispose();
    }
  }

  function fit() {
    const box = new THREE.Box3().setFromObject(group);
    if (box.isEmpty()) return;
    const size = box.getSize(new THREE.Vector3());
    box.getCenter(target);
    const radius = Math.max(size.x, size.y, size.z, 1) * 0.5;
    dist = radius / Math.sin((camera.fov * Math.PI / 180) / 2) * 1.25;
    camera.near = Math.max(0.1, dist / 100);
    camera.far = dist * 20;
    camera.updateProjectionMatrix();
  }

  function build(data) {
    clear();
    for (const m of data.meshes) {
      if (!m.indices.length) continue;
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.Float32BufferAttribute(m.positions, 3));
      g.setAttribute('normal', new THREE.Float32BufferAttribute(m.normals, 3));
      g.setAttribute('uv', new THREE.Float32BufferAttribute(m.uvs, 2));
      g.setIndex(m.indices);
      const isTga = /\.tga$/i.test(m.texture);
      const mat = new THREE.MeshLambertMaterial({ color: 0xffffff, side: THREE.DoubleSide, transparent: isTga, alphaTest: isTga ? 0.3 : 0, wireframe });
      if (m.textureUrl) {
        texLoader.load(m.textureUrl, (tex) => {
          tex.flipY = false;
          tex.encoding = THREE.sRGBEncoding;
          tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
          tex.anisotropy = 4;
          mat.map = tex;
          mat.needsUpdate = true;
        }, undefined, () => { mat.color.set(0x9aa4b5); });
      } else {
        mat.color.set(0x9aa4b5);
      }
      group.add(new THREE.Mesh(g, mat));
    }
    autoRotate = true; yaw = 0.6; pitch = 0.35;
    fit();
  }

  async function load(index) {
    currentIndex = index;
    let data = cache.get(index);
    if (!data) {
      const res = await fetch(`/api/models/${index}`);
      data = await res.json();
      cache.set(index, data);
    }
    if (currentIndex !== index) return; // cambiaron de item mientras cargaba
    loadedFor = index;
    if (data.meshes) build(data); else clear();
    if (onInfo.cb) onInfo.cb(data);
  }

  return {
    /** Monta el canvas en `el` y carga el modelo del indice dado. */
    mount(el, index, infoCb) {
      ensure();
      container = el;
      onInfo.cb = infoCb;
      el.appendChild(renderer.domElement);
      resize();
      if (index !== loadedFor || group.children.length === 0) load(index).catch((e) => infoCb && infoCb({ error: e.message }));
      else if (infoCb) infoCb(cache.get(index));
    },
    toggleRotate() { autoRotate = !autoRotate; return autoRotate; },
    toggleWireframe() { wireframe = !wireframe; group.children.forEach((m) => { m.material.wireframe = wireframe; }); return wireframe; },
    fit, resize,
    invalidate(index) { cache.delete(index); if (index === loadedFor) loadedFor = null; },
  };
})();
