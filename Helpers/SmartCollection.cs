using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace LogViewer.Helpers {
    public class SmartCollection<T> : ObservableCollection<T> {
        public SmartCollection()
            : base() {
        }

        public SmartCollection(IEnumerable<T> collection)
            : base(collection) {
        }

        public SmartCollection(List<T> list)
            : base(list) {
        }

        protected override void RemoveItem(int index) {
            // TODO: add callback.
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
