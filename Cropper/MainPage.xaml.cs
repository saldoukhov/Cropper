using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Cropper
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();
            var presses = Observable
                .FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerPressed += h,
                    h => Container.PointerPressed -= h)
                .Select(x => new MouseEvent(MouseEventType.Press, x.EventArgs.GetCurrentPoint(Container).Position));
            var releases = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerReleased += h,
                    h => Container.PointerReleased -= h)
                .Select(x => new MouseEvent(MouseEventType.Release, x.EventArgs.GetCurrentPoint(Container).Position));
            var moves = Observable.FromEventPattern<PointerEventHandler, PointerRoutedEventArgs>(
                    h => Container.PointerMoved += h,
                    h => Container.PointerMoved -= h)
                .Select(x => new MouseEvent(MouseEventType.Move, x.EventArgs.GetCurrentPoint(Container).Position));

            var mouseEvents = Observable.Merge(presses, releases, moves);
            SelectRectangle(mouseEvents).Subscribe(PositionTarget);
        }

        static IObservable<Rect> SelectRectangle(IObservable<MouseEvent> mouseEvents)
        {
            return Observable.Create<Rect>(observer =>
            {
                var dropCoords = new Rect(20, 20, 120, 120);
                observer.OnNext(dropCoords);

                var dragStarts = mouseEvents
                    .Where(x => x.EventType == MouseEventType.Press)
                    .Select(x => TestHitPoint(dropCoords, x.Coords))
                    .Where(x => x != null);
                var releases = mouseEvents
                    .Where(x => x.EventType == MouseEventType.Release)
                    .Select(x => x.Coords);
                var moves = mouseEvents
                    .Where(x => x.EventType == MouseEventType.Move)
                    .Select(x => x.Coords);

                var drags = dragStarts
                    .SelectMany(x => moves
                        .TakeUntil(releases)
                        .Select(y => NewCoords(dropCoords, new Drag(x, y))))
                    .DistinctUntilChanged();

                var dropSub = drags.Sample(releases)
                    .Subscribe(x => { dropCoords = x; });

                var dragSub = drags
                    .Subscribe(observer);

                return new CompositeDisposable(dragSub, dropSub); 
            });
        }

        static Rect NewCoords(Rect current, Drag delta)
        {
            double diag;
            switch (delta.Start.DragType)
            {
                case DragType.Drag:
                    return new Rect(current.X + delta.Width, current.Y + delta.Height, current.Width, current.Height);
                case DragType.TopLeft:
                    diag = Math.Max(-delta.Width, -delta.Height);
                    return new Rect(current.X -diag, current.Y -diag, current.Width + diag, current.Height + diag);
                case DragType.BottomLeft:
                    diag = Math.Max(-delta.Width, delta.Height);
                    return new Rect(current.X - diag, current.Y, current.Width + diag, current.Height + diag);
                case DragType.TopRight:
                    diag = Math.Max(delta.Width, -delta.Height);
                    return new Rect(current.X, current.Y - diag, current.Width + diag, current.Height + diag);
                case DragType.BottomRight:
                    diag = Math.Max(delta.Width, delta.Height);
                    return new Rect(current.X, current.Y, current.Width + diag, current.Height + diag);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static HitPoint TestHitPoint(Rect target, Point coords)
        {
            if (WithinCorner(new Point(target.Left, target.Top), coords))
                return new HitPoint(DragType.TopLeft, coords);
            if (WithinCorner(new Point(target.Left, target.Bottom), coords))
                return new HitPoint(DragType.BottomLeft, coords);
            if (WithinCorner(new Point(target.Right, target.Top), coords))
                return new HitPoint(DragType.TopRight, coords);
            if (WithinCorner(new Point(target.Right, target.Bottom), coords))
                return new HitPoint(DragType.BottomRight, coords);
            return target.Contains(coords) 
                ? new HitPoint(DragType.Drag, coords) 
                : null;
        }

        static bool WithinCorner(Point target, Point coords)
        {
            return Math.Abs(coords.X - target.X) < 10 && Math.Abs(coords.Y - target.Y) < 10;
        }

        public enum MouseEventType
        {
            Press,
            Release,
            Move
        }

        public enum DragType
        {
            Drag,
            TopLeft,
            BottomLeft,
            TopRight,
            BottomRight
        }

        public class HitPoint
        {
            public DragType DragType { get; }
            public Point Coords { get; }

            public HitPoint(DragType dragType, Point coords)
            {
                DragType = dragType;
                Coords = coords;
            }
        }

        public class MouseEvent
        {
            public MouseEventType EventType { get; }
            public Point Coords { get; }

            public MouseEvent(MouseEventType eventType, Point coords)
            {
                EventType = eventType;
                Coords = coords;
            }
        }

        public class Drag
        {
            public HitPoint Start { get; }
            public Point End { get; }
            public double Width => End.X - Start.Coords.X;
            public double Height => End.Y - Start.Coords.Y;

            public Drag(HitPoint start, Point end)
            {
                Start = start;
                End = end;
            }
        }

        void PositionTarget(Rect toRect)
        {
            Canvas.SetLeft(Target, toRect.X);
            Canvas.SetTop(Target, toRect.Y);
            Target.Width = toRect.Width;
            Target.Height = toRect.Height;
        }
    }
}