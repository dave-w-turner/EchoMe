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

    public void AddCropBox(CommunicationCard? existingCard = null)
    {
        _boxCounter++;

        var boxName = $"Item {_boxCounter}";
        var newBox = new CropBoxView(existingCard != null ? existingCard.LabelText : boxName, existingCard)
        {
            StyleId = _boxCounter.ToString()
        };

        double spawnX;
        double spawnY;

        CanvasContainer.Children.Add(newBox);

        double newBoxWidth = CurrentlySelectedBox == null ? Width : Width - 100;
        double newBoxHeight = CurrentlySelectedBox == null ? Height : Height - 100;

        spawnX = X + (Width - newBoxWidth) / 2;
        spawnY = Y + (Height - newBoxHeight) / 2;

        if (spawnX < 0) spawnX = 0;
        if (spawnY < 0) spawnY = 0;

        newBox.WidthRequest = newBoxWidth;
        newBox.HeightRequest = newBoxHeight;

        AbsoluteLayout.SetLayoutBounds(newBox, new Rect(spawnX, spawnY, newBoxWidth, newBoxHeight));
        AbsoluteLayout.SetLayoutFlags(newBox, AbsoluteLayoutFlags.None);

        Boxes.Add(newBox, existingCard != null ? existingCard.LabelText : boxName);
        Boxes = Boxes = Boxes.OrderBy(item => item.Value)
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

    private void OnWorkspacePinched(object? sender, PinchGestureUpdatedEventArgs e)
    {
        var targetImage = CapturedRawPhoto;

        if (targetImage == null || targetImage.Width <= 10 || targetImage.Height <= 10) return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                Point fingerMidPoint = e.ScaleOrigin;

                StartWorkspaceScale = targetImage.Scale;
                targetImage.AnchorX = Math.Clamp(fingerMidPoint.X, 0.0, 1.0);
                targetImage.AnchorY = Math.Clamp(fingerMidPoint.Y, 0.0, 1.0);

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

                    box.TranslationX = (box.InitialPositionX * zoomRatioDelta) + (currentTransX - (CurrentlySelectedBox?.StartTranslationX ?? 0));
                    box.TranslationY = (box.InitialPositionY * zoomRatioDelta) + (currentTransY - (CurrentlySelectedBox?.StartTranslationY ?? 0));

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
                break;
        }
    }

    private void OnWorkspacePanned(object? sender, PanUpdatedEventArgs e)
    {
        var targetImage = CapturedRawPhoto;

        if (targetImage.Width <= 10 || targetImage.Height <= 10) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                if (CurrentlySelectedBox != null)
                {
                    CurrentlySelectedBox.StartTranslationX = targetImage.TranslationX;
                    CurrentlySelectedBox.StartTranslationY = targetImage.TranslationY;
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

                double maxPanX = targetImage.Width * (CropBoxView.CurrentWorkspaceScale - 1) / 2;
                double maxPanY = targetImage.Height * (CropBoxView.CurrentWorkspaceScale - 1) / 2;

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

                    box.AnchorX = targetImage.AnchorX;
                    box.AnchorY = targetImage.AnchorY;

                    box.TranslationX = (box.InitialPositionX * panRatioDelta) + (clampedX - baseStartX);
                    box.TranslationY = (box.InitialPositionY * panRatioDelta) + (clampedY - baseStartY);

                    box.BatchCommit();
                }
                break;

            // 🌟 THE TRANSLATION MEMORY CURE:
            // When the user lifts their finger after panning, commit their new resting coordinates 
            // back into the internal registers! This ensures that your zoom code reads a clean, 
            // updated baseline on the next touch pass, completely preventing the transition jump!
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

                    // Sync internal registers so subsequent pinch gestures read the updated location
                    box.CurrentTranslationX = box.TranslationX;
                    box.CurrentTranslationY = box.TranslationY;

                    // Force the permanent initial tracking variables to log the final position
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

    public void UpdateImageHeight(int height)
    {
        CapturedRawPhoto.HeightRequest = height;

        if (height == 420)
            CapturedRawPhoto.WidthRequest = 360;
    }
}
