namespace EchoMe.Models
{
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
}
