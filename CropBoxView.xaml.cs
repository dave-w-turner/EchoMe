using EchoMe.Database;
using EchoMe.ExtensionMethods;

namespace EchoMe.UserControls;

public partial class CropBoxView : ContentView
{
    private double _lastPanDeltaX = 0;
    private double _lastPanDeltaY = 0;
    private double _lastMoveDeltaX = 0;
    private double _lastMoveDeltaY = 0;
    private double _resizeAnchorLeftEdge = 0;
    private double _resizeAnchorTopEdge = 0;
    public static double CurrentWorkspaceScale { get; set; } = 1.0;
    public double CurrentTranslationX { get; set; } = 0;
    public double CurrentTranslationY { get; set; } = 0;
    public double StartTranslationX { get; set; } = 0;
    public double StartTranslationY { get; set; } = 0;
    public double InitialScaleWidth { get; set; } = 100;
    public double InitialScaleHeight { get; set; } = 100;
    public double InitialPositionX { get; set; } = 0;
    public double InitialPositionY { get; set; } = 0;
    public double StartWorkspaceScale { get; set; } = 1;

    public string Name { get; private set; }

    public int? Id { get; set; } = null;

    public CropBoxView()
    {
        InitializeComponent();
    }

    public CropBoxView(string name, CommunicationCard? existingItem = null) : this()
    {
        Name = name;
        Id = existingItem?.Id;
    }

    public void UpdateSpeachText(string text)
    {
        Name = text;
    }

    private void OnContentViewLoaded(object sender, EventArgs e)
    {        
        BatchBegin();
        CropPhotoPage.CurrentInstance.UpdateView();
        BatchCommit();
    }

    private void OnBoxMoved(object? sender, PanUpdatedEventArgs e)
    {
        var targetImage = ElementExtensions.FindAncestorByName<Grid>(this, "CanvasContainer")
                           ?.FindByName<Image>("CapturedRawPhoto");

        double canvasMaxWidth = targetImage?.Width > 10 ? targetImage.Width : 360;
        double canvasMaxHeight = targetImage?.Height > 10 ? targetImage.Height : 420;

        double currentBoxWidth = WidthRequest > 0 ? WidthRequest : (Width > 0 ? Width : canvasMaxWidth);
        double currentBoxHeight = HeightRequest > 0 ? HeightRequest : (Height > 0 ? Height : canvasMaxHeight);

        if (currentBoxWidth >= canvasMaxWidth && currentBoxHeight >= canvasMaxHeight)
        {
            OnWorkspacePanned(sender, e);
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lastMoveDeltaX = 0;
                _lastMoveDeltaY = 0;
                break;

            case GestureStatus.Running:
                double incrementalX = e.TotalX - _lastMoveDeltaX;
                double incrementalY = e.TotalY - _lastMoveDeltaY;

                _lastMoveDeltaX = e.TotalX;
                _lastMoveDeltaY = e.TotalY;

                double maxAllowedX = canvasMaxWidth - currentBoxWidth;
                double maxAllowedY = canvasMaxHeight - currentBoxHeight;

                double minClampedX = -(maxAllowedX / 2);
                double maxClampedX = maxAllowedX - (maxAllowedX / 2);
                double minClampedY = -(maxAllowedY / 2);
                double maxClampedY = maxAllowedY - (maxAllowedY / 2);

                double targetTranslationX = TranslationX + incrementalX;
                double targetTranslationY = TranslationY + incrementalY;
                double clampedY, clampedX;

                if (maxClampedX < minClampedX)
                    clampedX = minClampedX;
                else
                    clampedX = Math.Clamp(targetTranslationX, minClampedX, maxClampedX);

                if (maxClampedY < minClampedY)
                    clampedY = minClampedY;
                else
                    clampedY = Math.Clamp(targetTranslationY, minClampedY, maxClampedY);

                BatchBegin();

                TranslationX = clampedX;
                TranslationY = clampedY;

                CurrentTranslationX = clampedX;
                CurrentTranslationY = clampedY;

                BatchCommit();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _lastMoveDeltaX = 0;
                _lastMoveDeltaY = 0;
                break;
        }
    }

