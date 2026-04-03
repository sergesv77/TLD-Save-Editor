using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace The_Long_Dark_Save_Editor_2
{
    public enum EditorThemePreference
    {
        System,
        White,
        Dark
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Uri LightThemeDictionaryUri = new Uri(
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml",
            UriKind.Absolute);

        private static readonly Uri DarkThemeDictionaryUri = new Uri(
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml",
            UriKind.Absolute);

        public App() : base()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((object sender, UnhandledExceptionEventArgs args) =>
            {
                string codeBase = System.Reflection.Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                path = Path.Combine(Path.GetDirectoryName(path), "crash.txt");

                File.WriteAllText(path, args.ExceptionObject.ToString());

                Environment.Exit(-1);
            });
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ApplyConfiguredTheme();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnExit(e);
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)ApplyConfiguredTheme);
        }

        private void ApplyConfiguredTheme()
        {
            ApplyThemePreference(ParseThemePreference(The_Long_Dark_Save_Editor_2.Properties.Settings.Default.ThemePreference));
        }

        public void ApplyThemePreference(EditorThemePreference preference)
        {
            bool useDarkTheme = ShouldUseDarkTheme(preference);
            ApplyBaseThemeDictionary(useDarkTheme);
            ApplyChromeBrushes(useDarkTheme, SystemParameters.HighContrast);
        }

        public static EditorThemePreference ParseThemePreference(string themePreference)
        {
            EditorThemePreference parsedPreference;
            if (!Enum.TryParse(themePreference, true, out parsedPreference))
                return EditorThemePreference.System;

            return parsedPreference;
        }

        private void ApplyBaseThemeDictionary(bool useDarkTheme)
        {
            Uri targetUri = useDarkTheme ? DarkThemeDictionaryUri : LightThemeDictionaryUri;
            ResourceDictionary themeDictionary = null;

            foreach (ResourceDictionary dictionary in Resources.MergedDictionaries)
            {
                if (dictionary.Source == null)
                    continue;

                string source = dictionary.Source.OriginalString;
                if (source.IndexOf("MaterialDesignTheme.Light.xaml", StringComparison.OrdinalIgnoreCase) >= 0
                    || source.IndexOf("MaterialDesignTheme.Dark.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    themeDictionary = dictionary;
                    break;
                }
            }

            if (themeDictionary == null)
            {
                Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = targetUri });
                return;
            }

            if (themeDictionary.Source != targetUri)
                themeDictionary.Source = targetUri;
        }

        private static bool ShouldUseDarkTheme(EditorThemePreference preference)
        {
            switch (preference)
            {
                case EditorThemePreference.Dark:
                    return true;
                case EditorThemePreference.White:
                    return false;
                default:
                    return IsWindowsAppsDarkThemeEnabled();
            }
        }

        private static bool IsWindowsAppsDarkThemeEnabled()
        {
            try
            {
                using (RegistryKey personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = personalizeKey?.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                        return intValue == 0;
                }
            }
            catch
            {
                // Fall back to light theme when Windows theme settings are unavailable.
            }

            return false;
        }

        private void ApplyChromeBrushes(bool useDarkTheme, bool useSystemHighContrastColors)
        {
            if (useSystemHighContrastColors)
            {
                UpdateResourceBrush("AppShellBackgroundBrush", SystemColors.WindowColor);
                UpdateResourceBrush("AppShellDividerBrush", SystemColors.ActiveBorderColor);
                UpdateResourceBrush("AppChromeIdleBrush", SystemColors.WindowTextColor);
                UpdateResourceBrush("AppChromeSelectedFillBrush", SystemColors.HighlightColor);
                UpdateResourceBrush("AppChromeSelectedTextBrush", SystemColors.HighlightTextColor);
                return;
            }

            UpdateResourceBrush("AppShellBackgroundBrush", useDarkTheme ? Color.FromRgb(0x2B, 0x31, 0x37) : Color.FromRgb(0xEE, 0xF1, 0xF3));
            UpdateResourceBrush("AppShellDividerBrush", useDarkTheme ? Color.FromRgb(0x48, 0x52, 0x5C) : Color.FromRgb(0xA6, 0xB8, 0xC3));
            UpdateResourceBrush("AppChromeIdleBrush", useDarkTheme ? Color.FromRgb(0xD4, 0xE0, 0xE8) : Color.FromRgb(0x4A, 0x60, 0x73));
            UpdateResourceBrush("AppChromeSelectedFillBrush", useDarkTheme ? Color.FromRgb(0x5A, 0x76, 0x89) : Color.FromRgb(0x52, 0x6C, 0x80));
            UpdateResourceBrush("AppChromeSelectedTextBrush", Colors.White);
        }

        private void UpdateResourceBrush(string resourceKey, Color color)
        {
            Resources[resourceKey] = new SolidColorBrush(color);
        }

    }
}
