﻿using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using SkiaSharp;

namespace Cropper.Lib
{
    internal class CropCalculator
    {
        internal static IObservable<SKRect> SelectRectangle(SKSize canvasSize, IObservable<CropHelper.MouseEvent> mouseEvents)
        {
            return Observable.Create<SKRect>(observer =>
            {
                // init base size for cropping region
                var dropCoords = SKRect.Create(canvasSize);
                dropCoords.Inflate(-canvasSize.Width / 4, -canvasSize.Height / 4);
                var minDimension = Math.Min(dropCoords.Width, dropCoords.Height);
                var cornerMargin = minDimension / 10;
                dropCoords = dropCoords.AspectFit(new SKSize(minDimension, minDimension));
                observer.OnNext(dropCoords);

                var dragStarts = mouseEvents
                    .Where(x => x.EventType == CropHelper.MouseEventType.Press)
                    .Select(x => TestHitPoint(dropCoords, x.Coords, cornerMargin))
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

        static HitPoint TestHitPoint(SKRect target, SKPoint coords, float margin)
        {
            if (WithinCorner(new SKPoint(target.Left, target.Top), coords, margin))
                return new HitPoint(DragType.TopLeft, coords);
            if (WithinCorner(new SKPoint(target.Left, target.Bottom), coords, margin))
                return new HitPoint(DragType.BottomLeft, coords);
            if (WithinCorner(new SKPoint(target.Right, target.Top), coords, margin))
                return new HitPoint(DragType.TopRight, coords);
            if (WithinCorner(new SKPoint(target.Right, target.Bottom), coords, margin))
                return new HitPoint(DragType.BottomRight, coords);
            return target.Contains(coords)
                ? new HitPoint(DragType.Drag, coords)
                : null;
        }

        static bool WithinCorner(SKPoint target, SKPoint coords, float margin)
        {
            return Math.Abs(coords.X - target.X) < margin && Math.Abs(coords.Y - target.Y) < margin;
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
            canvas.Flush();
        }

        public static IObservable<SKRect> CropImage(
            SKSize canvasSize,
            IObservable<MouseEvent> mouseEvents,
            Func<IObservable<SKCanvas>> canvasSelector)
        {
            return CropCalculator
                .SelectRectangle(canvasSize, mouseEvents)
                .Select(rect => canvasSelector()
                    .Do(canvas => DrawCropArea(rect, canvas))
                    .Select(_ => rect))
                .Switch();
        }
    }
}