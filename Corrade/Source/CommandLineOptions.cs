///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CommandLine;
using CommandLine.Text;
using Corrade.Constants;

namespace Corrade.Source
{
    public class CommandLineOptions
    {
        public CommandLineOptions()
        {
            InstallSubOptions = new InstallSubOptions();
            UninstallSubOptions = new UninstallSubOptions();
            InfoSubOptions = new InfoSubOptions();
        }

        [VerbOption("install", HelpText = "Install Corrade as a service.")]
        public InstallSubOptions InstallSubOptions { get; set; }

        [VerbOption("uninstall", HelpText = "Uninstall a Corrade service.")]
        public UninstallSubOptions UninstallSubOptions { get; set; }

        [VerbOption("info", HelpText = "Various information about Corrade.")]
        public InfoSubOptions InfoSubOptions { get; set; }

        [HelpVerbOption]
        public string GetUsage(string verb)
        {
            return HelpText.AutoBuild(this, verb);
        }
    }

    public class InstallSubOptions
    {
        [Option('n', "service-name", DefaultValue = CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME,
            HelpText = @"The name to give the Corrade service that will show up in Windows tools.")]
        public string Name { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    public class UninstallSubOptions
    {
        [Option('n', "service-name", DefaultValue = CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME,
            HelpText = @"The name of the Corrade service to uninstall.")]
        public string Name { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    public class InfoSubOptions
    {
        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
