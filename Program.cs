using HidSharp;
using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

static class Diag { public static string Seen = "Buscando...", Err = ""; }

class St
{
    public byte LX = 128, LY = 128, RX = 128, RY = 128, L2, R2, Small, Large, TL, TS;
    public int Dpad = 8, Batt; public bool Cable, Active;
    public bool Cross, Circle, Square, Triangle, L1, R1, Share, Options, L3, R3, PS, Touch;
    public string Conn = "";
}

class Pad
{
    public St S = new(); public string Path; public bool Done;
    HidDevice dev; public volatile bool Stop; static ViGEmClient vigem;
    public static void Init() => vigem = new ViGEmClient();
    public Pad(HidDevice d) { dev = d; Path = d.DevicePath; new Thread(Run) { IsBackground = true }.Start(); }

    static uint Crc(byte[] b, int n)
    {
        uint c = 0xFFFFFFFF; c = Step(c, 0xA2);
        for (int i = 0; i < n; i++) c = Step(c, b[i]);
        return ~c;
    }
    static uint Step(uint c, byte v) { c ^= v; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; return c; }

    void Out(HidStream st, bool bt, byte l, byte s)
    {
        var b = new byte[Math.Max(dev.GetMaxOutputReportLength(), bt ? 78 : 32)];
        if (!bt) { b[0] = 5; b[1] = 0xFF; b[4] = s; b[5] = l; b[8] = 64; }
        else { b[0] = 0x11; b[1] = 0xC0; b[3] = 7; b[6] = s; b[7] = l; b[10] = 64; BitConverter.GetBytes(Crc(b, 74)).CopyTo(b, 74); }
        st.Write(b);
    }

    void Parse(byte[] d, int o)
    {
        S.LX = d[o]; S.LY = d[o + 1]; S.RX = d[o + 2]; S.RY = d[o + 3];
        byte a = d[o + 4], b = d[o + 5], c = d[o + 6];
        S.Dpad = a & 15; S.Square = (a & 16) != 0; S.Cross = (a & 32) != 0; S.Circle = (a & 64) != 0; S.Triangle = (a & 128) != 0;
        S.L1 = (b & 1) != 0; S.R1 = (b & 2) != 0; S.Share = (b & 16) != 0; S.Options = (b & 32) != 0; S.L3 = (b & 64) != 0; S.R3 = (b & 128) != 0;
        S.PS = (c & 1) != 0; S.Touch = (c & 2) != 0; S.L2 = d[o + 7]; S.R2 = d[o + 8];
        if (d.Length > o + 29) { S.Cable = (d[o + 29] & 0x10) != 0; S.Batt = Math.Min((d[o + 29] & 0x0F) * 10, 100); }
    }

    static short Ax(byte v, bool inv) => (short)Math.Clamp((inv ? 128 - v : v - 128) * 258, -32768, 32767);

    void Map(IXbox360Controller x)
    {
        x.SetButtonState(Xbox360Button.A, S.Cross); x.SetButtonState(Xbox360Button.B, S.Circle);
        x.SetButtonState(Xbox360Button.X, S.Square); x.SetButtonState(Xbox360Button.Y, S.Triangle);
        x.SetButtonState(Xbox360Button.LeftShoulder, S.L1); x.SetButtonState(Xbox360Button.RightShoulder, S.R1);
        x.SetButtonState(Xbox360Button.Back, S.Share || S.Touch); x.SetButtonState(Xbox360Button.Start, S.Options);
        x.SetButtonState(Xbox360Button.LeftThumb, S.L3); x.SetButtonState(Xbox360Button.RightThumb, S.R3);
        x.SetButtonState(Xbox360Button.Guide, S.PS);
        int d = S.Dpad;
        x.SetButtonState(Xbox360Button.Up, d == 7 || d == 0 || d == 1); x.SetButtonState(Xbox360Button.Right, d >= 1 && d <= 3);
        x.SetButtonState(Xbox360Button.Down, d >= 3 && d <= 5); x.SetButtonState(Xbox360Button.Left, d >= 5 && d <= 7);
        x.SetAxisValue(Xbox360Axis.LeftThumbX, Ax(S.LX, false)); x.SetAxisValue(Xbox360Axis.LeftThumbY, Ax(S.LY, true));
        x.SetAxisValue(Xbox360Axis.RightThumbX, Ax(S.RX, false)); x.SetAxisValue(Xbox360Axis.RightThumbY, Ax(S.RY, true));
        x.SetSliderValue(Xbox360Slider.LeftTrigger, S.L2); x.SetSliderValue(Xbox360Slider.RightTrigger, S.R2);
    }

