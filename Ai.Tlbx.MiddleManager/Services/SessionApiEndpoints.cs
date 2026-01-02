using Ai.Tlbx.MiddleManager.Common.Protocol;
using Ai.Tlbx.MiddleManager.Models;
using Ai.Tlbx.MiddleManager.Common.Shells;

namespace Ai.Tlbx.MiddleManager.Services;

public static class SessionApiEndpoints
{
    public static void MapSessionEndpoints(
        WebApplication app,
        ConHostSessionManager sessionManager)
    {
        app.MapGet("/api/sessions", () =>
        {
            return Results.Json(sessionManager.GetSessionList(), AppJsonContext.Default.SessionListDto);
        });

        app.MapPost("/api/sessions", async (CreateSessionRequest? request) =>
        {
            var cols = request?.Cols ?? 120;
            var rows = request?.Rows ?? 30;

            ShellType? shellType = null;
            if (!string.IsNullOrEmpty(request?.Shell) && Enum.TryParse<ShellType>(request.Shell, true, out var parsed))
            {
                shellType = parsed;
            }

            var sessionInfo = await sessionManager.CreateSessionAsync(
                shellType?.ToString(), cols, rows, request?.WorkingDirectory);

            if (sessionInfo is null)
            {
                return Results.Problem("Failed to create session");
            }

            return Results.Json(MapToDto(sessionInfo), AppJsonContext.Default.SessionInfoDto);
        });

        app.MapDelete("/api/sessions/{id}", async (string id) =>
        {
            await sessionManager.CloseSessionAsync(id);
            return Results.Ok();
        });

        app.MapPost("/api/sessions/{id}/resize", async (string id, ResizeRequest request) =>
        {
            var session = sessionManager.GetSession(id);
            if (session is null)
            {
                return Results.NotFound();
            }
            await sessionManager.ResizeSessionAsync(id, request.Cols, request.Rows);
            return Results.Json(new ResizeResponse
            {
                Accepted = true,
                Cols = request.Cols,
                Rows = request.Rows
            }, AppJsonContext.Default.ResizeResponse);
        });

        app.MapGet("/api/sessions/{id}/buffer", async (string id) =>
        {
            var session = sessionManager.GetSession(id);
            if (session is null)
            {
                return Results.NotFound();
            }
            var buffer = await sessionManager.GetBufferAsync(id);
            return Results.Bytes(buffer ?? []);
        });

        app.MapPut("/api/sessions/{id}/name", async (string id, RenameSessionRequest request, bool auto = false) =>
        {
            if (!await sessionManager.SetSessionNameAsync(id, request.Name, isManual: !auto))
            {
                return Results.NotFound();
            }
            return Results.Ok();
        });
    }

    private static SessionInfoDto MapToDto(SessionInfo sessionInfo)
    {
        return new SessionInfoDto
        {
            Id = sessionInfo.Id,
            Pid = sessionInfo.Pid,
            CreatedAt = sessionInfo.CreatedAt,
            IsRunning = sessionInfo.IsRunning,
            ExitCode = sessionInfo.ExitCode,
            CurrentWorkingDirectory = sessionInfo.CurrentWorkingDirectory,
            Cols = sessionInfo.Cols,
            Rows = sessionInfo.Rows,
            ShellType = sessionInfo.ShellType,
            Name = sessionInfo.Name,
            ManuallyNamed = sessionInfo.ManuallyNamed
        };
    }
}
