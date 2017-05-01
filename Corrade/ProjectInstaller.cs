#region

using System.ComponentModel;
using System.Configuration.Install;

#endregion

namespace Corrade
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            // Set the service name.
            string serviceName = string.IsNullOrEmpty(Corrade.InstalledServiceName)
                ? CorradeInstaller.ServiceName
                : Corrade.InstalledServiceName;
            CorradeInstaller.ServiceName = serviceName;
            CorradeInstaller.DisplayName = serviceName;
        }
    }
}