    void Run()
    {
        IXbox360Controller x = null;
        try
        {
            using var st = dev.Open(); st.ReadTimeout = Timeout.Infinite;
            bool bt = dev.GetMaxInputReportLength() > 64; S.Conn = bt ? "Bluetooth" : "USB";
            if (bt) try { var f = new byte[38]; f[0] = 2; st.GetFeature(f); } catch { } // activa reporte completo 0x11
            x = vigem.CreateXbox360Controller();
            x.FeedbackReceived += (s, e) => { S.Large = e.LargeMotor; S.Small = e.SmallMotor; };
            x.Connect(); S.Active = true;
            var buf = new byte[dev.GetMaxInputReportLength()]; int lastL = -1, lastS = -1;
            Out(st, bt, 0, 0);
            while (!Stop)
            {
                int n = st.Read(buf);
                int o = buf[0] == 1 ? 1 : buf[0] == 0x11 ? 3 : -1;
                if (o < 0 || n < 10) continue;
                Parse(buf, o); Map(x);
                byte l = Math.Max(S.Large, S.TL), s2 = Math.Max(S.Small, S.TS);
                if (l != lastL || s2 != lastS) { Out(st, bt, l, s2); lastL = l; lastS = s2; }
            }
        }
        catch (Exception ex) { Diag.Err = ex.GetType().Name + ": " + ex.Message; }
        finally { S.Active = false; try { x?.Disconnect(); } catch { } Done = true; }
    }
}

static class Th
{
    public static bool Dark = true;
    public static Color Bg => Dark ? Color.FromArgb(10, 15, 24) : Color.FromArgb(243, 245, 249);
    public static Color Line => Dark ? Color.FromArgb(185, 193, 212) : Color.FromArgb(70, 80, 100);
    public static Color Dim => Dark ? Color.FromArgb(60, 70, 90) : Color.FromArgb(200, 206, 218);
    public static Color Text => Dark ? Color.FromArgb(230, 235, 245) : Color.FromArgb(25, 30, 45);
    public static Color Lit => Dark ? Color.White : Color.FromArgb(20, 110, 255);
}

class View : Panel
{
    public Func<Pad> Get;
    static readonly Bitmap Img = new Bitmap(typeof(View).Assembly.GetManifestResourceStream("controller.png"));
    static readonly Dictionary<string, Bitmap> Masks = new();
    public View() { DoubleBuffered = true; ResizeRedraw = true; }

