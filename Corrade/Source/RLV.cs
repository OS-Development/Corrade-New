///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Constants;
using OpenMetaverse;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using wasOpenMetaverse;
using wasSharpNET.Diagnostics;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public static class RLV
    {
        /// <summary>
        ///     Enumeration for supported RLV commands.
        /// </summary>
        public enum RLVBehaviour : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [RLVBehaviour("version")]
            [Reflection.NameAttribute("version")]
            VERSION,

            [RLVBehaviour("versionnew")]
            [Reflection.NameAttribute("versionnew")]
            VERSIONNEW,

            [RLVBehaviour("versionnum")]
            [Reflection.NameAttribute("versionnum")]
            VERSIONNUM,

            [RLVBehaviour("getgroup")]
            [Reflection.NameAttribute("getgroup")]
            GETGROUP,

            [RLVBehaviour("setgroup")]
            [Reflection.NameAttribute("setgroup")]
            SETGROUP,

            [RLVBehaviour("getsitid")]
            [Reflection.NameAttribute("getsitid")]
            GETSITID,

            [RLVBehaviour("getstatusall")]
            [Reflection.NameAttribute("getstatusall")]
            GETSTATUSALL,

            [RLVBehaviour("getstatus")]
            [Reflection.NameAttribute("getstatus")]
            GETSTATUS,

            [RLVBehaviour("sit")]
            [Reflection.NameAttribute("sit")]
            SIT,

            [RLVBehaviour("unsit")]
            [Reflection.NameAttribute("unsit")]
            UNSIT,

            [RLVBehaviour("setrot")]
            [Reflection.NameAttribute("setrot")]
            SETROT,

            [RLVBehaviour("tpto")]
            [Reflection.NameAttribute("tpto")]
            TPTO,

            [RLVBehaviour("getoutfit")]
            [Reflection.NameAttribute("getoutfit")]
            GETOUTFIT,

            [RLVBehaviour("getattach")]
            [Reflection.NameAttribute("getattach")]
            GETATTACH,

            [RLVBehaviour("remattach")]
            [Reflection.NameAttribute("remattach")]
            REMATTACH,

            [RLVBehaviour("detach")]
            [Reflection.NameAttribute("detach")]
            DETACH,

            [RLVBehaviour("detachall")]
            [Reflection.NameAttribute("detachall")]
            DETACHALL,

            [RLVBehaviour("detachme")]
            [Reflection.NameAttribute("detachme")]
            DETACHME,

            [RLVBehaviour("remoutfit")]
            [Reflection.NameAttribute("remoutfit")]
            REMOUTFIT,

            [RLVBehaviour("attach")]
            [Reflection.NameAttribute("attach")]
            ATTACH,

            [RLVBehaviour("attachoverorreplace")]
            [Reflection.NameAttribute("attachoverorreplace")]
            ATTACHOVERORREPLACE,

            [RLVBehaviour("attachover")]
            [Reflection.NameAttribute("attachover")]
            ATTACHOVER,

            [RLVBehaviour("attachall")]
            [Reflection.NameAttribute("attachall")]
            ATTACHALL,

            [RLVBehaviour("attachalloverorreplace")]
            [Reflection.NameAttribute("attachalloverorreplace")]
            ATTACHALLOVERORREPLACE,

            [RLVBehaviour("attachallover")]
            [Reflection.NameAttribute("attachallover")]
            ATTACHALLOVER,

            [RLVBehaviour("getinv")]
            [Reflection.NameAttribute("getinv")]
            GETINV,

            [RLVBehaviour("getinvworn")]
            [Reflection.NameAttribute("getinvworn")]
            GETINVWORN,

            [RLVBehaviour("getpath")]
            [Reflection.NameAttribute("getpath")]
            GETPATH,

            [RLVBehaviour("getpathnew")]
            [Reflection.NameAttribute("getpathnew")]
            GETPATHNEW,

            [RLVBehaviour("findfolder")]
            [Reflection.NameAttribute("findfolder")]
            FINDFOLDER,

            [RLVBehaviour("clear")]
            [Reflection.NameAttribute("clear")]
            CLEAR,

            [Reflection.NameAttribute("accepttp")]
            ACCEPTTP,

            [Reflection.NameAttribute("acceptpermission")]
            ACCEPTPERMISSION,

            [Reflection.NameAttribute("declinepermission")]
            DECLINEPERMISSION,

            [RLVBehaviour("getblacklist")]
            [Reflection.NameAttribute("getblacklist")]
            GETBLACKLIST,

            [RLVBehaviour("versionnumbl")]
            [Reflection.NameAttribute("versionnumbl")]
            VERSIONNUMBL,

            [Reflection.NameAttribute("notify")]
            NOTIFY
        }

        /// <summary>
        ///     Locks down RLV for linear concurrent access.
        /// </summary>
        public static readonly object RLVRulesLock = new object();

        /// <summary>
        ///     Processes a RLV behaviour.
        /// </summary>
        /// <param name="message">the RLV message to process</param>
        /// <param name="senderUUID">the UUID of the sender</param>
        public static void HandleRLVBehaviour(string message, UUID senderUUID)
        {
            if (string.IsNullOrEmpty(message)) return;

            // Split all commands.
            var unpack = message.Split(wasOpenMetaverse.RLV.RLV_CONSTANTS.CSV_DELIMITER[0]);
            // Pop first command to process.
            var first = unpack.First();
            // Remove command.
            unpack = unpack.AsParallel().Where(o => !o.Equals(first)).ToArray();
            // Keep rest of message.
            message = string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.CSV_DELIMITER, unpack);

            var match = wasOpenMetaverse.RLV.RLV_CONSTANTS.RLVRegEx.Match(first);
            if (!match.Success) goto CONTINUE;

            var RLVrule = new wasOpenMetaverse.RLV.RLVRule
            {
                Behaviour = match.Groups["behaviour"].ToString().ToLowerInvariant(),
                Option = match.Groups["option"].ToString().ToLowerInvariant(),
                Param = match.Groups["param"].ToString().ToLowerInvariant(),
                ObjectUUID = senderUUID
            };

            // Skip blacklisted behaviours.
            if(Corrade.corradeConfiguration.RLVBlacklist.Contains(RLVrule.Behaviour))
                goto CONTINUE;

            switch (RLVrule.Param)
            {
                case wasOpenMetaverse.RLV.RLV_CONSTANTS.Y:
                case wasOpenMetaverse.RLV.RLV_CONSTANTS.REM:
                    switch (string.IsNullOrEmpty(RLVrule.Option))
                    {
                        case true:
                            lock (RLVRulesLock)
                            {
                                Corrade.RLVRules.RemoveWhere(
                                    o =>
                                        o.Behaviour.Equals(
                                            RLVrule.Behaviour,
                                            StringComparison.OrdinalIgnoreCase) &&
                                        o.ObjectUUID.Equals(RLVrule.ObjectUUID));
                            }
                            break;
                        default:
                            lock (RLVRulesLock)
                            {
                                Corrade.RLVRules.RemoveWhere(
                                    o =>
                                        o.Behaviour.Equals(
                                            RLVrule.Behaviour,
                                            StringComparison.OrdinalIgnoreCase) &&
                                        o.ObjectUUID.Equals(RLVrule.ObjectUUID) &&
                                        string.Equals(RLVrule.Option, o.Option, StringComparison.OrdinalIgnoreCase));
                            }
                            break;
                    }
                    // Broadcast if @notify is present.
                    Corrade.RLVRules
                        .AsParallel()
                        .Where(o => string.Equals(o.Behaviour, Reflection.GetNameFromEnumValue(RLVBehaviour.NOTIFY)))
                        .ForAll(o =>
                        {
                            var notifyOptions = o.Option.Split(';');
                            if (notifyOptions.Length.Equals(0))
                                return;

                            int channel;
                            if (!int.TryParse(notifyOptions[0], NumberStyles.Integer, Utils.EnUsCulture, out channel) || channel < 1)
                            {
                                return;
                            }

                            var filter = notifyOptions.ElementAtOrDefault(1);
                            if (!string.IsNullOrEmpty(filter) 
                                && !RLVrule.Behaviour.Contains(notifyOptions[1])) return;
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Corrade.Client.Self.Chat($"/{RLVrule.Behaviour}={wasOpenMetaverse.RLV.RLV_CONSTANTS.Y}", channel, ChatType.Normal);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                        });
                    goto CONTINUE;
                case wasOpenMetaverse.RLV.RLV_CONSTANTS.N:
                case wasOpenMetaverse.RLV.RLV_CONSTANTS.ADD:
                    lock (RLVRulesLock)
                    {
                        Corrade.RLVRules.RemoveWhere(
                            o =>
                                o.Behaviour.Equals(
                                    RLVrule.Behaviour,
                                    StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(RLVrule.Option, o.Option, StringComparison.OrdinalIgnoreCase) &&
                                o.ObjectUUID.Equals(RLVrule.ObjectUUID));
                        Corrade.RLVRules.Add(RLVrule);
                    }
                    // Broadcast if @notify is present.
                    Corrade.RLVRules
                        .AsParallel()
                        .Where(o => string.Equals(o.Behaviour, Reflection.GetNameFromEnumValue(RLVBehaviour.NOTIFY)))
                        .ForAll(o =>
                        {
                            var notifyOptions = o.Option.Split(';');
                            if (notifyOptions.Length.Equals(0))
                                return;

                            int channel;
                            if (!int.TryParse(notifyOptions[0], NumberStyles.Integer, Utils.EnUsCulture, out channel) || channel < 1)
                            {
                                return;
                            }

                            var filter = notifyOptions.ElementAtOrDefault(1);
                            if (!string.IsNullOrEmpty(filter)
                                && !RLVrule.Behaviour.Contains(notifyOptions[1])) return;
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Corrade.Client.Self.Chat($"/{RLVrule.Behaviour}={wasOpenMetaverse.RLV.RLV_CONSTANTS.N}", channel, ChatType.Normal);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                        });
                    goto CONTINUE;
            }

            try
            {
                // Increment heartbeat behaviours.
                Interlocked.Increment(ref Corrade.CorradeHeartbeat.ExecutingRLVBehaviours);

                // Find RLV behaviour.
                var RLVBehaviour = Reflection.GetEnumValueFromName<RLVBehaviour>(RLVrule.Behaviour);
                if (RLVBehaviour.Equals(default(RLVBehaviour)))
                {
                    throw new Exception(string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR,
                        Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.BEHAVIOUR_NOT_IMPLEMENTED),
                        RLVrule.Behaviour));
                }
                var execute =
                    Reflection.GetAttributeFromEnumValue<RLVBehaviourAttribute>(RLVBehaviour);

                // Execute the command.
                execute.RLVBehaviour.Invoke(message, RLVrule, senderUUID);
                Interlocked.Increment(ref Corrade.CorradeHeartbeat.ProcessedRLVBehaviours);
            }
            catch (Exception ex)
            {
                Corrade.Feedback(
                    Reflection.GetDescriptionFromEnumValue(Enumerations.ConsoleMessage.FAILED_TO_MANIFEST_RLV_BEHAVIOUR),
                    RLVrule.Behaviour,
                    ex.PrettyPrint());
            }
            finally
            {
                Interlocked.Decrement(ref Corrade.CorradeHeartbeat.ExecutingRLVBehaviours);
            }

        CONTINUE:
            HandleRLVBehaviour(message, senderUUID);
        }

        public class RLVBehaviourAttribute : Attribute
        {
            public RLVBehaviourAttribute(string behaviour)
            {
                RLVBehaviour = Corrade.rlvBehaviours[behaviour];
            }

            public Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> RLVBehaviour { get; }
        }
    }
}
