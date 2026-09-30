using EchoMe.Database;
using Microsoft.Maui.Layouts;

namespace EchoMe.UserControls;

public partial class CroppingPane : ContentView
{
    private bool _hasInitializedDefaultBox = false;
    public double StartWorkspaceScale = 1;
    private int _boxCounter = 0;

    public static Dictionary<CropBoxView, string> Boxes { get; private set; } = [];
    public static CropBoxView? CurrentlySelectedBox { get; private set; }

    public static event EventHandler<CropBoxView> SelectedBoxChanged;

    public static readonly BindableProperty OverlayCanvasWidthProperty =
        BindableProperty.Create(nameof(OverlayCanvasWidth), typeof(double), typeof(CroppingPane), 0.0);

    public double OverlayCanvasWidth
    {
        get => (double)GetValue(OverlayCanvasWidthProperty);
        set => SetValue(OverlayCanvasWidthProperty, value);
    }

    public static readonly BindableProperty OverlayCanvasHeightProperty =
        BindableProperty.Create(nameof(OverlayCanvasHeight), typeof(double), typeof(CroppingPane), 0.0);

    public double OverlayCanvasHeight
    {
        get => (double)GetValue(OverlayCanvasHeightProperty);
        set => SetValue(OverlayCanvasHeightProperty, value);
    }

    public static CroppingPane CurrentInstance { get; private set; }

    public static readonly BindableProperty ImageSourceProperty =
        BindableProperty.Create(
            nameof(ImageSource),
            typeof(ImageSource),
            typeof(CroppingPane),
            null);

    public ImageSource? ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public CroppingPane()
    {
        CurrentInstance = this;

        Boxes = [];
        CurrentlySelectedBox = null;
        CurrentlySelectedBox?.StartTranslationX = 0;
        CurrentlySelectedBox?.StartTranslationY = 0;
        CurrentlySelectedBox?.CurrentTranslationX = 0;
        CurrentlySelectedBox?.CurrentTranslationY = 0;
        _boxCounter = 0;

        InitializeComponent();        
    }

