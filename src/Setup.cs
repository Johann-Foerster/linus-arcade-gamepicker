using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Arcade {
    class Setup : Form {
        readonly Library library;
        readonly TabControl tabs=new TabControl { Dock=DockStyle.Fill, Padding=new Point(16,8) };
        readonly Label status=new Label { AutoSize=true, Anchor=AnchorStyles.Left };
        readonly Label captureStatus=new Label { AutoSize=true };
        readonly Timer captureTimer=new Timer { Interval=50 };
        readonly Stopwatch captureClock=new Stopwatch();
        readonly PictureBox[] previews=new PictureBox[4];
        [STAThread] static void Main() {
            Native.SetProcessDPIAware(); Application.EnableVisualStyles();
            try { Application.Run(new Setup(Library.Read())); }
            catch(Exception ex) { MessageBox.Show(ex.Message+"\n\nLibrary: "+Library.FileName,"Arcade Setup",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        }
        public Setup(Library data) {
            library=data; Text="Linus Arcade Setup"; StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(810,610);
            MinimumSize=new Size(700,600);
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(20),RowCount=3,ColumnCount=1 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { Text="Game exits", Font=new Font(Font.FontFamily,22,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,0,14) },0,0);
            root.Controls.Add(tabs,0,1);
            for(int i=0;i<4;i++) AddPage(i);
            AddControllerPage();
            captureTimer.Tick+=(s,e)=>CaptureExitButtons();
            Disposed+=(s,e)=>captureTimer.Dispose();
            var footer=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,10,0,0) };
            var save=new Button { Text="Save games",AutoSize=true };
            save.Click+=(s,e)=>SaveGames(); footer.Controls.Add(save); footer.Controls.Add(status);
            root.Controls.Add(footer,0,2); Controls.Add(root);
        }
        void AddControllerPage() {
            var page=new TabPage("Controllers") { Padding=new Padding(12),AutoScroll=true };
            tabs.TabPages.Add(page);
            var controls=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false };
            controls.Controls.Add(new Label {
                Text="Click Learn exit buttons, then hold Start and Select on both controllers.",
                AutoSize=true
            });
            var learn=new Button { Text="Learn exit buttons",AutoSize=true };
            learn.Click+=(s,e)=>{
                captureStatus.Text="Press all four buttons within 10 seconds.";
                captureClock.Restart();
                captureTimer.Start();
            };
            controls.Controls.Add(learn);
            captureStatus.Text=library.GameExitButtons!=null && library.GameExitButtons.Length==2
                ? "Exit buttons configured." : "Exit buttons have not been configured.";
            controls.Controls.Add(captureStatus);
            page.Controls.Add(controls);
        }
        void CaptureExitButtons() {
            if (captureClock.Elapsed.TotalSeconds >= 10) {
                captureTimer.Stop();
                captureStatus.Text="No button combination detected. Click Learn exit buttons to retry.";
                return;
            }
            var bindings = new ControllerExitButtons[2];
            int count = 0;
            for (uint id = 0; id < Native.JoystickSlots; id++) {
                uint buttons = Native.ReadButtons(id);
                if (buttons == 0) continue;
                if (count == bindings.Length || !ControllerExitButtons.IsButtonPair(buttons)) return;
                bindings[count++] = new ControllerExitButtons { JoystickId = id, Buttons = buttons };
            }
            if (count != bindings.Length) return;
            captureTimer.Stop();
            library.GameExitButtons = bindings;
            captureStatus.Text="Exit buttons learned. Click Save games to apply after restarting the picker.";
            status.Text="Unsaved changes";
        }
        void AddPage(int i) {
            var exit=library.Exits[i];
            var page=new TabPage((i+1)+"   "+Library.Positions[i]) { Padding=new Padding(12),AutoScroll=true };
            tabs.TabPages.Add(page);
            var grid=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=3 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,135)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            page.Controls.Add(grid);
            Field(grid,0,"Game title",exit.Title,v=>{ exit.Title=v; page.ToolTipText=v; },null);
            tabs.ShowToolTips=true; page.ToolTipText=exit.Title;
            Field(grid,1,"Application",exit.Target,v=>exit.Target=v,"Games (*.exe;*.lnk)|*.exe;*.lnk");
            Field(grid,2,"Arguments",exit.Arguments,v=>exit.Arguments=v,null);
            Field(grid,3,"Start in folder",exit.Folder,v=>exit.Folder=v,"folder");
            Field(grid,4,"Thumbnail",exit.Image,v=>{ exit.Image=v; Preview(i); },"Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif");
            previews[i]=new PictureBox { SizeMode=PictureBoxSizeMode.Zoom,Width=304,Height=126,BackColor=Color.FromArgb(24,43,48),Margin=new Padding(4,14,0,4) };
            grid.Controls.Add(previews[i],1,5); Preview(i);
            var hint=new Label { Text="Select the game executable or a Windows shortcut to it. Shortcut arguments are kept. Extra arguments are appended.", AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(4,12,4,4) };
            grid.Controls.Add(hint,0,6); grid.SetColumnSpan(hint,3);
        }
        void Preview(int i) {
            var box=previews[i]; if(box==null) return;
            var old=box.Image; box.Image=null; if(old!=null) old.Dispose();
            try { using(var im=Image.FromFile(library.Exits[i].Image)) box.Image=new Bitmap(im); } catch(Exception) { }
        }
        void Field(TableLayoutPanel grid,int row,string label,string value,Action<string> change,string browse) {
            grid.Controls.Add(new Label { Text=label,AutoSize=true,Anchor=AnchorStyles.Left },0,row);
            var edit=new TextBox { Text=value,Dock=DockStyle.Fill,Margin=new Padding(4,6,4,6) };
            edit.TextChanged+=(s,e)=>{ change(edit.Text); status.Text="Unsaved changes"; };
            grid.Controls.Add(edit,1,row);
            if(browse==null) { grid.SetColumnSpan(edit,2); return; }
            var button=new Button { Text="Browse…",AutoSize=true,Anchor=AnchorStyles.Left };
            button.Click+=(s,e)=>{
                if(browse=="folder") {
                    using(var dlg=new FolderBrowserDialog()) if(dlg.ShowDialog(this)==DialogResult.OK) edit.Text=dlg.SelectedPath;
                } else using(var dlg=new OpenFileDialog { Filter=browse,DereferenceLinks=false })
                    if(dlg.ShowDialog(this)==DialogResult.OK) edit.Text=dlg.FileName;
            };
            grid.Controls.Add(button,2,row);
        }
        void SaveGames() {
            for(int i=0;i<4;i++) {
                var e=library.Exits[i]; string problem=null;
                if(e.Target.Length>0 && (!File.Exists(e.Target) || !(String.Equals(Path.GetExtension(e.Target),".exe",StringComparison.OrdinalIgnoreCase) || String.Equals(Path.GetExtension(e.Target),".lnk",StringComparison.OrdinalIgnoreCase)))) problem="Select an existing EXE or LNK file.";
                else if(e.Image.Length>0 && previews[i].Image==null) problem="Select a readable thumbnail.";
                else if(e.Folder.Length>0 && !Directory.Exists(e.Folder)) problem="Select an existing working directory.";
                if(problem!=null) { tabs.SelectedIndex=i; MessageBox.Show(this,problem,"Check "+Library.Positions[i]); return; }
            }
            try { library.Save(); status.Text="Saved. Changes apply at picker startup."; }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"Could not save games"); }
        }
    }
}

