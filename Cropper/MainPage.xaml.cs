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
        readonly IObservable<CropHelper.MouseEvent> mouseEvents;
        IDisposable cropSub;
        SKBitmap img;

        public MainPage()
        {
            InitializeComponent();
            var presses = Observable
                .FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerPressed += h,
                    h => Container.PointerPressed -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Press,
                    x.EventArgs.GetCurrentPoint(Container).Position.ToSKPoint()));
            var releases = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerReleased += h,
                    h => Container.PointerReleased -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Release,
                    x.EventArgs.GetCurrentPoint(Container).Position.ToSKPoint()));
            var moves = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerMoved += h,
                    h => Container.PointerMoved -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Move,
                    x.EventArgs.GetCurrentPoint(Container).Position.ToSKPoint()));

            mouseEvents = Observable.Merge(presses, releases, moves);
            Container.LayoutUpdated += Container_LayoutUpdated;

            img = SKBitmap.Decode("kitten.jpg");
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