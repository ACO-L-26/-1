const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const PORT = 8080;
const ROOT = __dirname;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css',
  '.js': 'application/javascript',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

// ===================== ACTIVITY DETECTION =====================

// Game process -> metadata
const GAME_MAP = {
  'genshinimpact.exe': { name:'原神', genre:'RPG', style:'Orchestral Epic Fantasy' },
  'yuanshen.exe':     { name:'原神', genre:'RPG', style:'Orchestral Epic Fantasy' },
  'starrail.exe':     { name:'崩坏:星穹铁道', genre:'RPG', style:'Sci-Fi Orchestral' },
  'honkaiimpact.exe': { name:'崩坏3', genre:'Action', style:'Electronic Rock' },
  'zenlesszonezero.exe':{ name:'绝区零', genre:'Action', style:'Urban Electronic' },
  'wutheringwaves.exe':{ name:'鸣潮', genre:'RPG', style:'Epic Orchestral' },
  'league of legends.exe':{ name:'英雄联盟', genre:'MOBA', style:'Electronic Pop' },
  'leagueclient.exe': { name:'英雄联盟', genre:'MOBA', style:'Electronic Pop' },
  'valorant.exe':     { name:'Valorant', genre:'FPS', style:'Electronic Drum & Bass' },
  'cs2.exe':          { name:'CS2', genre:'FPS', style:'Rock Electronic' },
  'csgo.exe':         { name:'CS:GO', genre:'FPS', style:'Rock Electronic' },
  'r5apex.exe':       { name:'Apex Legends', genre:'FPS', style:'Electronic Drum & Bass' },
  'overwatch.exe':    { name:'Overwatch', genre:'FPS', style:'Electronic Pop' },
  'eldenring.exe':    { name:'Elden Ring', genre:'RPG', style:'Dark Orchestral' },
  'darksouls3.exe':   { name:'Dark Souls 3', genre:'RPG', style:'Dark Orchestral' },
  'sekiro.exe':       { name:'Sekiro', genre:'Action', style:'Japanese Traditional' },
  'minecraft.exe':    { name:'Minecraft', genre:'Sandbox', style:'Ambient Lo-fi' },
  'javaw.exe':        { name:'Minecraft', genre:'Sandbox', style:'Ambient Lo-fi' },
  'terraria.exe':     { name:'Terraria', genre:'Sandbox', style:'Chiptune Retro' },
  'stardew valley.exe':{ name:'Stardew Valley', genre:'Simulation', style:'Acoustic Chill' },
  'cyberpunk2077.exe':{ name:'Cyberpunk 2077', genre:'RPG', style:'Synthwave Industrial' },
  'witcher3.exe':     { name:'The Witcher 3', genre:'RPG', style:'Fantasy Orchestral' },
  'pubg.exe':         { name:'PUBG', genre:'FPS', style:'Electronic Intense' },
  'tslgame.exe':      { name:'PUBG', genre:'FPS', style:'Electronic Intense' },
  'fortnite.exe':     { name:'Fortnite', genre:'FPS', style:'Electronic Pop' },
  'dota2.exe':        { name:'Dota 2', genre:'MOBA', style:'Epic Orchestral' },
  'warframe.exe':     { name:'Warframe', genre:'Action', style:'Sci-Fi Electronic' },
  'destiny2.exe':     { name:'Destiny 2', genre:'FPS', style:'Sci-Fi Orchestral' },
  'ffxiv.exe':        { name:'FFXIV', genre:'RPG', style:'Fantasy Orchestral' },
  'wow.exe':          { name:'World of Warcraft', genre:'RPG', style:'Fantasy Orchestral' },
  'monsterhunter.exe':{ name:'Monster Hunter', genre:'Action', style:'Epic Orchestral' },
  'bg3.exe':          { name:"Baldur's Gate 3", genre:'RPG', style:'Fantasy Orchestral' },
  'l4d2.exe':         { name:'Left 4 Dead 2', genre:'FPS', style:'Rock Metal' },
  'gta5.exe':         { name:'GTA V', genre:'Action', style:'Hip-Hop Electronic' },
  'rdr2.exe':         { name:'Red Dead Redemption 2', genre:'Action', style:'Western Acoustic' },
  'deadbydaylight.exe':{ name:'Dead by Daylight', genre:'Horror', style:'Dark Ambient' },
  'phasmophobia.exe': { name:'Phasmophobia', genre:'Horror', style:'Dark Ambient' },
  'residentevil.exe': { name:'Resident Evil', genre:'Horror', style:'Dark Industrial' },
  'sims4.exe':        { name:'The Sims 4', genre:'Simulation', style:'Pop Lo-fi' },
  'cities.exe':       { name:'Cities: Skylines', genre:'Simulation', style:'Ambient Chill' },
  'rocketleague.exe': { name:'Rocket League', genre:'Sports', style:'Electronic Rock' },
  'fifa.exe':         { name:'FIFA', genre:'Sports', style:'Pop Electronic' },
  'osu!.exe':         { name:'osu!', genre:'Rhythm', style:'J-Pop Electronic' },
  'celeste.exe':      { name:'Celeste', genre:'Platformer', style:'Chiptune Ambient' },
  'hollowknight.exe': { name:'Hollow Knight', genre:'Metroidvania', style:'Orchestral Ambient' },
};

