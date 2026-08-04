using Attendly.Data;
using Attendly.Desktop.Export;
using Attendly.Desktop.Pairing;
using Attendly.ViewModels;

namespace Attendly.Desktop;

public class DesktopShellViewModel : ViewModelBase
{
    public DesktopPairingViewModel Pairing { get; }
    public ExportViewModel Export { get; }

    public DesktopShellViewModel(AttendanceRepository repository)
    {
        Pairing = new DesktopPairingViewModel(repository);
        Export = new ExportViewModel(new AttendanceExportService(repository));
    }
}