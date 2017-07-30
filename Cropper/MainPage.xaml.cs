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

        public MainPage()
        {
            InitializeComponent();
            var presses = Observable
                .FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerPressed += h,
                    h => Container.PointerPressed -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Press, x.EventArgs.GetCurrentPoint(Container).Position.ToSKPoint()));
            var releases = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerReleased += h,
                    h => Container.PointerReleased -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Release, x.EventArgs.GetCurrentPoint(Container).Position.ToSKPoint()));
            var moves = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerMoved += h,
                    h => Container.PointerMoved -= h)
                .Select(x => new CropHelper.MouseEvent(CropHelper.MouseEventType.Move, x.EventArgs.GetCurrentPoint(Container).Position.ToSKPoint()));

            var mouseEvents = Observable.Merge(presses, releases, moves);
            CropHelper
                .CropImage(mouseEvents, () =>
                {
                    Container.Invalidate();
                    return whenNeedPaint.Take(1);
                })
                .Subscribe();
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