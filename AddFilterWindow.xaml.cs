using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LogViewer {
    /// <summary>
    /// Interaction logic for AddFilterWindow.xaml
    /// </summary>
    public partial class AddFilterWindow : Window {
        public Filter filter;

        public AddFilterWindow() {
            InitializeComponent();
        }

        private void SearchToggle_Click(object sender, RoutedEventArgs e) {
            var button = sender as ToggleButton;
            LogSearchMode flag = LogSearchMode.None;
            switch (button.Content) {
                case "Aa": // case sensitive toggle
                    flag = LogSearchMode.CaseSensitive;
                    break;
                case "Ex": // exact match toggle
                    flag = LogSearchMode.WholeWordMatch;
                    break;
                case ".*": // regex toggle
                    flag = LogSearchMode.Regex;
                    break;
                default:
                    break;
            }

            if ((bool)button.IsChecked) {
                filter.SearchMode |= flag;
            } else {
                filter.SearchMode &= ~flag;
            }
        }

        private void OkCancelButton_Click(object sender, RoutedEventArgs e) {
            if ((sender as Button).Name == "OkButton") {
                DialogResult = true;
            } else {
                DialogResult = false;
            }
            this.Close();
        }

        private void PriorityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            var textBox = sender as TextBox;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            double val;
            // If parsing is successful, set Handled to false
            e.Handled = !double.TryParse(fullText,
                                         NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingWhite,
                                         CultureInfo.InvariantCulture,
                                         out val);
        }
    }

    public class SearchModeValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            var param = int.Parse(parameter.ToString());
            switch (param) {
                case 0: // case sensitive toggle
                    return ((LogSearchMode)value & LogSearchMode.CaseSensitive) != 0;
                case 1: // exact match toggle
                    return ((LogSearchMode)value & LogSearchMode.WholeWordMatch) != 0;
                case 2: // regex toggle
                    return ((LogSearchMode)value & LogSearchMode.Regex) != 0;
                default:
                    return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