const MUSIC_APPS = {
  'cloudmusic.exe':    '网易云音乐',
  'qqmusic.exe':       'QQ音乐',
  'kugou.exe':         '酷狗音乐',
  'spotify.exe':       'Spotify',
  'foobar2000.exe':    'foobar2000',
  'wmplayer.exe':      'Windows Media Player',
  'music.ui.exe':      'Apple Music',
};

// Scene -> recommended search terms
const SCENE_GENRES = {
  'FPS':        ['Electronic', 'Drum and Bass', 'Rock', 'Phonk'],
  'RPG':        ['Epic Orchestral', 'Fantasy Soundtrack', 'Ambient Cinematic', 'Adventure Music'],
  'MOBA':       ['Electronic', 'Pop', 'K-Pop', 'Hype'],
  'Action':     ['Electronic Rock', 'Metal', 'Cyberpunk', 'Intense'],
  'Horror':     ['Dark Ambient', 'Industrial', 'Haunting', 'Suspense'],
  'Sandbox':    ['Lo-fi', 'Ambient', 'Chill', 'Acoustic'],
  'Simulation': ['Lo-fi', 'Acoustic', 'Chill Pop', 'Jazz'],
  'Sports':     ['Pop', 'Electronic', 'Rock', 'Hip-Hop'],
  'Rhythm':     ['J-Pop', 'Electronic', 'Anime', 'Vocaloid'],
  'Platformer': ['Chiptune', 'Retro Game', 'Ambient', '8-bit'],
  'Metroidvania':['Orchestral Ambient', 'Dark Cinematic', 'Gothic'],
};

function getTimeOfDay(){
  const h = new Date().getHours();
  if(h >= 5 && h < 8) return '清晨';
  if(h >= 8 && h < 12) return '上午';
  if(h >= 12 && h < 14) return '午间';
  if(h >= 14 && h < 18) return '下午';
  if(h >= 18 && h < 22) return '傍晚';
  return '深夜';
}

function getTimeBasedGenre(timeOfDay){
  const map = {
    '清晨': ['Acoustic Morning', 'Classical Piano', 'Nature Sounds'],
    '上午': ['Jazz', 'Instrumental', 'Bossa Nova'],
    '午间': ['Pop', 'R&B', 'Soul'],
    '下午': ['Indie', 'Alternative', 'Electronic Chill'],
    '傍晚': ['Lo-fi Hip Hop', 'Jazz Hop', 'Chill'],
    '深夜': ['Lo-fi', 'Ambient', 'Dark Jazz', 'Rain Sounds'],
  };
  return map[timeOfDay] || ['Pop'];
}

