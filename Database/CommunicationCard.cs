using SQLite;
using SQLiteNetExtensions.Attributes;

namespace EchoMe.Database;

[Table("CommunicationCards")]
public class CommunicationCard
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public string LabelText { get; set; } = string.Empty;
    public byte[] ImageBytes { get; set; } = [];

    [ForeignKey(typeof(CommunicationCardSourceImage))]
    public int SourceImageId { get; set; }

    [ManyToOne]
    public CommunicationCardSourceImage? SourceImage { get; set; }
}

[Table("CommunicationCardSourceImages")]
public class CommunicationCardSourceImage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public byte[] SourceBytes { get; set; } = [];

    [OneToMany(CascadeOperations = CascadeOperation.All)]
    public List<CommunicationCard> CommunicationCards { get; set; } = [];
}


[Table("HomeScreenCards")]
public class HomeScreenCard
{
    [PrimaryKey]
    public int CardId { get; set; }
}
