using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Cropper.Lib;
using SkiaSharp;
using SkiaSharp.Views.UWP;

namespace Cropper
{
    public sealed partial class MainPage
    {
        readonly Subject<SKCanvas> whenNeedPaint = new Subject<SKCanvas>();
        IDisposable cropSub;
        readonly SKBitmap img;
        readonly IObservable<CropHelper.MouseEvent> mouseEvents;
        readonly double scaleFactor;

        public MainPage()
        {
            InitializeComponent();
            scaleFactor = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            var presses = Observable
                .FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerPressed += h,
                    h => Container.PointerPressed -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Press, GetCurrentPoint(x.EventArgs)));
            var releases = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerReleased += h,
                    h => Container.PointerReleased -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Release, GetCurrentPoint(x.EventArgs)));
            var moves = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerMoved += h,
                    h => Container.PointerMoved -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Move, GetCurrentPoint(x.EventArgs)));

            mouseEvents = Observable.Merge(presses, releases, moves);
            Container.LayoutUpdated += Container_LayoutUpdated;

            img = SKBitmap.Decode("kitten.jpg");
        }

        SKPoint GetCurrentPoint(PointerRoutedEventArgs args)
        {
            var position = args.GetCurrentPoint(Container).Position;
            return new SKPoint((float) (position.X * scaleFactor), (float) (position.Y * scaleFactor));
        }

        void Container_LayoutUpdated(object sender, object e)
        {
            if (!Container.CanvasSize.IsEmpty && cropSub == null)
            {
                cropSub = CropHelper
                    .CropImage(
                        Container.CanvasSize,
                        img,
                        mouseEvents,
                        () =>
                        {
                            Container.Invalidate();
                            return whenNeedPaint.Take(1);
                        })
                    .Subscribe();
            }
        }

        void Canvas_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            whenNeedPaint.OnNext(e.Surface.Canvas);
        }

        void ButtonClear_OnClick(object sender, RoutedEventArgs e)
        {
        }
    }
}