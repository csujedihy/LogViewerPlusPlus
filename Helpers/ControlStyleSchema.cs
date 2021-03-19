namespace LogViewer.Helpers {
    public enum ColorTheme {
        LightTheme,
        DarkTheme,
    };

    public class ControlStyleSchema {
        public string LineNoColor { get; set; }
        public string LogTextFgColor { get; set; }
        public string LogTextBgSelectedColor { get; set; }
        public string LogTextBgNormalColor { get; set; }
        public string LogTextBgColor { get; set; }
        public string LogTextBgSearchResultColor { get; set; }
        public string LogTextNormalWeight { get; set; }
        public string LogTextSearchResultWeight { get; set; }
        public string LogTextWeight { get; set; }

        public ControlStyleSchema(ColorTheme Theme) {
            LineNoColor = "Red";
            LogTextFgColor = "Black";
            LogTextBgSelectedColor = "Silver";
            LogTextBgNormalColor = "White";
            LogTextBgSearchResultColor = "LightSkyBlue";
            LogTextNormalWeight = "Normal";
            LogTextSearchResultWeight = "DemiBold";
        }
    }
}
