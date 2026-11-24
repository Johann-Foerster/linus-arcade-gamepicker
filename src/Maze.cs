using System;
using System.Collections.Generic;
using System.Drawing;

namespace Arcade {
    class Runner {
        public Point Cell, Next;
        public int Direction;
        public double Progress;
        public Runner(Point cell) { Cell=Next=cell; }
        public float X { get { return Cell.X + (Next.X-Cell.X)*(float)Progress; } }
        public float Y { get { return Cell.Y + (Next.Y-Cell.Y)*(float)Progress; } }
    }
    class Maze {
        // The central column connects two mirrored wings.
        static readonly string[] Wing = {
            "###########", "#..........", "#.###.###.#", "....#...#..", "###.###.#.#",
            "#.......#..", "#.###.#.###", "#.....#....", "#.###.#.###", "#.......#..",
            "###.###.#.#", "....#...#..", "#.###.###.#", "#..........", "###########"
        };
        public const int Width=23, Height=15;
        public static readonly Point[] Gates = {new Point(0,3),new Point(22,3),new Point(0,11),new Point(22,11)};
        static readonly Point[] Delta = {Point.Empty,new Point(0,-1),new Point(1,0),new Point(0,1),new Point(-1,0)};
        static readonly Point Home = new Point(11,7);
        const double PelletRespawnSeconds = 10;
        public Runner Player;
        public Runner[] Ghosts;
        public readonly HashSet<Point> Pellets = new HashSet<Point>();
        internal MazeCue LastCue { get; private set; }

        readonly Random random = new Random();
        readonly Queue<KeyValuePair<Point,double>> pelletRespawns = new Queue<KeyValuePair<Point,double>>();
        int queued;
        bool moving;
        public Maze() { Refill(); Respawn(); }
        public static bool Open(int x,int y) {
            if (x<0 || x>=Width || y<=0 || y>=Height-1) return false;
            return x==11 || Wing[y][Math.Min(x,Width-1-x)]=='.';
        }
        public void Respawn() {
            Player = new Runner(Home);
            Ghosts = new [] {
                new Runner(new Point(3,3)), new Runner(new Point(19,11)), new Runner(new Point(11,1)),
                new Runner(new Point(5,9)), new Runner(new Point(17,5))
            };
            queued=0; moving=false;
        }
        void Refill() {
            for(int y=1;y<Height-1;y++) for(int x=0;x<Width;x++) {
                Point p=new Point(x,y);
                if(Open(x,y) && p!=Home && Array.IndexOf(Gates,p)<0) Pellets.Add(p);
            }
        }
        static Point Step(Point p,int d) { return Point.Add(p,new Size(Delta[d])); }
        static bool Can(Point p,int d) { Point n=Step(p,d); return d!=0 && Open(n.X,n.Y); }
        static void Advance(Runner a,double travel,Func<int> choose) {
            while(travel>0) {
                if(a.Cell==a.Next) {
                    int d=choose();
                    if(!Can(a.Cell,d)) return;
                    a.Direction=d; a.Next=Step(a.Cell,d);
                }
                double distance=Math.Min(1-a.Progress,travel);
                a.Progress+=distance; travel-=distance;
                if(a.Progress>=1) { a.Cell=a.Next; a.Progress=0; }
            }
        }
        int GhostTurn(Runner ghost,int index) {
            var options=new List<int>();
            int back=(ghost.Direction+1)%4+1;
            for(int d=1;d<=4;d++) if(Can(ghost.Cell,d) && Array.IndexOf(Gates,Step(ghost.Cell,d))<0) options.Add(d);
            if(options.Count>1) options.Remove(back);
            if(options.Count==0) return 0;
            if(random.Next(4)<=Math.Min(index,2)) return options[random.Next(options.Count)];
            int best=options[random.Next(options.Count)];
            foreach(int d in options) if(Distance(Step(ghost.Cell,d),Player.Cell)<Distance(Step(ghost.Cell,best),Player.Cell)) best=d;
            return best;
        }
        static int Distance(Point a,Point b) { return Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y); }
        public void RestorePellets(double now) {
            while(pelletRespawns.Count>0 && pelletRespawns.Peek().Value<=now)
                Pellets.Add(pelletRespawns.Dequeue().Key);
        }
        public int Tick(double dt,int direction,double now) {
            LastCue=MazeCue.None;
            RestorePellets(now);
            if(direction!=0) { queued=direction; moving=true; }
            if(!moving) return -1;
            Point previousCell=Player.Cell;
            Advance(Player,dt*5.25,delegate {
                if(Can(Player.Cell,queued)) { int d=queued; queued=0; return d; }
                return Player.Direction;
            });
            if(Player.Cell!=previousCell && Pellets.Remove(Player.Cell)) {
                pelletRespawns.Enqueue(new KeyValuePair<Point,double>(Player.Cell,now+PelletRespawnSeconds));
                LastCue=Pellets.Count==0?MazeCue.Cleared:MazeCue.Pellet;
            }
            int gate=Array.IndexOf(Gates,Player.Cell);
            if(gate>=0) return gate;
            for(int i=0;i<Ghosts.Length;i++) {
                Runner ghost=Ghosts[i];
                Advance(ghost,dt*(2.6+i*.22),()=>GhostTurn(ghost,i));
                if(Math.Abs(Player.X-ghost.X)<.62 && Math.Abs(Player.Y-ghost.Y)<.62) {
                    Respawn();
                    LastCue=MazeCue.Caught;
                    break;
                }
            }
            return -1;
        }
    }
}

