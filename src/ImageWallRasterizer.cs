using System;
using System.Collections.Generic;

namespace PngWall
{
    public struct PixelColor
    {
        public PixelColor(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public readonly float Red;
        public readonly float Green;
        public readonly float Blue;
        public readonly float Alpha;
    }

    public sealed class RasterizationOptions
    {
        public int Density { get; set; }
        public float PixelSize { get; set; }
        public float CenterX { get; set; }
        public float BottomY { get; set; }
        public float AlphaCutoff { get; set; }
        public int MaxWalls { get; set; }
    }

    public struct WallPixel
    {
        public WallPixel(float x, float y, float size, PixelColor color)
        {
            X = x;
            Y = y;
            Size = size;
            Color = color;
        }

        public readonly float X;
        public readonly float Y;
        public readonly float Size;
        public readonly PixelColor Color;
    }

    public sealed class RasterizedImage
    {
        public RasterizedImage(int width, int height, List<WallPixel> walls)
        {
            Width = width;
            Height = height;
            Walls = walls;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public List<WallPixel> Walls { get; private set; }
    }

    public static class ImageWallRasterizer
    {
        public static RasterizedImage Rasterize(
            int sourceWidth,
            int sourceHeight,
            Func<float, float, PixelColor> sampler,
            RasterizationOptions options)
        {
            if (sourceWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("sourceWidth");
            }

            if (sourceHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("sourceHeight");
            }

            if (sampler == null)
            {
                throw new ArgumentNullException("sampler");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.Density < 1)
            {
                throw new ArgumentOutOfRangeException("options.Density");
            }

            if (options.PixelSize <= 0)
            {
                throw new ArgumentOutOfRangeException("options.PixelSize");
            }

            int outputWidth;
            int outputHeight;
            CalculateOutputSize(sourceWidth, sourceHeight, options.Density, out outputWidth, out outputHeight);

            var gridCellCount = outputWidth * outputHeight;
            var initialCapacity = options.MaxWalls > 0
                ? Math.Min(gridCellCount, options.MaxWalls)
                : gridCellCount;
            var walls = new List<WallPixel>(initialCapacity);
            var left = options.CenterX - ((outputWidth * options.PixelSize) / 2f);

            for (var y = 0; y < outputHeight; y++)
            {
                var v = (y + 0.5f) / outputHeight;

                for (var x = 0; x < outputWidth; x++)
                {
                    var u = (x + 0.5f) / outputWidth;
                    var color = sampler(u, v);

                    // A fully transparent source pixel should never become an opaque wall,
                    // even when the user moves the cutoff slider all the way to zero.
                    if (color.Alpha <= 0f || color.Alpha < options.AlphaCutoff)
                    {
                        continue;
                    }

                    if (options.MaxWalls > 0 && walls.Count >= options.MaxWalls)
                    {
                        throw new InvalidOperationException(
                            "Wall数が上限を超えました。ピクセル密度を下げるか、透明度しきい値を上げてください。");
                    }

                    walls.Add(new WallPixel(
                        left + (x * options.PixelSize),
                        options.BottomY + (y * options.PixelSize),
                        options.PixelSize,
                        color));
                }
            }

            return new RasterizedImage(outputWidth, outputHeight, walls);
        }

        public static void CalculateOutputSize(
            int sourceWidth,
            int sourceHeight,
            int density,
            out int outputWidth,
            out int outputHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("画像サイズは1以上である必要があります。");
            }

            if (density < 1)
            {
                throw new ArgumentOutOfRangeException("density");
            }

            if (sourceWidth >= sourceHeight)
            {
                outputWidth = density;
                outputHeight = Math.Max(1, (int)Math.Round(
                    density * (sourceHeight / (double)sourceWidth),
                    MidpointRounding.AwayFromZero));
            }
            else
            {
                outputHeight = density;
                outputWidth = Math.Max(1, (int)Math.Round(
                    density * (sourceWidth / (double)sourceHeight),
                    MidpointRounding.AwayFromZero));
            }
        }
    }
}
