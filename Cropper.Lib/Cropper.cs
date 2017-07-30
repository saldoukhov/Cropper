using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using SkiaSharp;

namespace Cropper.Lib
{
    internal class CropCalculator
    {
        internal static IObservable<SKRect> SelectRectangle(IObservable<CropHelper.MouseEvent> mouseEvents)
        {
            return Observable.Create<SKRect>(observer =>
            {
                var dropCoords = new SKRect(20, 20, 120, 120);
                observer.OnNext(dropCoords);

                var dragStarts = mouseEvents
                    .Where(x => x.EventType == CropHelper.MouseEventType.Press)
                    .Select(x => TestHitPoint(dropCoords, x.Coords))
                    .Where(x => x != null);
                var releases = mouseEvents
                    .Where(x => x.EventType == CropHelper.MouseEventType.Release)
                    .Select(x => x.Coords);
                var moves = mouseEvents
                    .Where(x => x.EventType == CropHelper.MouseEventType.Move)
                    .Select(x => x.Coords);

                var drags = dragStarts
                    .SelectMany(x => moves
                        .TakeUntil(releases)
                        .Select(y => NewCoords(dropCoords, new Drag(x, y))))
                    .DistinctUntilChanged();

                var dropSub = drags
                    .Sample(releases)
                    .Subscribe(x => { dropCoords = x; });

                var dragSub = drags
                    .Subscribe(observer);

                return new CompositeDisposable(dragSub, dropSub);
            });
        }

        static HitPoint TestHitPoint(SKRect target, SKPoint coords)
        {
            if (WithinCorner(new SKPoint(target.Left, target.Top), coords))
                return new HitPoint(DragType.TopLeft, coords);
            if (WithinCorner(new SKPoint(target.Left, target.Bottom), coords))
                return new HitPoint(DragType.BottomLeft, coords);
            if (WithinCorner(new SKPoint(target.Right, target.Top), coords))
                return new HitPoint(DragType.TopRight, coords);
            if (WithinCorner(new SKPoint(target.Right, target.Bottom), coords))
                return new HitPoint(DragType.BottomRight, coords);
            return target.Contains(coords)
                ? new HitPoint(DragType.Drag, coords)
                : null;
        }

        static bool WithinCorner(SKPoint target, SKPoint coords)
        {
            return Math.Abs(coords.X - target.X) < 10 && Math.Abs(coords.Y - target.Y) < 10;
        }

        enum DragType
        {
            Drag,
            TopLeft,
            BottomLeft,
            TopRight,
            BottomRight
        }

        class HitPoint
        {
            public DragType DragType { get; }
            public SKPoint Coords { get; }

            public HitPoint(DragType dragType, SKPoint coords)
            {
                DragType = dragType;
                Coords = coords;
            }
        }

        class Drag
        {
            public HitPoint Start { get; }
            public SKPoint Delta { get; }

            public Drag(HitPoint start, SKPoint end)
            {
                Start = start;
                Delta = end - Start.Coords;
            }
        }

        static SKRect NewCoords(SKRect current, Drag drag)
        {
            float diag;
            switch (drag.Start.DragType)
            {
                case DragType.Drag:
                    return SKRect.Create(current.Location + drag.Delta, current.Size);
                case DragType.TopLeft:
                    diag = Math.Max(-drag.Delta.X, -drag.Delta.Y);
                    return new SKRect(current.Left - diag, current.Top - diag, current.Right, current.Bottom);
                case DragType.BottomLeft:
                    diag = Math.Max(-drag.Delta.X, drag.Delta.Y);
                    return new SKRect(current.Left - diag, current.Top, current.Right, current.Bottom + diag);
                case DragType.TopRight:
                    diag = Math.Max(drag.Delta.X, -drag.Delta.Y);
                    return new SKRect(current.Left, current.Top - diag, current.Right + diag, current.Bottom);
                case DragType.BottomRight:
                    diag = Math.Max(drag.Delta.X, drag.Delta.Y);
                    return new SKRect(current.Left, current.Top, current.Right + diag, current.Bottom + diag);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public static class CropHelper
    {
        public enum MouseEventType
        {
            Press,
            Release,
            Move
        }

        public class MouseEvent
        {
            public MouseEventType EventType { get; }
            public SKPoint Coords { get; }

            public MouseEvent(MouseEventType eventType, SKPoint coords)
            {
                EventType = eventType;
                Coords = coords;
            }
        }

        static void DrawCropArea(SKRect rect, SKCanvas canvas)
        {
            canvas.DrawColor(new SKColor(130, 130, 130));
            canvas.DrawRect(rect, new SKPaint
            {
                Color = new SKColor(20, 20, 20)
            });
        }

        public static IObservable<SKRect> CropImage(
            IObservable<MouseEvent> mouseEvents,
            Func<IObservable<SKCanvas>> canvasSelector)
        {
            return CropCalculator
                .SelectRectangle(mouseEvents)
                .Select(rect => canvasSelector()
                    .Do(canvas => DrawCropArea(rect, canvas))
                    .Select(_ => rect))
                .Switch();
        }
    }
}