    private void OnBoxResized(object? sender, PanUpdatedEventArgs e)
    {
        if (this != CroppingPane.CurrentlySelectedBox)
        {
            CroppingPane.SelectBox(this);
        }

        var targetImage = ElementExtensions.FindAncestorByName<Grid>(this, "CanvasContainer")
                           ?.FindByName<Image>("CapturedRawPhoto");

        double canvasMaxWidth = targetImage?.Width > 10 ? targetImage.Width : 360;
        double canvasMaxHeight = targetImage?.Height > 10 ? targetImage.Height : 420;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lastPanDeltaX = 0;
                _lastPanDeltaY = 0;

                double initialWidth = WidthRequest > 10 ? WidthRequest : (Width > 10 ? Width : 100);
                double initialHeight = HeightRequest > 10 ? HeightRequest : (Height > 10 ? Height : 100);

                _resizeAnchorLeftEdge = (canvasMaxWidth - initialWidth) / 2 + TranslationX;
                _resizeAnchorTopEdge = (canvasMaxHeight - initialHeight) / 2 + TranslationY;
                break;

            case GestureStatus.Running:
                double incrementalDeltaX = e.TotalX - _lastPanDeltaX;
                double incrementalDeltaY = e.TotalY - _lastPanDeltaY;

                _lastPanDeltaX = e.TotalX;
                _lastPanDeltaY = e.TotalY;

                double baseWidth = WidthRequest > 10 ? WidthRequest : (Width > 10 ? Width : 100);
                double baseHeight = HeightRequest > 10 ? HeightRequest : (Height > 10 ? Height : 100);

                double targetWidth = baseWidth + incrementalDeltaX;
                double targetHeight = baseHeight + incrementalDeltaY;

                double absoluteMaxWidth = canvasMaxWidth - _resizeAnchorLeftEdge;
                double absoluteMaxHeight = canvasMaxHeight - _resizeAnchorTopEdge;

                targetWidth = Math.Clamp(targetWidth, 50, absoluteMaxWidth);
                targetHeight = Math.Clamp(targetHeight, 50, absoluteMaxHeight);

                double targetTranslationX = _resizeAnchorLeftEdge - (canvasMaxWidth - targetWidth) / 2;
                double targetTranslationY = _resizeAnchorTopEdge - (canvasMaxHeight - targetHeight) / 2;

                double currentRightEdge = _resizeAnchorLeftEdge + targetWidth;
                double currentBottomEdge = _resizeAnchorTopEdge + targetHeight;

                if (incrementalDeltaX > 1.0 && (canvasMaxWidth - currentRightEdge) < 15)
                {
                    targetWidth = canvasMaxWidth - _resizeAnchorLeftEdge;
                    targetTranslationX = _resizeAnchorLeftEdge - (canvasMaxWidth - targetWidth) / 2;
                }
                if (incrementalDeltaY > 1.0 && (canvasMaxHeight - currentBottomEdge) < 15)
                {
                    targetHeight = canvasMaxHeight - _resizeAnchorTopEdge;
                    targetTranslationY = _resizeAnchorTopEdge - (canvasMaxHeight - targetHeight) / 2;
                }

                targetWidth = Math.Clamp(targetWidth, 50, canvasMaxWidth);
                targetHeight = Math.Clamp(targetHeight, 50, canvasMaxHeight);

                BatchBegin();

                WidthRequest = targetWidth;
                MoveSurfaceBody.WidthRequest = targetWidth;
                HeightRequest = targetHeight;
                MoveSurfaceBody.HeightRequest = targetHeight;

                Border.WidthRequest = targetWidth;
                Border.HeightRequest = targetHeight + 1;

                TranslationX = targetTranslationX;
                TranslationY = targetTranslationY;

                CurrentTranslationX = targetTranslationX;
                CurrentTranslationY = targetTranslationY;

                BatchCommit();
                InvalidateMeasure();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _lastPanDeltaX = 0;
                _lastPanDeltaY = 0;
                break;
        }
    }

    private void OnWorkspaceTapped(object sender, TappedEventArgs e)
    {
        var tappedBox = ElementExtensions.FindAncestor<CropBoxView>((Grid)sender);

        if (tappedBox != null && tappedBox != CroppingPane.CurrentlySelectedBox)
        {
            CroppingPane.SelectBox(tappedBox);
        }
    }

    private void OnWorkspacePinched(object? sender, PinchGestureUpdatedEventArgs e)
    {
        //var targetImage = ElementExtensions.FindAncestorByName<Grid>(this, "CanvasContainer")
        //   ?.FindByName<Image>("CapturedRawPhoto");

        //if (targetImage == null || targetImage.Width <= 10 || targetImage.Height <= 10) return;
        //switch (e.Status)
        //{
        //    case GestureStatus.Started:
        //        Point fingerMidPoint = e.ScaleOrigin;

        //        StartWorkspaceScale = targetImage.Scale;
        //        targetImage.AnchorX = Math.Clamp(fingerMidPoint.X, 0.0, 1.0);
        //        targetImage.AnchorY = Math.Clamp(fingerMidPoint.Y, 0.0, 1.0);

        //        foreach (var boxPair in CroppingPane.Boxes)
        //        {
        //            CropBoxView box = boxPair.Key;
        //            box.InitialScaleWidth = box.WidthRequest > 10 ? box.WidthRequest : box.Width;
        //            box.InitialScaleHeight = box.HeightRequest > 10 ? box.HeightRequest : box.Height;
        //            box.InitialPositionX = box.TranslationX;
        //            box.InitialPositionY = box.TranslationY;
        //        }
        //        break;

        //    case GestureStatus.Running:
        //        double incrementalScale = targetImage.Scale * e.Scale;
        //        double validScale = Math.Clamp(incrementalScale, 1.0, 5.0);

        //        CurrentWorkspaceScale = validScale;

        //        double maxPanX = targetImage.Width * (validScale - 1.0) / 2;
        //        double maxPanY = targetImage.Height * (validScale - 1.0) / 2;

        //        double currentTransX = CurrentTranslationX;
        //        double currentTransY = CurrentTranslationY;

        //        currentTransX = Math.Clamp(currentTransX, -maxPanX, maxPanX);
        //        currentTransY = Math.Clamp(currentTransY, -maxPanY, maxPanY);

        //        targetImage.BatchBegin();
        //        targetImage.Scale = validScale;
        //        targetImage.TranslationX = currentTransX;
        //        targetImage.TranslationY = currentTransY;
        //        targetImage.BatchCommit();

        //        double zoomRatioDelta = validScale / StartWorkspaceScale;

        //        foreach (var boxPair in CroppingPane.Boxes)
        //        {
        //            CropBoxView box = boxPair.Key;

        //            box.BatchBegin();

        //            box.AnchorX = targetImage.AnchorX;
        //            box.AnchorY = targetImage.AnchorY;

        //            double targetBoxWidth = box.InitialScaleWidth * zoomRatioDelta;
        //            double targetBoxHeight = box.InitialScaleHeight * zoomRatioDelta;

        //            box.UpdateCropBoxHeight(targetBoxHeight, targetBoxWidth);

        //            box.TranslationX = (box.InitialPositionX * zoomRatioDelta) + (currentTransX - StartTranslationX);
        //            box.TranslationY = (box.InitialPositionY * zoomRatioDelta) + (currentTransY - StartTranslationY);

        //            box.BatchCommit();
        //        }
        //        break;
        //}
    }

    private void OnWorkspacePanned(object? sender, PanUpdatedEventArgs e)
    {
        //var targetImage = ElementExtensions.FindAncestorByName<Grid>(this, "CanvasContainer")
        //    ?.FindByName<Image>("CapturedRawPhoto");

        //if (targetImage == null || targetImage.Width <= 10 || targetImage.Height <= 10 || (Border.Height < targetImage.Height && Border.Width < targetImage.Width)) return;

        //if (CurrentWorkspaceScale <= 1.02)
        //{
        //    targetImage.BatchBegin();
        //    targetImage.TranslationX = 0;
        //    targetImage.TranslationY = 0;
        //    targetImage.BatchCommit();

        //    foreach (var boxPair in CroppingPane.Boxes)
        //    {
        //        boxPair.Key.BatchBegin();
        //        boxPair.Key.TranslationX = 0;
        //        boxPair.Key.TranslationY = 0;
        //        boxPair.Key.BatchCommit();
        //    }

        //    CurrentTranslationX = 0;
        //    CurrentTranslationY = 0;
        //    StartTranslationX = 0;
        //    StartTranslationY = 0;
        //    return;
        //}

        //switch (e.StatusType)
        //{
        //    case GestureStatus.Started:
        //        StartTranslationX = targetImage.TranslationX;
        //        StartTranslationY = targetImage.TranslationY;

        //        foreach (var boxPair in CroppingPane.Boxes)
        //        {
        //            CropBoxView box = boxPair.Key;
        //            box.InitialScaleWidth = box.WidthRequest > 10 ? box.WidthRequest : box.Width;
        //            box.InitialScaleHeight = box.HeightRequest > 10 ? box.HeightRequest : box.Height;
        //            box.InitialPositionX = box.TranslationX;
        //            box.InitialPositionY = box.TranslationY;
        //        }
        //        break;

        //    case GestureStatus.Running:
        //        double baseStartX = StartTranslationX;
        //        double baseStartY = StartTranslationY;

        //        double targetX = baseStartX + e.TotalX;
        //        double targetY = baseStartY + e.TotalY;

        //        double maxPanX = targetImage.Width * (CurrentWorkspaceScale - 1) / 2;
        //        double maxPanY = targetImage.Height * (CurrentWorkspaceScale - 1) / 2;

        //        double clampedX = Math.Clamp(targetX, -maxPanX, maxPanX);
        //        double clampedY = Math.Clamp(targetY, -maxPanY, maxPanY);

        //        CurrentTranslationX = clampedX;
        //        CurrentTranslationY = clampedY;

        //        targetImage.BatchBegin();
        //        targetImage.TranslationX = clampedX;
        //        targetImage.TranslationY = clampedY;
        //        targetImage.BatchCommit();

        //        double panRatioDelta = 1.0;

        //        foreach (var boxPair in CroppingPane.Boxes)
        //        {
        //            CropBoxView box = boxPair.Key;

        //            box.BatchBegin();

        //            box.AnchorX = targetImage.AnchorX;
        //            box.AnchorY = targetImage.AnchorY;

        //            // With baseStartX properly initialized, this delta calculates smooth dragging movement
        //            box.TranslationX = (box.InitialPositionX * panRatioDelta) + (clampedX - baseStartX);
        //            box.TranslationY = (box.InitialPositionY * panRatioDelta) + (clampedY - baseStartY);

        //            box.BatchCommit();
        //        }
        //        break;
        //}
    }

    public void UpdateCropBoxHeight(double height, double width = 0)
    {
        if (height == 420)
            WidthRequest = 360;
        else
            WidthRequest = width > 0 ? width : Border.Width;

        height = height + 1;

        HeightRequest = height;
        TranslationY = Math.Clamp(-Y, 0, int.MaxValue);
        TranslationX = Math.Clamp(-X, 0, int.MaxValue);

        Border.HeightRequest = height;
        Border.WidthRequest = WidthRequest;
        MoveSurfaceBody.HeightRequest = height;
        MoveSurfaceBody.WidthRequest = WidthRequest;
    }
}