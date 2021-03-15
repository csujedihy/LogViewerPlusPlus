using LogViewer.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using System.Windows;
using System.Windows.Controls;

using System.Windows.Input;

namespace LogViewer {
    public partial class MainWindow : Window {
        public MainWindow() {
            this.InitializeComponent();
            this.Loaded += MainWindowLoaded;
            LogListView.ItemsSource = Log.Logs;
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e) {
            var window = Window.GetWindow(this);
            window.KeyDown += Window_KeyDown;
        }

        private void OpenCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                Log.LoadLogFile(openFileDialog.FileName);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) {
                var sb = new StringBuilder();
                var selectedItems = LogListView.SelectedItems;

                foreach (var item in selectedItems) {
                    sb.Append($"{((Log)item).Text}\n");
                }

                Clipboard.SetDataObject(sb.ToString());
            } else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control) {
                GoToLineBox.Focus();
            } else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) {
                SearchResultTextBox.Focus();
            }
        }

        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            this.Close();
        }

        private async void SearchBoxTextChanged(object sender, TextChangedEventArgs e) {
            TextBox SearchTextBox = (TextBox)sender;
            string SearchTextBoxContent = SearchTextBox.Text;

            await Log.SearchWorkerQueue.QueueTask(() => {
                Log.ClearSearchResults();
                if (SearchTextBoxContent.Length > 0) {
                    Log.SearchInLogs(SearchTextBoxContent);
                }
                int LeastIndex = Log.Logs.Count;
                foreach (var Index in Log.SearchMatchedIndices) {
                    Log.Logs[Index].TextWeight = "DemiBold";
                    Log.Logs[Index].LogRowBgColor = "Blue";
                    LeastIndex = Math.Min(LeastIndex, Index);
                }
                if (LeastIndex < Log.Logs.Count) {
                    this.Dispatcher.Invoke(() => {
                        LogListView.ScrollIntoView(LogListView.Items[LeastIndex]);
                        LogListView.SelectedItem = LogListView.Items[LeastIndex];
                    });
                }
            });
        }

        private void GoToLineBoxTextChanged(object sender, TextChangedEventArgs e) {
            TextBox SearchTextBox = (TextBox)sender;
            try {
                int Index = Int32.Parse(SearchTextBox.Text) - 1;
                if (Index >= 0 && Index < LogListView.Items.Count) {
                    LogListView.SelectedItem = LogListView.Items[Index];
                    LogListView.ScrollIntoView(LogListView.SelectedItem);
                }
            } catch (FormatException) {
                return;
            }
        }
    }

    public class Log : INotifyPropertyChanged {
        public string Text { get; set; }
        public string LineNoText { get; set; }
        public int LineNoWidth { get; set; }
        private string _TextWeight;
        public string TextWeight {
            get { return _TextWeight; }
            set { _TextWeight = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextWeight))); }
        }
        private string _LogRowBgColor;
        public string LogRowBgColor {
            get { return _LogRowBgColor; }
            set { _LogRowBgColor = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogRowBgColor))); }
        }
        private bool _IsSelected;
        public bool IsSelected {
            get { return _IsSelected; }
            set {
                SelectLog(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public uint LineNo;
        public static uint Count;
        public static ObservableCollection<Log> Logs = new ObservableCollection<Log>();
        public static HashSet<int> SearchMatchedIndices = new HashSet<int>();
        public static BackgroundQueue SearchWorkerQueue = new BackgroundQueue();

        public event PropertyChangedEventHandler PropertyChanged;

        public Log(string Text) {
            ++Count;
            this.Text = Text;
            this.LineNo = Count;
            this.LineNoText = this.LineNo.ToString();
            this.TextWeight = "Normal";
            this.LogRowBgColor = "White";
        }

        private void SelectLog(bool IsSelected) {
            if (IsSelected) {
                _IsSelected = true;
                LogRowBgColor = "Silver";
            } else {
                _IsSelected = false;
                LogRowBgColor = "White";
            }
        }

        public static void ClearSearchResults() {
            foreach (var Index in SearchMatchedIndices) {
                Logs[Index].TextWeight = "Normal";
                Logs[Index].LogRowBgColor = "White";
            }
            SearchMatchedIndices.Clear();
        }

        public static void SearchInLogs(String Text) {
            for (int i = 0; i < Count; ++i) {
                if (Logs[i].Text.IndexOf(Text) != -1) {
                    SearchMatchedIndices.Add(i);
                }
            }
        }

        private static void ResetLogs() {
            Logs.Clear();
            Count = 0;
        }

        public static void LoadLogFile(string Path) {
            string Line;
            StreamReader LogFileStream = new StreamReader(Path);

            ResetLogs();

            while ((Line = LogFileStream.ReadLine()) != null) {
                Log.Logs.Add(new Log(Line));
            }
            Log.GenerateLineNoText();
        }

        public static void GenerateLineNoText() {
            if (Logs.Count == 0) {
                return;
            }

            for (int i = 0; i < Logs.Count; ++i) {
                Logs[i].LineNoWidth = Logs.Last().LineNoText.Length * 8;
            }
        }
    }
}
