namespace EchoMe
{
    public partial class CropBadgeModel : BindableObject
    {
        private string _boxName = string.Empty;
        private Color _bgColor = Colors.Transparent;
        private Color _borderColor = Colors.Transparent;

        public int BoxId { get; set; }

        public string BoxName
        {
            get => _boxName;
            set { _boxName = value; OnPropertyChanged(); }
        }

        public Color BGColor
        {
            get => _bgColor;
            set { _bgColor = value; OnPropertyChanged(); }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; OnPropertyChanged(); }
        }
    }

}
