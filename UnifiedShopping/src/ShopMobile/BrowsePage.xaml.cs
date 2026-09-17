#if MAUI
namespace ShopMobile;

public partial class BrowsePage : ContentPage
{
    public BrowsePage()
    {
        InitializeComponent();
        BindingContext = new BrowseViewModel();
    }
}
#endif
