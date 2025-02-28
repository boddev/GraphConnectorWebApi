using System.Threading.Channels;

public class BackgroundTaskQueue
{
    // Define a private readonly Channel to hold the background work items
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    // Constructor to initialize the Channel with a bounded capacity
    public BackgroundTaskQueue(int capacity)
    {
        _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(capacity);
    }

    // Method to queue a background work item asynchronously
    public async Task QueueBackgroundWorkItemAsync(Func<CancellationToken, Task> workItem)
    {
        await _queue.Writer.WriteAsync(workItem);
    }

    // Method to dequeue a background work item asynchronously
    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}

public class QueuedHostedService : BackgroundService
{
    // Define a private readonly BackgroundTaskQueue to hold the task queue
    private readonly BackgroundTaskQueue _taskQueue;

    // Constructor to initialize the BackgroundTaskQueue
    public QueuedHostedService(BackgroundTaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    }

    // Override the ExecuteAsync method to process the queued work items
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Dequeue a work item from the task queue
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);
            try
            {
                // Execute the work item
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                // Handle exception
            }
        }
    }
}
