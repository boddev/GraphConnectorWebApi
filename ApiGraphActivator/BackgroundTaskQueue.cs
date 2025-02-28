using System.Threading.Channels;

public class BackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public BackgroundTaskQueue(int capacity)
    {
        _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(capacity);
    }

    public async Task QueueBackgroundWorkItemAsync(Func<CancellationToken, Task> workItem)
    {
        await _queue.Writer.WriteAsync(workItem);
    }

    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}

public class QueuedHostedService : BackgroundService
{
    private readonly BackgroundTaskQueue _taskQueue;

    public QueuedHostedService(BackgroundTaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);
            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                // Handle exception
            }
        }
    }
}
