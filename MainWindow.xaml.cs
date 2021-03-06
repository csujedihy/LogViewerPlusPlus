using LogViewer.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LogViewer {
    public partial class MainWindow : Window {
        public static ColorThemeViewModel colorThemeViewModel = new ColorThemeViewModel();
        private DispatcherTimer _searchTextBoxTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        public MainWindow() {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs _) {
            var border = (Border)LogListView.Template.FindName("Bd", LogListView);
            if (border != null) {
                border.Padding = new Thickness(0);
            }
            LogListView.ItemsSource = Log.Logs;
            FilterDataGrid.ItemsSource = Filter.Filters;
            var view = (CollectionView)CollectionViewSource.GetDefaultView(LogListView.ItemsSource);
            view.Filter = UserFilter;
            PrevButton.Click += SearchPrev;
            NextButton.Click += SearchNext;
            this.KeyDown += (s, e) => {
                if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) {
                    Log.CopySelectedLogs();
                } else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control) {
                    GoToLineBox.Focus();
                } else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) {
                    SearchBox.Focus();
                } else if (e.Key == Key.F3) {
                    SearchPrev(this, null);
                } else if (e.Key == Key.F4) {
                    SearchNext(this, null);
                }
            };
            _searchTextBoxTimer.Tick += _searchTextBoxTimer_Tick;
        }

        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }

        private void LogListView_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.A && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))) {
                LogListView.SelectAll();
                e.Handled = true;
            }
        }

        #region Go to line
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
        #endregion

        #region Switch textbox view for double-click

        private void LogListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var log = (LogListView.SelectedItem as Log);
            // log could be null when we open another log file.
            if (log != null) {
                LogTextTextBox.Visibility = Visibility.Visible;
                LogTextTextBox.Document.Blocks.Clear();
                LogTextTextBox.Document.Blocks.Add(new Paragraph(new Run(log.Text)));
            } else {
                LogTextTextBox.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Toggle Handlers
        private void CaseSensitiveToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if ((bool)toggleButton.IsChecked) {
                Log.SearchMode |= LogSearchMode.CaseSensitive;
            } else {
                Log.SearchMode &= ~LogSearchMode.CaseSensitive;
            }

            UpdateUIBeforeSearch();
            Log.SearchInLogs((string)SearchBox.Text.Clone(), UpdateSearchResult);
        }

        private void ExactMatchToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if ((bool)toggleButton.IsChecked) {
                Log.SearchMode |= LogSearchMode.WholeWordMatch;
            } else {
                Log.SearchMode &= ~LogSearchMode.WholeWordMatch;
            }

            UpdateUIBeforeSearch();
            Log.SearchInLogs((string)SearchBox.Text.Clone(), UpdateSearchResult);
        }

        private void RegexToggle_Click(object sender, RoutedEventArgs e) {
            var toggleButton = sender as ToggleButton;
            if ((bool)toggleButton.IsChecked) {
                Log.SearchMode |= LogSearchMode.Regex;
            } else {
                Log.SearchMode &= ~LogSearchMode.Regex;
            }

            UpdateUIBeforeSearch();
            Log.SearchInLogs((string)SearchBox.Text.Clone(), UpdateSearchResult);
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

        #endregion

        #region Open log file
        private void LoadLogFileAndShowProgress(string path) {
            var loadingWindow = new ProgressWindow
            {
                Owner = this
            };

            loadingWindow.Loaded += (s, e) => {
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += (_s, _e) => {
                    var progress = new Progress<long>((value) => {
                        loadingWindow.Dispatcher.Invoke(() => {
                            loadingWindow.LoadingProgressBar.Value = value;
                        });
                    });
                    Log.ParseLogFile(path, progress, (logs) => {
                        Dispatcher.Invoke(() => {
                            Log.LoadLogs(logs);
                        });
                    });
                };
                worker.RunWorkerCompleted += (_s, _e) => loadingWindow.Close();
                worker.RunWorkerAsync();
            };
            loadingWindow.ShowDialog();
        }

        private void OpenCommandHandler(object sender, ExecutedRoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                LoadLogFileAndShowProgress(openFileDialog.FileName);
            }
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

                LoadLogFileAndShowProgress(Path[0]);
            }
        }
        #endregion

        #region Search UI logic
        private void UpdateSearchResult(Log log, int numResults) {
            Dispatcher.Invoke(() => {
                if (log != null) {
                    SearchResultTextBox.Text = String.Format("1 of {0}", numResults);
                    LogListView.ScrollIntoView(log);
                    LogListView.SelectedItem = log;
                } else {
                    Dispatcher.Invoke(() => {
                        SearchResultTextBox.Text = "No results";
                    });
                }
            });
        }

        private void UpdateUIBeforeSearch() {
            SearchResultTextBox.Text = "Searching...";
            FilterToggle.IsChecked = Log.FilterToSearchResults = false;
            CollectionViewSource.GetDefaultView(LogListView.ItemsSource).Refresh();
        }

        private void GoToNextOrPrevSearchResult(Log log) {
            if (log != null) {
                Dispatcher.Invoke(() => {
                    LogListView.SelectedItem = log;
                    LogListView.ScrollIntoView(LogListView.SelectedItem);
                    SearchResultTextBox.Text = String.Format("{0} of {1}", Log.GetCurrentSearchResultIndex() + 1, Log.SearchResults.Count);
                });
            }
        }

        private void SearchNext(object sender, RoutedEventArgs e) {
            Log.GetNextSearchResult(GoToNextOrPrevSearchResult);

        }

        private void SearchPrev(object sender, RoutedEventArgs e) {
            Log.GetPrevSearchResult(GoToNextOrPrevSearchResult);
        }

        private void SearchBoxTextChanged(object sender, TextChangedEventArgs e) {
            _searchTextBoxTimer.Stop();
            _searchTextBoxTimer.Start();
        }

        private void _searchTextBoxTimer_Tick(object sender, EventArgs e) {
            _searchTextBoxTimer.Stop();
            UpdateUIBeforeSearch();
            Log.SearchInLogs(SearchBox.Text, UpdateSearchResult);
        }

        private bool UserFilter(object item) {
            if (Log.FilterToSearchResults && SearchBox.Text.Length > 0) {
                var log = item as Log;
                lock(log.HighlightStateLock) {
                    if ((log.HighlightState & Log.LogHighlightState.SearchResultHighlight) != 0) {
                        return true;
                    } else {
                        return false;
                    }
                }
            } else {
                return true;
            }
        }
        #endregion

        private void AddFilterHandler(object sender, RoutedEventArgs e) {
            var filter = new Filter();
            var addFilterWindow = new AddFilterWindow
            {
                Owner = this
            };
            addFilterWindow.Title = "Add Filter";
            addFilterWindow.Loaded += (_s, _e) => {
                addFilterWindow.filter = new Filter(filter);
                addFilterWindow.DataContext = addFilterWindow.filter;
            };
            var ok = addFilterWindow.ShowDialog();
            if (ok == true) {
                filter.UpdateFilter(addFilterWindow.filter);
                Debug.Assert(filter._IsClone == false);
                Filter.Filters.Add(filter);
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var row = sender as DataGridRow;
            var filter = (row.Item as Filter);
            var addFilterWindow = new AddFilterWindow   
            {
                Owner = this
            };
            addFilterWindow.Title = "Edit Filter";
            addFilterWindow.Loaded += (_s, _e) => {
                addFilterWindow.filter = new Filter(filter);
                addFilterWindow.DataContext = addFilterWindow.filter;
            };
            var ok = addFilterWindow.ShowDialog();
            if (ok == true) {
                filter.UpdateFilter(addFilterWindow.filter);
                Debug.Assert(filter._IsClone == false);
            }
        }
    }

    public class ColorThemeViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        #region Light theme constants
        public static Brush LineNoLightModeBrush = (Brush)(new BrushConverter().ConvertFrom("Crimson"));
        public static Brush LogTextFgLightModeBrush = (Brush)(new BrushConverter().ConvertFrom("Black"));
        public static Brush LogListViewBgLightModeBrush = (Brush)(new BrushConverter().ConvertFrom("Transparent"));
        public static Brush LogTextBgSelectedLightModeBrush = (Brush)(new BrushConverter().ConvertFrom("Silver"));
        public static Brush LogTextBgSearchResultLightModeBrush = (Brush)(new BrushConverter().ConvertFrom("LightSkyBlue"));
        #endregion

        #region Dark theme constants
        public static Brush LineNoDarkModeBrush = (Brush)(new BrushConverter().ConvertFrom("#606D83"));
        public static Brush LogTextFgDarkModeBrush = (Brush)(new BrushConverter().ConvertFrom("#E3E3E3"));
        public static Brush LogListViewBgDarkModeBrush = (Brush)(new BrushConverter().ConvertFrom("#282C34"));
        public static Brush LogTextBgSelectedDarkModeBrush = (Brush)(new BrushConverter().ConvertFrom("Indigo"));
        public static Brush LogTextBgSearchResultDarkModeBrush = (Brush)(new BrushConverter().ConvertFrom("DarkSlateBlue"));
        #endregion

        #region Text weight constants
        public static string LogTextNormalWeight = "Normal";
        public static string LogTextSearchResultWeight = "DemiBold";
        #endregion

        #region Properties
        public Brush LogTextBgSelectedBrush {
            get => DarkModeEnabled ? LogTextBgSelectedDarkModeBrush : LogTextBgSelectedLightModeBrush;
        }

        public Brush LogTextBgSearchResultBrush {
            get => DarkModeEnabled ? LogTextBgSearchResultDarkModeBrush : LogTextBgSearchResultLightModeBrush;
        }

        public Brush _ListViewBgBrush;
        public Brush ListViewBgBrush {
            get => _ListViewBgBrush;
            set {
                _ListViewBgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListViewBgBrush)));
            }
        }
        public Brush _LineNoFgBrush;
        public Brush LineNoFgBrush {
            get => _LineNoFgBrush;
            set {
                _LineNoFgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineNoFgBrush)));
            }
        }

        public Brush _LogTextFgBrush;
        public Brush LogTextFgBrush {
            get => _LogTextFgBrush;
            set {
                _LogTextFgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogTextFgBrush)));
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
        #endregion

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
                LogTextFgBrush = LogTextFgDarkModeBrush;
                ListViewBgBrush = LogListViewBgDarkModeBrush;
                LineNoFgBrush = LineNoDarkModeBrush;
            } else {
                LogTextFgBrush = LogTextFgLightModeBrush;
                ListViewBgBrush = LogListViewBgLightModeBrush;
                LineNoFgBrush = LineNoLightModeBrush;
            }
        }
    }

    [Flags]
    public enum LogSearchMode {
        None = 0,
        CaseSensitive = 1,
        Regex = 2,
        WholeWordMatch = 4,
    };

    public class Filter : INotifyPropertyChanged {
        #region Properties
        private bool _IsEnabled;
        public bool IsEnabled {
            get => _IsEnabled;
            set {
                Apply(value, _IsEnabled);
                _IsEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        private string _Pattern;
        public string Pattern {
            get => _Pattern;
            set {
                _Pattern = value;
                _IsDirty = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pattern)));
            }
        }

        private LogSearchMode _SearchMode;
        public LogSearchMode SearchMode {
            get => _SearchMode;
            set {
                _SearchMode = value;
                _IsDirty = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchMode)));
            }
        }

        private int _Priority;
        public int Priority {
            get => _Priority;
            set {
                _Priority = value;
                _IsDirty = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
            }
        }

        private int _Hits;
        public int Hits {
            get => _Hits;
            set {
                _Hits = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hits)));
            }
        }

        private Brush _PatternFgColor;
        public Brush PatternFgColor {
            get => _PatternFgColor;
            set { _PatternFgColor = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PatternFgColor))); }
        }

        private Brush _PatternBgColor;
        public Brush PatternBgColor {
            get => _PatternBgColor;
            set { _PatternBgColor = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PatternBgColor))); }
        }
        #endregion

        public static void FilterOnRemoval(Filter filter) {
            filter.IsEnabled = false;
        }

        public static SmartCollection<Filter> Filters = new SmartCollection<Filter>(FilterOnRemoval);
        public event PropertyChangedEventHandler PropertyChanged;
        private int FilterId;
        public bool _IsClone;
        private static int _FilterId = 0;
        private bool _IsDirty;

        public Filter() {
            FilterId = ++_FilterId;
            _IsClone = false;
            PatternFgColor = MainWindow.colorThemeViewModel.LogTextFgBrush;
            PatternBgColor = MainWindow.colorThemeViewModel.LogTextBgSearchResultBrush;
        }

        public Filter(Filter filter) {
            FilterId = ++_FilterId;
            _IsClone = true;
            UpdateFilter(filter);
        }

        public void UpdateFilter(Filter filter) {
            Pattern = filter.Pattern;
            SearchMode = filter.SearchMode;
            PatternFgColor = filter.PatternFgColor;
            PatternBgColor = filter.PatternBgColor;
            Priority = filter.Priority;
            // IsEnabled has to be the last one set because it checks whether the above states changed.
            IsEnabled = filter.IsEnabled;
        }

        public void Apply(bool isEnabled, bool prevIsEnabled) {
            // We must not apply cloned filter on logs.
            if (_IsClone) {
                return;
            }

            // If not properties we care changed and enabled state didn't change,
            // we do not apply the filer.
            if (!_IsDirty && (isEnabled == prevIsEnabled)) {
                return;
            }

            _IsDirty = false;

            // TODO: better handle dup items in Log::Filters.
            if (isEnabled) {
                if (prevIsEnabled) {
                    Log.RemoveFilterInLogs(this);
                }
                Log.ApplyFilterInLogs(this);
            } else {
                Log.RemoveFilterInLogs(this);
            }
        }
    }

    class FilterComparer : IComparer<Filter> {
        public int Compare(Filter x, Filter y) {
            return x.Priority - y.Priority;
        }
    }

    public class Log : INotifyPropertyChanged {
        [Flags]
        public enum LogHighlightState {
            NoHighlight = 0,
            SearchResultHighlight = 1,
            SelectedHighlight = 2,
        };

        #region Properties
        public string Text { get; set; }
        public string LineNoText { get; set; }
        public int LineNoWidth { get; set; }
        private Brush _LogFilteredBgBrush;
        public Brush LogFilteredBgBrush {
            get => _LogFilteredBgBrush;
            set { _LogFilteredBgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogFilteredBgBrush))); }
        }
        private Brush _LogRowBgBrush;
        public Brush LogRowBgBrush {
            get => _LogRowBgBrush;
            set { _LogRowBgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogRowBgBrush))); }
        }
        private bool _IsSelected;
        public bool IsSelected {
            get => _IsSelected;
            set {
                _IsSelected = value;
                lock(HighlightStateLock) {
                    if (value) {
                        HighlightState |= LogHighlightState.SelectedHighlight;
                    } else {
                        HighlightState &= ~LogHighlightState.SelectedHighlight;
                    }
                    TryHighlight();
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        #endregion

        public PriorityQueue<Filter> _filtersApplied;
        public object HighlightStateLock = new object();
        public LogHighlightState HighlightState;
        public int LineNo;
        public static SmartCollection<Log> Logs = new SmartCollection<Log>();
        public static List<Log> SearchResults = new List<Log>();
        private static int SearchMatchesIndicesPos = -1;
        public static LogSearchMode SearchMode = LogSearchMode.None;
        private static BackgroundQueue _workerQueue = new BackgroundQueue();
        public static bool FilterToSearchResults = false;
        public event PropertyChangedEventHandler PropertyChanged;
        private static FilterComparer _filterComparer = new FilterComparer();

        public Log(string Text, int LineNo) {
            this.Text = Text;
            this.LineNo = LineNo;
            this.LineNoText = this.LineNo.ToString();
            this.HighlightState = LogHighlightState.NoHighlight;
            this._filtersApplied = new PriorityQueue<Filter>(0, _filterComparer);
            this.TryHighlight();
        }

        public void TryHighlight() {
            if ((HighlightState & LogHighlightState.SelectedHighlight) != 0) {
                LogRowBgBrush = MainWindow.colorThemeViewModel.LogTextBgSelectedBrush;
            } else if ((HighlightState & LogHighlightState.SearchResultHighlight) != 0) {
                LogRowBgBrush = MainWindow.colorThemeViewModel.LogTextBgSearchResultBrush;
            } else if (_filtersApplied.Count > 0) {
                LogRowBgBrush = _filtersApplied.Top.PatternBgColor;
            } else {
                LogRowBgBrush = Brushes.Transparent;
            }
        }

        public static void SelectAllLogs() {
            foreach (var log in Logs) {
                log.IsSelected = true;
            }
        }

        public static void CopySelectedLogs() {
            var sb = new StringBuilder();

            foreach (var log in Logs) {
                if (log.IsSelected) {
                    sb.Append($"{log.Text}\n");
                }
            }

            Clipboard.SetDataObject(sb.ToString());
        }

        public static void ClearSearchResults() {
            foreach (var log in SearchResults) {
                lock (log.HighlightStateLock) {
                    log.HighlightState &= ~LogHighlightState.SearchResultHighlight;
                    log.TryHighlight();
                }
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

        public static void GetNextSearchResult(Action<Log> completionCallback) {
            if (SearchResults.Count == 0 || SearchMatchesIndicesPos >= SearchResults.Count - 1) {
                return;
            }

            completionCallback(SearchResults[++SearchMatchesIndicesPos]);
            return;
        }

        public static void GetPrevSearchResult(Action<Log> completionCallback) {
            _workerQueue.QueueTask(() => {
                if (SearchResults.Count == 0 || SearchMatchesIndicesPos <= 0) {
                    return;
                }

                completionCallback(SearchResults[--SearchMatchesIndicesPos]);
                return;
            });

        }

        private static bool SearchPatternInText(string text, string pattern, LogSearchMode searchMode) {
            bool IgnoreCase = ((searchMode & LogSearchMode.CaseSensitive) == 0);
            if ((searchMode & LogSearchMode.Regex) != 0) {
                return Regex.IsMatch(
                    text, pattern,
                    RegexOptions.Singleline |
                        RegexOptions.CultureInvariant |
                        RegexOptions.Compiled | (IgnoreCase ? RegexOptions.IgnoreCase : 0));
            } else if ((searchMode & LogSearchMode.WholeWordMatch) != 0) {
                // The simple algorithm here is use \b<regex>\b to find the whole word match.
                // If the first character or the last character is a separator, then remove the corresponding \b.
                var leftPattern = char.IsPunctuation(text[0]) ? @"" : @"\b";
                var rightPattern = char.IsPunctuation(text[text.Length - 1]) ? @"" : @"\b";
                var FinalPattern = string.Format(@"{0}{1}{2}", leftPattern, Regex.Escape(pattern), rightPattern);
                return Regex.IsMatch(
                    text, FinalPattern,
                    RegexOptions.Singleline |
                        RegexOptions.CultureInvariant |
                        RegexOptions.Compiled | (IgnoreCase ? RegexOptions.IgnoreCase : 0));
            } else {
                return
                    text.IndexOf(pattern, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) != -1;
            }
        }

        public static void SearchInLogs(String pattern, Action<Log, int> completionCallback) {
            _workerQueue.QueueTask(() => {
                ClearSearchResults();
                if (pattern.Length > 0) {
                    var tempSearchResults = new ConcurrentBag<Log>();
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    Parallel.For(0, Logs.Count, (i) => {
                        if (Logs[i].Text.Length > 0 && SearchPatternInText(Logs[i].Text, pattern, SearchMode)) {
                            tempSearchResults.Add(Logs[i]);
                            lock (Logs[i].HighlightStateLock) {
                                Logs[i].HighlightState |= LogHighlightState.SearchResultHighlight;
                                Logs[i].TryHighlight();
                            }
                        }
                    });

                    SearchResults.AddRange(tempSearchResults);
                    SearchResults.Sort((x, y) => {
                        return x.LineNo - y.LineNo;
                    });
                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;
                    string elapsedTime =
                        String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                            ts.Hours, ts.Minutes, ts.Seconds,
                            ts.Milliseconds / 10);
                    Trace.TraceInformation("SearchInLogs log file took " + elapsedTime);
                }

                if (SearchResults.Count > 0) {
                    SearchMatchesIndicesPos = 0;
                }

                completionCallback(GetCurrentSearchResult(), SearchResults.Count);
            });
        }

        public static void ApplyFilterInLogs(Filter filter) {
            _workerQueue.QueueTask(() => {
                if (filter.Pattern.Length > 0) {
                    int Hits = 0;
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    Parallel.For(0, Logs.Count, ()=>0, (i, loop, total) => {
                        if (Logs[i].Text.Length > 0 && SearchPatternInText(Logs[i].Text, filter.Pattern, filter.SearchMode)) {
                            Logs[i]._filtersApplied.Push(filter);
                            Logs[i].TryHighlight();
                            ++total;
                        }
                        return total;
                    }, (x) => {
                        Interlocked.Add(ref Hits, x);
                    });

                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;
                    string elapsedTime =
                        String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                            ts.Hours, ts.Minutes, ts.Seconds,
                            ts.Milliseconds / 10);
                    Trace.TraceInformation("ApplyFilterInLogs log file took " + elapsedTime);
                    filter.Hits = Hits;
                }
            });
        }

        public static void RemoveFilterInLogs(Filter filter) {
            _workerQueue.QueueTask(() => {
                Parallel.For(0, Logs.Count, (i) => {
                    Logs[i]._filtersApplied.Remove(filter);
                    Logs[i].TryHighlight();
                });
                filter.Hits = 0;
            });
        }

        public static void LoadLogs(List<Log> logs) {
            Log.Logs.Clear();
            ClearSearchResults();
            Logs.AddRange(logs);
        }

        public static void ParseLogFile(string path, IProgress<long> progress, Action<List<Log>> completionCallback) {
            var filestream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8092, FileOptions.SequentialScan);
            var logFileStream = new StreamReader(filestream, Encoding.Unicode, true, 8092);
            var tempLogs = new List<Log>();
            long percentComplete = 0;
            var TextList = new List<string>();
            string Line;

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            while ((Line = logFileStream.ReadLine()) != null) {
                TextList.Add(Line);
            }

            var LineNoWidth = TextList.Count.ToString().Length * 8;

            for (int i = 0; i < TextList.Count; ++i) {
                var log = new Log(TextList[i], i + 1)
                {
                    LineNoWidth = LineNoWidth
                };
                tempLogs.Add(log);
                var currProgress = (i * 100 / TextList.Count);
                if (currProgress > percentComplete) {
                    percentComplete = currProgress;
                    progress.Report(percentComplete);
                }
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime =
                String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
            Trace.TraceInformation("Open log file took " + elapsedTime);

            progress.Report(percentComplete);
            completionCallback(tempLogs);
        }
    }
}
