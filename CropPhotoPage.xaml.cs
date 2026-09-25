using EchoMe.Database;
using EchoMe.UserControls;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EchoMe;

[QueryProperty(nameof(ImagePath), "ImagePath")]
[QueryProperty(nameof(CardID), "CardID")]
public partial class CropPhotoPage : ContentPage
{
    public ObservableCollection<CropBadgeModel> OverviewBadges { get; set; } = [];
    public ICommand BadgeTappedCommand { get; private set; }
    public ICommand AddSquareTappedCommand { get; private set; }

    public static readonly BindableProperty ImageSourceProperty =
        BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(CropPhotoPage), null);
    
    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public static readonly BindableProperty SelectedBoxIndicatorTextProperty =
        BindableProperty.Create(nameof(SelectedBoxIndicatorText), typeof(string), typeof(CropPhotoPage), null);

    public string SelectedBoxIndicatorText
    {
        get => (string)GetValue(SelectedBoxIndicatorTextProperty);
        set => SetValue(SelectedBoxIndicatorTextProperty, value);
    }

    public static readonly BindableProperty ActiveBoxNameEntryTextProperty =
    BindableProperty.Create(nameof(ActiveBoxNameEntryText), typeof(string), typeof(CropPhotoPage), null);

    public string ActiveBoxNameEntryText
    {
        get => (string)GetValue(ActiveBoxNameEntryTextProperty);
        set
        {
            SetValue(ActiveBoxNameEntryTextProperty, value);
            OnActiveBoxNameChanged(value);
        }
    }

    private string? _imagePath;
    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = Uri.UnescapeDataString(value ?? string.Empty);

            if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
            {
                Task.Run(async () =>
                {
                    int maxRetries = 5;
                    int delayMilliseconds = 150;
                    byte[]? imageBytes = null;

                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            using var fileStream = new FileStream(_imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var memoryStream = new MemoryStream();
                            await fileStream.CopyToAsync(memoryStream);
                            imageBytes = memoryStream.ToArray();
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(delayMilliseconds);
                        }
                    }

                    if (imageBytes != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // 🌟 2. USE NATIVE SETVALUE TO SAFELY TRIGGER BINDINGS ON THE MAIN THREAD
                            SetValue(ImageSourceProperty, ImageSource.FromStream(() => new MemoryStream(imageBytes)));
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Could not read camera file: Timeout waiting for file lock release.");
                    }
                });
            }
        }
    }

    private string? _cardIdStr;

    public string? CardID
    {
        get => _cardIdStr;
        set
        {
            if (_cardIdStr == value) return;
            _cardIdStr = value;

            if (int.TryParse(_cardIdStr, out int parsedId))
            {
                ActiveCardId = parsedId;
                LoadCard();
            }
            else
            {
                ActiveCardId = null;
            }

            OnPropertyChanged();
        }
    }

    public int? ActiveCardId { get; set; }

    public static CropPhotoPage CurrentInstance { get; private set; }

    public CropPhotoPage()
    {
        CurrentInstance = this;

        CroppingPane.SelectedBoxChanged -= OnCropBoxSelected;
        CroppingPane.SelectedBoxChanged += OnCropBoxSelected;
        InitializeComponent();

        BadgeTappedCommand = new Command<int>(OnBadgeTapped);
        AddSquareTappedCommand = new Command(OnAddCropBoxClicked);
        BindingContext = this;        
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        if (ActiveCardId == null)
            LoadCard();
    }

    private async void LoadCard()
    {
        CommunicationCard? existingCard = null;

        if (ActiveCardId != null)
        {
            try
            {
                var dbService = new DatabaseService();
                existingCard = await dbService.GetCardById(ActiveCardId.Value);

                if (existingCard == null || existingCard.ImageBytes == null || existingCard.ImageBytes.Length == 0)
                {
                    await DisplayAlertAsync("Error ⚠️", "Could not retrieve full photo source for re-cropping.", "OK");
                    return;
                }

                string tempFileName = $"recrop_{ActiveCardId}_{DateTime.Now.Ticks}.jpg";
                string tempFilePath = Path.Combine(FileSystem.CacheDirectory, tempFileName);

                using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
                await fileStream.WriteAsync(existingCard.ImageBytes.AsMemory(0, existingCard.ImageBytes.Length));

                ImagePath = tempFilePath;
                ActiveBoxNameEntryText = existingCard.LabelText;
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Edit Failure ⚠️", $"Could not open image: {ex.Message}", "OK");
            }
        }

        CroppingPane.CurrentInstance.AddCropBox(existingCard);
    }

    private void RefreshOverviewBadges()
    {
        if (OverviewBadges == null) return;

        OverviewBadges.Clear();

        if (CroppingPane.Boxes == null) return;

        foreach (var entry in CroppingPane.Boxes)
        {
            bool isCurrent = entry.Value == CroppingPane.CurrentlySelectedBox?.Name;
            OverviewBadges.Add(new CropBadgeModel
            {
                BoxId = int.TryParse(entry.Key.StyleId, out var id) ? id : 0,
                BoxName = string.IsNullOrWhiteSpace(entry.Value) ? "❓ Unnamed Box" : entry.Value,
                BGColor = isCurrent ? Color.FromRgb(2, 136, 209) : Color.FromRgb(69, 90, 100),
                BorderColor = isCurrent ? Colors.DeepSkyBlue : Colors.Transparent
            });
        }
    }

    private void UpdateSingleBadgeText(int id, string name)
    {
        var targetModel = OverviewBadges.FirstOrDefault(b => b.BoxId == id);
        targetModel?.BoxName = string.IsNullOrWhiteSpace(name) ? "❓ Unnamed Box" : name;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnBadgeTapped(int boxId)
    {
        if (CroppingPane.Boxes == null) return;

        var targetBox = CroppingPane.Boxes.Keys.FirstOrDefault(b => b.StyleId == boxId.ToString());
        if (targetBox != null && targetBox != CroppingPane.CurrentlySelectedBox)
        {
            CroppingPane.SelectBox(targetBox);
        }
    }

    private void OnAddCropBoxClicked()
    {
        CroppingPane.CurrentInstance.AddCropBox();
    }

    private async void OnDeleteCropBoxClicked(object? sender, EventArgs e)
    {
        CroppingPane.CurrentInstance.DeleteCropBox();
    }

    private void OnCropBoxSelected(object? sender, CropBoxView e)
    {
        if (e == null) return;

        if (e.FindByName("Border") is Border innerBorder)
        {
            innerBorder.Stroke = Colors.DeepSkyBlue;
            innerBorder.BackgroundColor = Colors.Transparent;
        }

        SelectedBoxIndicatorText = $"Selected: {e.Name}";

        if (!e.Name.StartsWith("Item"))
            ActiveBoxNameEntryText = e.Name;
        else
            ActiveBoxNameEntryText = string.Empty;

        DeleteBoxButton?.IsEnabled = true;
        RefreshOverviewBadges();
    }

    private void OnActiveBoxNameChanged(string value)
    {
        if (CroppingPane.CurrentlySelectedBox == null) return;

        SelectedBoxIndicatorText = $"Selected: {value}";

        CroppingPane.UpdateCropBoxName(value);
        UpdateSingleBadgeText(int.TryParse(CroppingPane.CurrentlySelectedBox.StyleId, out var id) ? id : 0, value ?? string.Empty);
    }

    private async void OnProcessCropsClicked(object? sender, EventArgs e)
    {
        foreach (var box in CroppingPane.Boxes)
        {
            if (string.IsNullOrWhiteSpace(box.Value))
            {
                CroppingPane.SelectBox(box.Key);
                ActiveBoxNameEntry?.Focus();
                await DisplayAlertAsync("Name Required 🏷️", "Every selection box must have speech text configured!", "Fix It");
                return;
            }
            else
            {
                var db = new DatabaseService();
                var boxes = CroppingPane.Boxes.Where(b => b.Key != box.Key)
                    .Select(b => b.Key);

                if (box.Key.Id != ActiveCardId || box.Key.Id == null)
                {
                    if (boxes.Any() && boxes.FirstOrDefault(b => b.Name != box.Key.Name) == null || await db.CheckDuplicate(box.Key.Name))
                    {
                        CroppingPane.SelectBox(box.Key);
                        ActiveBoxNameEntry?.Focus();
                        await DisplayAlertAsync("Duplicate Speach Text ⚠️", $"An existing item name '{box.Key.Name}' already exists! Please enter a different value!", "Fix It");
                        return;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            await DisplayAlertAsync("Error ⚠️", "Original photo file could not be found.", "OK");
            return;
        }

        try
        {
            byte[] fullRawSourceBytes = File.ReadAllBytes(ImagePath);
            var dbService = new DatabaseService();

            if (CroppingPane.CurrentInstance == null) return;
            var targetImage = CroppingPane.CurrentInstance.FindByName<Image>("CapturedRawPhoto");

            if (targetImage == null)
            {
                await DisplayAlertAsync("Layout Error", "Could not locate the workspace image control.", "OK");
                return;
            }

            using (SKBitmap originalBitmap = SKBitmap.Decode(ImagePath))
            {
                if (originalBitmap == null) return;

                double canvasW = CroppingPane.CurrentInstance.OverlayCanvasWidth;
                double canvasH = CroppingPane.CurrentInstance.OverlayCanvasHeight;
                if (canvasW <= 10) canvasW = 360;
                if (canvasH <= 10) canvasH = 420;

                double aspectX = canvasW / originalBitmap.Width;
                double aspectY = canvasH / originalBitmap.Height;
                double uniformRenderScale = Math.Min(aspectX, aspectY);

                double visualPhotoWidth = originalBitmap.Width * uniformRenderScale;
                double visualPhotoHeight = originalBitmap.Height * uniformRenderScale;

                double activeScale = targetImage.Scale;
                double translationX = targetImage.TranslationX;
                double translationY = targetImage.TranslationY;
                double anchorX = targetImage.AnchorX;
                double anchorY = targetImage.AnchorY;

                double baseLeft = (canvasW - visualPhotoWidth) / 2.0;
                double baseTop = (canvasH - visualPhotoHeight) / 2.0;

                double pivotX = baseLeft + (visualPhotoWidth * anchorX);
                double pivotY = baseTop + (visualPhotoHeight * anchorY);

                double liveImageLeft = pivotX + (baseLeft - pivotX) * activeScale + translationX;
                double liveImageTop = pivotY + (baseTop - pivotY) * activeScale + translationY;

                foreach (var box in CroppingPane.Boxes)
                {
                    CropBoxView cropBox = box.Key;
                    string cardLabelName = box.Value;

                    var moveSurface = cropBox.FindByName<Grid>("MoveSurfaceBody");
                    if (moveSurface == null) continue;

                    Rect initialLayout = AbsoluteLayout.GetLayoutBounds(cropBox);
                    double boxLeftX = initialLayout.X + cropBox.CurrentTranslationX;
                    double boxTopY = initialLayout.Y + cropBox.CurrentTranslationY;

                    double boxActualWidth = moveSurface.Width > 10 ? moveSurface.Width : moveSurface.WidthRequest;
                    double boxActualHeight = moveSurface.Height > 10 ? moveSurface.Height : moveSurface.HeightRequest;

                    if (boxActualWidth <= 10 || boxActualWidth == 100) boxActualWidth = canvasW;
                    if (boxActualHeight <= 10 || boxActualHeight == 100) boxActualHeight = canvasH;

                    double relativeX = boxLeftX - liveImageLeft;
                    double relativeY = boxTopY - liveImageTop;

                    double unscaledX = relativeX / activeScale;
                    double unscaledY = relativeY / activeScale;
                    double unscaledWidth = boxActualWidth / activeScale;
                    double unscaledHeight = boxActualHeight / activeScale;

                    int cropX = (int)(unscaledX / uniformRenderScale);
                    int cropY = (int)(unscaledY / uniformRenderScale);
                    int cropW = (int)(unscaledWidth / uniformRenderScale);
                    int cropH = (int)(unscaledHeight / uniformRenderScale);

                    cropX = Math.Clamp(cropX, 0, originalBitmap.Width);
                    cropY = Math.Clamp(cropY, 0, originalBitmap.Height);
                    cropW = Math.Clamp(cropW, 10, originalBitmap.Width - cropX);
                    cropH = Math.Clamp(cropH, 10, originalBitmap.Height - cropY);

                    if (cropW <= 0 || cropH <= 0) continue;

                    byte[] processedTileBytes;

                    using (SKBitmap croppedBitmap = new(cropW, cropH))
                    {
                        originalBitmap.ExtractSubset(croppedBitmap, new SKRectI(cropX, cropY, cropX + cropW, cropY + cropH));

                        using SKData encodedData = croppedBitmap.Encode(SKEncodedImageFormat.Png, 100);
                        processedTileBytes = encodedData.ToArray();
                    }

                    await dbService.SaveCardAsync(cardLabelName, processedTileBytes, fullRawSourceBytes, box.Key.Id);
                }
            }

            await DisplayAlertAsync("Success!", "Your crop view selections were captured and saved perfectly.", "Awesome!");
            await Shell.Current.GoToAsync("ManageCollectionPage");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Slicing Error", $"Could not process zoomed crop bounds: {ex.Message}", "OK");
        }
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (RootLayoutGrid == null || WorkspaceContainer == null || FormPanel == null || HeaderPanel == null) return;

        if (Width > Height)
        {
            RootLayoutGrid.RowDefinitions = new RowDefinitionCollection { new RowDefinition(GridLength.Star) };
            RootLayoutGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(380)),
                new ColumnDefinition(GridLength.Star)
            };

            HeaderPanel.IsVisible = false;

            Grid.SetRow(WorkspaceContainer, 0);
            Grid.SetColumn(WorkspaceContainer, 0);
            WorkspaceContainer.Margin = new Thickness(6, 0, 0, 0);

            WorkspaceContainer.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];

            var croppingBorder = WorkspaceContainer.FindByName<Border>("CroppingPaneBorder") ?? WorkspaceContainer.Children.OfType<Border>().FirstOrDefault();
            if (croppingBorder != null)
            {
                croppingBorder.WidthRequest = 360;
                croppingBorder.HeightRequest = 220;
                croppingBorder.HorizontalOptions = LayoutOptions.Center;
                croppingBorder.VerticalOptions = LayoutOptions.Start;
                croppingBorder.Margin = new Thickness(0, 4, 0, 0);

                CroppingPane.CurrentInstance.UpdateImageHeight(220);
                CroppingPane.CurrentlySelectedBox?.UpdateCropBoxHeight(220);
            }
                        
            Grid.SetRow(FormPanel, 0);
            Grid.SetColumn(FormPanel, 1);
            FormPanel.WidthRequest = -1;
            FormPanel.HorizontalOptions = LayoutOptions.Fill;
            FormPanel.VerticalOptions = LayoutOptions.Center;
            FormPanel.Margin = new Thickness(12, 0, 16, 0);
        }
        else
        {
            RootLayoutGrid.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];
            RootLayoutGrid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star)];

            HeaderPanel.IsVisible = true;
            Grid.SetRow(HeaderPanel, 0);
            Grid.SetColumn(HeaderPanel, 0);

            Grid.SetRow(WorkspaceContainer, 1);
            Grid.SetColumn(WorkspaceContainer, 0);
            WorkspaceContainer.Margin = new Thickness(0, 0, 0, 140);
            WorkspaceContainer.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];

            var croppingBorder = WorkspaceContainer.FindByName<Border>("CroppingPaneBorder") ?? WorkspaceContainer.Children.OfType<Border>().FirstOrDefault();
            if (croppingBorder != null)
            {
                croppingBorder.WidthRequest = 360;
                croppingBorder.HeightRequest = 420;
                croppingBorder.HorizontalOptions = LayoutOptions.Center;
                croppingBorder.VerticalOptions = LayoutOptions.Start;

                CroppingPane.CurrentInstance.UpdateImageHeight(420);
                CroppingPane.CurrentlySelectedBox?.UpdateCropBoxHeight(420);
            }

            Grid.SetRow(FormPanel, 1);
            Grid.SetColumn(FormPanel, 0);
            FormPanel.WidthRequest = -1;
            FormPanel.HorizontalOptions = LayoutOptions.Fill;
            FormPanel.VerticalOptions = LayoutOptions.End;
            FormPanel.Margin = new Thickness(4, 4, 4, 0);
        }
    }
}
