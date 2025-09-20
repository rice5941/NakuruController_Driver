namespace UnoApp1.Presentation;

public class ShellViewModel
{
    private readonly INavigator _navigator;

    public ShellViewModel(
        INavigator navigator)
    {
        _navigator = navigator;
        _navigator.NavigateViewModelAsync<MainViewModel>(this);
        // Add code here to initialize or attach event handlers to singleton services
    }
}
