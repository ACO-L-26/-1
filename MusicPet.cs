using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net;
using System.Text;
using System.Collections.Generic;

public class MusicPet : Form
{
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;

    // Sizing
    const int PET_W = 110, PET_H = 130;
    const int CHAT_W = 210, CHAT_H = 180;
    const int FULL_W = 230, FULL_H = 320;

    private bool dragging = false, wasDragged = false;
    private Point dragStart;
    private System.Timers.Timer pollTimer;
    private System.Windows.Forms.Timer animTimer;
    private ToolTip tooltip;

    // State
    private ActivityData activity;
    private List<TrackInfo> recs;
    private int chatPhase = 0; // 0=none, 1=dialogue, 2=recommendations
    private string[] chatOptions;
    private Rectangle[] optionRects;
    private string dialogueMsg = "";
    private string recTitle = "";
    private float bobOffset = 0, bobDir = 1;
    private int noteFrame = 0;

    // Colors - baby seal
    readonly Brush sealBody = new SolidBrush(Color.FromArgb(180, 200, 220));
    readonly Brush sealBelly = new SolidBrush(Color.FromArgb(230, 238, 245));
    readonly Brush sealDark = new SolidBrush(Color.FromArgb(45, 52, 60));
    readonly Brush sealBlush = new SolidBrush(Color.FromArgb(255, 180, 190));
    readonly Brush sealNose = new SolidBrush(Color.FromArgb(55, 60, 70));
    readonly Pen sealOutline = new Pen(Color.FromArgb(150, 170, 190), 1.5f);
    readonly Brush bubbleBrush = new SolidBrush(Color.FromArgb(30, 30, 55));
    readonly Brush dimBrush = new SolidBrush(Color.FromArgb(136, 136, 168));
    readonly Brush accentBrush = new SolidBrush(Color.FromArgb(255, 107, 157));
    readonly Brush optHoverBrush = new SolidBrush(Color.FromArgb(50, 50, 80));
    readonly Brush whiteBrush = new SolidBrush(Color.White);

    public MusicPet()
    {
        this.Size = new Size(PET_W, PET_H);
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.Manual;
        this.BackColor = Color.Fuchsia;
        this.TransparencyKey = Color.Fuchsia;
        this.AllowTransparency = true;
        this.DoubleBuffered = true;

        var screen = Screen.PrimaryScreen.WorkingArea;
        this.Location = new Point(screen.Width - PET_W - 10, screen.Height - PET_H - 10);

        // Right-click menu: quit
        var ctxMenu = new ContextMenuStrip();
        ctxMenu.Items.Add("退出桌宠", null, (s2, e2) => { Application.Exit(); });
        this.ContextMenuStrip = ctxMenu;

        // Mouse
        this.MouseDown += (s, e) => { dragging=true; wasDragged=false; dragStart=e.Location; };
        this.MouseMove += (s, e) => {
            if(dragging && (Math.Abs(e.X-dragStart.X)>3 || Math.Abs(e.Y-dragStart.Y)>3)) {
                this.Left += e.X-dragStart.X; this.Top += e.Y-dragStart.Y; wasDragged=true;
            }
            if(chatPhase > 0) { CheckOptionHover(e.Location); this.Invalidate(); }
        };
        this.MouseUp += (s, e) => { dragging=false; };
        this.Click += (s, e) => {
            if(wasDragged) return;
            if(chatPhase == 0) { ShowDialogue(); }
            else if(chatPhase == 1) { /* handled by option click */ }
            else { HideChat(); }
        };

        // Animation
        animTimer = new System.Windows.Forms.Timer();
        animTimer.Interval = 50;
        animTimer.Tick += (s, e) => {
            bobOffset += 0.15f * bobDir;
            if(Math.Abs(bobOffset) > 3) bobDir *= -1;
            noteFrame = (noteFrame+1) % 60;
            this.Invalidate();
        };
        animTimer.Start();

        // Tooltip
        tooltip = new ToolTip();
        tooltip.SetToolTip(this, "♪ Music Pet");

        // Poll activity
        pollTimer = new System.Timers.Timer(15000);
        pollTimer.Elapsed += (s, e) => PollActivity();
        pollTimer.AutoReset = true;
        pollTimer.Start();
        PollActivity();

        this.Load += (s, e) => SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        this.Paint += OnPaint;
    }

