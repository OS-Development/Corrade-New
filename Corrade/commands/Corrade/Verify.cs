///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using wasSharp;
using wasSharp.Linq;
using wasSharpNET.Cryptography;
using wasStitchNET;
using wasStitchNET.Repository;
using SHA1 = System.Security.Cryptography.SHA1;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> verify =
                (corradeCommandParameters, result) =>
                {
                    var server =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SERVER)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(server))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_SERVER_PROVIDED);
                    var localFiles = Directory
                        .GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
                        .ToDictionary(file =>
                                string.Join(@"/", file
                                    .Split(Path.DirectorySeparatorChar)
                                    .SequenceExcept(Directory.GetCurrentDirectory()
                                        .Split(Path.DirectorySeparatorChar))
                                    .Where(o => !string.IsNullOrEmpty(o))), file =>
                            {
                                try
                                {
                                    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read,
                                        FileShare.Read, 16384, true))
                                    {
                                        return SHA1.Create().ToHex(fileStream);
                                    }
                                }
                                catch
                                {
                                    // A local file could not be opened but that's not so bad.
                                    return string.Empty;
                                }
                            }
                        );
                    var verifies = 0;
                    var modified = 0;

                    try
                    {
                        Tools
                            .GetReleaseFileHashes(server, Assembly.GetEntryAssembly().GetName().Version,
                                (int) corradeConfiguration.ServicesTimeout)
                            .GroupBy(o => o.Key)
                            .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value)
                            .AsParallel()
                            .ForAll(item =>
                            {
                                string localHash;
                                if (!localFiles.TryGetValue(item.Key, out localHash) ||
                                    !string.Equals(item.Value, localHash, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Interlocked.Increment(ref modified);
                                    return;
                                }
                                Interlocked.Increment(ref verifies);
                            });
                    }
                    catch (StitchException ex)
                    {
                        if (!((HttpStatusCode) ex.GetHttpCode()).Equals(HttpStatusCode.OK))
                            throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                    }

                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                        CSV.FromEnumerable(new[]
                        {
                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.VERIFIED),
                            verifies.ToString(Utils.EnUsCulture),
                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.MODIFIED),
                            modified.ToString(Utils.EnUsCulture)
                        }));
                };
        }
    }
}