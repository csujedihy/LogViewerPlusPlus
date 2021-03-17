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
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LogViewer {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
            Loaded += MainWindowLoaded;
            LogListView.ItemsSource = Log.Logs;
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e) {
            var window = Window.GetWindow(this);
            window.KeyDown += Window_KeyDown;
            window.MouseDown += Window_MouseDown;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            var log = ((sender as FrameworkElement).DataContext) as Log;
            if (Log.LogInTextSelectionState != null) {
                if (log == null || Log.LogInTextSelectionState != log) {
                    Log.LogInTextSelectionState.TextBlockVisibility = Visibility.Visible;
                    Log.LogInTextSelectionState.TextSelectableBoxVisibility = Visibility.Hidden;
                }
            }
        }

        private void OpenCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
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
                SearchBox.Focus();
            }
        }

        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }

        private async void SearchBoxTextChanged(object sender, TextChangedEventArgs e) {
            var SearchTextBox = (TextBox)sender;
            string SearchTextBoxContent = SearchTextBox.Text;

            await Log.WorkerQueue.QueueTask(() => {
                Log.ClearSearchResults();
                if (SearchTextBoxContent.Length > 0) {
                    Log.SearchInLogs(SearchTextBoxContent);
                    if (Log.GetCurrentSearchResult() != null) {
                        Dispatcher.Invoke(() => {
                            SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                            LogListView.ScrollIntoView(Log.GetCurrentSearchResult());
                            LogListView.SelectedItem = Log.GetCurrentSearchResult();
                        });
                    }
                } else {
                    Dispatcher.Invoke(() => {
                        SearchResultTextBox.Text = "No results";
                    });
                }
            });
        }

        private void GoToLineBoxTextChanged(object sender, TextChangedEventArgs e) {
            var SearchTextBox = (TextBox)sender;
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

        private async void PrevButton_Click(object sender, RoutedEventArgs e) {
            await Log.WorkerQueue.QueueTask(() => {
                var log = Log.GetPrevSearchResult();
                if (log != null) {
                    Dispatcher.Invoke(() => {
                        LogListView.SelectedItem = log;
                        LogListView.ScrollIntoView(LogListView.SelectedItem);
                        SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                    });
                }
            });
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e) {
            await Log.WorkerQueue.QueueTask(() => {
                var log = Log.GetNextSearchResult();
                if (log != null) {
                    Dispatcher.Invoke(() => {
                        LogListView.SelectedItem = log;
                        LogListView.ScrollIntoView(LogListView.SelectedItem);
                        SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                    });
                }
            });
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var log = (sender as FrameworkElement).DataContext as Log;
            log.TextBlockVisibility = Visibility.Hidden;
            log.TextSelectableBoxVisibility = Visibility.Visible;
            Log.LogInTextSelectionState = log;
        }

        private void LogListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var log = Log.LogInTextSelectionState;
            if (log != null) {
                log.TextBlockVisibility = Visibility.Visible;
                log.TextSelectableBoxVisibility = Visibility.Hidden;
            }
        }

        private void CaseSensitiveToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if (toggleButton.IsChecked == null) {
                Log.SearchCaseSensitive = false;
            } else {
                Log.SearchCaseSensitive = (bool)toggleButton.IsChecked;
            }
        }

        private void LogListView_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] Path = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (Path.Length > 1) {
                    MessageBox.Show(
                        "We only accept one log file at a time",
                        Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        MessageBoxResult.No);
                    return;
                }

                Log.LoadLogFile(Path[0]);
            }
        }
    }

    public class Log : INotifyPropertyChanged {
        [Flags]
        enum HighlightState {
            NoHighlight = 0,
            SearchResultHighlight = 1,
            SelectedHighlight = 2,
        };
        public string Text { get; set; }
        public string LineNoText { get; set; }
        public int LineNoWidth { get; set; }
        private Visibility _TextSelectableBoxVisibility;
        public Visibility TextSelectableBoxVisibility {
            get { return _TextSelectableBoxVisibility; }
            set { _TextSelectableBoxVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextSelectableBoxVisibility))); }
        }
        private Visibility _TextBlockVisibility;
        public Visibility TextBlockVisibility {
            get { return _TextBlockVisibility; }
            set { _TextBlockVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextBlockVisibility))); }
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
                if (value) {
                    _IsSelected = true;
                    highlightState |= HighlightState.SelectedHighlight;
                } else {
                    _IsSelected = false;
                    highlightState &= ~HighlightState.SelectedHighlight;
                }
                TryHighlight();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        private HighlightState highlightState = HighlightState.NoHighlight;
        public int LineNo;
        public static ObservableCollection<Log> Logs = new ObservableCollection<Log>();
        public static List<Log> SearchResults = new List<Log>();
        private static int SearchMatchesIndicesPos = -1;
        public static BackgroundQueue WorkerQueue = new BackgroundQueue();
        public static ControlStyleSchema ControlStyleSchema = new ControlStyleSchema(ColorTheme.LightTheme);
        public static Log LogInTextSelectionState;
        public static bool SearchCaseSensitive = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public Log(string Text) {
            this.Text = Text;
            this.LineNo = Logs.Count + 1;
            this.LineNoText = this.LineNo.ToString();
            this.highlightState = HighlightState.NoHighlight;
            this.TextBlockVisibility = Visibility.Visible;
            this.TextSelectableBoxVisibility = Visibility.Hidden;
            this.TryHighlight();
        }

        private void TryHighlight() {
            if ((highlightState & HighlightState.SelectedHighlight) != 0) {
                LogRowBgColor = ControlStyleSchema.LogTextBgSelectedColor;
            } else if ((highlightState & HighlightState.SearchResultHighlight) != 0) {
                LogRowBgColor = ControlStyleSchema.LogTextBgSearchResultColor;
            } else {
                LogRowBgColor = ControlStyleSchema.LogTextBgNormalColor;
            }
        }

        public static void ClearSearchResults() {
            foreach (var log in SearchResults) {
                log.highlightState &= ~HighlightState.SearchResultHighlight;
                log.TryHighlight();
            }
            SearchResults.Clear();
        }

        public static Log GetCurrentSearchResult() {
            if (SearchResults.Count == 0) {
                return null;
            }

            return SearchResults[SearchMatchesIndicesPos];
        }

        public static int GetCurrentSearchResultIndex() {
            return SearchMatchesIndicesPos;
        }

        public static Log GetNextSearchResult() {
            if (SearchResults.Count == 0 || SearchMatchesIndicesPos >= SearchResults.Count - 1) {
                return null;
            }

            return SearchResults[++SearchMatchesIndicesPos];
        }

        public static Log GetPrevSearchResult() {
            if (SearchResults.Count == 0 || SearchMatchesIndicesPos <= 0) {
                return null;
            }

            return SearchResults[--SearchMatchesIndicesPos];
        }

        private static bool SearchInLogText(string Text, string TargetText) {
            if (SearchCaseSensitive) {
                return Text.Contains(TargetText);
            } else {
                return Text.IndexOf(TargetText) != -1;
            }
        }

        public static void SearchInLogs(String Text) {
            for (int i = 0; i < Logs.Count; ++i) {
                if (SearchInLogText(Logs[i].Text, Text)) {
                    SearchResults.Add(Logs[i]);
                    Logs[i].highlightState |= HighlightState.SearchResultHighlight;
                    Logs[i].TryHighlight();
                }
            }

            if (SearchResults.Count > 0) {
                SearchMatchesIndicesPos = 0;
            }
        }

        private static void ResetLogs() {
            Log.LogInTextSelectionState = null;
            ClearSearchResults();
            Logs.Clear();
        }

        public static void LoadLogFile(string Path) {
            string Line;
            var LogFileStream = new StreamReader(Path);
            Debug.WriteLine("Load log file...");
            ResetLogs();

            while ((Line = LogFileStream.ReadLine()) != null) {
                Logs.Add(new Log(Line));
            }
            GenerateLineNoText();
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
