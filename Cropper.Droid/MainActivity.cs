using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Android.App;
using Android.Views;
using Android.OS;
using Cropper.Lib;
using SkiaSharp;
using SkiaSharp.Views.Android;

namespace Cropper.Droid
{
    [Activity(Label = "Cropper.Droid", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        static readonly Dictionary<MotionEventActions, CropHelper.MouseEventType> ActionMap =
            new Dictionary<MotionEventActions, CropHelper.MouseEventType>
            {
                {MotionEventActions.Down, CropHelper.MouseEventType.Press},
                {MotionEventActions.Up, CropHelper.MouseEventType.Release},
                {MotionEventActions.Move, CropHelper.MouseEventType.Move}
            };

        readonly Subject<SKCanvas> whenNeedPaint = new Subject<SKCanvas>();
        readonly Subject<CropHelper.MouseEvent> whenMouseEvent = new Subject<CropHelper.MouseEvent>();
        SKCanvasView canvasView;
        IDisposable cropSub;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.Main);

            canvasView = FindViewById<SKCanvasView>(Resource.Id.canvasView);
            canvasView.PaintSurface += OnPaintSurface;
            canvasView.Touch += CanvasViewTouch;
            canvasView.LayoutChange += CanvasView_LayoutChange;
        }

        void CanvasView_LayoutChange(object sender, View.LayoutChangeEventArgs e)
        {
            if (cropSub != null)
                return;
            cropSub = CropHelper
                .CropImage(
                    canvasView.CanvasSize,
                    whenMouseEvent,
                    () =>
                    {
                        canvasView.Invalidate();
                        return whenNeedPaint.Take(1);
                    })
                .Subscribe();
        }

        void CanvasViewTouch(object sender, View.TouchEventArgs e)
        {
            if (!ActionMap.TryGetValue(e.Event.Action, out CropHelper.MouseEventType eventType))
                return;
            var point = new SKPoint(e.Event.GetX(), e.Event.GetY());
            whenMouseEvent.OnNext(new CropHelper.MouseEvent(eventType, point));
        }

        void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            whenNeedPaint.OnNext(e.Surface.Canvas);
        }
    }
}