    static GraphicsPath RR(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90); p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    static Bitmap Mask(string n)
    {
        if (!Masks.TryGetValue(n, out var b)) Masks[n] = b = new Bitmap(typeof(View).Assembly.GetManifestResourceStream(n + ".png"));
        return b;
    }
    // pinta una zona del mando (con la forma exacta del dibujo) del color de resaltado
    static void Lit(Graphics g, string n, bool on, float alpha = 1f, float dx = 0, float dy = 0)
    {
        if (!on) return;
        var c = Th.Lit; using var ia = new ImageAttributes();
        ia.SetColorMatrix(new ColorMatrix(new[] { new[] { 0f, 0, 0, 0, 0 }, new[] { 0f, 0, 0, 0, 0 }, new[] { 0f, 0, 0, 0, 0 }, new[] { 0f, 0, 0, alpha, 0 }, new[] { c.R / 255f, c.G / 255f, c.B / 255f, 0, 1 } }));
        g.DrawImage(Mask(n), new[] { new PointF(dx, dy), new PointF(dx + 590, dy), new PointF(dx, dy + 423) }, new RectangleF(0, 0, 590, 423), GraphicsUnit.Pixel, ia);
    }
    static void Stick(Graphics g, string n, byte vx, byte vy, bool pr)
    {
        float dx = (vx - 128) / 128f * 12, dy = (vy - 128) / 128f * 12;
        bool mv = Math.Abs(vx - 128) > 10 || Math.Abs(vy - 128) > 10;
        Lit(g, n, pr || mv, pr ? 1f : 0.55f, dx, dy);
    }
    // L2/R2: se rellena de abajo hacia arriba según cuánto lo aprietas
    static void Pull(Graphics g, string n, byte v)
    {
        if (v == 0) return;
        float h = 27f * v / 255; var sv = g.Save();
        g.SetClip(new RectangleF(0, 49 - h, 590, h + 1), CombineMode.Intersect); Lit(g, n, true); g.Restore(sv);
    }
    static void Trig(Graphics g, float x, float y, byte v, string t, bool left)
    {
        using (var p = RR(x, y, 100, 18, 9))
        {
            g.SetClip(p); using (var b = new SolidBrush(Th.Lit)) g.FillRectangle(b, x, y, 100f * v / 255, 18); g.ResetClip();
            using var pen = new Pen(v > 0 ? Th.Lit : Th.Line, 2.2f); g.DrawPath(pen, p);
        }
        using var f = new Font("Segoe UI", 9); using var tb = new SolidBrush(v > 0 ? Th.Lit : Th.Text);
        g.DrawString(t + " " + v, f, tb, left ? x - 64 : x + 108, y + 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; g.Clear(Th.Bg);
        var p = Get(); var s = p != null && p.S.Active ? p.S : null; var st = s ?? new St();
        using (var tf = new Font("Segoe UI", 11)) using (var tb = new SolidBrush(Th.Text)) using (var ok = new SolidBrush(s == null ? Color.Gray : Color.LimeGreen))
        {
            g.FillEllipse(ok, 16, 16, 12, 12);
            g.DrawString(s == null ? "Sin mando conectado" : $"Conexión: {s.Conn}     |     Batería: {s.Batt}%{(s.Cable ? " (cargando)" : "")}     |     Vibración: grande {Math.Max(s.Large, s.TL)} / pequeña {Math.Max(s.Small, s.TS)}", tf, tb, 38, 11);
        }
        using (var ln = new Pen(Th.Dim)) g.DrawLine(ln, 0, 44, Width, 44);

        float sc = Math.Max(0.1f, Math.Min(Width / 620f, (Height - 120) / 410f));
        g.TranslateTransform((Width - 590 * sc) / 2, 56 + ((Height - 120) - 400 * sc) / 2); g.ScaleTransform(sc, sc);

        // rellenos (debajo del dibujo, para que las líneas queden encima)
        Lit(g, "touch", st.Touch, 0.65f); Lit(g, "share", st.Share); Lit(g, "options", st.Options);
        Lit(g, "l1", st.L1); Lit(g, "r1", st.R1); Pull(g, "l2", st.L2); Pull(g, "r2", st.R2);
        int d = st.Dpad;
        Lit(g, "up", d == 7 || d == 0 || d == 1); Lit(g, "down", d >= 3 && d <= 5); Lit(g, "left", d >= 5 && d <= 7); Lit(g, "right", d >= 1 && d <= 3);
        Lit(g, "triangle", st.Triangle); Lit(g, "cross", st.Cross); Lit(g, "square", st.Square); Lit(g, "circle", st.Circle);
        Lit(g, "ps", st.PS);
        Stick(g, "l3", st.LX, st.LY, st.L3); Stick(g, "r3", st.RX, st.RY, st.R3);

        using (var ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix(new[] { new[] { 0f, 0, 0, 0, 0 }, new[] { 0f, 0, 0, 0, 0 }, new[] { 0f, 0, 0, 0, 0 }, new[] { 0f, 0, 0, 1, 0 },
                new[] { Th.Line.R / 255f, Th.Line.G / 255f, Th.Line.B / 255f, 0, 1 } }));
            g.DrawImage(Img, new Rectangle(0, 0, 590, 423), 0, 0, 590, 423, GraphicsUnit.Pixel, ia);
        }
        using (var f = new Font("Segoe UI", 9))
        {
            using (var tb = new SolidBrush(st.L2 > 0 ? Th.Lit : Th.Text)) g.DrawString("L2 " + st.L2, f, tb, 99, 2);
            using (var tb = new SolidBrush(st.R2 > 0 ? Th.Lit : Th.Text)) g.DrawString("R2 " + st.R2, f, tb, 429, 2);
        }
        using (var f = new Font("Segoe UI", 9)) using (var tb = new SolidBrush(Th.Text))
        {
            g.DrawString($"X {st.LX}   Y {st.LY}", f, tb, 167, 292); g.DrawString($"X {st.RX}   Y {st.RY}", f, tb, 339, 292);
        }
        if (s == null)
        {
            g.ResetTransform();
            using var f2 = new Font("Segoe UI", 9.5f); using var db = new SolidBrush(Th.Text);
            g.DrawString("Conecta un mando DualShock 4 (USB o Bluetooth)\n" + Diag.Seen + (Diag.Err == "" ? "" : "\nÚltimo error: " + Diag.Err), f2, db, new RectangleF(20, Height - 62, Width - 40, 58));
        }
    }
}

