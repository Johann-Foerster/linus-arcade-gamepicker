using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Arcade {
    class GameExit {
        const double HoldSeconds = 2;
        const double CloseGraceSeconds = 3;
        double? heldSince;
        double? forceCloseAt;
        bool closeRequested;

        internal void Update(Process game, bool buttonsHeld, double now) {
            if (!buttonsHeld) {
                heldSince = null;
                closeRequested = false;
            }
            if (forceCloseAt.HasValue) {
                if (now >= forceCloseAt.Value) {
                    forceCloseAt = null;
                    game.Kill();
                }
                return;
            }
            if (!buttonsHeld || closeRequested) return;
            if (!heldSince.HasValue) heldSince = now;
            if (now - heldSince.Value < HoldSeconds) return;

            closeRequested = true;
            if (game.CloseMainWindow())
                forceCloseAt = now + CloseGraceSeconds;
            else
                game.Kill();
        }
    }

    class ShutdownHotkey {
        const double HoldSeconds = 3;
        double? heldSince;
        bool releaseRequired;

        internal bool Update(bool gameOpen, bool buttonsHeld, double now) {
            if (gameOpen || !buttonsHeld) {
                heldSince = null;
                releaseRequired = buttonsHeld;
                return false;
            }
            if (releaseRequired) return false;
            if (!heldSince.HasValue) heldSince = now;
            if (now - heldSince.Value < HoldSeconds) return false;

            releaseRequired = true;
            return true;
        }
    }

    class Picker : Form {
        readonly Maze maze=new Maze();
        readonly MazeSounds sounds=new MazeSounds();
        Library library=new Library();
        readonly Image[] covers=new Image[4];
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer { Interval=16 };
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly ShutdownHotkey shutdownHotkey = new ShutdownHotkey();
        double last, flashUntil;
        int failedGate=-1;
        int keyDirection;
        Process game;
        GameExit gameExit;
        bool exiting;
        static readonly Color Ink=Color.FromArgb(10,23,27), Paper=Color.FromArgb(245,241,219),
            Muted=Color.FromArgb(159,188,180);
        static readonly Color[] GateAccents={Color.FromArgb(106,226,198),Color.FromArgb(251,165,116),Color.FromArgb(184,169,248),Color.FromArgb(240,208,91)};
        static readonly Color[] GhostColors={GateAccents[0],GateAccents[1],GateAccents[2],Color.FromArgb(244,109,159),Color.FromArgb(110,174,255)};
        const int CellWidth=54, CellHeight=72, LeftEdge=(1920-Maze.Width*CellWidth)/2;

        [STAThread] static void Main() {
            bool owner;
            using(var mutex=new Mutex(true,"Local\\LinusArcadePicker",out owner)) {
                if(!owner) return;
                Native.SetProcessDPIAware(); Application.EnableVisualStyles();
                Application.Run(new Picker());
            }
        }
        public Picker() {
            Text="Linus Arcade"; BackColor=Ink; DoubleBuffered=true; FormBorderStyle=FormBorderStyle.None;
            StartPosition=FormStartPosition.Manual; Bounds=Screen.PrimaryScreen.Bounds;
            TopMost=true; ShowInTaskbar=false; KeyPreview=true;
            try { library=Library.Read(); } catch(Exception ex) { LogError(ex); }
            for(int i=0;i<4;i++) {
                try { using(var im=Image.FromFile(library.Exits[i].Image)) covers[i]=new Bitmap(im); }
                catch(Exception) { covers[i]=null; }
            }
            Shown+=(s,e)=>{ Cursor.Hide(); Native.SetThreadExecutionState(0x80000003); last=Now; timer.Start(); };
            timer.Tick+=(s,e)=>Frame();
            KeyUp+=(s,e)=>{ if(Direction(e.KeyCode)==keyDirection) keyDirection=0; };
            Deactivate+=(s,e)=>keyDirection=0;
            Disposed+=(s,e)=>sounds.Dispose();
            FormClosed+=(s,e)=>{
                timer.Dispose(); Cursor.Show(); Native.SetThreadExecutionState(0x80000000);
                foreach(var im in covers) if(im!=null) im.Dispose();
                if(game!=null) game.Dispose();
                if(exiting && Native.GetShellWindow()==IntPtr.Zero)
                    Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"));
            };
        }
        double Now { get { return clock.Elapsed.TotalSeconds; } }
        static void LogError(Exception ex) {
            try {
                string folder=Path.GetDirectoryName(Library.FileName);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"last-error.txt"),DateTime.Now.ToString("s")+Environment.NewLine+ex.Message);
            } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        static int Direction(Keys k) {
            switch(k) { case Keys.Up:return 1; case Keys.Right:return 2; case Keys.Down:return 3; case Keys.Left:return 4; default:return 0; }
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys key) {
            if(key==(Keys.Alt|Keys.F4) && game==null) { if ((msg.LParam.ToInt64() & (1L<<30))==0) { exiting=true; Close(); } return true; }
            int d=Direction(key); if(d!=0) { keyDirection=d; return true; }
            return base.ProcessCmdKey(ref msg,key);
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if(e.CloseReason==CloseReason.UserClosing && !exiting) e.Cancel=true;
            base.OnFormClosing(e);
        }
        static void RequestShutdown() {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/s /t 0") {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try { using (Process.Start(start)) { } }
            catch (Win32Exception ex) { LogError(ex); }
            catch (InvalidOperationException ex) { LogError(ex); }
        }

        void Frame() {
            double now=Now, dt=Math.Min(.05,now-last); last=now;
            bool buttonsHeld = Native.ExitButtonsHeld(library.GameExitButtons);
            if (shutdownHotkey.Update(game != null, buttonsHeld, now)) {
                RequestShutdown();
                return;
            }
            if(game!=null) {
                if(!game.HasExited) {
                    try { gameExit.Update(game, buttonsHeld, now); }
                    catch(Win32Exception ex) { if(!game.HasExited) LogError(ex); }
                    catch(InvalidOperationException ex) { if(!game.HasExited) LogError(ex); }
                    return;
                }
                game.Dispose(); game=null; gameExit=null; maze.Respawn(); TopMost=true; Activate(); Native.SetForegroundWindow(Handle);
                maze.RestorePellets(now);
                sounds.Play(MazeCue.Returned,now);
            } else {
                int input=Native.Stick();
                int gate=maze.Tick(dt,input==0?keyDirection:input,now);
                if(gate>=0) {
                    maze.Respawn(); keyDirection=0;
                    try {
                        TopMost=false;
                        game=Library.Start(library.Exits[gate]);
                        gameExit=new GameExit();
                    } catch(Exception ex) { TopMost=true; failedGate=gate; flashUntil=Now+1.5; LogError(ex); }
                    sounds.Play(game!=null?MazeCue.Launch:MazeCue.LaunchFailed,Now);
                } else sounds.Play(maze.LastCue,now);
            }
            Invalidate();
        }
        static void Box(Graphics g,Color c,float x,float y,float w,float h) {
            using(var b=new SolidBrush(c)) g.FillRectangle(b,x,y,w,h);
        }
        protected override void OnPaint(PaintEventArgs e) {
            PaintScene(e.Graphics,ClientSize);
        }
        internal void PaintScene(Graphics g,Size size) {
            g.Clear(Ink);
            float scale=Math.Min(size.Width/1920f,size.Height/1080f);
            g.TranslateTransform((size.Width-1920*scale)/2,(size.Height-1080*scale)/2); g.ScaleTransform(scale,scale);
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            for(int i=0;i<4;i++) Card(g,i);
            DrawMaze(g);
        }
        void Card(Graphics g,int i) {
            bool right=i%2==1;
            const int width=284,height=174,margin=24;
            int x=right?1920-margin-width:margin;
            int centerY=Maze.Gates[i].Y*CellHeight+CellHeight/2;
            var area=new Rectangle(x,centerY-height/2,width,height);
            Color accent=(i==failedGate && Now<flashUntil && (int)(Now*8)%2==0)?Color.White:GateAccents[i];
            Box(g,accent,right?1920-LeftEdge:x+width,centerY-2,LeftEdge-margin-width,4);
            if(covers[i]!=null) {
                var im=covers[i]; float factor=Math.Max((float)width/im.Width,(float)height/im.Height);
                float w=width/factor,h=height/factor;
                g.DrawImage(im,area,new RectangleF((im.Width-w)/2,(im.Height-h)/2,w,h),GraphicsUnit.Pixel);
            } else {
                Box(g,Color.FromArgb(accent.R/5,accent.G/5,accent.B/5),area.X,area.Y,width,height);
                var clip=g.Save(); g.SetClip(area);
                using(var stripe=new Pen(Color.FromArgb(26,accent),5))
                    for(int k=-3;k<12;k++) g.DrawLine(stripe,x+k*35,area.Bottom,x+k*35+100,area.Top);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.TranslateTransform(x+width/2,centerY);
                using(var pen=new Pen(accent,8)) {
                    switch(i) {
                        case 0: g.DrawPolygon(pen,new[]{new Point(0,-40),new Point(40,32),new Point(-40,32)}); break;
                        case 1: g.DrawEllipse(pen,-36,-36,72,72); break;
                        case 2: g.DrawPolygon(pen,new[]{new Point(0,-43),new Point(43,0),new Point(0,43),new Point(-43,0)}); break;
                        case 3: g.DrawLine(pen,-29,-29,29,29); g.DrawLine(pen,-29,29,29,-29); break;
                    }
                }
                g.Restore(clip);
            }
            Box(g,accent,right?x:x+width-4,area.Y,4,height);
        }
        void DrawMaze(Graphics g) {
            var wall=Color.FromArgb(30,56,60); var edge=Color.FromArgb(74,116,113);
            for(int y=0;y<Maze.Height;y++) for(int x=0;x<Maze.Width;x++) {
                int px=LeftEdge+x*CellWidth,py=y*CellHeight;
                if(!Maze.Open(x,y)) {
                    Box(g,wall,px,py,CellWidth,CellHeight);
                    if(Maze.Open(x-1,y)) Box(g,edge,px,py,2,CellHeight);
                    if(Maze.Open(x+1,y)) Box(g,edge,px+CellWidth-2,py,2,CellHeight);
                    if(Maze.Open(x,y-1)) Box(g,edge,px,py,CellWidth,2);
                    if(Maze.Open(x,y+1)) Box(g,edge,px,py+CellHeight-2,CellWidth,2);
                } else if(maze.Pellets.Contains(new Point(x,y))) Box(g,Muted,px+CellWidth/2-3,py+CellHeight/2-3,6,6);
            }
            for(int i=0;i<4;i++) {
                Point p=Maze.Gates[i]; int px=LeftEdge+p.X*CellWidth+CellWidth/2,py=p.Y*CellHeight+CellHeight/2,sign=i%2==0?-1:1;
                using(var pen=new Pen(GateAccents[i],5)) g.DrawLines(pen,new[]{new Point(px-sign*5,py-10),new Point(px+sign*6,py),new Point(px-sign*5,py+10)});
            }
            for(int i=0;i<maze.Ghosts.Length;i++) DrawGhost(g,maze.Ghosts[i],GhostColors[i],i);
            var saved=g.Save();
            g.TranslateTransform(LeftEdge+(maze.Player.X+.5f)*CellWidth,(maze.Player.Y+.5f)*CellHeight);
            HeroSprite.Draw(g,maze.Player);
            g.Restore(saved);
        }
        void DrawGhost(Graphics g,Runner a,Color c,int index) {
            var saved=g.Save();
            g.TranslateTransform(LeftEdge+(a.X+.5f)*CellWidth,(a.Y+.5f)*CellHeight);
            g.ScaleTransform(1.22f,1.22f);
            g.TranslateTransform(0,(float)Math.Sin(Now*5+index)*2);
            Box(g,c,-14,-8,28,18); Box(g,c,-9,-14,18,7);
            Box(g,c,-14,10,7,5); Box(g,c,7,10,7,5);
            Box(g,Ink,-10,-5,20,8); Box(g,Paper,-7,-3,4,4); Box(g,Paper,4,-3,4,4);
            g.Restore(saved);
        }
    }
}


