using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ai.Tlbx.MidTerm.Models.Update;
using Ai.Tlbx.MidTerm.Settings;

namespace Ai.Tlbx.MidTerm.Services;

public sealed class SettingsWebSocketHandler
{
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly AuthService _authService;

    public SettingsWebSocketHandler(SettingsService settingsService, UpdateService updateService, AuthService authService)
    {
        _settingsService = settingsService;
        _updateService = updateService;
        _authService = authService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        // SECURITY: Validate auth before accepting WebSocket
        var settings = _settingsService.Load();
        if (settings.AuthenticationEnabled && !string.IsNullOrEmpty(settings.PasswordHash))
        {
            var token = context.Request.Cookies["mm-session"];
            if (token is null || !_authService.ValidateSessionToken(token))
            {
                context.Response.StatusCode = 401;
                return;
            }
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var sendLock = new SemaphoreSlim(1, 1);

        async Task SendMessageAsync(SettingsWsMessage message)
        {
            if (ws.State != WebSocketState.Open)
            {
                return;
            }

            await sendLock.WaitAsync();
            try
            {
                if (ws.State != WebSocketState.Open)
                {
                    return;
                }

                var json = JsonSerializer.Serialize(message, AppJsonContext.Default.SettingsWsMessage);
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
            }
            finally
            {
                sendLock.Release();
            }
        }

        void OnSettingsChange(MidTermSettings s) => _ = SendMessageAsync(new SettingsWsMessage
        {
            Type = "settings",
            Settings = s
        });

        void OnUpdateChange(UpdateInfo u) => _ = SendMessageAsync(new SettingsWsMessage
        {
            Type = "update",
            Update = u
        });

        var settingsListenerId = _settingsService.AddSettingsListener(OnSettingsChange);
        var updateListenerId = _updateService.AddUpdateListener(OnUpdateChange);

        try
        {
            // Send initial settings
            await SendMessageAsync(new SettingsWsMessage
            {
                Type = "settings",
                Settings = _settingsService.Load()
            });

            // Send initial update info if available
            var latestUpdate = _updateService.LatestUpdate;
            if (latestUpdate is not null)
            {
                await SendMessageAsync(new SettingsWsMessage
                {
                    Type = "update",
                    Update = latestUpdate
                });
            }

            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
                catch
                {
                    break;
                }
            }
        }
        finally
        {
            _settingsService.RemoveSettingsListener(settingsListenerId);
            _updateService.RemoveUpdateListener(updateListenerId);
            sendLock.Dispose();

            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }
}
