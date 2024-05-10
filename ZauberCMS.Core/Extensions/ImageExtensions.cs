namespace ZauberCMS.Core.Extensions;

public static class ImageExtensions
{
    /// <summary>
    /// Checks if an image is over max size and resizes if it is
    /// </summary>
    /// <param name="image"></param>
    /// <param name="maxPixelSize"></param>
    public static void OverMaxSizeCheck(this Image image, int maxPixelSize)
    {
        if (image?.Width > maxPixelSize || image?.Height > maxPixelSize)
        {
            var size = new Size();
            if (image.Width > image.Height)
            {
                size.Width = maxPixelSize;
            }
            else
            {
                size.Height = maxPixelSize;
            }
            image.Mutate(x => x.Resize(size));
        }
    }
}