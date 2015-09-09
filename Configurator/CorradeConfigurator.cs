///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Windows.Forms;

namespace Configurator
{
    internal static class CorradeConfigurator
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // Set the current directory to the service directory.
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new CorradeConfiguratorForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Who put a clog in my cogs?" + Environment.NewLine + ex.Message);
                Environment.Exit(-1);
            }
        }
    }
}