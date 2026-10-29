namespace Golem.Tablet.Core.Logging;

/// <summary>Marshals an action onto the UI thread. Implemented by the host app.</summary>
public interface IUiDispatcher
{
    void Post(Action action);

    void Send(Action action);
}
