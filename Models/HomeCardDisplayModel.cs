namespace EchoMe.Models
{
    public class HomeCardDisplayModel
    {
        public int CardId { get; set; }
        public string LabelText { get; set; } = string.Empty;
        public ImageSource? DecodedImage { get; set; }
    }
}
