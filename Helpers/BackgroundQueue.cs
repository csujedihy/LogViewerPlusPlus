﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogViewer.Helpers {
    public class BackgroundQueue {
        private Task previousTask = Task.FromResult(true);
        private object key = new object();

        public Task QueueTask(Action action) {
            lock (key) {
                previousTask = previousTask.ContinueWith(
                  t => action(),
                  CancellationToken.None,
                  TaskContinuationOptions.None,
                  TaskScheduler.Default);
                return previousTask;
            }
        }
    }
}
