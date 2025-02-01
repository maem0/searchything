using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;

namespace Searchything
{
    public partial class MainWindow : Window
    {
        private readonly GlobalKeyboardHook _globalKeyboardHook;
        private List<ShortcutItem> _allItems;
        private readonly Storyboard _slideInStoryboard;
        private readonly Storyboard _slideOutStoryboard;
        private bool _isAnimating = false;
        private readonly Dictionary<string, List<ShortcutItem>> _searchCache = new();
        public SolidColorBrush AccentBrush { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            this.MainBorder.Background = SystemParameters.WindowGlassBrush;
            DataContext = this;
            Left = SystemParameters.PrimaryScreenWidth - Width;
            Top = 0;
            Height = SystemParameters.PrimaryScreenHeight;

            LoadShortcuts();

            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyboardPressed += OnKeyboardPressed;

            _slideInStoryboard = (Storyboard)FindResource("SlideIn");
            _slideOutStoryboard = (Storyboard)FindResource("SlideOut");
            _slideOutStoryboard.Completed += (s, e) =>
            {
                Hide();
                _isAnimating = false;
            };
            _slideInStoryboard.Completed += (s, e) =>
            {
                _isAnimating = false;
                IntPtr handle = new WindowInteropHelper(this).Handle;
                SetForegroundWindow(handle);
            };

            Hide();
            MainBorder.Margin = new Thickness(350, 0, -350, 0);
            ContentPanel.Margin = new Thickness(350, 10, -350, 10);
        }

        private void LoadShortcuts()
        {
            _allItems = new List<ShortcutItem>();
            string programsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs"
            );

            foreach (
                string file in Directory.GetFiles(
                    programsPath,
                    "*.lnk",
                    SearchOption.AllDirectories
                )
            )
            {
                try
                {
                    var iconSource = IconExtractor.GetIcon(file);
                    _allItems.Add(
                        new ShortcutItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Path = file,
                            Icon = iconSource
                        }
                    );
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void OnKeyboardPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
            if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown)
            {
                if (
                    e.KeyboardData.VirtualCode == 0x20
                    && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                )
                {
                    Dispatcher.Invoke(
                        () =>
                        {
                            if (!IsVisible && !_isAnimating)
                            {
                                Show();
                                _isAnimating = true;
                                _slideInStoryboard.Begin();
                            }

                            IntPtr handle = new WindowInteropHelper(this).Handle;
                            ShowWindow(handle, SW_RESTORE);
                            SetForegroundWindow(handle);
                            Activate();
                            Focus();
                            SearchBox.Focus();
                            IntPtr foregroundWindow = GetForegroundWindow();
                            uint foregroundThread = GetWindowThreadProcessId(
                                foregroundWindow,
                                out _
                            );
                            uint currentThread = GetWindowThreadProcessId(handle, out _);

                            if (AttachThreadInput(currentThread, foregroundThread, true))
                            {
                                ShowWindow(handle, SW_RESTORE);
                                SetForegroundWindow(handle);
                                AttachThreadInput(currentThread, foregroundThread, false);
                            }
                            else
                            {
                                ShowWindow(handle, SW_RESTORE);
                                SetForegroundWindow(handle);
                            }

                            Dispatcher.BeginInvoke(
                                new Action(
                                    () =>
                                    {
                                        Activate();
                                        Focus();
                                        SearchBox.Focus();
                                        SearchBox.SelectAll();
                                    }
                                ),
                                System.Windows.Threading.DispatcherPriority.Input
                            );
                        }
                    );
                }
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (IsVisible && !_isAnimating)
            {
                _isAnimating = true;
                _slideOutStoryboard.Begin();
                SearchBox.Text = "";
            }
        }
        private void SearchBox_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e
        )
        {
            string searchText = string.Join(
                " ",
                SearchBox.Text.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            );

            Dispatcher.BeginInvoke(
                new Action(
                    () =>
                    {
                        var filtered = _allItems
                            .Where(
                                item =>
                                    string.IsNullOrEmpty(searchText)
                                    || item.Name.ToLower().Contains(searchText)
                            )
                            .Take(50)
                            .ToList();
                        ResultsList.ItemsSource = filtered;

                        if (filtered.Any())
                        {
                            ResultsList.SelectedIndex = 0;
                        }
                    }
                )
            );
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem is ShortcutItem selectedItem)
            {
                Process.Start("explorer.exe", selectedItem.Path);
                _isAnimating = true;
                _slideOutStoryboard.Begin();
                SearchBox.Text = "";
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                e.Handled = true;
                if (ResultsList.Items.Count > 0)
                {
                    int currentIndex = ResultsList.SelectedIndex;

                    if (e.Key == Key.Down)
                    {
                        ResultsList.SelectedIndex = (currentIndex + 1) % ResultsList.Items.Count;
                    }
                    else if (e.Key == Key.Up)
                    {
                        ResultsList.SelectedIndex =
                            (currentIndex - 1 + ResultsList.Items.Count) % ResultsList.Items.Count;
                    }

                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (ResultsList.SelectedItem is ShortcutItem selectedItem)
                {
                    Process.Start("explorer.exe", selectedItem.Path);
                    _isAnimating = true;
                    _slideOutStoryboard.Begin();
                    SearchBox.Text = "";
                }
            }
            else if (e.Key == Key.Escape)
            {
                _isAnimating = true;
                _slideOutStoryboard.Begin();
                SearchBox.Text = "";
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!ResultsList.IsKeyboardFocusWithin)
            {
                ResultsList.Focus();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_globalKeyboardHook != null)
            {
                _globalKeyboardHook.KeyboardPressed -= OnKeyboardPressed;
                _globalKeyboardHook.Dispose();
            }
            base.OnClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const int SW_SHOWNORMAL = 1;
        private const int SW_RESTORE = 9;
    }

    public class ShortcutItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public ImageSource Icon { get; set; }
    }
}
