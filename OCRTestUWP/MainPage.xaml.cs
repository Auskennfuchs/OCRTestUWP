using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Streams;

// Die Vorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 dokumentiert.

namespace OCRTestUWP
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Screenshot screenShot;

        private bool selectMode = false;
        private Rect selectRectangle = new Rect();
        private Point startPos, selectStartPos;

        public MainPage()
        {
            this.InitializeComponent();

            Application.Current.DebugSettings.EnableFrameRateCounter = false;

            this.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(onImageRelease), true);
            this.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(onImageClick), false);
            this.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(onImageMove), true);

            LoadImage();
        }

        private async void LoadImage() {
            screenShot = await Screenshot.LoadFromFile("Assets\\bdo.jpg");
            preview.Source = screenShot.Bitmap;
        }

        private async void RunOCR() {
            var ocr = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"));
            var srcImg = (WriteableBitmap)cropimage.Source;
            var ocrImg = new SoftwareBitmap(BitmapPixelFormat.Rgba8, srcImg.PixelWidth, srcImg.PixelHeight);
            ocrImg.CopyFromBuffer(srcImg.PixelBuffer);
            var ocrResult = await ocr.RecognizeAsync(ocrImg);
            string text = "";
            foreach(var line in ocrResult.Lines) {
                text += line.Text + "\n";
            }
            this.ocrResult.Text = text;
        }

        private void button_Click_1(object sender, RoutedEventArgs e) {
            RunOCR();
        }

        private Point GetTransformedPos(Windows.UI.Input.PointerPoint point) {
            var transform = preview.TransformToVisual(Window.Current.Content);
            var previewBounds = transform.TransformBounds(new Rect(0, 0, preview.ActualWidth, preview.ActualHeight));

            var pointPos = transform.TransformPoint(point.Position);
            pointPos.X = Math.Max(pointPos.X, previewBounds.Left);
            pointPos.Y = Math.Max(pointPos.Y, previewBounds.Top);
            pointPos.X = Math.Min(pointPos.X, previewBounds.Right);
            pointPos.Y = Math.Min(pointPos.Y, previewBounds.Bottom);

            return pointPos;
        }

        private Point GetRealPoint(Point transformedPos) {
            var transform = preview.TransformToVisual(Window.Current.Content);
            var startImg = transform.TransformPoint(new Point(0, 0));
            var img = (WriteableBitmap)preview.Source;
            var point = new Point((transformedPos.X-startImg.X) / preview.ActualWidth * img.PixelWidth, (transformedPos.Y-startImg.Y) / preview.ActualHeight * img.PixelHeight);
            point.X = Math.Max(point.X, 0);
            point.Y = Math.Max(point.Y, 0);
            point.X = Math.Min(point.X, img.PixelWidth);
            point.Y = Math.Min(point.Y, img.PixelHeight);
            return point;
        }

        private void onImageClick(object sender, PointerRoutedEventArgs e) {
            var pointPos = GetTransformedPos(e.GetCurrentPoint(preview));
            
            selectStartPos = GetRealPoint(pointPos);
            selectRectangle = new Rect(selectStartPos, new Size());

            startPos = pointPos;

            Canvas.SetLeft(selectBox, startPos.X);
            Canvas.SetTop(selectBox, startPos.Y);
            selectBox.Width = selectBox.Height = 0;

            selectMode = true;
        }

        private void onImageMove(object sender, PointerRoutedEventArgs e) {
            if(selectMode) {
                var pointPos = GetTransformedPos(e.GetCurrentPoint(preview));

                Point realPoint = GetRealPoint(pointPos);

                if(realPoint.X>= selectStartPos.X) {
                    selectRectangle.Width = realPoint.X - selectStartPos.X;
                } else {
                    selectRectangle.X = realPoint.X;
                    selectRectangle.Width = selectStartPos.X - realPoint.X;
                }
                if (realPoint.Y >= selectStartPos.Y) {
                    selectRectangle.Height = realPoint.Y - selectStartPos.Y;
                } else {
                    selectRectangle.Y = realPoint.Y;
                    selectRectangle.Height = selectStartPos.Y - realPoint.Y;
                }

                var curPos = pointPos;
                if (curPos.X >= startPos.X) {
                    selectBox.Width = curPos.X - startPos.X;
                } else {
                    Canvas.SetLeft(selectBox, curPos.X);
                    selectBox.Width = startPos.X - curPos.X;
                }
                if (curPos.Y >= startPos.Y) {
                    selectBox.Height = curPos.Y - startPos.Y;
                } else {
                    Canvas.SetTop(selectBox, curPos.Y);
                    selectBox.Height = startPos.Y - curPos.Y;
                }
            }
        }

        private void onImageRelease(object sender, PointerRoutedEventArgs e) {
            if (selectMode) {
                selectRectangle.X = Math.Min(selectRectangle.X, screenShot.Bitmap.PixelWidth - 1);
                selectRectangle.Y = Math.Min(selectRectangle.Y, screenShot.Bitmap.PixelHeight - 1);
                selectRectangle.Width = Math.Max(selectRectangle.Width, 1);
                selectRectangle.Height = Math.Max(selectRectangle.Height, 1);
//                Debug.WriteLine(selectRectangle.Left+":"+selectRectangle.Top+", "+selectRectangle.Right+":"+selectRectangle.Bottom);
                UpdateCroppedImage();
            }
            selectMode = false;
        }

        private async void UpdateCroppedImage() {
            var img = (WriteableBitmap)preview.Source;
            var croppedImage = await screenShot.GetCroppedBitmapAsync(selectRectangle);
            cropimage.Source = croppedImage;
            RunOCR();
        }

        private Rect GetTransformedSelectRectangle() {
            var img = (WriteableBitmap)preview.Source;
            if (img == null) {
                return new Rect();
            }
            return new Rect(
                selectRectangle.X/ img.PixelWidth*preview.ActualWidth,
                selectRectangle.Y / img.PixelHeight * preview.ActualHeight,
                selectRectangle.Width / img.PixelWidth * preview.ActualWidth,
                selectRectangle.Height / img.PixelHeight * preview.ActualHeight
                );
        }

        private void RecalcSelectRectangle() {
            var transform = preview.TransformToVisual(Window.Current.Content);
            var rect = transform.TransformBounds(GetTransformedSelectRectangle());
            Canvas.SetLeft(selectBox, rect.X);
            Canvas.SetTop(selectBox, rect.Y);
            selectBox.Width = rect.Width;
            selectBox.Height = rect.Height;
        }

        private void onSizeChanged(object sender, SizeChangedEventArgs e) {
            RecalcSelectRectangle();
        }

        private void onSelectBoxMove(object sender, PointerRoutedEventArgs e) {
            if(selectMode) {
                onImageMove(sender, e);
            }
        }
    }
}
