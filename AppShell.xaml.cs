using EchoMe.UserControls;

namespace EchoMe
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("AddImagePage", typeof(AddImagePage));
            Routing.RegisterRoute("CropPhotoPage", typeof(CropPhotoPage));
            Routing.RegisterRoute("ManageCollectionPage", typeof(ManageCollectionPage));
            Routing.RegisterRoute("CroppingPane", typeof(CroppingPane));
        }
    }
}
