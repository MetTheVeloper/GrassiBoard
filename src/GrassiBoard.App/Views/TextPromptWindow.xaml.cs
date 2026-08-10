using System.Windows;
using System.Windows.Input;

namespace GrassiBoard.Views;

public partial class TextPromptWindow : Window
{
    private TextPromptWindow(string title, string heading, string value)
    {
        InitializeComponent();
        Title = title;
        Eyebrow.Text = title.ToUpperInvariant();
        Heading.Text = heading;
        ValueBox.Text = value;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
        PreviewKeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter)
            {
                Save();
            }
        };
    }

    public string Value => ValueBox.Text.Trim();

    public static string? Prompt(string title, string heading, string value = "")
    {
        var window = new TextPromptWindow(title, heading, value)
        {
            Owner = Application.Current?.MainWindow
        };
        return window.ShowDialog() == true ? window.Value : null;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            MessageBox.Show(this, "Enter a name.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
