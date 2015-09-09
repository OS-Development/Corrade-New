///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
            // mono does not have custom cultures since 2006 - it sucks!
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    // Load the Linden Lab Globalization.
                    try
                    {
                        // If the Linden Lab culture exists, then unregister it (for updates).
                        CultureInfo[] customCultures = CultureInfo.GetCultures(CultureTypes.UserCustomCulture);
                        if (
                            customCultures.FirstOrDefault(
                                o => o.Name.Equals(@"Linden-Lab")) != null)
                        {
                            CultureAndRegionInfoBuilder.Unregister(@"Linden-Lab");
                        }
                        // Create the Linden culture from the globalization file and register it.
                        CultureAndRegionInfoBuilder cultureAndRegionInfoBuilder =
                            CultureAndRegionInfoBuilder.CreateFromLdml(Path.Combine(@"libs", @"LindenGlobalization.xml"));
                        cultureAndRegionInfoBuilder.Register();
                        CultureInfo.DefaultThreadCurrentCulture =
                            CultureInfo.CreateSpecificCulture(@"Linden-Lab");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(@"Could not create Linden globalization: " + ex.Message);
                        Environment.Exit(-1);
                    }
                    break;
            }
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