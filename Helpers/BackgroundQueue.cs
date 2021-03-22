using System.Diagnostics;
using System.Threading;

namespace LogViewer.Helpers {
    public class BackgroundQueue {
        public Thread runningThread;
        public volatile ManualResetEvent SignalEvent = new ManualResetEvent(true);
        public object key = new object();

        public void QueueTask(ThreadStart action) {
            SignalEvent.Reset();
            new Thread(() => { action(); SignalEvent.Set(); }).Start();
        }
    }
}
