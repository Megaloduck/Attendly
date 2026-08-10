using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendly.Desktop.Pairing;

public partial class DesktopPairingView : UserControl
{
    public DesktopPairingView()
    {
        InitializeComponent();
    }

    private DesktopPairingViewModel? ViewModel => DataContext as DesktopPairingViewModel;

    // Candidate IP chips are plain Buttons (not an editable ComboBox - Avalonia doesn't
    // have one) whose Tag carries the IP string; clicking one just fills the TextBox above.
    private void OnIpCandidateClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        if (sender is Button { Tag: string ip })
            ViewModel.SelectedIp = ip;
    }
}