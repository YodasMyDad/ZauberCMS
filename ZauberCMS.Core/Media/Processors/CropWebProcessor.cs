﻿using System.Globalization;
using System.Numerics;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.Processors;

namespace ZauberCMS.Core.Media.Processors;

/// <summary>
/// Allows the cropping of images.
/// </summary>
/// <remarks>
/// This code was originally authored by Umbraco and modified slightly by me. Please ensure appropriate credit is given when used or modified.
/// </remarks>

/// <seealso cref="SixLabors.ImageSharp.Web.Processors.IImageWebProcessor" />
public class CropWebProcessor : IImageWebProcessor
{
    /// <summary>
    /// The command constant for the crop coordinates.
    /// </summary>
    private const string Coordinates = "cc";

    /// <summary>
    /// The command constant for the resize orientation handling mode.
    /// </summary>
    private const string Orient = "orient";

    /// <inheritdoc />
    public IEnumerable<string> Commands { get; } = [Coordinates, Orient];

    /// <inheritdoc />
    public FormattedImage Process(FormattedImage image, ILogger logger, CommandCollection commands, CommandParser parser, CultureInfo culture)
    {
        Rectangle? cropRectangle = GetCropRectangle(image, commands, parser, culture);
        if (cropRectangle.HasValue)
        {
            image.Image.Mutate(x => x.Crop(cropRectangle.Value));
        }

        return image;
    }

    /// <inheritdoc />
    public bool RequiresTrueColorPixelFormat(CommandCollection commands, CommandParser parser, CultureInfo culture) =>
        false;

    private static Rectangle? GetCropRectangle(FormattedImage image, CommandCollection commands, CommandParser parser, CultureInfo culture)
    {
        var coordinates = parser.ParseValue<float[]>(commands.GetValueOrDefault(Coordinates), culture);
        if (coordinates != null && (coordinates.Length != 4 ||
                                    (coordinates[0] == 0 && coordinates[1] == 0 && coordinates[2] == 0 && coordinates[3] == 0)))
        {
            return null;
        }

        // The right and bottom values are actually the distance from those sides, so convert them into real coordinates and transform to correct orientation
        var left = Math.Clamp(coordinates![0], 0, 1);
        var top = Math.Clamp(coordinates[1], 0, 1);
        var right = Math.Clamp(1 - coordinates[2], 0, 1);
        var bottom = Math.Clamp(1 - coordinates[3], 0, 1);
        var orientation = GetExifOrientation(image, commands, parser, culture);
        var xy1 = ExifOrientationUtilities.Transform(new Vector2(left, top), Vector2.Zero, Vector2.One, orientation);
        var xy2 = ExifOrientationUtilities.Transform(new Vector2(right, bottom), Vector2.Zero, Vector2.One, orientation);

        // Scale points to a pixel based rectangle
        var size = image.Image.Size;

        return Rectangle.Round(RectangleF.FromLTRB(
            MathF.Min(xy1.X, xy2.X) * size.Width,
            MathF.Min(xy1.Y, xy2.Y) * size.Height,
            MathF.Max(xy1.X, xy2.X) * size.Width,
            MathF.Max(xy1.Y, xy2.Y) * size.Height));
    }

    private static ushort GetExifOrientation(FormattedImage image, CommandCollection commands, CommandParser parser, CultureInfo culture)
    {
        if (commands.Contains(Orient) && !parser.ParseValue<bool>(commands.GetValueOrDefault(Orient), culture))
        {
            return ExifOrientationMode.Unknown;
        }

        image.TryGetExifOrientation(out var orientation);

        return orientation;
    }
}