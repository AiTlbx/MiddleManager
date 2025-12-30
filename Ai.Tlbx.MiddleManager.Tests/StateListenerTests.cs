using Ai.Tlbx.MiddleManager.Settings;
using Ai.Tlbx.MiddleManager.Shells;
using Ai.Tlbx.MiddleManager.Services;
using Xunit;

namespace Ai.Tlbx.MiddleManager.Tests;

public class StateListenerTests : IDisposable
{
    private readonly SessionManager _sessionManager = new(new ShellRegistry(), new SettingsService());

    public void Dispose()
    {
        _sessionManager.Dispose();
    }

    [Fact]
    public void AddStateListener_CalledOnSessionCreate()
    {
        var callCount = 0;
        _sessionManager.AddStateListener(() => callCount++);

        _sessionManager.CreateSession();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void AddStateListener_CalledOnSessionClose()
    {
        var session = _sessionManager.CreateSession();
        var callCount = 0;
        _sessionManager.AddStateListener(() => callCount++);

        _sessionManager.CloseSession(session.Id);

        // Close triggers at least 1 notification (may be more due to PTY shutdown)
        Assert.True(callCount >= 1, $"Expected at least 1 notification, got {callCount}");
    }

    [Fact]
    public void RemoveStateListener_StopsNotifications()
    {
        var callCount = 0;
        var listenerId = _sessionManager.AddStateListener(() => callCount++);

        _sessionManager.CreateSession();
        Assert.Equal(1, callCount);

        _sessionManager.RemoveStateListener(listenerId);
        _sessionManager.CreateSession();

        Assert.Equal(1, callCount); // Should not increase
    }

    [Fact]
    public void MultipleListeners_AllNotified()
    {
        var count1 = 0;
        var count2 = 0;
        _sessionManager.AddStateListener(() => count1++);
        _sessionManager.AddStateListener(() => count2++);

        _sessionManager.CreateSession();

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void StateListener_FailureDoesNotAffectOthers()
    {
        var count = 0;
        _sessionManager.AddStateListener(() => throw new Exception("Listener failure"));
        _sessionManager.AddStateListener(() => count++);

        _sessionManager.CreateSession();

        Assert.Equal(1, count);
    }

    [Fact]
    public void GetSessionList_ContainsCreatedSession()
    {
        var session = _sessionManager.CreateSession();

        var list = _sessionManager.GetSessionList();

        Assert.Single(list.Sessions);
        Assert.Equal(session.Id, list.Sessions[0].Id);
    }

    [Fact]
    public void GetSessionList_ContainsMultipleSessions()
    {
        var session1 = _sessionManager.CreateSession();
        var session2 = _sessionManager.CreateSession();
        var session3 = _sessionManager.CreateSession();

        var list = _sessionManager.GetSessionList();

        Assert.Equal(3, list.Sessions.Count);
        var ids = list.Sessions.Select(s => s.Id).ToHashSet();
        Assert.Contains(session1.Id, ids);
        Assert.Contains(session2.Id, ids);
        Assert.Contains(session3.Id, ids);
    }

    [Fact]
    public void GetSessionList_SessionsOrderedByCreatedAt()
    {
        var session1 = _sessionManager.CreateSession();
        Thread.Sleep(10); // Ensure different timestamps
        var session2 = _sessionManager.CreateSession();
        Thread.Sleep(10);
        var session3 = _sessionManager.CreateSession();

        var list = _sessionManager.GetSessionList();

        Assert.Equal(3, list.Sessions.Count);
        Assert.Equal(session1.Id, list.Sessions[0].Id);
        Assert.Equal(session2.Id, list.Sessions[1].Id);
        Assert.Equal(session3.Id, list.Sessions[2].Id);
    }

    [Fact]
    public void GetSessionList_SessionRemovedAfterClose()
    {
        var session1 = _sessionManager.CreateSession();
        var session2 = _sessionManager.CreateSession();

        _sessionManager.CloseSession(session1.Id);
        var list = _sessionManager.GetSessionList();

        Assert.Single(list.Sessions);
        Assert.Equal(session2.Id, list.Sessions[0].Id);
    }

    [Fact]
    public void GetSessionList_EmptyWhenNoSessions()
    {
        var list = _sessionManager.GetSessionList();

        Assert.Empty(list.Sessions);
    }

    [Fact]
    public void GetSessionList_SessionHasCorrectProperties()
    {
        var session = _sessionManager.CreateSession(cols: 100, rows: 40);

        var list = _sessionManager.GetSessionList();
        var info = list.Sessions[0];

        Assert.Equal(session.Id, info.Id);
        Assert.Equal(100, info.Cols);
        Assert.Equal(40, info.Rows);
        Assert.True(info.IsRunning);
        Assert.NotNull(info.ShellType);
    }
}
