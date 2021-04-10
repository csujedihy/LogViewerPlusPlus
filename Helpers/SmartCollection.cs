using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace LogViewer.Helpers {
    public interface INotifyRemoval<T> {
        void OnRemoval(T x);
    }

    public class SmartCollection<T> : ObservableCollection<T> {
        public delegate void OnRemovalDelegate(T x);
        private readonly OnRemovalDelegate del;
        public SmartCollection(OnRemovalDelegate del = null)
            : base() {
            this.del = del;
        }

        protected override void RemoveItem(int index) {
            del?.Invoke(Items[index]);
            base.RemoveItem(index);
        }

        public void AddRange(IEnumerable<T> range) {
            foreach (var item in range) {
                Items.Add(item);
            }

            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
