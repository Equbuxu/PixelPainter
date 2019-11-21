using System;
using System.Collections.Generic;
using System.Drawing;

namespace GUIPixelPainter
{
    class ImageConverter
    {
        private Bitmap original;
        private Bitmap result;
        private bool isConverted;

        private IReadOnlyCollection<Color> palette;
        private bool dither;

        public ImageConverter(Bitmap original, IReadOnlyCollection<Color> palette, bool dither)
        {
            this.original = (Bitmap)original.Clone();
            this.palette = palette;
            this.dither = dither;
        }

        public Bitmap Convert()
        {
            if (isConverted)
                return result;
            isConverted = true;

            result = new Bitmap(original.Width, original.Height);
            ConvertImage();

            return result;
        }

        private void ConvertImage()
        {
            for (int j = 0; j < original.Height; j++)
            {
                for (int i = 0; i < original.Width; i++)
                {
                    Color pixel = original.GetPixel(i, j);
                    Color closestColor;
                    if (pixel.A == 0)
                    {
                        closestColor = pixel;
                        result.SetPixel(i, j, Color.FromArgb(0, 1, 2, 3));
                    }
                    else
                    {
                        closestColor = FindClosestPaletteColor(pixel);
                        result.SetPixel(i, j, closestColor);
                    }

                    if (dither)
                    {
                        DistributeErrorDither(original, i, j, pixel, closestColor);
                    }
                }
            }

        }

        private static void AddError(Bitmap image, int x, int y, int errR, int errG, int errB)
        {
            if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                return;
            Color cur = image.GetPixel(x, y);
            if (cur.A == 0)
                return;
            int nextR = cur.R + errR;
            int nextG = cur.G + errG;
            int nextB = cur.B + errB;

            if (nextR > 255) nextR = 255;
            if (nextG > 255) nextG = 255;
            if (nextB > 255) nextB = 255;

            if (nextR < 0) nextR = 0;
            if (nextG < 0) nextG = 0;
            if (nextB < 0) nextB = 0;

            Color newColor = Color.FromArgb(nextR, nextG, nextB);
            image.SetPixel(x, y, newColor);
        }

        private static void DistributeErrorDither(Bitmap image, int x, int y, Color original, Color closest)
        {
            int errorR = original.R - closest.R;
            int errorG = original.G - closest.G;
            int errorB = original.B - closest.B;

            AddError(image, x + 1, y, errorR * 7 / 16, errorG * 7 / 16, errorB * 7 / 16);
            AddError(image, x - 1, y + 1, errorR * 3 / 16, errorG * 3 / 16, errorB * 3 / 16);
            AddError(image, x, y + 1, errorR * 5 / 16, errorG * 5 / 16, errorB * 5 / 16);
            AddError(image, x + 1, y + 1, errorR * 1 / 16, errorG * 1 / 16, errorB * 1 / 16);
        }

        private Color FindClosestPaletteColor(Color c)
        {
            Color minColor = Color.FromArgb(0, 1, 2, 3);
            int minDist = 9999;
            foreach (Color p in palette)
            {
                int dist = Math.Abs(p.R - c.R) + Math.Abs(p.G - c.G) + Math.Abs(p.B - c.B);

                if (dist < minDist)
                {
                    minDist = dist;
                    minColor = p;
                }
            }

            return minColor;
        }

    }
}
