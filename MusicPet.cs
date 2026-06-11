using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net;
using System.Text;
using System.Timers;
using Microsoft.Win32;

public class MusicPet : Form
{
    // Win32 for always-on-top and click-through prevention
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;
    const int WS_EX_LAYERED = 0x80000;
    const int WS_EX_TRANSPARENT = 0x20;

    private bool dragging = false;
    private Point dragStart;
    private System.Timers.Timer pollTimer;
    private ActivityData activity;
    private string[] recs;
    private ToolTip tooltip;

    // Animation state
    private float bobOffset = 0;
    private float bobDir = 1;
    private int noteFrame = 0;
    private System.Windows.Forms.Timer animTimer;

    // Character colors
    readonly Brush pinkBrush = new SolidBrush(Color.FromArgb(255, 107, 157));
    readonly Brush darkBrush = new SolidBrush(Color.FromArgb(26, 26, 46));
    readonly Brush whiteBrush = new SolidBrush(Color.White);
    readonly Brush blushBrush = new SolidBrush(Color.FromArgb(255, 157, 188));
    readonly Pen pinkPen = new Pen(Color.FromArgb(255, 107, 157), 2);

    // Form size for the character
    const int CW = 100;
    const int CH = 120;

    public MusicPet()
    {
        // Window setup: tiny, transparent, no borders
        this.Size = new Size(CW, CH);
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.Manual;
        this.BackColor = Color.Fuchsia; // Will be transparent
        this.TransparencyKey = Color.Fuchsia;
        this.AllowTransparency = true;
        this.DoubleBuffered = true;

        // Position at bottom-right
        var screen = Screen.PrimaryScreen.WorkingArea;
        this.Location = new Point(screen.Width - CW - 10, screen.Height - CH - 10);

        // Mouse events for drag
        this.MouseDown += (s, e) => { dragging = true; dragStart = e.Location; };
        this.MouseMove += (s, e) => { if(dragging) { this.Left += e.X - dragStart.X; this.Top += e.Y - dragStart.Y; } };
        this.MouseUp += (s, e) => { dragging = false; };
        this.Click += (s, e) => ShowRecommendations();

        // Paint the character
        this.Paint += OnPaint;

        // Animation timer
        animTimer = new System.Windows.Forms.Timer();
        animTimer.Interval = 50;
        animTimer.Tick += (s, e) => {
            bobOffset += 0.15f * bobDir;
            if(Math.Abs(bobOffset) > 3) bobDir *= -1;
            noteFrame = (noteFrame + 1) % 60;
            this.Invalidate();
        };
        animTimer.Start();

        // Tooltip for activity info
        tooltip = new ToolTip();
        tooltip.SetToolTip(this, "Music Pet - 检测中...");

        // Activity polling
        pollTimer = new System.Timers.Timer(15000);
        pollTimer.Elapsed += (s, e) => PollActivity();
        pollTimer.AutoReset = true;
        pollTimer.Start();

        // Initial poll
        PollActivity();

        // Keep on top
        this.Load += (s, e) => SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }

