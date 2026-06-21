namespace Preagonal.GServer.Game;

public sealed class RuntimeTimeoutCounter
{
    private int _timeout;

    public RuntimeTimeoutCounter(int timeout = 0)
    {
        _timeout = timeout;
    }

    public void SetTimeout(int timeout)
    {
        _timeout = timeout;
    }

    public int GetTimeout() => _timeout;

    public int DoTimeout()
    {
        return _timeout > 0 ? --_timeout : -1;
    }
}