    public async void AddCropBox(CommunicationCard? existingCard = null)
    {
        if (Boxes.Count > 1)
        {
            SelectBox(Boxes.Last().Key);
        }

        _boxCounter++;

        var boxName = $"Item {_boxCounter}";
        var newBox = new CropBoxView(existingCard != null ? existingCard.LabelText : boxName, existingCard)
        {
            StyleId = _boxCounter.ToString()
        };

        double spawnX;
        double spawnY;

        double newBoxWidth = CurrentlySelectedBox == null ? Width : CurrentlySelectedBox.Width;
        double newBoxHeight = CurrentlySelectedBox == null ? Height : CurrentlySelectedBox.Height;

        if (CurrentlySelectedBox == null)
        {
            spawnX = X + (Width - newBoxWidth) / 2;
            spawnY = Y + (Height - newBoxHeight) / 2;
        }
        else
        {
            double proposedTranslationX = CurrentlySelectedBox.TranslationX + CurrentlySelectedBox.Width + 6;
            double proposedTranslationY = CurrentlySelectedBox.TranslationY;

            double absoluteCanvasLimitWidth = CanvasContainer.Width > 10 ? CanvasContainer.Width : 360.0;
            double absoluteCanvasLimitHeight = CanvasContainer.Height > 10 ? CanvasContainer.Height : 420.0;

            if ((proposedTranslationX + CurrentlySelectedBox.Width) > absoluteCanvasLimitWidth - CurrentlySelectedBox.Width - 50)
            {
                double lowestXOffset = 0;

                foreach (var box in Boxes)
                {
                    if (box.Key.CurrentTranslationX < lowestXOffset)
                        lowestXOffset = box.Key.CurrentTranslationX;
                }

                newBox.TranslationX = lowestXOffset;

                if ((proposedTranslationY + CurrentlySelectedBox.Height) > absoluteCanvasLimitHeight - CurrentlySelectedBox.Height - 150)
                {
                    await CropPhotoPage.CurrentInstance.DisplayAlertAsync("Canvas Full", "Maximum number of selection boxes. Please delete some, make them smaller, or extract your selections and start again.", "OK");
                    return;
                }
                else
                {
                    newBox.TranslationY = proposedTranslationY + CurrentlySelectedBox.Height + 6;
                }
            }
            else
            {
                newBox.TranslationX = proposedTranslationX;
                newBox.TranslationY = proposedTranslationY;
            }

            spawnX = newBox.TranslationX;
            spawnY = newBox.TranslationY;

            newBox.InitialScaleWidth = newBoxWidth;
            newBox.InitialScaleHeight = newBoxHeight;
        }

        newBox.WidthRequest = newBoxWidth;
        newBox.HeightRequest = newBoxHeight;

        AbsoluteLayout.SetLayoutFlags(newBox, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(newBox, new Rect(spawnX, spawnY, newBoxWidth, newBoxHeight));

        CanvasContainer.Children.Add(newBox);

        Boxes.Add(newBox, existingCard != null ? existingCard.LabelText : boxName);
        Boxes = Boxes.OrderBy(item => item.Value)
             .ToDictionary(pair => pair.Key, pair => pair.Value);

        SelectBox(newBox);
    }

    public async void DeleteCropBox()
    {
        if (CurrentlySelectedBox == null) return;

        if (Boxes.Count == 1)
        {
            await CropPhotoPage.CurrentInstance.DisplayAlertAsync("Cannot Delete 🚫", "The default full image boundary cannot be removed.", "OK");
            return;
        }

        bool confirm = await CropPhotoPage.CurrentInstance.DisplayAlertAsync("Remove Square? ❓", "Are you sure you want to delete this selection box?", "Yes, Delete", "Cancel");
        if (!confirm) return;

        CanvasContainer.Children.Remove(CurrentlySelectedBox);
        Boxes.Remove(CurrentlySelectedBox);
        SelectBox(Boxes.Last().Key);
    }

    public static void UpdateCropBoxName(string name)
    {
        if (CurrentlySelectedBox != null)
        {
            CurrentlySelectedBox.UpdateSpeachText(name);
            Boxes.Remove(CurrentlySelectedBox);
            Boxes.Add(CurrentlySelectedBox, name);
        }
    }

    public static void SelectBox(CropBoxView target)
    {
        CurrentlySelectedBox = target;

        foreach (var child in Boxes.Keys)
        {
            Border? border = (Border)child.FindByName("Border");

            border?.Stroke = Colors.Yellow;
            border?.BackgroundColor = Colors.Transparent;
            border?.StrokeThickness = 1;
            child.ZIndex = 0;
        }

        Border? currentBorder = (Border)CurrentlySelectedBox.FindByName("Border");
        currentBorder?.Stroke = Colors.DeepSkyBlue;
        currentBorder?.StrokeThickness = 3;
        currentBorder?.BackgroundColor = Colors.Transparent;
        CurrentlySelectedBox.ZIndex = 1;

        SelectedBoxChanged?.Invoke(CurrentInstance, target);
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e)
    {
        if (_hasInitializedDefaultBox) return;

        double actualWidth = CanvasContainer.Width;
        double actualHeight = CanvasContainer.Height;

        if (actualWidth <= 10) actualWidth = 360;
        if (actualHeight <= 10) actualHeight = 420;

        _hasInitializedDefaultBox = true;

        if (CurrentlySelectedBox != null)
        {
            Grid moveSurface = (Grid)CurrentlySelectedBox.FindByName("MoveSurfaceBody");

            if (moveSurface != null)
            {
                moveSurface.WidthRequest = actualWidth;
                moveSurface.HeightRequest = actualHeight;

                moveSurface.BatchBegin();
                AbsoluteLayout.SetLayoutBounds(moveSurface, new Rect(0, 0, actualWidth, actualHeight));

                double initialHandleX = actualWidth - 40;
                double initialHandleY = actualHeight - 40;

                AbsoluteLayout.SetLayoutBounds(moveSurface, new Rect(initialHandleX, initialHandleY, 40, 40));

                moveSurface.BatchCommit();
            }
        }
    }

    public void OnWorkspacePinched(object? sender, PinchGestureUpdatedEventArgs e)
    {
        var targetImage = CapturedRawPhoto;

        if (targetImage == null || targetImage.Width <= 10 || targetImage.Height <= 10) return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                StartWorkspaceScale = targetImage.Scale;

                targetImage.AnchorX = 0.5;
                targetImage.AnchorY = 0.5;

                double canvasCenterX = targetImage.Width / 2.0;
                double canvasCenterY = targetImage.Height / 2.0;

                foreach (var boxPair in Boxes)
                {
                    CropBoxView box = boxPair.Key;
                    box.InitialScaleWidth = box.WidthRequest > 10 ? box.WidthRequest : box.Width;
                    box.InitialScaleHeight = box.HeightRequest > 10 ? box.HeightRequest : box.Height;
                    box.InitialPositionX = box.TranslationX;
                    box.InitialPositionY = box.TranslationY;

                    double boxOriginalCenterX = box.X + (box.InitialScaleWidth / 2.0) + box.InitialPositionX;
                    double boxOriginalCenterY = box.Y + (box.InitialScaleHeight / 2.0) + box.InitialPositionY;

                    box.ZoomCenterOffsetX = boxOriginalCenterX - canvasCenterX;
                    box.ZoomCenterOffsetY = boxOriginalCenterY - canvasCenterY;
                }
                break;

            case GestureStatus.Running:
                double incrementalScale = targetImage.Scale * e.Scale;
                double validScale = Math.Clamp(incrementalScale, 1.0, 5.0);

                CropBoxView.CurrentWorkspaceScale = validScale;

                double maxPanX = (targetImage.Width * (validScale - 1.0)) / 2;
                double maxPanY = (targetImage.Height * (validScale - 1.0)) / 2;

                double currentTransX = CurrentlySelectedBox?.CurrentTranslationX ?? 0;
                double currentTransY = CurrentlySelectedBox?.CurrentTranslationY ?? 0;

                currentTransX = Math.Clamp(currentTransX, -maxPanX, maxPanX);
                currentTransY = Math.Clamp(currentTransY, -maxPanY, maxPanY);

                targetImage.BatchBegin();
                targetImage.Scale = validScale;
                targetImage.TranslationX = currentTransX;
                targetImage.TranslationY = currentTransY;
                targetImage.BatchCommit();

                double zoomRatioDelta = validScale / (StartWorkspaceScale > 0 ? StartWorkspaceScale : 1.0);

                foreach (var boxPair in Boxes)
                {
                    CropBoxView box = boxPair.Key;

                    box.BatchBegin();

                    box.AnchorX = targetImage.AnchorX;
                    box.AnchorY = targetImage.AnchorY;

                    double targetBoxWidth = box.InitialScaleWidth * zoomRatioDelta;
                    double targetBoxHeight = box.InitialScaleHeight * zoomRatioDelta;

                    box.UpdateCropBoxHeight(targetBoxHeight, targetBoxWidth);

                    box.TranslationX = (box.InitialPositionX * zoomRatioDelta) + (currentTransX - ((CurrentlySelectedBox?.StartTranslationX ?? 0) * zoomRatioDelta));
                    box.TranslationY = (box.InitialPositionY * zoomRatioDelta) + (currentTransY - ((CurrentlySelectedBox?.StartTranslationY ?? 0) * zoomRatioDelta));

                    box.BatchCommit();
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (CurrentlySelectedBox != null)
                {
                    CurrentlySelectedBox.StartTranslationX = targetImage.TranslationX;
                    CurrentlySelectedBox.StartTranslationY = targetImage.TranslationY;
                }

                foreach (var boxPair in Boxes)
                {
                    CropBoxView box = boxPair.Key;

                    box.CurrentTranslationX = box.TranslationX;
                    box.CurrentTranslationY = box.TranslationY;

                    box.InitialPositionX = box.TranslationX;
                    box.InitialPositionY = box.TranslationY;
                }

                if (CurrentlySelectedBox != null && CropBoxView.CurrentWorkspaceScale > 1.05)
                {
                    double viewportCenterX = targetImage.Width / 2.0;
                    double viewportCenterY = targetImage.Height / 2.0;

                    double boxLiveX = CurrentlySelectedBox.X + (CurrentlySelectedBox.Width / 2.0) + CurrentlySelectedBox.TranslationX;
                    double boxLiveY = CurrentlySelectedBox.Y + (CurrentlySelectedBox.Height / 2.0) + CurrentlySelectedBox.TranslationY;

                    double focusShiftX = viewportCenterX - boxLiveX;
                    double focusShiftY = viewportCenterY - boxLiveY;

                    double newTargetTransX = targetImage.TranslationX + focusShiftX;
                    double newTargetTargetY = targetImage.TranslationY + focusShiftY;

                    double maxPanLimitX = targetImage.Width * (CropBoxView.CurrentWorkspaceScale - 1.0) / 2;
                    double maxPanLimitY = targetImage.Height * (CropBoxView.CurrentWorkspaceScale - 1.0) / 2;

                    double finalFocusX = Math.Clamp(newTargetTransX, -maxPanLimitX, maxPanLimitX);
                    double finalFocusY = Math.Clamp(newTargetTargetY, -maxPanLimitY, maxPanLimitY);

                    double netAdjustmentDeltaX = finalFocusX - targetImage.TranslationX;
                    double netAdjustmentDeltaY = finalFocusY - targetImage.TranslationY;

                    targetImage.BatchBegin();
                    targetImage.TranslationX = finalFocusX;
                    targetImage.TranslationY = finalFocusY;
                    targetImage.BatchCommit();

                    if (CurrentlySelectedBox != null)
                    {
                        CurrentlySelectedBox.StartTranslationX = finalFocusX;
                        CurrentlySelectedBox.StartTranslationY = finalFocusY;
                        CurrentlySelectedBox.CurrentTranslationX = finalFocusX;
                        CurrentlySelectedBox.CurrentTranslationY = finalFocusY;
                    }

                    foreach (var boxPair in Boxes)
                    {
                        CropBoxView box = boxPair.Key;
                        box.BatchBegin();

                        box.TranslationX += netAdjustmentDeltaX;
                        box.TranslationY += netAdjustmentDeltaY;

                        box.CurrentTranslationX = box.TranslationX;
                        box.CurrentTranslationY = box.TranslationY;
                        box.InitialPositionX = box.TranslationX;
                        box.InitialPositionY = box.TranslationY;

                        box.BatchCommit();
                    }
                }
                break;
        }
    }

    public void OnWorkspacePanned(object? sender, PanUpdatedEventArgs e)
    {
        var targetImage = CapturedRawPhoto;

        if (targetImage == null || targetImage.Width <= 10 || targetImage.Height <= 10) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                if (CurrentlySelectedBox != null)
                {
                    CurrentlySelectedBox.StartTranslationX = targetImage.TranslationX;
                    CurrentlySelectedBox.StartTranslationY = targetImage.TranslationY;
                    CurrentlySelectedBox.CurrentTranslationX = targetImage.TranslationX;
                    CurrentlySelectedBox.CurrentTranslationY = targetImage.TranslationY;
                }

                foreach (var boxPair in Boxes)
                {
                    CropBoxView box = boxPair.Key;
                    box.InitialScaleWidth = box.WidthRequest > 10 ? box.WidthRequest : box.Width;
                    box.InitialScaleHeight = box.HeightRequest > 10 ? box.HeightRequest : box.Height;

                    box.InitialPositionX = box.TranslationX;
                    box.InitialPositionY = box.TranslationY;
                }
                break;
            case GestureStatus.Running:
                double baseStartX = CurrentlySelectedBox?.StartTranslationX ?? 0;
                double baseStartY = CurrentlySelectedBox?.StartTranslationY ?? 0;

                double targetX = baseStartX + e.TotalX;
                double targetY = baseStartY + e.TotalY;

                double currentScale = targetImage.Scale > 0 ? targetImage.Scale : 1.0;

                double maxPanX = targetImage.Width * (currentScale - 1) / 2;
                double maxPanY = targetImage.Height * (currentScale - 1) / 2;

                if (targetImage.Width > 0 && targetImage.Height > 0)
                {
                    maxPanY = (targetImage.Height * currentScale) / 2;
                }

                double clampedX = Math.Clamp(targetX, -maxPanX, maxPanX);
                double clampedY = Math.Clamp(targetY, -maxPanY, maxPanY);

                if (CurrentlySelectedBox != null)
                {
                    CurrentlySelectedBox.CurrentTranslationX = clampedX;
                    CurrentlySelectedBox.CurrentTranslationY = clampedY;
                }

                targetImage.BatchBegin();
                targetImage.TranslationX = clampedX;
                targetImage.TranslationY = clampedY;
                targetImage.BatchCommit();

                double panRatioDelta = 1.0;

                foreach (var boxPair in Boxes)
                {
                    CropBoxView box = boxPair.Key;
                    box.BatchBegin();

                    box.AnchorX = 0.5;
                    box.AnchorY = 0.5;

                    box.TranslationX = (box.InitialPositionX * panRatioDelta) + (clampedX - baseStartX);
                    box.TranslationY = (box.InitialPositionY * panRatioDelta) + (clampedY - baseStartY);

                    box.BatchCommit();
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (CurrentlySelectedBox != null)
                {
                    CurrentlySelectedBox.StartTranslationX = targetImage.TranslationX;
                    CurrentlySelectedBox.StartTranslationY = targetImage.TranslationY;
                    CurrentlySelectedBox.CurrentTranslationX = targetImage.TranslationX;
                    CurrentlySelectedBox.CurrentTranslationY = targetImage.TranslationY;
                }

                foreach (var boxPair in Boxes)
                {
                    CropBoxView box = boxPair.Key;

                    box.CurrentTranslationX = box.TranslationX;
                    box.CurrentTranslationY = box.TranslationY;

                    box.InitialPositionX = box.TranslationX;
                    box.InitialPositionY = box.TranslationY;
                }
                break;

        }
    }

    private void OnOverlayCanvasSizeChanged(object? sender, EventArgs e)
    {
        if (CapturedRawPhoto.Width > 10 && OverlayCanvas.Height > 10)
        {
            OverlayCanvasWidth = OverlayCanvas.Width;
            OverlayCanvasHeight = OverlayCanvas.Height;
        }
    }

    public void UpdateImageHeight(double height)
    {
        CapturedRawPhoto.HeightRequest = height;

        if (height == 420)
            CapturedRawPhoto.WidthRequest = 360;
    }
}
