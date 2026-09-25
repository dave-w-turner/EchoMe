using SQLite;
using SQLiteNetExtensionsAsync.Extensions;

namespace EchoMe.Database;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;

    private async Task InitAsync()
    {
        if (_database != null) return;

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "EchoMeData.db3");
        _database = new SQLiteAsyncConnection(dbPath);

        await _database.CreateTableAsync<CommunicationCardSourceImage>();
        await _database.CreateTableAsync<CommunicationCard>();
        await _database.CreateTableAsync<HomeScreenCard>();
    }

    public async Task SaveCardAsync(string label, byte[] imageBytes, byte[] originalSourceBytes, int? cardId = null)
    {
        await InitAsync();

        if (cardId == null)
        {
            var card = new CommunicationCard
            {
                LabelText = label,
                ImageBytes = imageBytes,
                SourceImage = new CommunicationCardSourceImage()
                {
                    SourceBytes = originalSourceBytes
                }
            };

            await _database!.InsertAsync(card);
        }
        else
        {
            await UpdateCardById(cardId.Value, label, imageBytes);
        }
    }

    public async Task<List<CommunicationCard>> GetAllCardsAsync()
    {
        await InitAsync();
        return await _database!.Table<CommunicationCard>().ToListAsync();
    }

    public async Task<CommunicationCard> GetCardById(int id)
    {
        await InitAsync();
        return await _database!.Table<CommunicationCard>().Where(item => item.Id == id).FirstOrDefaultAsync();
    }

    public async Task UpdateCardById(int id, string label, byte[] imageBytes)
    {
        await InitAsync();

        var card = await GetCardById(id);
        card.LabelText = label;
        card.ImageBytes = imageBytes;

        await _database!.UpdateAsync(card);
    }

    public async Task AddToHomeScreenAsync(int cardId)
    {
        await InitAsync();
        var mapping = new HomeScreenCard { CardId = cardId };
        await _database!.InsertOrReplaceAsync(mapping);
    }

    public async Task RemoveFromHomeScreenAsync(int cardId)
    {
        await InitAsync();
        await _database!.DeleteAsync<HomeScreenCard>(cardId);
    }

    public async Task<List<CommunicationCard>> GetHomeScreenCardsAsync()
    {
        await InitAsync();

        var pinnedIds = await _database!.Table<HomeScreenCard>().ToListAsync();
        var idList = pinnedIds.Select(p => p.CardId).ToList();

        if (idList.Count == 0) return [];

        return await _database!.Table<CommunicationCard>()
                               .Where(c => idList.Contains(c.Id))
                               .ToListAsync();
    }

    public async Task<HashSet<int>> GetHomeScreenIdMapAsync()
    {
        await InitAsync();
        var list = await _database!.Table<HomeScreenCard>().ToListAsync();
        return [.. list.Select(static l => l.CardId)];
    }

    public async Task DeleteLibraryCardPermanentAsync(int cardId)
    {
        await InitAsync();

        var cardToDelete = await _database!.GetWithChildrenAsync<CommunicationCard>(cardId);
        if (cardToDelete == null) return;

        int parentImageId = cardToDelete.SourceImageId;

        var homeScreenPin = await _database!.Table<HomeScreenCard>()
                                            .Where(h => h.CardId == cardId) // Or whatever your link column is named!
                                            .FirstOrDefaultAsync();

        if (homeScreenPin != null)
        {
            await _database!.DeleteAsync(homeScreenPin);
        }

        await _database!.DeleteAsync<CommunicationCard>(cardId);

        int remainingCardsUsingThisPhoto = await _database!.Table<CommunicationCard>()
                                                            .Where(c => c.SourceImageId == parentImageId)
                                                            .CountAsync();

        if (remainingCardsUsingThisPhoto == 0)
        {
            await _database!.DeleteAsync<CommunicationCardSourceImage>(parentImageId);
            System.Diagnostics.Debug.WriteLine($"--> [DB CONTROL]: Cleaned up orphaned master image ID: {parentImageId}");
        }
    }


    public async Task<bool> CheckDuplicate(string speachText)
    {
        await InitAsync();

        var existingItem = await _database!.Table<CommunicationCard>().Where(item => item.LabelText == speachText).FirstOrDefaultAsync();

        if (existingItem == null) return false;

        return true;
    }
}
