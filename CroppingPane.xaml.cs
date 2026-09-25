using EchoMe.Database;
using Microsoft.Maui.Layouts;

namespace EchoMe.UserControls;

public partial class CroppingPane : ContentView
{
    private bool _hasInitializedDefaultBox = false;
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
                targetImage.AnchorX = Math.Clamp(fingerMidPoint.X, 0.0, 1.0);
                targetImage.AnchorY = Math.Clamp(fingerMidPoint.Y, 0.0, 1.0);
                break;

            case GestureStatus.Running:
                double incrementalScale = targetImage.Scale * e.Scale;
                double validScale = Math.Clamp(incrementalScale, 1.0, 5.0);

                CropBoxView.CurrentWorkspaceScale = validScale;

                double maxPanX = (targetImage.Width * (validScale - 1.0)) / 2;
                double maxPanY = (targetImage.Height * (validScale - 1.0)) / 2;

                CurrentlySelectedBox?.CurrentTranslationX = Math.Clamp(CurrentlySelectedBox?.CurrentTranslationX ?? 0, -maxPanX, maxPanX);
                CurrentlySelectedBox?.CurrentTranslationY = Math.Clamp(CurrentlySelectedBox?.CurrentTranslationY ?? 0, -maxPanY, maxPanY);

                targetImage.BatchBegin();
                targetImage.Scale = validScale;
                targetImage.TranslationX = CurrentlySelectedBox?.CurrentTranslationX ?? 0;
                targetImage.TranslationY = CurrentlySelectedBox?.CurrentTranslationY ?? 0;
                targetImage.BatchCommit();
                break;
        }
    }

    private void OnWorkspacePanned(object? sender, PanUpdatedEventArgs e)
    {
        if (CropBoxView.CurrentWorkspaceScale <= 1.02)
        {
            CapturedRawPhoto.TranslationX = 0;
            CapturedRawPhoto.TranslationY = 0;
            CurrentlySelectedBox?.CurrentTranslationX = 0;
            CurrentlySelectedBox?.CurrentTranslationY = 0;
            return;
        }

        if (CapturedRawPhoto.Width <= 10 || CapturedRawPhoto.Height <= 10) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                CurrentlySelectedBox?.StartTranslationX = CurrentlySelectedBox.CurrentTranslationX;
                CurrentlySelectedBox?.StartTranslationY = CurrentlySelectedBox.CurrentTranslationY;
                break;

            case GestureStatus.Running:
                double targetX = (CurrentlySelectedBox?.StartTranslationX ?? 0) + e.TotalX;
                double targetY = (CurrentlySelectedBox?.StartTranslationY ?? 0) + e.TotalY;

                double maxPanX = CapturedRawPhoto.Width * (CropBoxView.CurrentWorkspaceScale - 1) / 2;
                double maxPanY = CapturedRawPhoto.Height * (CropBoxView.CurrentWorkspaceScale - 1) / 2;

                CurrentlySelectedBox?.CurrentTranslationX = Math.Clamp(targetX, -maxPanX, maxPanX);
                CurrentlySelectedBox?.CurrentTranslationY = Math.Clamp(targetY, -maxPanY, maxPanY);

                CapturedRawPhoto.BatchBegin();
                CapturedRawPhoto.TranslationX = CurrentlySelectedBox?.CurrentTranslationX ?? 0;
                CapturedRawPhoto.TranslationY = CurrentlySelectedBox?.CurrentTranslationY ?? 0;
                CapturedRawPhoto.BatchCommit();
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
