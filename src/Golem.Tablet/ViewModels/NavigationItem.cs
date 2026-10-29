namespace Golem.Tablet.ViewModels;

public sealed class NavigationItem
{
    public NavigationItem(string number, string title, string description, object viewModel)
    {
        Number = number;
        Title = title;
        Description = description;
        ViewModel = viewModel;
    }

    public string Number { get; }

    public string Title { get; }

    public string Description { get; }

    public object ViewModel { get; }
}