function detectActivity(callback){
  exec('tasklist /fo csv /nh', { timeout: 5000 }, (err, stdout) => {
    if(err){ callback({}); return; }
    const lines = (stdout||'').toLowerCase().split('\n');
    const result = { game: null, music: null, timeOfDay: getTimeOfDay() };

    for(const line of lines){
      const name = line.replace(/"/g,'').split(',')[0]?.trim();
      if(!name) continue;
      if(!result.game && GAME_MAP[name]){
        result.game = GAME_MAP[name];
      }
      if(!result.music && MUSIC_APPS[name]){
        result.music = MUSIC_APPS[name];
        result.musicKey = name;
      }
      if(result.game && result.music) break;
    }
    callback(result);
  });
}

function getRecommendGenre(activity){
  const genres = [];
  if(activity.game){
    genres.push(...(SCENE_GENRES[activity.game.genre] || ['Electronic']));
  }
  if(!activity.game || activity.music){
    genres.push(...getTimeBasedGenre(activity.timeOfDay));
  }
  return [...new Set(genres)];
}

function getContextMessage(activity){
  let msg = '';
  if(activity.game && activity.music){
    msg = `在玩${activity.game.name}同时听${activity.music}，`;
  } else if(activity.game){
    msg = `在玩${activity.game.name}，`;
  } else if(activity.music){
    msg = `在用${activity.music}听歌，`;
  } else {
    const t = activity.timeOfDay;
    msg = `${t}时光，`;
  }
  return msg + '试试这些音乐？';
}

// ===================== ITUNES PROXY =====================
function proxyRequest(req, res, targetBase) {
  const reqUrl = new URL(req.url, 'http://localhost');
  const apiPath = reqUrl.pathname.replace('/api/deezer', '') + reqUrl.search;
  const targetUrl = targetBase + apiPath;

  const targetParsed = new URL(targetUrl);
  let responded = false;

  function reply(status, body) {
    if(responded) return;
    responded = true;
    res.writeHead(status, {
      'Content-Type': 'application/json; charset=utf-8',
      'Access-Control-Allow-Origin': '*',
    });
    res.end(typeof body === 'string' ? body : JSON.stringify(body));
  }

  const opts = {
    hostname: targetParsed.hostname,
    path: targetParsed.pathname + targetParsed.search,
    method: req.method,
    headers: {
      'User-Agent': 'MusicDiscovery/1.0',
      'Accept': 'application/json',
    },
    timeout: 15000,
  };

  const proxy = https.request(opts, (proxyRes) => {
    let body = '';
    proxyRes.on('data', chunk => body += chunk);
    proxyRes.on('end', () => reply(proxyRes.statusCode || 200, body));
  });

  proxy.on('error', (e) => {
    reply(502, { error: 'Proxy error: ' + e.message });
  });

  proxy.on('timeout', () => {
    proxy.destroy();
    reply(504, { error: 'Proxy timeout' });
  });

  proxy.end();
}

// Proxy iTunes to avoid browser CORS issues
function proxyiTunes(req, res){
  const reqUrl = new URL(req.url, 'http://localhost');
  const apiPath = reqUrl.pathname.replace('/api/itunes', '') + reqUrl.search;
  const targetUrl = 'https://itunes.apple.com' + apiPath;

  const targetParsed = new URL(targetUrl);
  let responded = false;

  function reply(status, body) {
    if(responded) return;
    responded = true;
    res.writeHead(status, {
      'Content-Type': 'application/json; charset=utf-8',
      'Access-Control-Allow-Origin': '*',
    });
    res.end(typeof body === 'string' ? body : JSON.stringify(body));
  }

  const opts = {
    hostname: targetParsed.hostname,
    path: targetParsed.pathname + targetParsed.search,
    method: req.method,
    headers: { 'User-Agent': 'MusicDiscovery/1.0', 'Accept': 'application/json' },
    timeout: 15000,
  };

  const proxy = https.request(opts, (proxyRes) => {
    let body = '';
    proxyRes.on('data', chunk => body += chunk);
    proxyRes.on('end', () => reply(proxyRes.statusCode || 200, body));
  });

  proxy.on('error', (e) => reply(502, { error: 'Proxy error: ' + e.message }));
  proxy.on('timeout', () => { proxy.destroy(); reply(504, { error: 'Proxy timeout' }); });
  proxy.end();
}

// Activity API
function handleActivityAPI(req, res){
  detectActivity(activity => {
    const genres = getRecommendGenre(activity);
    const message = getContextMessage(activity);
    res.writeHead(200, {
      'Content-Type': 'application/json; charset=utf-8',
      'Access-Control-Allow-Origin': '*',
    });
    res.end(JSON.stringify({ activity, genres, message }));
  });
}

// ===================== HTTP SERVER =====================
http.createServer((req, res) => {
  const reqUrl = new URL(req.url, 'http://localhost');
  let urlPath = reqUrl.pathname;

  // Activity API
  if (urlPath === '/api/activity') {
    handleActivityAPI(req, res);
    return;
  }

  // iTunes proxy
  if (urlPath.startsWith('/api/itunes/')) {
    proxyiTunes(req, res);
    return;
  }

  // Deezer proxy (legacy)
  if (urlPath.startsWith('/api/deezer/')) {
    proxyRequest(req, res, 'https://api.deezer.com');
    return;
  }

  // Static files
  let filePath = path.join(ROOT, urlPath);
  try {
    if (fs.existsSync(filePath) && fs.statSync(filePath).isDirectory()) {
      filePath = path.join(filePath, 'index.html');
    }
    if (!path.extname(filePath)) {
      filePath = path.join(filePath, 'index.html');
    }
    if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end('Not found');
      return;
    }
    const ext = path.extname(filePath);
    res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
    const stream = fs.createReadStream(filePath);
    stream.on('error', () => { res.writeHead(500); res.end('Server error'); });
    stream.pipe(res);
  } catch (e) {
    res.writeHead(500);
    res.end('Server error');
  }
}).listen(PORT, () => {
  console.log('Server running at http://127.0.0.1:' + PORT);
  console.log('Activity API: GET /api/activity');
});
