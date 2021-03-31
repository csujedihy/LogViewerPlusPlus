﻿using LogViewer.Helpers;
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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace LogViewer {
    public partial class MainWindow : Window {
        public static ColorThemeViewModel colorThemeViewModel = new ColorThemeViewModel();
        public MainWindow() {
            InitializeComponent();
            LogListView.ItemsSource = Log.Logs;
            var view = (CollectionView)CollectionViewSource.GetDefaultView(LogListView.ItemsSource);
            view.Filter = UserFilter;
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
            Loaded += (sender, _) => {
                var border = (Border)LogListView.Template.FindName("Bd", LogListView);
                if (border != null) {
                    border.Padding = new Thickness(0);
                }
            };
        }

        private bool UserFilter(object item) {
            if (Log.FilterToSearchResults && SearchBox.Text.Length > 0) {
                var log = item as Log;
                if ((log.HighlightState & Log.LogHighlightState.SearchResultHighlight) != 0) {
                    return true;
                } else {
                    return false;
                }
            } else {
                return true;
            }
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
                if (Index >= 0 && Index < Log.Logs.Count) {
                    LogListView.SelectedItem = Log.Logs[Index];
                    LogListView.ScrollIntoView(LogListView.SelectedItem);
                }
            } catch (FormatException) {
                return;
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e) {
            var log = Log.GetPrevSearchResult();
            if (log != null) {
                Dispatcher.Invoke(() => {
                    LogListView.SelectedItem = log;
                    LogListView.ScrollIntoView(LogListView.SelectedItem);
                    SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                });
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e) {
            var log = Log.GetNextSearchResult();
            if (log != null) {
                Dispatcher.Invoke(() => {
                    LogListView.SelectedItem = log;
                    LogListView.ScrollIntoView(LogListView.SelectedItem);
                    SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                });
            }
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

        private void UpdateSearchResult(Log log, int numResults) {
            Dispatcher.Invoke(() => {
                if (log != null) {
                    SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, numResults);
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
            UpdateUIBeforeSearch();
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
            UpdateUIBeforeSearch();
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
            UpdateUIBeforeSearch();
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
            UpdateUIBeforeSearch();
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

        private void UpdateUIBeforeSearch() {
            SearchResultTextBox.Text = "Searching...";
            FilterToggle.IsChecked = Log.FilterToSearchResults = false;
            CollectionViewSource.GetDefaultView(LogListView.ItemsSource).Refresh();
        }

        private void FilterToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            Log.FilterToSearchResults = (bool)toggleButton.IsChecked;
            CollectionViewSource.GetDefaultView(LogListView.ItemsSource).Refresh();
        }

        private void DarkModeToggle_Click(object sender, RoutedEventArgs e) {
            foreach (var log in Log.Logs) {
                log.TryHighlight();
            }
            LogListView.Items.Refresh();
        }
    }

    public class ColorThemeViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        #region Light Theme
        public static SolidColorBrush LineNoLightModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("Crimson"));
        public static SolidColorBrush LogTextFgLightModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("Black"));
        public static SolidColorBrush LogListViewBgLightModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("Transparent"));
        public static SolidColorBrush LogTextBgSelectedLightModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("Silver"));
        public static SolidColorBrush LogTextBgSearchResultLightModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("LightSkyBlue"));
        #endregion

        #region Dark Theme
        public static SolidColorBrush LineNoDarkModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#606D83"));
        public static SolidColorBrush LogTextFgDarkModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#E3E3E3"));
        public static SolidColorBrush LogListViewBgDarkModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#282C34"));
        public static SolidColorBrush LogTextBgSelectedDarkModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("Indigo"));
        public static SolidColorBrush LogTextBgSearchResultDarkModeBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("DarkSlateBlue"));
        #endregion

        #region Text Weight
        public static string LogTextNormalWeight = "Normal";
        public static string LogTextSearchResultWeight = "DemiBold";
        #endregion

        public SolidColorBrush LogTextBgSelectedBrush {
            get => DarkModeEnabled ? LogTextBgSelectedDarkModeBrush : LogTextBgSelectedLightModeBrush;
        }

        public SolidColorBrush LogTextBgSearchResultBrush {
            get => DarkModeEnabled ? LogTextBgSearchResultDarkModeBrush : LogTextBgSearchResultLightModeBrush;
        }

        public SolidColorBrush _ListViewBgBrush;
        public SolidColorBrush ListViewBgBrush {
            get => _ListViewBgBrush;
            set {
                _ListViewBgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListViewBgBrush)));
            }
        }
        public SolidColorBrush _LineNoFgBrush;
        public SolidColorBrush LineNoFgBrush {
            get => _LineNoFgBrush;
            set {
                _LineNoFgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineNoFgBrush)));
            }
        }

        public bool _DarkModeEnabled;
        public bool DarkModeEnabled {
            get => _DarkModeEnabled;
            set {
                _DarkModeEnabled = value;
                ToggleDarkMode(_DarkModeEnabled);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DarkModeEnabled)));
            }
        }

        public ColorThemeViewModel() {
            LineNoLightModeBrush.Freeze();
            LogTextFgLightModeBrush.Freeze();
            LogTextBgSelectedLightModeBrush.Freeze();
            LogListViewBgLightModeBrush.Freeze();
            LogTextBgSearchResultLightModeBrush.Freeze();
            LineNoDarkModeBrush.Freeze();
            LogTextFgDarkModeBrush.Freeze();
            LogTextBgSelectedDarkModeBrush.Freeze();
            LogTextBgSearchResultDarkModeBrush.Freeze();
            LogListViewBgDarkModeBrush.Freeze();
            DarkModeEnabled = true;
        }

        public void ToggleDarkMode(bool Enabled) {
            if (Enabled) {
                LwTextBlock.TextColor = LogTextFgDarkModeBrush;
                ListViewBgBrush = LogListViewBgDarkModeBrush;
                LineNoFgBrush = LineNoDarkModeBrush;
            } else {
                LwTextBlock.TextColor = LogTextFgLightModeBrush;
                ListViewBgBrush = LogListViewBgLightModeBrush;
                LineNoFgBrush = LineNoLightModeBrush;
            }
        }
    }

    public class Log : INotifyPropertyChanged {
        [Flags]
        public enum LogHighlightState {
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
            Filter = 8,
        };
        public string Text { get; set; }
        public string LineNoText { get; set; }
        public int LineNoWidth { get; set; }
        private Visibility _textSelectableBoxVisibility;
        public Visibility TextSelectableBoxVisibility {
            get => _textSelectableBoxVisibility;
            set { _textSelectableBoxVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextSelectableBoxVisibility))); }
        }
        private Visibility _textBlockVisibility;
        public Visibility TextBlockVisibility {
            get => _textBlockVisibility;
            set { _textBlockVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextBlockVisibility))); }
        }
        private SolidColorBrush _logRowBgBrush;
        public SolidColorBrush LogRowBgBrush {
            get => _logRowBgBrush;
            set { _logRowBgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogRowBgBrush))); }
        }
        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                _isSelected = value;
                if (value) {
                    HighlightState |= LogHighlightState.SelectedHighlight;
                } else {
                    HighlightState &= ~LogHighlightState.SelectedHighlight;
                }
                TryHighlight();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public LogHighlightState HighlightState = LogHighlightState.NoHighlight;
        public int LineNo;
        public static SmartCollection<Log> Logs = new SmartCollection<Log>();
        public static List<Log> SearchResults = new List<Log>();
        private volatile static bool StopSearch = false;
        private static int SearchMatchesIndicesPos = -1;
        public static Log LogInTextSelectionState;
        public static LogSearchMode SearchMode = LogSearchMode.None;
        private static volatile ManualResetEvent SignalEvent = new ManualResetEvent(true);
        private static BackgroundQueue _workerQueue = new BackgroundQueue();
        public static bool FilterToSearchResults = false;
        public event PropertyChangedEventHandler PropertyChanged;

        public Log(string Text, int LineNo) {
            this.Text = Text;
            this.LineNo = LineNo;
            this.LineNoText = this.LineNo.ToString();
            this.HighlightState = LogHighlightState.NoHighlight;
            this.TextBlockVisibility = Visibility.Visible;
            this.TextSelectableBoxVisibility = Visibility.Hidden;
            this.TryHighlight();
        }

        public void TryHighlight() {
            if ((HighlightState & LogHighlightState.SelectedHighlight) != 0) {
                LogRowBgBrush = MainWindow.colorThemeViewModel.LogTextBgSelectedBrush;
            } else if ((HighlightState & LogHighlightState.SearchResultHighlight) != 0) {
                LogRowBgBrush = MainWindow.colorThemeViewModel.LogTextBgSearchResultBrush;
            } else {
                LogRowBgBrush = Brushes.Transparent;
            }
        }

        public static void ClearSearchResults() {
            foreach (var log in SearchResults) {
                log.HighlightState &= ~LogHighlightState.SearchResultHighlight;
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
            SignalEvent.WaitOne();
            SignalEvent.Reset();

            if (SearchResults.Count == 0 || SearchMatchesIndicesPos >= SearchResults.Count - 1) {
                return null;
            }

            return SearchResults[++SearchMatchesIndicesPos];
        }

        public static Log GetPrevSearchResult() {
            SignalEvent.WaitOne();
            SignalEvent.Reset();

            if (SearchResults.Count == 0 || SearchMatchesIndicesPos <= 0) {
                return null;
            }

            return SearchResults[--SearchMatchesIndicesPos];
        }

        private static bool SearchPatternInText(string text, string pattern) {
            bool IgnoreCase = ((SearchMode & LogSearchMode.CaseSensitive) == 0);
            if ((SearchMode & LogSearchMode.Regex) != 0) {
                return Regex.IsMatch(
                    text, pattern, RegexOptions.Compiled | (IgnoreCase ? RegexOptions.IgnoreCase : 0));
            } else if ((SearchMode & LogSearchMode.WholeWordMatch) != 0) {
                // The simple algorithm here is use \b<regex>\b to find the whole word match.
                // If the first character or the last character is a separator, then remove the corresponding \b.
                var leftPattern = char.IsPunctuation(text[0]) ? @"" : @"\b";
                var rightPattern = char.IsPunctuation(text[text.Length - 1]) ? @"" : @"\b";
                var FinalPattern = string.Format(@"{0}{1}{2}", leftPattern, Regex.Escape(pattern), rightPattern);
                return Regex.IsMatch(
                    text, FinalPattern,
                    RegexOptions.Compiled | (IgnoreCase ? RegexOptions.IgnoreCase : 0));
            } else {
                return
                    text.IndexOf(pattern, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) != -1;
            }
        }

        public static void SearchInLogs(String pattern, Action<Log, int> SearchCompleteCallback) {
            StopSearch = true;
            SignalEvent.WaitOne();
            StopSearch = false;
            SignalEvent.Reset();
            new Thread(() => {
                ClearSearchResults();

                if (pattern.Length > 0) {
                    for (int i = 0; i < Logs.Count; ++i) {
                        if (StopSearch) {
                            break;
                        }
                        if (Logs[i].Text.Length > 0 && SearchPatternInText(Logs[i].Text, pattern)) {
                            SearchResults.Add(Logs[i]);
                            Logs[i].HighlightState |= LogHighlightState.SearchResultHighlight;
                            Logs[i].TryHighlight();
                        }
                    }
                }

                if (SearchResults.Count > 0) {
                    SearchMatchesIndicesPos = 0;
                }

                var firstSearchResult = GetCurrentSearchResult();
                _workerQueue.QueueTask(() => {
                    SearchCompleteCallback(firstSearchResult, SearchResults.Count); });
                SignalEvent.Set();
            }).Start();
        }

        private static void ResetLogs() {
            LogInTextSelectionState = null;
            ClearSearchResults();
            Logs.Clear();
        }

        public static void LoadLogFile(string path) {
            int i = 0;
            string Line;
            var LogFileStream = new StreamReader(path);
            var tempLogs = new List<Log>();

            ResetLogs();

            while ((Line = LogFileStream.ReadLine()) != null) {
                tempLogs.Add(new Log(Line, ++i));
            }
            Logs.AddRange(tempLogs);
            for (i = 0; i < Logs.Count; ++i) {
                Logs[i].LineNoWidth = Logs.Last().LineNoText.Length * 8;
            }
        }
    }
}
