using System;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

public class MusicPet : Form
{
    private WebBrowser browser;
    private bool dragging = false;
    private Point dragStart;

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;

    static void SetIE11Mode()
    {
        try {
            var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
            key.SetValue("MusicPet.exe", 11001, RegistryValueKind.DWord);
            key.Close();
        } catch {}
    }

    public MusicPet()
    {
        SetIE11Mode();
        this.Text = "Music Pet";
        this.Width = 300;
        this.Height = 500;
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.ShowInTaskbar = true;
        this.StartPosition = FormStartPosition.Manual;

        // Position at bottom-right of primary screen
        var screen = Screen.PrimaryScreen.WorkingArea;
        this.Location = new Point(screen.Width - this.Width - 20, screen.Height - this.Height - 40);

        // WebBrowser control
        browser = new WebBrowser();
        browser.Width = this.Width;
        browser.Height = this.Height;
        browser.ScrollBarsEnabled = false;
        browser.IsWebBrowserContextMenuEnabled = false;
        browser.ScriptErrorsSuppressed = true;
        browser.AllowWebBrowserDrop = false;
        browser.Url = new Uri("http://127.0.0.1:8080/pet-desktop.html");
        this.Controls.Add(browser);

        // Make the window draggable
        this.MouseDown += (s, e) => { dragging = true; dragStart = e.Location; };
        this.MouseMove += (s, e) => { if(dragging) this.Location = new Point(
            this.Location.X + e.X - dragStart.X,
            this.Location.Y + e.Y - dragStart.Y
        ); };
        this.MouseUp += (s, e) => { dragging = false; };

        // Keep always on top
        this.Load += (s, e) => {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        };
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MusicPet());
    }
}
