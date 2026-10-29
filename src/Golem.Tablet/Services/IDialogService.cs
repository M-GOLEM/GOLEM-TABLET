namespace Golem.Tablet.Services;

public interface IDialogService
{
    bool Confirm(string title, string message);

    void ShowInfo(string title, string message);

    string? PickOpenFile(string title, string filter);

    string? PickSaveFile(string title, string filter, string defaultFileName);

    string? PickFolder(string title);
}
