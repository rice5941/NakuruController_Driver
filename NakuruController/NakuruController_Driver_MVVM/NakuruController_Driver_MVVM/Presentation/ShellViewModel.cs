namespace NakuruController_Driver_MVVM.Presentation;

public class ShellViewModel
{
    private readonly INavigator _navigator;

    public ShellViewModel(
        INavigator navigator)
    {
        _navigator = navigator;
        _navigator.NavigateViewModelAsync<RealTimeChartViewModel>(this);
        // Add code here to initialize or attach event handlers to singleton services
    }
}
