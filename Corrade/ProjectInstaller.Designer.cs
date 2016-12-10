using System.ComponentModel;
using System.ServiceProcess;

namespace Corrade
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.CorradeProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.CorradeInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // CorradeProcessInstaller
            // 
            this.CorradeProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.CorradeProcessInstaller.Password = null;
            this.CorradeProcessInstaller.Username = null;
            // 
            // CorradeInstaller
            // 
            this.CorradeInstaller.DelayedAutoStart = true;
            this.CorradeInstaller.Description = "Corrade Second Life and OpenSim Scripted Agent";
            this.CorradeInstaller.DisplayName = "Corrade";
            this.CorradeInstaller.ServiceName = "Corrade";
            this.CorradeInstaller.ServicesDependedOn = new string[] {
        "eventlog",
        "Netman"};
            this.CorradeInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.CorradeProcessInstaller,
            this.CorradeInstaller});

        }

        #endregion

        private ServiceProcessInstaller CorradeProcessInstaller;
        private ServiceInstaller CorradeInstaller;
    }
}