    // =========== PAINTING ===========

    private void OnPaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int cy;
        if(chatPhase == 0) {
            cy = PET_H - 75; // character bottom-relative
        } else {
            cy = FULL_H - 75; // same screen position
            DrawBubble(g);
        }
        DrawCharacter(g, PET_W/2, cy);
    }

    private void DrawCharacter(Graphics g, int cx, int cy)
    {
        cy += (int)bobOffset;
        bool gaming = activity != null && activity.game != null;
        bool listening = activity != null && activity.music != null;
        bool blinking = !gaming && !listening && noteFrame % 60 > 56;

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.HighQuality;

        // === BODY (oval) ===
        int bx = cx - 22, by = cy + 5, bw = 44, bh = 50;
        g.FillEllipse(sealBody, bx, by, bw, bh);
        g.DrawEllipse(sealOutline, bx, by, bw, bh);

        // === BELLY (lighter oval) ===
        g.FillEllipse(sealBelly, cx - 14, cy + 15, 28, 32);

        // === TAIL (two small fins at bottom) ===
        g.FillPie(sealBody, cx - 8, cy + 48, 16, 14, 180, 60);
        g.FillPie(sealBody, cx - 8, cy + 48, 16, 14, 300, 60);

        // === FLIPPERS (side) ===
        g.FillEllipse(sealBody, bx - 6, cy + 18, 12, 20);
        g.FillEllipse(sealBody, bx + bw - 6, cy + 18, 12, 20);
        g.DrawEllipse(sealOutline, bx - 6, cy + 18, 12, 20);
        g.DrawEllipse(sealOutline, bx + bw - 6, cy + 18, 12, 20);

        // === HEAD (round top) ===
        int hx = cx - 18, hy = cy - 12, hw = 36, hh = 34;
        g.FillEllipse(sealBody, hx, hy, hw, hh);
        g.DrawEllipse(sealOutline, hx, hy, hw, hh);

        // === EYES ===
        int eyeY = cy - 2;
        // Left eye
        g.FillEllipse(sealDark, cx - 12, eyeY, 7, 9);
        // Right eye
        g.FillEllipse(sealDark, cx + 5, eyeY, 7, 9);

        if(!blinking) {
            // Eye shine
            g.FillEllipse(whiteBrush, cx - 10, eyeY + 1, 2.5f, 2.5f);
            g.FillEllipse(whiteBrush, cx + 7, eyeY + 1, 2.5f, 2.5f);
        } else {
            // Blink: draw body-colored line over eyes
            var blinkPen = new Pen(Color.FromArgb(180, 200, 220), 2);
            g.DrawLine(blinkPen, cx - 15, eyeY + 4, cx - 5, eyeY + 4);
            g.DrawLine(blinkPen, cx + 2, eyeY + 4, cx + 12, eyeY + 4);
        }

        // === NOSE ===
        g.FillEllipse(sealNose, cx - 4, cy + 6, 8, 5);

        // === WHISKERS ===
        var wPen = new Pen(Color.FromArgb(120, 140, 155), 0.8f);
        // Left whiskers
        g.DrawLine(wPen, cx - 6, cy + 8, cx - 20, cy + 4);
        g.DrawLine(wPen, cx - 6, cy + 9, cx - 20, cy + 9);
        g.DrawLine(wPen, cx - 6, cy + 10, cx - 20, cy + 14);
        // Right whiskers
        g.DrawLine(wPen, cx + 6, cy + 8, cx + 20, cy + 4);
        g.DrawLine(wPen, cx + 6, cy + 9, cx + 20, cy + 9);
        g.DrawLine(wPen, cx + 6, cy + 10, cx + 20, cy + 14);

        // === MOUTH ===
        var mPen = new Pen(Color.FromArgb(80, 90, 100), 1);
        if(gaming) {
            // Determined mouth
            g.DrawLine(mPen, cx - 3, cy + 14, cx + 3, cy + 14);
        } else {
            // Happy curve
            g.DrawArc(mPen, cx - 4, cy + 11, 8, 6, 0, -180);
        }

        // === BLUSH ===
        g.FillEllipse(sealBlush, cx - 18, eyeY + 6, 7, 5);
        g.FillEllipse(sealBlush, cx + 11, eyeY + 6, 7, 5);

        // === HEADPHONES (gaming) ===
        if(gaming) {
            var hpPen = new Pen(Color.FromArgb(60, 65, 75), 2.5f);
            var hpBrush = new SolidBrush(Color.FromArgb(50, 55, 65));
            // Ear cups
            g.FillEllipse(hpBrush, cx - 22, cy - 8, 14, 16);
            g.FillEllipse(hpBrush, cx + 8, cy - 8, 14, 16);
            g.DrawEllipse(hpPen, cx - 22, cy - 8, 14, 16);
            g.DrawEllipse(hpPen, cx + 8, cy - 8, 14, 16);
            // Band
            g.DrawArc(hpPen, cx - 17, cy - 10, 34, 20, 200, 140);
        }

        // === MUSIC NOTES (listening) ===
        if(listening) {
            var nf = new Font("Arial", 11, FontStyle.Bold);
            var nb = new SolidBrush(Color.FromArgb(120, 140, 220));
            g.DrawString("♪", nf, nb, cx + 14, cy - 14 - (noteFrame*3%24));
            g.DrawString("♫", nf, nb, cx + 26, cy - 22 - ((noteFrame*3+12)%24));
            // Sway eyes (half-close)
            var lashPen = new Pen(Color.FromArgb(180, 200, 220), 1.5f);
            g.DrawLine(lashPen, cx - 15, eyeY, cx - 5, eyeY);
            g.DrawLine(lashPen, cx + 2, eyeY, cx + 12, eyeY);
        }

        g.Restore(state);
    }

    private void DrawBubble(Graphics g)
    {
        int bx = 10, by = 5, bw = FULL_W - 20, bh;
        string text;

        if(chatPhase == 1) {
            bh = 60 + chatOptions.Length * 32;
            text = dialogueMsg;
        } else {
            bh = 40 + (recs != null ? recs.Count * 22 : 20);
            text = recTitle;
        }

        // Bubble background - rounded rect
        var path = RoundedRect(bx, by, bw, bh, 12);
        g.FillPath(bubbleBrush, path);
        g.DrawPath(new Pen(Color.FromArgb(80, 80, 120), 1), path);

        // Little triangle pointing down to character
        var tri = new Point[] {
            new Point(FULL_W/2 - 8, by + bh),
            new Point(FULL_W/2 + 8, by + bh),
            new Point(FULL_W/2, by + bh + 10)
        };
        g.FillPolygon(bubbleBrush, tri);
        g.DrawLine(new Pen(Color.FromArgb(80, 80, 120), 1), tri[0], tri[2]);
        g.DrawLine(new Pen(Color.FromArgb(80, 80, 120), 1), tri[1], tri[2]);

        // Text
        var textFont = new Font("Microsoft YaHei", 9, FontStyle.Regular);
        g.DrawString(text, textFont, accentBrush, bx + 12, by + 10);

        if(chatPhase == 1) {
            // Draw options
            optionRects = new Rectangle[chatOptions.Length];
            for(int i=0; i<chatOptions.Length; i++) {
                int oy = by + 50 + i * 32;
                var optRect = new Rectangle(bx + 12, oy, bw - 24, 26);
                optionRects[i] = optRect;

                // Check if mouse is over this option (set via CheckOptionHover)
                bool hover = (i == hoveredOption);

                if(hover) g.FillRectangle(optHoverBrush, optRect);
                g.DrawString(chatOptions[i], textFont, hover ? whiteBrush : dimBrush, bx + 16, oy + 4);
            }
        } else if(chatPhase == 2) {
            // Draw recommendation items (clickable → opens music app)
            if(recs != null) {
                trackRects = new Rectangle[recs.Count];
                var smallFont = new Font("Microsoft YaHei", 8, FontStyle.Regular);
                var linkFont = new Font("Microsoft YaHei", 8, FontStyle.Underline);
                for(int i=0; i<recs.Count; i++) {
                    int ty = by + 38 + i * 22;
                    var tr = new Rectangle(bx + 12, ty, bw - 24, 20);
                    trackRects[i] = tr;
                    bool hover = (i == hoveredTrack);
                    var f = hover ? linkFont : smallFont;
                    var br = hover ? accentBrush : whiteBrush;
                    g.DrawString(recs[i].ToString(), f, br, bx + 12, ty);
                }
            }
        }
    }

    private int hoveredOption = -1;
    private int hoveredTrack = -1;
    private Rectangle[] trackRects;

    private void CheckOptionHover(Point mouse)
    {
        hoveredOption = -1;
        hoveredTrack = -1;
        if(optionRects != null) {
            for(int i=0; i<optionRects.Length; i++) {
                if(optionRects[i].Contains(mouse)) { hoveredOption = i; return; }
            }
        }
        if(trackRects != null) {
            for(int i=0; i<trackRects.Length; i++) {
                if(trackRects[i].Contains(mouse)) { hoveredTrack = i; return; }
            }
        }
    }

    // Handle option click via MouseUp
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if(wasDragged) return;

        if(chatPhase == 1 && optionRects != null) {
            for(int i=0; i<optionRects.Length; i++) {
                if(optionRects[i].Contains(e.Location)) {
                    ShowRecs(chatOptions[i]);
                    return;
                }
            }
            HideChat();
        } else if(chatPhase == 2) {
            // Check if a track was clicked → open in music app
            if(trackRects != null && recs != null) {
                for(int i=0; i<trackRects.Length; i++) {
                    if(trackRects[i].Contains(e.Location)) {
                        OpenInMusicApp(recs[i]);
                        return;
                    }
                }
            }
            // Click elsewhere = dismiss
            HideChat();
        }
    }

    // =========== CHAT LOGIC ===========

    private void ShowDialogue()
    {
        chatPhase = 1;
        dialogueMsg = GetDialogue();
        chatOptions = GetOptions();
        SwitchToChatMode();
    }

    private void ShowRecs(string choice)
    {
        chatPhase = 2;
        recTitle = choice;
        LoadRecsForChoice(choice);
        SwitchToChatMode();
    }

    private void HideChat()
    {
        chatPhase = 0;
        chatOptions = null;
        optionRects = null;
        trackRects = null;
        hoveredOption = -1;
        hoveredTrack = -1;
        SwitchToPetMode();
    }

    // Character Y offset from form top in each mode
    const int CHAR_Y_PET = PET_H - 75;    // character center Y in pet mode
    const int CHAR_Y_FULL = FULL_H - 75;   // character center Y in chat mode

    private void SwitchToChatMode()
    {
        // Save character screen position (using current pet-mode offset)
        int charY = this.Top + CHAR_Y_PET;
        int charX = this.Left + PET_W / 2;
        this.MinimumSize = new Size(0,0);
        this.Size = new Size(FULL_W, FULL_H);
        this.Location = new Point(charX - FULL_W / 2, charY - CHAR_Y_FULL);
        this.Invalidate();
    }

    private void SwitchToPetMode()
    {
        // Save character screen position (using current chat-mode offset)
        int charY = this.Top + CHAR_Y_FULL;
        int charX = this.Left + FULL_W / 2;
        this.Size = new Size(PET_W, PET_H);
        this.Location = new Point(charX - PET_W / 2, charY - CHAR_Y_PET);
        this.Invalidate();
    }

    private GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r*2, r*2, 180, 90);
        path.AddArc(x+w-r*2, y, r*2, r*2, 270, 90);
        path.AddArc(x+w-r*2, y+h-r*2, r*2, r*2, 0, 90);
        path.AddArc(x, y+h-r*2, r*2, r*2, 90, 90);
        path.CloseFigure();
        return path;
    }

    // =========== DIALOGUE CONTENT ===========

    private string GetDialogue()
    {
        if(activity != null && activity.game != null)
            return "在玩 " + activity.game.name + " 呀~\n想听点什么音乐？";
        if(activity != null && activity.music != null)
            return "在听歌呢~\n要推荐同类还是换换口味？";
        var h = DateTime.Now.Hour;
        if(h < 6 || h >= 23) return "夜深了...\n来点安静的音乐吧~";
        if(h < 9) return "早安~\n来点音乐开启新一天？";
        if(h < 14) return "下午好~\n想来点提神的吗？";
        return "想听点什么呢？";
    }

    private string[] GetOptions()
    {
        if(activity != null && activity.game != null)
            return new[] { "🎮 游戏同款风格", "😌 安静放松一下", "🎲 随机来点" };
        if(activity != null && activity.music != null)
            return new[] { "🎵 相似风格", "🔄 换换口味", "🎲 随机推荐" };
        var h = DateTime.Now.Hour;
        if(h < 6 || h >= 23) return new[] { "🌙 助眠轻音乐", "🎹 钢琴独奏", "🎲 随便听听" };
        if(h < 9) return new[] { "🌅 元气流行", "☕ 咖啡爵士", "🎲 随机推荐" };
        return new[] { "🎸 流行热歌", "🎻 古典器乐", "🎲 随机来点" };
    }

    // =========== RECOMMENDATIONS ===========

    private void LoadRecsForChoice(string choice)
    {
        string[] genres;
        if(choice.Contains("游戏") || choice.Contains("风格")) genres = GetGenres();
        else if(choice.Contains("安静") || choice.Contains("放松") || choice.Contains("助眠") || choice.Contains("轻音"))
            genres = new[] { "Ambient", "Lo-fi", "Piano" };
        else if(choice.Contains("换换") || choice.Contains("随机") || choice.Contains("随便")) {
            var all = new[]{"Jazz","Rock","Electronic","Classical","Pop","R&B","Indie","Folk","Hip-Hop","Chill"};
            var rng = new Random();
            genres = new[]{all[rng.Next(all.Length)],all[rng.Next(all.Length)],all[rng.Next(all.Length)]};
        } else if(choice.Contains("咖啡") || choice.Contains("爵士"))
            genres = new[]{"Jazz","Bossa Nova","Acoustic"};
        else if(choice.Contains("钢琴") || choice.Contains("古典") || choice.Contains("器乐"))
            genres = new[]{"Classical","Piano","Instrumental"};
        else if(choice.Contains("元气") || choice.Contains("流行"))
            genres = new[]{"Pop","Acoustic","Indie"};
        else genres = GetGenres();

        try {
            var all = new List<TrackInfo>();
            foreach(var g in genres) {
                using(var wc = new WebClient()) {
                    wc.Encoding = Encoding.UTF8;
                    var json = wc.DownloadString("http://127.0.0.1:8080/api/itunes/search?term="
                        + Uri.EscapeDataString(g) + "&entity=song&limit=3&country=cn");
                    foreach(var t in ParseTracks(json)) {
                        bool dup = false;
                        foreach(var e in all) { if(e.name == t.name && e.artist == t.artist) { dup=true; break; } }
                        if(!dup) all.Add(t);
                    }
                }
            }
            recs = all.GetRange(0, Math.Min(4, all.Count));
        } catch { recs = new List<TrackInfo> { new TrackInfo { name = "服务器离线", artist = "" } }; }
    }

    private void OpenInMusicApp(TrackInfo track)
    {
        if(string.IsNullOrEmpty(track.name) || track.name.Contains("离线")) return;
        var query = Uri.EscapeDataString(track.name + " " + track.artist);
        string url;
        var key = activity != null ? activity.musicKey : null;

        if(key != null && key.Contains("cloudmusic"))
            url = "https://music.163.com/#/search/m/?s=" + query;
        else if(key != null && key.Contains("qqmusic"))
            url = "https://y.qq.com/portal/search.html#page=1&searchid=1&t=song&w=" + query;
        else if(key != null && key.Contains("kugou"))
            url = "https://www.kugou.com/yy/html/search.html#searchType=song&searchKeyWord=" + query;
        else if(key != null && key.Contains("spotify"))
            url = "https://open.spotify.com/search/" + query;
        else
            url = "https://music.163.com/#/search/m/?s=" + query;

        try {
            System.Diagnostics.Process.Start("explorer.exe", "\"" + url + "\"");
        } catch {}
    }

    private string[] GetGenres()
    {
        var g = new List<string>();
        if(activity != null && activity.game != null && activity.game.style != null)
            g.AddRange(activity.game.style.Split(' '));
        var h = DateTime.Now.Hour;
        if(h<6||h>=22){g.Add("Lo-fi");g.Add("Ambient");}
        else if(h<10){g.Add("Acoustic");g.Add("Classical");}
        else{g.Add("Electronic");g.Add("Pop");}
        if(g.Count==0)g.Add("Lo-fi");
        return g.ToArray();
    }

    // =========== API & PARSING ===========

    private void PollActivity()
    {
        try {
            using(var wc = new WebClient()) {
                wc.Encoding = Encoding.UTF8;
                var json = wc.DownloadString("http://127.0.0.1:8080/api/activity");
                activity = ParseActivity(json);
                this.BeginInvoke((Action)(() => {
                    string tip = "♪ Music Pet";
                    if(activity.game != null) tip = "🎮 " + activity.game.name;
                    if(activity.music != null) tip += " | 🎵 " + activity.music;
                    tooltip.SetToolTip(this, tip);
                    this.Invalidate();
                }));
            }
        } catch {}
    }

    private ActivityData ParseActivity(string json)
    {
        var r = new ActivityData();
        try {
            r.game = ExtractGame(json);
            r.music = ExtractStr(json, "\"music\":\"");
            r.musicKey = ExtractStr(json, "\"musicKey\":\"");
            r.timeOfDay = ExtractStr(json, "\"timeOfDay\":\"");
        } catch {}
        return r;
    }

    private GameData ExtractGame(string json)
    {
        var n = ExtractStr(json, "\"name\":\"");
        if(string.IsNullOrEmpty(n)) return null;
        return new GameData {
            name = n,
            genre = ExtractStr(json, "\"genre\":\""),
            style = ExtractStr(json, "\"style\":\"")
        };
    }

    private string ExtractStr(string json, string key)
    {
        int si = json.IndexOf(key);
        if(si < 0) return null;
        si += key.Length;
        int ei = json.IndexOf("\"", si);
        if(ei < 0) return null;
        return json.Substring(si, ei - si);
    }

    private List<TrackInfo> ParseTracks(string json)
    {
        var list = new List<TrackInfo>();
        int pos = 0;
        while(true) {
            var name = ExtractBetween(json, "\"trackName\":\"", "\"", ref pos);
            var artist = ExtractBetween(json, "\"artistName\":\"", "\"", ref pos);
            if(name == null) break;
            if(!string.IsNullOrEmpty(name))
                list.Add(new TrackInfo { name = name, artist = artist ?? "" });
        }
        return list;
    }

    private string ExtractBetween(string json, string key, string end, ref int pos)
    {
        int si = json.IndexOf(key, pos);
        if(si < 0) return null;
        si += key.Length;
        int ei = json.IndexOf(end, si);
        if(ei < 0) return null;
        pos = ei + 1;
        return json.Substring(si, ei - si);
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MusicPet());
    }
}

public class ActivityData
{
    public GameData game;
    public string music;
    public string musicKey;
    public string timeOfDay;
}

public class TrackInfo
{
    public string name;
    public string artist;
    public override string ToString() { return string.IsNullOrEmpty(artist) ? name : name + " - " + artist; }
}

public class GameData
{
    public string name;
    public string genre;
    public string style;
}
