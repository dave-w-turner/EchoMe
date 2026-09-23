using System.Collections.ObjectModel;

namespace EchoMe.Models
{
    public class BoardPageModel
    {
        public ObservableCollection<HomeCardDisplayModel> Cards { get; set; } = new();
    }
}
