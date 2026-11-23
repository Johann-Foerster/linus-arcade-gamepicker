using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Arcade {
    class Checks {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        static void CheckGameExit(string command) {
            var start = new ProcessStartInfo(command, "/d /c set /p exitCheck=") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
            };
            using (var game = Process.Start(start)) {
                try {
                    var gameExit = new GameExit();
                    gameExit.Update(game, true, 0);
                    gameExit.Update(game, true, 4.9);
                    Check(!game.HasExited, "Game exited before five seconds");
                    gameExit.Update(game, false, 4.95);
                    gameExit.Update(game, true, 5);
                    gameExit.Update(game, true, 9.9);
                    Check(!game.HasExited, "Releasing a button did not reset the hold");
                    gameExit.Update(game, true, 10);
                    Check(game.WaitForExit(5000), "Controller exit did not terminate the windowless game");
                } finally {
                    game.StandardInput.Close();
                }
            }
        }
        [STAThread] public static void Main() {
            var maze=new Maze();
            var seen=new HashSet<Point>(); var queue=new Queue<Point>(); queue.Enqueue(maze.Player.Cell);
            while(queue.Count>0) {
                Point p=queue.Dequeue(); if(!Maze.Open(p.X,p.Y) || !seen.Add(p)) continue;
                foreach(var delta in new[]{new Size(1,0),new Size(-1,0),new Size(0,1),new Size(0,-1)}) queue.Enqueue(Point.Add(p,delta));
            }
            foreach(var p in Maze.Gates) Check(seen.Contains(p),"Unreachable exit");
            foreach(var p in maze.Pellets) Check(seen.Contains(p),"Unreachable pellet");
            for(int i=0;i<4;i++) {
                var gate=Maze.Gates[i];
                maze.Respawn(); maze.Player=new Runner(new Point(i%2==0?1:21,gate.Y));
                int result=-1;
                for(int n=0;n<40 && result<0;n++) result=maze.Tick(.016,i%2==0?4:2);
                Check(result==i,"Wrong gate trigger");
            }
            for(int life=0;life<10;life++) {
                maze.Shield=0; maze.Player=new Runner(new Point(3,3)); maze.Ghosts[0]=new Runner(maze.Player.Cell);
                maze.Tick(.016,1); Check(maze.Player.Cell==new Point(11,7) && maze.Shield>0,"Respawn failed");
            }
            string dir=Path.Combine(Path.GetTempPath(),"Arcade checks "+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string shortcut=Path.Combine(dir,"test game.lnk");
            try {
                string command=Path.Combine(Environment.SystemDirectory,"cmd.exe");
                CheckGameExit(command);
                var direct=new Exit { Target=command, Arguments="/d /c exit 7", Folder=dir };
                using(var proc=Library.Start(direct)) { Check(proc.WaitForExit(5000) && proc.ExitCode==7,"Direct launch failed"); }
                dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic link=shell.CreateShortcut(shortcut);
                link.TargetPath=command; link.Arguments="/d /c exit"; link.WorkingDirectory=dir; link.Save();
                Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell);
                using(var proc=Library.Start(new Exit { Target=shortcut,Arguments="13" })) { Check(proc.WaitForExit(5000) && proc.ExitCode==13,"Shortcut arguments failed"); }
                using(var picker=new Picker()) using(var bitmap=new Bitmap(1920,1080)) using(var g=Graphics.FromImage(bitmap)) {
                    picker.PaintScene(g,bitmap.Size);
                    if(Environment.GetEnvironmentVariable("ARCADE_PREVIEW")!=null) bitmap.Save(Environment.GetEnvironmentVariable("ARCADE_PREVIEW"),System.Drawing.Imaging.ImageFormat.Png);
                }
                using(var setup=new Setup(new Library())) { setup.PerformLayout(); }
                Native.Stick();
                Console.WriteLine("Passed maze connectivity, four exit triggers, repeated respawns, executable and shortcut launches, controller exit timing, rendering, setup construction and input polling.");
            } finally { File.Delete(shortcut); Directory.Delete(dir); }
        }
    }
}
