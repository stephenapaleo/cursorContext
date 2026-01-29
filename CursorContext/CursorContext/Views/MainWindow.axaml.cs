using Avalonia.Controls;
using CursorContext.Services;

namespace CursorContext.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnConversationSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not BubbleItem bubble)
                return;
            var detail = new BubbleDetailWindow(bubble.FullJson);
            detail.Show();
        }
    }
}