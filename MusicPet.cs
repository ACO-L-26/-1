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
    const int PET_W = 100, PET_H = 120;
    const int CHAT_W = 210, CHAT_H = 180;
    const int FULL_W = 220, FULL_H = 310;

    private bool dragging = false, wasDragged = false;
    private Point dragStart;
    private System.Timers.Timer pollTimer;
    private System.Windows.Forms.Timer animTimer;
    private ToolTip tooltip;

    // State
    private ActivityData activity;
    private string[] recs;
    private int chatPhase = 0; // 0=none, 1=dialogue, 2=recommendations
    private string[] chatOptions;
    private Rectangle[] optionRects;
    private string dialogueMsg = "";
    private string recTitle = "";
    private float bobOffset = 0, bobDir = 1;
    private int noteFrame = 0;

    // Colors
    readonly Brush pinkBrush = new SolidBrush(Color.FromArgb(255, 107, 157));
    readonly Brush darkBrush = new SolidBrush(Color.FromArgb(26, 26, 46));
    readonly Brush whiteBrush = new SolidBrush(Color.White);
    readonly Brush blushBrush = new SolidBrush(Color.FromArgb(255, 157, 188));
    readonly Brush bubbleBrush = new SolidBrush(Color.FromArgb(30, 30, 55));
    readonly Brush dimBrush = new SolidBrush(Color.FromArgb(136, 136, 168));
    readonly Brush accentBrush = new SolidBrush(Color.FromArgb(255, 107, 157));
    readonly Brush optHoverBrush = new SolidBrush(Color.FromArgb(50, 50, 80));

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
            // Just the character, centered
            cy = PET_H / 2 - 13;
        } else {
            // Character at bottom of expanded form
            cy = FULL_H - 80;
            DrawBubble(g);
        }
        DrawCharacter(g, PET_W/2, cy);
    }

    private void DrawCharacter(Graphics g, int cx, int cy)
    {
        cy += (int)bobOffset;
        bool gaming = activity != null && activity.game != null;
        bool listening = activity != null && activity.music != null;

        // Body
        g.FillRectangle(pinkBrush, cx - 14, cy + 30, 28, 14);

        // Face
        int fx = cx - 20, fy = cy - 4;
        g.FillRectangle(pinkBrush, fx, fy, 40, 36);

        // Ears
        g.FillRectangle(pinkBrush, fx - 8, fy - 2, 10, 10);
        g.FillRectangle(pinkBrush, fx + 38, fy - 2, 10, 10);

        // Blush
        g.FillEllipse(blushBrush, fx - 4, fy + 14, 8, 6);
        g.FillEllipse(blushBrush, fx + 36, fy + 14, 8, 6);

        // Eyes
        g.FillEllipse(darkBrush, fx + 6, fy + 8, 6, 8);
        g.FillEllipse(darkBrush, fx + 28, fy + 8, 6, 8);
        g.FillEllipse(whiteBrush, fx + 8, fy + 10, 2, 2);
        g.FillEllipse(whiteBrush, fx + 30, fy + 10, 2, 2);

        // Mouth
        if(gaming) g.FillRectangle(darkBrush, fx + 15, fy + 24, 10, 4);
        else g.FillRectangle(darkBrush, fx + 16, fy + 23, 8, 3);

        // Headphones
        if(gaming) {
            var hpPen = new Pen(Color.FromArgb(85, 85, 85), 2);
            g.DrawEllipse(hpPen, fx - 8, fy - 6, 14, 14);
            g.DrawEllipse(hpPen, fx + 34, fy - 6, 14, 14);
            g.DrawLine(hpPen, fx - 2, fy, fx + 40, fy);
        }

        // Notes
        if(listening) {
            var nf = new Font("Arial", 12, FontStyle.Bold);
            var nb = new SolidBrush(Color.FromArgb(196, 77, 255));
            g.DrawString("♪", nf, nb, fx + 16, fy - 15 - (noteFrame*3%30));
            g.DrawString("♫", nf, nb, fx + 30, fy - 25 - ((noteFrame*3+15)%30));
        }

        // Blink
        if(!gaming && !listening && noteFrame % 60 > 56) {
            g.FillRectangle(pinkBrush, fx + 6, fy + 10, 6, 2);
            g.FillRectangle(pinkBrush, fx + 28, fy + 10, 6, 2);
        }
    }

    private void DrawBubble(Graphics g)
    {
        int bx = 10, by = 5, bw = FULL_W - 20, bh;
        string text;

        if(chatPhase == 1) {
            bh = 60 + chatOptions.Length * 32;
            text = dialogueMsg;
        } else {
            bh = 40 + (recs != null ? recs.Length * 22 : 20);
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
            // Draw recommendation items
            if(recs != null) {
                var smallFont = new Font("Microsoft YaHei", 8, FontStyle.Regular);
                for(int i=0; i<recs.Length; i++) {
                    g.DrawString(recs[i], smallFont, whiteBrush, bx + 12, by + 38 + i * 22);
                }
            }
        }
    }

    private int hoveredOption = -1;

    private void CheckOptionHover(Point mouse)
    {
        hoveredOption = -1;
        if(optionRects == null) return;
        for(int i=0; i<optionRects.Length; i++) {
            if(optionRects[i].Contains(mouse)) { hoveredOption = i; return; }
        }
    }

    // Handle option click via MouseUp
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if(wasDragged || chatPhase != 1 || optionRects == null) return;
        for(int i=0; i<optionRects.Length; i++) {
            if(optionRects[i].Contains(e.Location)) {
                string choice = chatOptions[i];
                ShowRecs(choice);
                return;
            }
        }
        // Click outside options = dismiss
        if(e.Y < optionRects[0].Top - 20) HideChat();
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
        hoveredOption = -1;
        SwitchToPetMode();
    }

    private void SwitchToChatMode()
    {
        this.SuspendLayout();
        // Keep same bottom position, expand upward
        int newTop = this.Top - (FULL_H - PET_H);
        this.MinimumSize = new Size(0,0);
        this.Size = new Size(FULL_W, FULL_H);
        this.Location = new Point(this.Left - (FULL_W - PET_W) / 2, newTop);
        this.ResumeLayout();
        this.Invalidate();
    }

    private void SwitchToPetMode()
    {
        this.SuspendLayout();
        int oldBottom = this.Bottom;
        this.Size = new Size(PET_W, PET_H);
        this.Location = new Point(
            this.Left + (FULL_W - PET_W) / 2,
            this.Top + (FULL_H - PET_H)
        );
        this.ResumeLayout();
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
            var all = new List<string>();
            foreach(var g in genres) {
                using(var wc = new WebClient()) {
                    wc.Encoding = Encoding.UTF8;
                    var json = wc.DownloadString("http://127.0.0.1:8080/api/itunes/search?term="
                        + Uri.EscapeDataString(g) + "&entity=song&limit=3&country=cn");
                    foreach(var t in ParseTracks(json))
                        if(!all.Contains(t)) all.Add(t);
                }
            }
            recs = all.GetRange(0, Math.Min(4, all.Count)).ToArray();
        } catch { recs = new[]{"服务器离线"}; }
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

    private string[] ParseTracks(string json)
    {
        var list = new List<string>();
        int pos = 0;
        while(true) {
            var name = ExtractBetween(json, "\"trackName\":\"", "\"", ref pos);
            var artist = ExtractBetween(json, "\"artistName\":\"", "\"", ref pos);
            if(name == null) break;
            if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(artist))
                list.Add(name + " - " + artist);
        }
        return list.ToArray();
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
    public string timeOfDay;
}

public class GameData
{
    public string name;
    public string genre;
    public string style;
}
