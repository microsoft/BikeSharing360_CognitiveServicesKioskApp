using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

public static class SoftwareBitmapExtensions
{
    /// <summary>
    /// Converts this SoftwareBitmap instance to a Stream.
    /// </summary>
    /// <param name="softwareBitmap"></param>
    /// <returns></returns>
    public static async Task<Stream> AsStream(this SoftwareBitmap softwareBitmap)
    {
        var stream = new InMemoryRandomAccessStream();

        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        encoder.BitmapTransform.ScaledWidth = (uint)softwareBitmap.PixelWidth;
        encoder.BitmapTransform.ScaledHeight = (uint)softwareBitmap.PixelHeight;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();

        return stream.AsStreamForRead();
    }
}
