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
    public double ZoomCenterOffsetX { get; set; } = 0;
    public double ZoomCenterOffsetY { get; set; } = 0;

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

        var activeBox = CroppingPane.CurrentlySelectedBox;
        if (activeBox != null)
        {
            StartWorkspaceScale = activeBox.StartWorkspaceScale;

            StartTranslationX = activeBox.StartTranslationX;
            StartTranslationY = activeBox.StartTranslationY;
            CurrentTranslationX = activeBox.CurrentTranslationX;
            CurrentTranslationY = activeBox.CurrentTranslationY;

            InitialPositionX = activeBox.TranslationX;
            InitialPositionY = activeBox.TranslationY;
            StartWorkspaceScale = activeBox.StartWorkspaceScale;

            double activeZoomFactor = activeBox.WidthRequest / activeBox.InitialScaleWidth;
            if (double.IsNaN(activeZoomFactor) || activeZoomFactor <= 0) activeZoomFactor = 1.0;

            InitialScaleWidth /= activeZoomFactor;
            InitialScaleHeight /= activeZoomFactor;
        }
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
        CroppingPane.CurrentInstance.OnWorkspacePinched(sender, e);
    }

    private void OnWorkspacePanned(object? sender, PanUpdatedEventArgs e)
    {
        if (CurrentWorkspaceScale > 1 && Width >= CroppingPane.CurrentInstance.OverlayCanvasWidth - 100 && Height >= CroppingPane.CurrentInstance.OverlayCanvasHeight - 100)
            CroppingPane.CurrentInstance.OnWorkspacePanned(sender, e);
    }

    public void UpdateCropBoxHeight(double height, double width = 0)
    {
        if (height == 420)
            WidthRequest = 360;
        else
            WidthRequest = width > 0 ? width : Border.Width;

        height = height + 1;

        HeightRequest = height;

        Border.HeightRequest = height;
        Border.WidthRequest = WidthRequest;
        MoveSurfaceBody.HeightRequest = height;
        MoveSurfaceBody.WidthRequest = WidthRequest;
    }
}