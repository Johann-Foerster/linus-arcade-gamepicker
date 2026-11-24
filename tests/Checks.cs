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
                    gameExit.Update(game, true, 1.9);
                    Check(!game.HasExited, "Game exited before two seconds");
                    gameExit.Update(game, false, 1.95);
                    gameExit.Update(game, true, 2);
                    gameExit.Update(game, true, 3.9);
                    Check(!game.HasExited, "Releasing a button did not reset the hold");
                    gameExit.Update(game, true, 4);
                    Check(game.WaitForExit(5000), "Controller exit did not terminate the windowless game");
                } finally {
                    game.StandardInput.Close();
                }
            }
        }
        static void CheckPelletRespawns() {
            var maze=new Maze();
            maze.Ghosts=new Runner[0];
            var first=new Point(11,6);
            var second=new Point(11,5);
            maze.Tick(.2,1,0);
            Check(maze.LastCue==MazeCue.Pellet && !maze.Pellets.Contains(first),"First pellet was not collected");
            maze.Tick(.2,0,1);
            Check(!maze.Pellets.Contains(second),"Second pellet was not collected");
            maze.Tick(0,0,9.99);
            Check(maze.LastCue==MazeCue.None && !maze.Pellets.Contains(first),"First pellet respawned early");
            maze.Tick(0,0,10);
            Check(maze.Pellets.Contains(first) && !maze.Pellets.Contains(second),"Pellets did not respawn independently");
            maze.Tick(0,0,11);
            Check(maze.Pellets.Contains(second) && maze.LastCue==MazeCue.None,"Second pellet did not stay visible under the player");

            var clearMaze=new Maze();
            clearMaze.Pellets.Clear();
            clearMaze.Pellets.Add(first);
            clearMaze.Tick(.2,1,0);
            Check(clearMaze.LastCue==MazeCue.Cleared && clearMaze.Pellets.Count==0,"Clearing the maze refilled pellets early or lost the clear cue");
            clearMaze.Respawn();
            clearMaze.Tick(0,0,10);
            Check(clearMaze.Pellets.Contains(first),"Pellet did not respawn while the player was idle");
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
            var ghostSpawns=new HashSet<Point>();
            Check(maze.Ghosts.Length==5,"Expected five ghosts");
            foreach(var ghost in maze.Ghosts)
                Check(seen.Contains(ghost.Cell) && ghostSpawns.Add(ghost.Cell),"Invalid or duplicate ghost spawn");
            CheckPelletRespawns();
            for(int i=0;i<4;i++) {
                var gate=Maze.Gates[i];
                maze.Respawn(); maze.Player=new Runner(new Point(i%2==0?1:21,gate.Y));
                int result=-1;
                for(int n=0;n<40 && result<0;n++) result=maze.Tick(.016,i%2==0?4:2,i+n*.016);
                Check(result==i,"Wrong gate trigger");
            }
            for(int life=0;life<10;life++) {
                maze.Respawn();
                maze.Ghosts[life%maze.Ghosts.Length]=new Runner(maze.Player.Cell);
                maze.Tick(.016,1,4+life*.016); Check(maze.Player.Cell==new Point(11,7) && maze.Player.Progress==0,"Respawn failed");
                Check(maze.LastCue==MazeCue.Caught,"Missing ghost collision cue");
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
                Console.WriteLine("Passed maze connectivity, four exit triggers, player and pellet respawns, executable and shortcut launches, controller exit timing, rendering, setup construction and input polling.");
            } finally { File.Delete(shortcut); Directory.Delete(dir); }
        }
    }
}