    // Set IE11 mode for WebClient (not used for drawing, but kept for compatibility)
    static MusicPet() {
        try { var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"); k.SetValue("MusicPet.exe", 11001, RegistryValueKind.DWord); k.Close(); } catch {}
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int cx = CW / 2;
        int cy = 30 + (int)bobOffset;
        bool gaming = activity != null && activity.game != null;
        bool listening = activity != null && activity.music != null;

        // === BODY (shadow/pants) ===
        g.FillRectangle(pinkBrush, cx - 14, cy + 30, 28, 14);

        // === FACE ===
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
        // Eye shine
        g.FillEllipse(whiteBrush, fx + 8, fy + 10, 2, 2);
        g.FillEllipse(whiteBrush, fx + 30, fy + 10, 2, 2);

        // Mouth
        if(gaming) g.FillRectangle(darkBrush, fx + 15, fy + 24, 10, 4);
        else g.FillRectangle(darkBrush, fx + 16, fy + 23, 8, 3);

        // === HEADPHONES (gaming) ===
        if(gaming) {
            var hpPen = new Pen(Color.FromArgb(85, 85, 85), 2);
            g.DrawEllipse(hpPen, fx - 8, fy - 6, 14, 14);
            g.DrawEllipse(hpPen, fx + 34, fy - 6, 14, 14);
            g.DrawLine(hpPen, fx - 2, fy, fx + 40, fy);
        }

        // === FLOATING NOTES (music) ===
        if(listening) {
            var noteFont = new Font("Arial", 12, FontStyle.Bold);
            var noteBrush = new SolidBrush(Color.FromArgb(196, 77, 255));
            int n1y = fy - 15 - (noteFrame * 3 % 30);
            int n2y = fy - 25 - ((noteFrame * 3 + 15) % 30);
            g.DrawString("♪", noteFont, noteBrush, fx + 16, n1y);
            g.DrawString("♫", noteFont, noteBrush, fx + 30, n2y);
        }

        // === IDLE ANIMATION (subtle eye blink) ===
        if(!gaming && !listening) {
            // Occasional blink (every ~3 seconds)
            if(noteFrame % 60 > 56) {
                g.FillRectangle(pinkBrush, fx + 6, fy + 10, 6, 2);
                g.FillRectangle(pinkBrush, fx + 28, fy + 10, 6, 2);
            }
        }
    }

    // Click handler - show a mini popup with recommendations
    private void ShowRecommendations()
    {
        var popup = new Form();
        popup.Size = new Size(250, 220);
        popup.FormBorderStyle = FormBorderStyle.None;
        popup.TopMost = true;
        popup.ShowInTaskbar = false;
        popup.StartPosition = FormStartPosition.Manual;
        popup.Location = new Point(this.Left - 80, this.Top - 230);
        popup.BackColor = Color.FromArgb(26, 26, 46);
        popup.Paint += (s, e) => {
            var g = e.Graphics;
            g.DrawRectangle(new Pen(Color.FromArgb(255, 107, 157), 2), 1, 1, popup.Width-3, popup.Height-3);
        };

        // Content
        var label = new Label();
        label.Text = activity != null ? GetActivityText() : "♪ Music Pet";
        label.ForeColor = Color.FromArgb(255, 107, 157);
        label.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
        label.Location = new Point(10, 8);
        label.Size = new Size(230, 20);
        popup.Controls.Add(label);

        var recLabel = new Label();
        recLabel.Text = recs != null ? string.Join("\n", recs) : "加载中...";
        recLabel.ForeColor = Color.FromArgb(224, 224, 232);
        recLabel.Font = new Font("Microsoft YaHei", 8);
        recLabel.Location = new Point(10, 30);
        recLabel.Size = new Size(230, 160);
        recLabel.TextAlign = ContentAlignment.TopLeft;
        popup.Controls.Add(recLabel);

        // Close on click
        popup.Click += (s2, e2) => popup.Close();
        popup.MouseLeave += (s2, e2) => { var t = new System.Windows.Forms.Timer(); t.Interval=2000; t.Tick+=(s3,e3)=>{popup.Close();t.Stop();};t.Start(); };

        popup.Show();
    }

    private string GetActivityText()
    {
        var sb = new StringBuilder();
        if(activity.game != null) sb.AppendLine("🎮 " + activity.game.name);
        if(activity.music != null) sb.AppendLine("🎵 " + activity.music);
        sb.Append("🕐 " + (activity.timeOfDay ?? ""));
        return sb.ToString();
    }

    private void PollActivity()
    {
        try {
            using(var wc = new WebClient()) {
                wc.Encoding = Encoding.UTF8;
                var json = wc.DownloadString("http://127.0.0.1:8080/api/activity");
                // Simple JSON parsing (avoid dependency on Newtonsoft)
                activity = ParseActivity(json);
                LoadRecs();
                this.BeginInvoke((Action)(() => {
                    string tip = "♪ Music Pet";
                    if(activity.game != null) tip = "🎮 " + activity.game.name;
                    if(activity.music != null) tip += " | 🎵 " + activity.music;
                    tooltip.SetToolTip(this, tip);
                    this.Invalidate();
                }));
            }
        } catch { /* Server might be offline */ }
    }

    private void LoadRecs()
    {
        try {
            var genres = GetGenres();
            var all = new System.Collections.Generic.List<string>();
            foreach(var g in genres) {
                using(var wc = new WebClient()) {
                    wc.Encoding = Encoding.UTF8;
                    var json = wc.DownloadString("http://127.0.0.1:8080/api/itunes/search?term=" + Uri.EscapeDataString(g) + "&entity=song&limit=3&country=cn");
                    var results = ParseTracks(json);
                    foreach(var t in results) {
                        if(!all.Contains(t)) all.Add(t);
                    }
                }
            }
            recs = all.GetRange(0, Math.Min(4, all.Count)).ToArray();
        } catch { recs = new[] { "离线" }; }
    }

    private string[] GetGenres()
    {
        var g = new System.Collections.Generic.List<string>();
        if(activity != null && activity.game != null && activity.game.style != null)
            g.AddRange(activity.game.style.Split(' '));
        var h = DateTime.Now.Hour;
        if(h < 6 || h >= 22) { g.Add("Lo-fi"); g.Add("Ambient"); }
        else if(h < 10) { g.Add("Acoustic"); g.Add("Classical"); }
        else { g.Add("Electronic"); g.Add("Pop"); }
        if(g.Count == 0) g.Add("Lo-fi");
        return g.ToArray();
    }

    // Minimal JSON parsing
    private ActivityData ParseActivity(string json)
    {
        var result = new ActivityData();
        try {
            // Very simple parsing - extract known fields
            result.game = ExtractGame(json);
            result.music = ExtractString(json, "\"music\":\"", "\"");
            result.timeOfDay = ExtractString(json, "\"timeOfDay\":\"", "\"");
        } catch {}
        return result;
    }

    private GameData ExtractGame(string json)
    {
        var name = ExtractString(json, "\"name\":\"", "\"");
        var genre = ExtractString(json, "\"genre\":\"", "\"");
        var style = ExtractString(json, "\"style\":\"", "\"");
        if(string.IsNullOrEmpty(name)) return null;
        return new GameData { name = name, genre = genre, style = style };
    }

    private string ExtractString(string json, string start, string end)
    {
        int si = json.IndexOf(start);
        if(si < 0) return null;
        si += start.Length;
        int ei = json.IndexOf(end, si);
        if(ei < 0) return null;
        return json.Substring(si, ei - si);
    }

    private string[] ParseTracks(string json)
    {
        var list = new System.Collections.Generic.List<string>();
        int pos = 0;
        while(true) {
            var name = ExtractBetween(json, "\"trackName\":\"", "\"", ref pos);
            var artist = ExtractBetween(json, "\"artistName\":\"", "\"", ref pos);
            if(name == null || artist == null) break;
            if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(artist))
                list.Add(name + " - " + artist);
        }
        return list.ToArray();
    }

    private string ExtractBetween(string json, string key, string end, ref int startPos)
    {
        int si = json.IndexOf(key, startPos);
        if(si < 0) return null;
        si += key.Length;
        int ei = json.IndexOf(end, si);
        if(ei < 0) return null;
        startPos = ei + 1;
        return json.Substring(si, ei - si);
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MusicPet());
    }
}

// Simple data classes (no external dependencies)
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
