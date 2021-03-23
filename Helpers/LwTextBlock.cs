﻿using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogViewer.Helpers {
    public class LwTextBlock : FrameworkElement {
        private Typeface _typeface = new Typeface(new FontFamily(), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        protected FormattedText _formattedText;
        private Point _textPosition = new Point(0, 0);

        public static readonly DependencyProperty TextProperty =
             DependencyProperty.Register(
                 "Text",
                 typeof(string),
                 typeof(LwTextBlock),
                 new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure,
                    (o, e) => ((LwTextBlock)o).TextPropertyChanged((string)e.NewValue)));

        protected virtual void TextPropertyChanged(string text) {
            _formattedText =
                new FormattedText(
                    Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface, 12.0,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        public string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext) {
            if (_formattedText != null) {
                _textPosition.Y = (ActualHeight - _formattedText.Height) / 2;
                drawingContext.DrawText(_formattedText, _textPosition);
            }
        }
    }
}
