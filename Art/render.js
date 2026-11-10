// Renders ./svg/*.svg into the mod's texture folders.
// Usage: node render.js [modRoot]   (default: ./out)
// Set QA=1 to also write previews on a soil-colored background into ./qa.
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const svgDir = path.join(__dirname, 'svg');
const outRoot = process.argv[2] ? path.resolve(process.argv[2]) : path.join(__dirname, 'out');
const qaDir = path.join(__dirname, 'qa');

const jobs = [
  ['IC_AssimilationPod_north.svg', 'Textures/Things/Building/IC_AssimilationPod/IC_AssimilationPod_north.png'],
  ['IC_AssimilationPod_east.svg', 'Textures/Things/Building/IC_AssimilationPod/IC_AssimilationPod_east.png'],
  ['IC_AssimilationPod_south.svg', 'Textures/Things/Building/IC_AssimilationPod/IC_AssimilationPod_south.png'],
  ['Assimilate.svg', 'Textures/UI/IdentityCrisis/Assimilate.png'],
  ['EditSetup.svg', 'Textures/UI/IdentityCrisis/EditSetup.png'],
  ['Preview.svg', 'About/Preview.png'],
];

// Explicit state variants are optional integration assets; not auto-wired effects.
for (const state of ['idle', 'active', 'no-power', 'completion', 'error']) {
  for (const direction of ['north', 'east', 'south']) {
    jobs.push([
      `IC_AssimilationPod_${state}_${direction}.svg`,
      `Textures/Things/Building/IC_AssimilationPod/States/${state}/IC_AssimilationPod_${direction}.png`,
    ]);
  }
}

(async () => {
  for (const [src, dst] of jobs) {
    const target = path.join(outRoot, dst);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    await sharp(path.join(svgDir, src)).png().toFile(target);
    const meta = await sharp(target).metadata();
    console.log(dst, `${meta.width}x${meta.height}`);
    if (process.env.QA === '1') {
      fs.mkdirSync(qaDir, { recursive: true });
      const pad = 24;
      await sharp({ create: { width: meta.width + pad * 2, height: meta.height + pad * 2, channels: 4, background: '#6f604b' } })
        .composite([{ input: target, left: pad, top: pad }])
        .png()
        .toFile(path.join(qaDir, 'qa_' + path.basename(dst)));
    }
  }
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
