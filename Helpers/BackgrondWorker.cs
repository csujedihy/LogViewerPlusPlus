using System;
using System.Threading;
using System.Threading.Tasks;

namespace LogViewer.Helpers {
    public class BackgroundQueue {
        private Task previousTask = Task.FromResult(true);
        private object mutex = new object();

        public Task QueueTask(Action action) {
            lock (mutex) {
                previousTask = previousTask.ContinueWith(
                  t => action(),
                  CancellationToken.None,
                  TaskContinuationOptions.None,
                  TaskScheduler.Default);
                return previousTask;
            }
        }

        public Task<T> QueueTask<T>(Func<T> func) {
            lock (mutex) {
                var task = previousTask.ContinueWith(
                  t => func(),
                  CancellationToken.None,
                  TaskContinuationOptions.None,
                  TaskScheduler.Default);
                previousTask = task;
                return task;
            }
        }
    }
}
