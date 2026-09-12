import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { chromium } from 'playwright';
import ffmpeg from '@ffmpeg-installer/ffmpeg';

const root = path.dirname(fileURLToPath(import.meta.url));
const previewOnly = process.argv.includes('--preview');
const videoPath = path.join(root, 'horse-grassland-5s.mp4');
if (!previewOnly && existsSync(videoPath)) throw new Error(`Output already exists: ${videoPath}`);
const contentTypes = { '.html': 'text/html', '.js': 'text/javascript', '.glb': 'model/gltf-binary', '.json': 'application/json' };
const server = createServer(async (request, response) => {
  try {
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    const resource = path.resolve(root, '.' + (pathname === '/' ? '/scene.html' : pathname));
    if (!resource.startsWith(root + path.sep)) {
      response.writeHead(403).end();
      return;
    }
    const data = await readFile(resource);
    response.writeHead(200, { 'Content-Type': contentTypes[path.extname(resource)] || 'application/octet-stream' });
    response.end(data);
  } catch {
    response.writeHead(404).end();
  }
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port;
const browserPath = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
].find(existsSync);
if (!browserPath) throw new Error('No installed Chrome or Edge browser was found.');
let browser;
try {
  browser = await chromium.launch({
    executablePath: browserPath,
    headless: true,
    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-background-timer-throttling']
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto(`http://127.0.0.1:${port}/scene.html?capture=1`, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.sceneReady === true, { timeout: 60000 });
  await page.evaluate(() => window.renderFrame(0.6));
  const desktop = await page.evaluate(() => window.inspectFrame());
  await page.screenshot({ path: path.join(root, 'horse-grassland-preview.png') });
  await page.evaluate(() => window.renderFrame(0.79));
  const moved = await page.evaluate(() => window.inspectFrame());
  if (desktop.colors < 100 || desktop.brightness < 20 || !desktop.horseInFrame || desktop.hash === moved.hash) {
    throw new Error(`Scene failed visual checks: ${JSON.stringify({ desktop, moved })}`);
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.renderFrame(0.6));
  const mobile = await page.evaluate(() => window.inspectFrame());
  await page.screenshot({ path: path.join(root, 'mobile-preview.png') });
  if (mobile.colors < 100 || !mobile.horseInFrame || errors.length) {
    throw new Error(`Mobile or console check failed: ${JSON.stringify({ mobile, errors })}`);
  }
  const verification = { desktop, moved, mobile, errors };
  await writeFile(path.join(root, 'verification.json'), JSON.stringify(verification, null, 2) + '\n');
  console.log(JSON.stringify(verification));
  if (!previewOnly) {
    await page.setViewportSize({ width: 1280, height: 720 });
    await mkdir(path.join(root, 'frames'), { recursive: true });
    for (let frame = 0; frame < 150; frame++) {
      await page.evaluate((time) => window.renderFrame(time), frame / 30);
      await page.screenshot({ path: path.join(root, 'frames', `frame-${String(frame).padStart(4, '0')}.png`) });
      if ((frame + 1) % 30 === 0) console.log(`Rendered ${frame + 1}/150 frames`);
    }
    await new Promise((resolve, reject) => {
      const encoder = spawn(ffmpeg.path, [
        '-hide_banner', '-loglevel', 'error', '-n', '-framerate', '30',
        '-i', path.join(root, 'frames', 'frame-%04d.png'), '-frames:v', '150',
        '-c:v', 'libx264', '-preset', 'medium', '-crf', '21', '-pix_fmt', 'yuv420p',
        '-movflags', '+faststart', '-an', videoPath
      ], { stdio: 'inherit', windowsHide: true });
      encoder.on('error', reject);
      encoder.on('exit', (code) => code === 0 ? resolve() : reject(new Error(`FFmpeg exited ${code}`)));
    });
    const decoded = spawnSync(ffmpeg.path, ['-hide_banner', '-i', videoPath, '-f', 'null', '-'], { encoding: 'utf8', windowsHide: true });
    const duration = decoded.stderr.match(/Duration:\s*([\d:.]+)/)?.[1];
    if (decoded.status !== 0 || duration !== '00:00:05.00') {
      throw new Error(`Video validation failed: ${decoded.stderr}`);
    }
    console.log(`Verified video: ${videoPath}; duration=${duration}; 1280x720; 30 fps; 150 frames`);
  }
} finally {
  if (browser) await browser.close();
  await new Promise((resolve) => server.close(resolve));
}
