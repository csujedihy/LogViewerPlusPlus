using LogViewer.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LogViewer {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
            LogListView.ItemsSource = Log.Logs;
            var window = Window.GetWindow(this);
            window.KeyDown += (sender, e) => {
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
            };
            window.MouseDown += (sender, e) => {
                var log = ((sender as FrameworkElement).DataContext) as Log;
                if (Log.LogInTextSelectionState != null) {
                    if (log == null || Log.LogInTextSelectionState != log) {
                        Log.LogInTextSelectionState.TextBlockVisibility = Visibility.Visible;
                        Log.LogInTextSelectionState.TextSelectableBoxVisibility = Visibility.Hidden;
                    }
                }
            };
        }


        private void OpenCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                Log.LoadLogFile(openFileDialog.FileName);
            }
        }

        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            Close();
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

        private void UpdateSearchResult(Log log) {
            Dispatcher.Invoke(() => {
                if (log != null) {
                    SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                    LogListView.ScrollIntoView(Log.GetCurrentSearchResult());
                    LogListView.SelectedItem = Log.GetCurrentSearchResult();
                } else {
                    Dispatcher.Invoke(() => {
                        SearchResultTextBox.Text = "No results";
                    });
                }
            });
        }

        private void SearchBoxTextChanged(object sender, TextChangedEventArgs e) {
            var SearchTextBox = (TextBox)sender;
            string SearchTextBoxContent = SearchTextBox.Text;
            Log.SearchInLogs(SearchTextBoxContent, UpdateSearchResult);
        }

        private void CaseSensitiveToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if ((bool)toggleButton.IsChecked) {
                Log.SearchMode |= Log.LogSearchMode.CaseSensitive;
            } else {
                Log.SearchMode &= ~Log.LogSearchMode.CaseSensitive;
            }

            string SearchTextBoxContent = SearchBox.Text;
            Log.SearchInLogs(SearchTextBoxContent, UpdateSearchResult);
        }

        private void ExactMatchToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if ((bool)toggleButton.IsChecked) {
                Log.SearchMode |= Log.LogSearchMode.WholeWordMatch;
            } else {
                Log.SearchMode &= ~Log.LogSearchMode.WholeWordMatch;
            }

            string SearchTextBoxContent = SearchBox.Text;
            Log.SearchInLogs(SearchTextBoxContent, UpdateSearchResult);
        }

        private void RegexToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if ((bool)toggleButton.IsChecked) {
                Log.SearchMode |= Log.LogSearchMode.Regex;
            } else {
                Log.SearchMode &= ~Log.LogSearchMode.Regex;
            }

            string SearchTextBoxContent = SearchBox.Text;
            Log.SearchInLogs(SearchTextBoxContent, UpdateSearchResult);
        }

        private void LogListView_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] Path = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (Path.Length > 1) {
                    MessageBox.Show(
                        "We only accept one log file at a time.",
                        Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        MessageBoxResult.No);
                    return;
                }

                new Thread(() => {
                    Dispatcher.Invoke(() => {
                        Log.LoadLogFile(Path[0]);
                    });
                }).Start();
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
        [Flags]
        public enum LogSearchMode {
            None = 0,
            CaseSensitive = 1,
            Regex = 2,
            WholeWordMatch = 4,
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
        public static SmartCollection<Log> Logs = new();
        public static List<Log> SearchResults = new();
        private static int SearchMatchesIndicesPos = -1;
        public static BackgroundQueue WorkerQueue = new();
        public static ControlStyleSchema ControlStyleSchema = new(ColorTheme.LightTheme);
        public static Log LogInTextSelectionState;
        public static LogSearchMode SearchMode = LogSearchMode.None;

        public event PropertyChangedEventHandler PropertyChanged;

        public Log(string Text, int LineNo) {
            this.Text = Text;
            this.LineNo = LineNo;
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

        private static bool SearchInLogText(string Text, string Pattern) {
            bool IgnoreCase = ((SearchMode & LogSearchMode.CaseSensitive) == 0);
            if ((SearchMode & LogSearchMode.Regex) != 0) {
                return Regex.IsMatch(
                    Text, Pattern,
                    RegexOptions.Compiled | (IgnoreCase ? RegexOptions.IgnoreCase : 0));
            } else if ((SearchMode & LogSearchMode.WholeWordMatch) != 0) {
                // The simple algorithm here is use \b<regex>\b to find the whole word match.
                // If the first character or the last character is a separator, then remove the corresponding \b.
                var leftPattern = char.IsPunctuation(Text[0]) ? @"" : @"\b";
                var rightPattern = char.IsPunctuation(Text[Text.Length - 1]) ? @"" : @"\b";
                var FinalPattern = string.Format(@"{0}{1}{2}", leftPattern, Regex.Escape(Pattern), rightPattern);
                return Regex.IsMatch(
                    Text, FinalPattern,
                    RegexOptions.Compiled | (IgnoreCase ? RegexOptions.IgnoreCase : 0));
            } else {
                return
                    Text.Contains(
                        Pattern,
                        IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
        }

        public static async void SearchInLogs(String Text, Action<Log> action) {
            await WorkerQueue.QueueTask(() => {
                Log.ClearSearchResults();

                if (Text.Length > 0) {
                    for (int i = 0; i < Logs.Count; ++i) {
                        if (Logs[i].Text.Length > 0 && SearchInLogText(Logs[i].Text, Text)) {
                            SearchResults.Add(Logs[i]);
                            Logs[i].highlightState |= HighlightState.SearchResultHighlight;
                            Logs[i].TryHighlight();
                        }
                    }
                }

                if (SearchResults.Count > 0) {
                    SearchMatchesIndicesPos = 0;
                }

                action(Log.GetCurrentSearchResult());
            });
        }

        private static void ResetLogs() {
            Log.LogInTextSelectionState = null;
            ClearSearchResults();
            Logs.Clear();
        }

        public static void LoadLogFile(string Path) {
            int i = 0;
            string Line;
            var LogFileStream = new StreamReader(Path);
            var tempLogs = new List<Log>();

            ResetLogs();

            while ((Line = LogFileStream.ReadLine()) != null) {
                tempLogs.Add(new Log(Line, i++));
            }
            Logs.AddRange(tempLogs);
            for (i = 0; i < Logs.Count; ++i) {
                Logs[i].LineNoWidth = Logs.Last().LineNoText.Length * 8;
            }
        }
    }
}
