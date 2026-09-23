using EchoMe.Database;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EchoMe;

public partial class ManageCollectionPage : ContentPage
{
    private readonly DatabaseService _db = new();
    private bool _labelHeaderVisible = false;

    public ObservableCollection<ManageItemViewModel> MasterItems { get; set; } = [];
    public bool LabelHeaderVisible 
    {
        get => _labelHeaderVisible;
        private set
        {
            if (_labelHeaderVisible == value) return;
            _labelHeaderVisible = value;
            OnPropertyChanged(nameof(LabelHeaderVisible));
        }
    }

    public ICommand ToggleStatusCommand { get; private set; }
    public ICommand DeletePermanentItemCommand { get; private set; }
    public ICommand EditCropItemCommand { get; private set; }

    public ManageCollectionPage()
    {
        InitializeComponent();
        ToggleStatusCommand = new Command<ManageItemViewModel>(async (item) => await OnToggleItemStatus(item));
        DeletePermanentItemCommand = new Command<ManageItemViewModel>(async (item) => await OnDeletePermanentItemAsync(item));
        EditCropItemCommand = new Command<ManageItemViewModel>(async (item) => await OnEditCropItemAsync(item));
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMasterLibraryAsync();
    }

    private async void OnAddImageClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("AddImagePage");

    private async Task LoadMasterLibraryAsync()
    {
        var allLibraryCards = await _db.GetAllCardsAsync();
        var currentlyPinnedIds = await _db.GetHomeScreenIdMapAsync();

        MasterItems.Clear();
        foreach (var card in allLibraryCards)
        {
            bool isPinned = currentlyPinnedIds.Contains(card.Id);
            MasterItems.Add(new ManageItemViewModel
            {
                CardId = card.Id,
                LabelText = card.LabelText,
                ImageUrl = ImageSource.FromStream(() => new MemoryStream(card.ImageBytes)),
                IsOnHomeScreen = isPinned,
                StatusColor = isPinned ? Colors.DeepSkyBlue : Colors.LightGray,
                StatusText = isPinned ? "✔️ Active" : "💤 Hidden"
            });
        }

        if (MasterItems.Count > 0)
            LabelHeaderVisible = true;
    }

    private async Task OnDeletePermanentItemAsync(ManageItemViewModel item)
    {
        if (item == null) return;

        bool confirm = await DisplayAlertAsync(
            "Delete Image Permanently? 🗑️",
            $"Are you sure you want to completely erase '{item.LabelText}' from the entire application library storage memory?",
            "Yes, Delete It",
            "Cancel");

        if (!confirm) return;

        try
        {
            await _db.DeleteLibraryCardPermanentAsync(item.CardId);

            MasterItems.Remove(item);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Deletion Error ⚠️", $"Could not clear card entry: {ex.Message}", "OK");
        }

        if (MasterItems.Count == 0)
            LabelHeaderVisible = false;
    }

    private async Task OnToggleItemStatus(ManageItemViewModel item)
    {
        if (item.IsOnHomeScreen)
        {
            await _db.RemoveFromHomeScreenAsync(item.CardId);
            item.IsOnHomeScreen = false;
            item.StatusColor = Colors.LightGray;
            item.StatusText = "💤 Hidden";
        }
        else
        {
            await _db.AddToHomeScreenAsync(item.CardId);
            item.IsOnHomeScreen = true;
            item.StatusColor = Colors.DeepSkyBlue;
            item.StatusText = "✔️ Active";
        }
    }

    private async Task OnEditCropItemAsync(ManageItemViewModel item)
    {
        if (item == null) return;

        await Shell.Current.GoToAsync($"{nameof(CropPhotoPage)}?CardID={Uri.EscapeDataString(item.CardId.ToString())}");
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("///MainPage");
}

public class ManageItemViewModel : BindableObject
{
    private Color _statusColor = Colors.LightGray;
    private string _statusText = "💤 Hidden";

    public int CardId { get; set; }
    public string LabelText { get; set; } = string.Empty;
    public ImageSource? ImageUrl { get; set; }
    public bool IsOnHomeScreen { get; set; }

    public Color StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
}
