using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Arcade {
    static class HeroSprite {
        static readonly Bitmap Pixels = DrawPixels();

        static Bitmap DrawPixels() {
            string[] rows = {
                "...............",
                "......#####....",
                ".....#hHHhhh#..",
                "....#hhhsshh#..",
                "....#hssssss#..",
                "....#ssessess#.",
                "....#psssssps#.",
                ".....#ssssss#..",
                ".....#ssmmss#..",
                "......#ssss#...",
                ".....###sss##..",
                "....#rBNnNNrB#.",
                "...#Br#NnWCWBr#",
                "...#rB#NnCCCrB#",
                "..#wWw#NnnWNwW#",
                "..#wWW#NbNbnww#",
                "...####bnb#bb#.",
                ".....#wwb#wb#..",
                ".....#rwb#rb#..",
                ".....##Bn##Bn#.",
                ".......##..##.."
            };
            var palette = new Dictionary<char, Color> {
                { '#', Color.FromArgb(21, 29, 43) },
                { 'h', Color.FromArgb(115, 64, 40) }, { 'H', Color.FromArgb(195, 113, 61) },
                { 's', Color.FromArgb(255, 196, 141) }, { 'p', Color.FromArgb(232, 132, 119) },
                { 'e', Color.FromArgb(53, 35, 39) }, { 'm', Color.FromArgb(132, 65, 54) },
                { 'w', Color.FromArgb(199, 216, 231) }, { 'W', Color.FromArgb(247, 248, 244) },
                { 'r', Color.FromArgb(230, 63, 88) }, { 'C', Color.FromArgb(195, 34, 59) },
                { 'N', Color.FromArgb(45, 57, 87) }, { 'n', Color.FromArgb(68, 88, 121) },
                { 'b', Color.FromArgb(36, 47, 70) }, { 'B', Color.FromArgb(135, 166, 199) }
            };
            var sprite = new Bitmap(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++) {
                for (int x = 0; x < rows[y].Length; x++) {
                    Color color;
                    if (palette.TryGetValue(rows[y][x], out color)) sprite.SetPixel(x, y, color);
                }
            }
            return sprite;
        }

        internal static void Draw(Graphics g, Runner player) {
            var saved = g.Save();
            float bounce = (float)Math.Sin(player.Progress * 2 * Math.PI) * 2;
            g.ScaleTransform(player.Direction == 4 ? -1 : 1, 1);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            const int pixelSize = 3;
            int width = Pixels.Width * pixelSize, height = Pixels.Height * pixelSize;
            g.DrawImage(Pixels, new RectangleF(-width / 2f, -height / 2f + bounce, width, height));
            g.Restore(saved);
        }
    }
}
