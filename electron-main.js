const { app, BrowserWindow, Tray, Menu, nativeImage } = require('electron');
const { fork } = require('child_process');
const path = require('path');

let win, tray, server;
const PORT = 8080;
const PET_URL = `http://127.0.0.1:${PORT}/pet.html`;

function startServer() {
  return new Promise((resolve) => {
    server = fork(path.join(__dirname, 'server.js'), [], { silent: true });
    server.stdout.on('data', d => console.log('[Server]', d.toString().trim()));
    server.stderr.on('data', d => console.error('[Server]', d.toString().trim()));
    // Give server time to start
    setTimeout(resolve, 1500);
  });
}

function createTray() {
  // Create a simple colored square as tray icon
  const size = 16;
  const buf = Buffer.alloc(size * size * 4);
  for (let i = 0; i < size * size; i++) {
    const x = i % size, y = Math.floor(i / size);
    const cx = size/2, cy = size/2;
    const d = Math.sqrt((x-cx)**2 + (y-cy)**2);
    const o = i * 4;
    if (d < 7) { buf[o]=255; buf[o+1]=107; buf[o+2]=157; buf[o+3]=255; }
    else { buf[o]=0; buf[o+1]=0; buf[o+2]=0; buf[o+3]=0; }
  }
  const icon = nativeImage.createFromBuffer(buf, { width: size, height: size });
  tray = new Tray(icon);
  tray.setToolTip('Music Pet');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Show Pet', click: () => win.show() },
    { label: 'Hide Pet', click: () => win.hide() },
    { type: 'separator' },
    { label: 'Quit', click: () => { tray.destroy(); app.quit(); } }
  ]));
  tray.on('double-click', () => win.show());
}

function createWindow() {
  win = new BrowserWindow({
    width: 320,
    height: 480,
    x: 0,
    y: 0,
    frame: false,
    transparent: true,
    alwaysOnTop: true,
    resizable: false,
    skipTaskbar: true,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true
    }
  });

  // Position at bottom-right of screen
  const { screen } = require('electron');
  const { width: sw, height: sh } = screen.getPrimaryDisplay().workAreaSize;
  win.setPosition(sw - 340, sh - 500);

  win.loadURL(PET_URL);

  // Prevent closing, just hide
  win.on('close', (e) => {
    if (!app.isQuitting) {
      e.preventDefault();
      win.hide();
    }
  });
}

app.isQuitting = false;

app.on('before-quit', () => {
  app.isQuitting = true;
  if (server) server.kill();
});

app.whenReady().then(async () => {
  await startServer();
  createTray();
  createWindow();
});

app.on('window-all-closed', () => {
  // Don't quit, keep running in tray
});
