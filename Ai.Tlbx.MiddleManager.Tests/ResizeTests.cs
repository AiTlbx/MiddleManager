using Ai.Tlbx.MiddleManager.Settings;
using Ai.Tlbx.MiddleManager.Shells;
using Ai.Tlbx.MiddleManager.Services;
using Xunit;

namespace Ai.Tlbx.MiddleManager.Tests;

public class ResizeTests : IDisposable
{
    private readonly SessionManager _sessionManager = new(new ShellRegistry(), new SettingsService());

    public void Dispose()
    {
        _sessionManager.Dispose();
    }

    [Fact]
    public void Resize_UpdatesDimensions()
    {
        var session = _sessionManager.CreateSession(cols: 80, rows: 24);

        var accepted = session.Resize(120, 40);

        Assert.True(accepted);
        Assert.Equal(120, session.Cols);
        Assert.Equal(40, session.Rows);
    }

    [Fact]
    public void Resize_SameDimensions_ReturnsTrue()
    {
        var session = _sessionManager.CreateSession(cols: 80, rows: 24);

        var accepted = session.Resize(80, 24);

        Assert.True(accepted);
    }

    [Fact]
    public void Resize_AnyViewerCanResize()
    {
        var session = _sessionManager.CreateSession(cols: 80, rows: 24);

        // Any resize should be accepted
        var accepted = session.Resize(120, 40);
        Assert.True(accepted);
        Assert.Equal(120, session.Cols);
        Assert.Equal(40, session.Rows);

        // Another resize should also be accepted
        accepted = session.Resize(100, 30);
        Assert.True(accepted);
        Assert.Equal(100, session.Cols);
        Assert.Equal(30, session.Rows);
    }

    [Fact]
    public void Resize_StateChangeNotified()
    {
        var stateChanged = false;
        var session = _sessionManager.CreateSession(cols: 80, rows: 24);
        session.OnStateChanged += () => stateChanged = true;

        session.Resize(120, 40);

        Assert.True(stateChanged);
    }
}