class MainForm : Form
{
    List<Pad> pads = new(); NotifyIcon tray; bool exiting; View view; Panel foot; CheckBox auto; Button test, bLight, bDark;
    static readonly int[] Pids = { 0x05C4, 0x09CC, 0x0BA0 };
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    public MainForm(bool hidden)
    {
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\DS4Perm")) Th.Dark = (int)(k?.GetValue("Dark") ?? 1) == 1;
        Text = "DS4Perm - PS4 → Xbox 360"; ClientSize = new Size(900, 600); MinimumSize = new Size(760, 560); Font = new Font("Segoe UI", 10);
        view = new View { Dock = DockStyle.Fill, Get = () => pads.FirstOrDefault(p => p.S.Active) };
        foot = new Panel { Dock = DockStyle.Bottom, Height = 64 };
        auto = new CheckBox { Text = "Iniciar con Windows (minimizado)", Left = 16, Top = 20, Width = 290 };
        using (var k = Registry.CurrentUser.OpenSubKey(Key)) auto.Checked = k?.GetValue("DS4Perm") != null;
        auto.CheckedChanged += (s, e) =>
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, true);
            if (auto.Checked) k.SetValue("DS4Perm", $"\"{Application.ExecutablePath}\" --tray"); else k.DeleteValue("DS4Perm", false);
        };
        test = new Button { Text = "Probar vibración", Left = 320, Top = 14, Width = 150, Height = 36 };
        test.Click += async (s, e) =>
        {
            var p = pads.FirstOrDefault(q => q.S.Active); if (p == null) return;
            p.S.TL = 255; await Task.Delay(500); p.S.TL = 0; p.S.TS = 255; await Task.Delay(500); p.S.TS = 0;
        };
        bLight = new Button { Text = "☀  Modo claro", Left = 900 - 330, Top = 14, Width = 150, Height = 36, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        bDark = new Button { Text = "☾  Modo oscuro", Left = 900 - 170, Top = 14, Width = 150, Height = 36, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        bLight.Click += (s, e) => { Th.Dark = false; ApplyTheme(); }; bDark.Click += (s, e) => { Th.Dark = true; ApplyTheme(); };
        foot.Controls.AddRange(new Control[] { auto, test, bLight, bDark });
        Controls.Add(view); Controls.Add(foot); ApplyTheme();

        Icon = Ico(32); tray = new NotifyIcon { Icon = Ico(16), Text = "DS4Perm", Visible = true };
        var m = new ContextMenuStrip(); m.Items.Add("Abrir", null, (s, e) => ShowMe()); m.Items.Add("Salir", null, (s, e) => { exiting = true; Close(); });
        tray.ContextMenuStrip = m; tray.DoubleClick += (s, e) => ShowMe();
        var scan = new System.Windows.Forms.Timer { Interval = 1000 }; scan.Tick += (s, e) => Scan(); scan.Start();
        var draw = new System.Windows.Forms.Timer { Interval = 16 }; draw.Tick += (s, e) => { if (Visible) view.Invalidate(); }; draw.Start();
        if (hidden) { ShowInTaskbar = false; WindowState = FormWindowState.Minimized; Load += (s, e) => Hide(); }
    }
    static Icon Ico(int n) => new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("app.ico"), n, n);
    void ApplyTheme()
    {
        BackColor = Th.Bg; foot.BackColor = Th.Bg; auto.ForeColor = Th.Text;
        foreach (var b in new[] { test, bLight, bDark })
        {
            b.FlatStyle = FlatStyle.Flat; b.ForeColor = Th.Text; b.BackColor = Th.Bg; b.FlatAppearance.BorderColor = Th.Dim; b.FlatAppearance.BorderSize = 1;
        }
        var sel = Th.Dark ? bDark : bLight; sel.FlatAppearance.BorderColor = Color.FromArgb(20, 110, 255); sel.FlatAppearance.BorderSize = 2;
        if (IsHandleCreated) { int v = Th.Dark ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref v, 4); }
        view.Invalidate();
        using var k = Registry.CurrentUser.CreateSubKey(@"Software\DS4Perm"); k.SetValue("Dark", Th.Dark ? 1 : 0);
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(); }
    void ShowMe() { ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
    void Scan()
    {
        pads.RemoveAll(p => p.Done);
        var all = DeviceList.Local.GetHidDevices(0x054C).ToList();
        Diag.Seen = all.Count == 0 ? "Windows no ve ningún dispositivo HID de Sony." : "HID de Sony vistos (PID): " + string.Join(", ", all.Select(d => "0x" + d.ProductID.ToString("X4")));
        foreach (var d in all)
            if (Pids.Contains(d.ProductID) && !pads.Any(p => p.Path == d.DevicePath)) pads.Add(new Pad(d));
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
        foreach (var p in pads) p.Stop = true; tray.Visible = false; base.OnFormClosing(e);
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mx = new Mutex(true, "DS4Perm_single", out bool first); if (!first) return;
        try { Pad.Init(); }
        catch
        {
            MessageBox.Show("Falta el driver ViGEmBus (se instala una sola vez y queda permanente).\nDescárgalo de:\nhttps://github.com/nefarius/ViGEmBus/releases\nInstálalo, reinicia y abre DS4Perm de nuevo.", "DS4Perm");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Contains("--tray")));
    }
}
