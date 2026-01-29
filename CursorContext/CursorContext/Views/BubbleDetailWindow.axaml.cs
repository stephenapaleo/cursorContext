using System.Text.Json;
using Avalonia.Controls;

namespace CursorContext.Views
{
    public partial class BubbleDetailWindow : Window
    {
        private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

        public BubbleDetailWindow()
        {
            InitializeComponent();
        }

        public BubbleDetailWindow(string json) : this()
        {
            try
            {
                using var doc = JsonDocument.Parse(json ?? "{}");
                JsonTextBox.Text = JsonSerializer.Serialize(doc.RootElement, IndentedOptions);
            }
            catch
            {
                JsonTextBox.Text = json ?? "";
            }
        }
    }
}
