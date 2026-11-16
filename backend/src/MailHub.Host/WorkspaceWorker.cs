using MailHub.Application.Workspace;

namespace MailHub.Host;

public sealed class WorkspaceWorker(WorkspaceService workspace, ILogger<WorkspaceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await workspace.TickAsync(stoppingToken); }
            catch (OperationCanceledException) { }
            catch (Exception error) { logger.LogWarning("Background step failed: {ErrorType}", error.GetType().Name); }
            try { await Task.Delay(500, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
