using Ai.Tlbx.MidTerm.Models;

namespace Ai.Tlbx.MidTerm.Services;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(WebApplication app, HistoryService historyService)
    {
        app.MapGet("/api/history", () =>
        {
            return Results.Json(historyService.GetEntries(), AppJsonContext.Default.ListLaunchEntry);
        });

        app.MapPut("/api/history/{id}/star", (string id) =>
        {
            if (historyService.ToggleStar(id))
            {
                return Results.Ok();
            }
            return Results.NotFound();
        });

        app.MapDelete("/api/history/{id}", (string id) =>
        {
            if (historyService.RemoveEntry(id))
            {
                return Results.Ok();
            }
            return Results.NotFound();
        });
    }
}
