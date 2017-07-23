using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;

namespace OCRTestUWP {
    class Screenshot {
        private WriteableBitmap screen;

        public WriteableBitmap Bitmap { get { return screen; } }

        private Screenshot() { }

        public static async Task<Screenshot> LoadFromFile(string filename) {
            var screenShot = new Screenshot();
            var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(filename);
            using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read)) {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var bitmap = await decoder.GetSoftwareBitmapAsync();
                screenShot.screen = new WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight);
                bitmap.CopyToBuffer(screenShot.screen.PixelBuffer);
            }
            return screenShot;
        }

        public async Task<WriteableBitmap> GetCroppedBitmapAsync(Rect cropRect) {
            var startX = (int)Math.Floor(Math.Max(cropRect.X, 0));
            var startY = (int)Math.Floor(Math.Max(cropRect.Y, 0));
            var resultHeight = (int)Math.Floor(Math.Min(cropRect.Height, screen.PixelHeight - startY));

            var origWidth = screen.PixelWidth * 4; //size of 1 line
            var resultWidth = ((int)Math.Floor(Math.Min(cropRect.Width, screen.PixelWidth - startX))) * 4; //size of 1 line

            WriteableBitmap cropImg;
            cropImg = new WriteableBitmap(resultWidth / 4, resultHeight);

            using (var stream = screen.PixelBuffer.AsStream()) {
                var pixels = new byte[(uint)stream.Length];
                var dstPixels = new byte[(uint)(resultWidth * resultHeight)];
                await stream.ReadAsync(pixels, 0, pixels.Length);
                for (var y = 0; y < resultHeight; y++) {
                    Array.Copy(pixels, startX * 4 + (startY + y) * origWidth, dstPixels, y * resultWidth, resultWidth);
                }

                using (var dstStream = cropImg.PixelBuffer.AsStream()) {
                    await dstStream.WriteAsync(dstPixels, 0, dstPixels.Length);
                }
            }

            return cropImg;
        }
    }
}
