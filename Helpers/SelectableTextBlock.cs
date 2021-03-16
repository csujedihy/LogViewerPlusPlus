using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace LogViewer.Helpers {

    public partial class SelectableTextBlock : TextBlock {
        public String SelectedText = "";

        public delegate void TextSelectedHandler(string SelectedText);
        public event TextSelectedHandler OnTextSelected;
        protected void RaiseEvent() {
            if (OnTextSelected != null) { OnTextSelected(SelectedText); }
        }

        TextPointer StartSelectPosition;
        TextPointer EndSelectPosition;
        Brush _saveForeGroundBrush;
        Brush _saveBackGroundBrush;

        TextRange _ntr = null;

        protected override void OnMouseDown(MouseButtonEventArgs e) {
            base.OnMouseDown(e);

            if (_ntr != null) {
                _ntr.ApplyPropertyValue(TextElement.ForegroundProperty, _saveForeGroundBrush);
                _ntr.ApplyPropertyValue(TextElement.BackgroundProperty, _saveBackGroundBrush);
            }

            Point mouseDownPoint = e.GetPosition(this);
            StartSelectPosition = this.GetPositionFromPoint(mouseDownPoint, true);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);
            Point mouseUpPoint = e.GetPosition(this);
            EndSelectPosition = this.GetPositionFromPoint(mouseUpPoint, true);

            _ntr = new TextRange(StartSelectPosition, EndSelectPosition);

            // keep saved
            _saveForeGroundBrush = (Brush)_ntr.GetPropertyValue(TextElement.ForegroundProperty);
            _saveBackGroundBrush = (Brush)_ntr.GetPropertyValue(TextElement.BackgroundProperty);
            // change style
            _ntr.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow));
            _ntr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.DarkBlue));

            SelectedText = _ntr.Text;
        }
    }
}
