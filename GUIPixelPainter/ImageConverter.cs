using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

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
            //assuming 32bppargb
            unsafe
            {
                int w = original.Width;
                int h = original.Height;

                BitmapData data = original.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, original.PixelFormat);
                BitmapData resultData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, result.PixelFormat);

                int stride = data.Stride;
                int resultStride = resultData.Stride;

                byte* pointer = (byte*)data.Scan0;
                byte* resStart = (byte*)resultData.Scan0;
                byte* origStart = (byte*)data.Scan0;

                for (int j = 0; j < h; j++)
                {
                    for (int i = 0; i < w; i++)
                    {
                        //pointer[0] Blue
                        //pointer[1] Green
                        //pointer[2] Red
                        //pointer[3] Alpha

                        byte closestA = 0;
                        byte closestR = 0;
                        byte closestG = 0;
                        byte closestB = 0;

                        if (pointer[3] == 0)
                        {
                            (((int*)resStart)[j * resultStride / 4 + i]) = 0x03020100;
                            closestA = pointer[3];
                            closestR = pointer[2];
                            closestG = pointer[1];
                            closestB = pointer[0];
                        }
                        else
                        {
                            int pos = (j * resultStride) + i * 4;
                            FindClosestPaletteColor(pointer[3], pointer[2], pointer[1], pointer[0], out closestA, out closestR, out closestG, out closestB);
                            resStart[pos + 3] = closestA;
                            resStart[pos + 2] = closestR;
                            resStart[pos + 1] = closestG;
                            resStart[pos] = closestB;
                        }

                        if (dither)
                        {
                            DistributeErrorDither(origStart, w, h, resultStride, i, j, pointer[3], pointer[2], pointer[1], pointer[0], closestA, closestR, closestG, closestB);
                        }

                        pointer += 4;
                    }
                    pointer += stride - w * 4;
                }

                original.UnlockBits(data);
                result.UnlockBits(resultData);
            }
        }

        private static unsafe void AddError(byte* image, int imgW, int imgH, int stride, int x, int y, int errR, int errG, int errB)
        {
            if (x < 0 || y < 0 || x >= imgW || y >= imgH)
                return;
            //Color cur = image.GetPixel(x, y);
            int pos = y * stride + x * 4;
            byte curA = image[pos + 3];
            byte curR = image[pos + 2];
            byte curG = image[pos + 1];
            byte curB = image[pos + 0];

            if (curA == 0)
                return;
            int nextR = curR + errR;
            int nextG = curG + errG;
            int nextB = curB + errB;

            if (nextR > 255) nextR = 255;
            if (nextG > 255) nextG = 255;
            if (nextB > 255) nextB = 255;

            if (nextR < 0) nextR = 0;
            if (nextG < 0) nextG = 0;
            if (nextB < 0) nextB = 0;

            //Color newColor = Color.FromArgb(nextR, nextG, nextB);
            //image.SetPixel(x, y, newColor);

            image[pos + 3] = 255;
            image[pos + 2] = (byte)nextR;
            image[pos + 1] = (byte)nextG;
            image[pos] = (byte)nextB;
        }

        private static unsafe void DistributeErrorDither(byte* image, int imgW, int imgH, int stride, int x, int y, byte orA, byte orR, byte orG, byte orB, byte clA, byte clR, byte clG, byte clB)
        {
            int errorR = orR - clR;
            int errorG = orG - clG;
            int errorB = orB - clB;

            AddError(image, imgW, imgH, stride, x + 1, y, errorR * 7 / 16, errorG * 7 / 16, errorB * 7 / 16);
            AddError(image, imgW, imgH, stride, x - 1, y + 1, errorR * 3 / 16, errorG * 3 / 16, errorB * 3 / 16);
            AddError(image, imgW, imgH, stride, x, y + 1, errorR * 5 / 16, errorG * 5 / 16, errorB * 5 / 16);
            AddError(image, imgW, imgH, stride, x + 1, y + 1, errorR * 1 / 16, errorG * 1 / 16, errorB * 1 / 16);
        }

        private void FindClosestPaletteColor(byte a, byte r, byte g, byte b, out byte closestA, out byte closestR, out byte closestG, out byte closestB)
        {
            closestA = 0;
            closestR = 0;
            closestG = 0;
            closestB = 0;

            int minDist = int.MaxValue;
            foreach (Color p in palette)
            {
                //int dist = Math.Abs(p.R - r) + Math.Abs(p.G - g) + Math.Abs(p.B - b);
                int dR = (p.R - r);
                int dG = (p.G - g);
                int dB = (p.B - b);
                int dist = dR * dR + dG * dG + dB * dB;

                if (dist < minDist)
                {
                    minDist = dist;
                    closestA = p.A;
                    closestR = p.R;
                    closestG = p.G;
                    closestB = p.B;
                }
            }
        }

    }
}
