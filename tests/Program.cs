using System;
using PngWall;

internal static class Program
{
    private static int Main()
    {
        try
        {
            LandscapeKeepsAspectRatio();
            PortraitKeepsAspectRatio();
            CoordinatesAreCenteredAndOffset();
            AlphaCutoffRemovesPixels();
            PartialAlphaIsPreserved();
            FullyTransparentPixelsAreAlwaysSkipped();
            WallLimitIsEnforced();
            Console.WriteLine("Rasterizer tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void LandscapeKeepsAspectRatio()
    {
        int width;
        int height;
        ImageWallRasterizer.CalculateOutputSize(1920, 1080, 64, out width, out height);
        Equal(64, width, "landscape width");
        Equal(36, height, "landscape height");
    }

    private static void PortraitKeepsAspectRatio()
    {
        int width;
        int height;
        ImageWallRasterizer.CalculateOutputSize(1080, 1920, 64, out width, out height);
        Equal(36, width, "portrait width");
        Equal(64, height, "portrait height");
    }

    private static void CoordinatesAreCenteredAndOffset()
    {
        var options = Defaults();
        options.Density = 2;
        options.PixelSize = 0.5f;
        options.CenterX = 1f;
        options.BottomY = 2f;

        var image = ImageWallRasterizer.Rasterize(
            2,
            1,
            (u, v) => new PixelColor(1f, 1f, 1f, 1f),
            options);

        Equal(2, image.Walls.Count, "wall count");
        Near(0.5f, image.Walls[0].X, "first x");
        Near(1f, image.Walls[1].X, "second x");
        Near(2f, image.Walls[0].Y, "bottom y");
    }

    private static void AlphaCutoffRemovesPixels()
    {
        var options = Defaults();
        options.Density = 2;
        options.AlphaCutoff = 0.5f;

        var image = ImageWallRasterizer.Rasterize(
            2,
            1,
            (u, v) => new PixelColor(1f, 0f, 0f, u < 0.5f ? 0.25f : 1f),
            options);

        Equal(1, image.Walls.Count, "alpha-filtered wall count");
    }

    private static void PartialAlphaIsPreserved()
    {
        var options = Defaults();
        options.Density = 1;
        options.AlphaCutoff = 0.1f;
        var image = ImageWallRasterizer.Rasterize(1, 1,
            (u, v) => new PixelColor(1f, 0f, 0f, 0.4f), options);
        Equal(1, image.Walls.Count, "partial alpha wall count");
        Near(0.4f, image.Walls[0].Color.Alpha, "preserved alpha");
    }

    private static void WallLimitIsEnforced()
    {
        var options = Defaults();
        options.Density = 4;
        options.MaxWalls = 3;

        var threw = false;
        try
        {
            ImageWallRasterizer.Rasterize(
                4,
                4,
                (u, v) => new PixelColor(1f, 1f, 1f, 1f),
                options);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        if (!threw)
        {
            throw new Exception("wall limit was not enforced");
        }
    }

    private static void FullyTransparentPixelsAreAlwaysSkipped()
    {
        var options = Defaults();
        options.Density = 1;
        options.AlphaCutoff = 0f;

        var image = ImageWallRasterizer.Rasterize(
            1,
            1,
            (u, v) => new PixelColor(1f, 1f, 1f, 0f),
            options);

        Equal(0, image.Walls.Count, "fully transparent wall count");
    }

    private static RasterizationOptions Defaults()
    {
        return new RasterizationOptions
        {
            Density = 4,
            PixelSize = 1f,
            CenterX = 0f,
            BottomY = 0f,
            AlphaCutoff = 0f,
            MaxWalls = 100
        };
    }

    private static void Equal(int expected, int actual, string name)
    {
        if (expected != actual)
        {
            throw new Exception(string.Format("{0}: expected {1}, got {2}", name, expected, actual));
        }
    }

    private static void Near(float expected, float actual, string name)
    {
        if (Math.Abs(expected - actual) > 0.0001f)
        {
            throw new Exception(string.Format("{0}: expected {1}, got {2}", name, expected, actual));
        }
    }
}
