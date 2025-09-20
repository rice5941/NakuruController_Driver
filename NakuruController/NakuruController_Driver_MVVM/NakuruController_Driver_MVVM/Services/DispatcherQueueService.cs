using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnoApp1.Services;
public interface IDispatcherQueueService
{
    Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; }
}
public class DispatcherQueueService : IDispatcherQueueService
{
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

    public Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue
    {
        get => _dispatcherQueue ?? throw new InvalidOperationException("DispatcherQueue not initialized");
    }

    public void Initialize(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }
}
