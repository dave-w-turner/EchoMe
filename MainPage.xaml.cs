using EchoMe.Database;
using EchoMe.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EchoMe;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _databaseService = new();

    public ObservableCollection<BoardPageModel> CarouselPages { get; set; } = new();

    public ICommand CardTappedCommand { get; private set; }

    public MainPage()
    {
        InitializeComponent();
        CardTappedCommand = new Command<HomeCardDisplayModel>(OnCardTapped);
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshActiveBoardItemsAsync();
    }

    private async Task RefreshActiveBoardItemsAsync()
    {
        try
        {
            var pinnedRecords = await _databaseService.GetHomeScreenCardsAsync();
            var flatCardsList = new List<HomeCardDisplayModel>();

            foreach (var card in pinnedRecords)
            {
                var memoryStreamSource = ImageSource.FromStream(() => new MemoryStream(card.ImageBytes));

                flatCardsList.Add(new HomeCardDisplayModel
                {
                    CardId = card.Id,
                    LabelText = card.LabelText,
                    DecodedImage = memoryStreamSource
                });
            }

            var chunks = flatCardsList.Chunk(8);

            CarouselPages.Clear();
            foreach (var chunk in chunks)
            {
                var newPage = new BoardPageModel();
                foreach (var card in chunk)
                {
                    newPage.Cards.Add(card);
                }

                CarouselPages.Add(newPage);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Slicing error: {ex.Message}");
        }
    }

    private void OnCardTapped(HomeCardDisplayModel tappedCard)
    {
        if (tappedCard == null) return;

        if (string.IsNullOrWhiteSpace(WordsEntry.Text))
        {
            WordsEntry.Text = tappedCard.LabelText;
        }
        else
        {
            WordsEntry.Text += " " + tappedCard.LabelText;
        }
    }

    private async void OnManageBoardClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("ManageCollectionPage");

    private void OnKeyboardButtonClicked(object? sender, EventArgs e) => WordsEntry.Focus();

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width > Height)
        {
            cardsView.HeightRequest = 180;
        }
        else
        {
            cardsView.HeightRequest = 420;
        }
    }
}
