///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public static class Command
    {
        /// <summary>
        ///     Keys returned by Corrade.
        /// </summary>
        public enum ResultKeys : uint
        {
            [Reflection.NameAttribute("none")] NONE = 0,
            [Reflection.NameAttribute("data")] DATA,
            [Reflection.NameAttribute("success")] SUCCESS,
            [Reflection.NameAttribute("error")] ERROR,
            [Reflection.NameAttribute("status")] STATUS,
            [Reflection.NameAttribute("time")] TIME
        }

        /// <summary>
        ///     Keys reconigzed by Corrade.
        /// </summary>
        public enum ScriptKeys : uint
        {
            [Reflection.NameAttribute("none")] NONE = 0,

            [Reflection.NameAttribute("authentication")] AUTHENTICATION,

            [CommandInputSyntax(
                "<command=nucleus>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop|purge|get|set>>&action=get:<entity=<URL|AUTHENTICATION>>&action=set:<entity=<AUTHENTICATION>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("nucleus")] [Reflection.NameAttribute("nucleus")] NUCLEUS,

            [Reflection.NameAttribute("shell")] SHELL,
            [Reflection.NameAttribute("window")] WINDOW,

            [CommandInputSyntax(
                "<command=copynotecardasset>&<group=<UUID|STRING>>&<password=<STRING>>&[name=<STRING>]&<asset=<UUID>>&<item=<STRING|UUID>&<folder=<STRING|UUID>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("copynotecardasset")] [Reflection.NameAttribute("copynotecardasset")] COPYNOTECARDASSET,

            [Reflection.NameAttribute("exclude")] EXCLUDE,

            [CommandInputSyntax(
                "<command=exportoar>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<STRING>>&entity=object:<item=<STRING|UUID>>&entity=object:[range=<FLOAT>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("exportoar")] [Reflection.NameAttribute("exportoar")] EXPORTOAR,

            [Reflection.NameAttribute("fee")] FEE,
            [Reflection.NameAttribute("demote")] DEMOTE,
            [Reflection.NameAttribute("note")] NOTE,

            [CommandInputSyntax(
                "<command=removeconfigurationgroup>&<group=<UUID|STRING>>&<password=<STRING>>&<target=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("removeconfigurationgroup")] [Reflection.NameAttribute("removeconfigurationgroup")] REMOVECONFIGURATIONGROUP,

            [CommandInputSyntax(
                "<command=addconfigurationgroup>&<group=<UUID|STRING>>&<password=<STRING>>&<target=<STRING|UUID>>&<secret=<STRING>>&<workers=<INTEGER>>&<schedules=<INTEGER>>&[database=<STRING>]&[logs=<BOOL>]&[path=<STRING>]&[permissions=<STRING,[STRING...]>]&[notifications=<STRING,[STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("addconfigurationgroup")] [Reflection.NameAttribute("addconfigurationgroup")] ADDCONFIGURATIONGROUP,

            [Reflection.NameAttribute("notifications")] NOTIFICATIONS,
            [Reflection.NameAttribute("schedules")] SCHEDULES,
            [Reflection.NameAttribute("workers")] WORKERS,

            [CommandInputSyntax(
                "<command=removeitem>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("removeitem")] [Reflection.NameAttribute("removeitem")] REMOVEITEM,

            [CommandInputSyntax(
                "<command=getheartbeatdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<HeartBeat[,HeartBeat...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getheartbeatdata")] [Reflection.NameAttribute("getheartbeatdata")] GETHEARTBEATDATA,

            [CommandInputSyntax(
                "<command=getavatarclassifieds>&<group=<UUID|STRING>>&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatarclassifieds")] [Reflection.NameAttribute("getavatarclassifieds")] GETAVATARCLASSIFIEDS,

            [CommandInputSyntax(
                "<command=getavatarpicks>&<group=<UUID|STRING>>&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatarpicks")] [Reflection.NameAttribute("getavatarpicks")] GETAVATARPICKS,

            [CommandInputSyntax(
                "<command=getavatarclassifieddata>&<group=<UUID|STRING>>&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<item=<UUID>>&<data=<ClassifiedAd[,ClassifiedAd...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatarclassifieddata")] [Reflection.NameAttribute("getavatarclassifieddata")] GETAVATARCLASSIFIEDDATA,

            [CommandInputSyntax(
                "<command=getavatarpickdata>&<group=<UUID|STRING>>&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<item=<UUID>>&<data=<ProfilePick[,ProfilePick...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatarpickdata")] [Reflection.NameAttribute("getavatarpickdata")] GETAVATARPICKDATA,

            [CommandInputSyntax(
                "<command=bayes>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&<action=<train|classify|list|merge|untrain|export|import|remove|add|rename>>&action=train,classify,untrain,import:<data=<STRING>>&action=train,untrain,remove,add:<category=<STRING>>&action=merge,rename:<source=<STRING>>&action=merge,rename:<target=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Database)] [CorradeCommand("bayes")] [Reflection.NameAttribute("bayes")] BAYES,

            [CommandInputSyntax(
                "<command=setinventorydata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&<data=<InventoryItem[,InventoryItem...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("setinventorydata")] [Reflection.NameAttribute("setinventorydata")] SETINVENTORYDATA,

            [CommandInputSyntax(
                "<command=getremoteparcelinfodata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<ParcelInfo[,ParcelInfo...]>>&[position=<VECTOR2>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getremoteparcelinfodata")] [Reflection.NameAttribute("getremoteparcelinfodata")] GETREMOTEPARCELINFODATA,

            [CommandInputSyntax(
                "<command=deactivate>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("deactivate")] [Reflection.NameAttribute("deactivate")] DEACTIVATE,

            [Reflection.NameAttribute("restructure")] RESTRUCTURE,

            [CommandInputSyntax(
                "<command=renameitem>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("renameitem")] [Reflection.NameAttribute("renameitem")] RENAMEITEM,

            [CommandInputSyntax(
                "<command=shoot>&<group=<UUID|STRING>>&<password=<STRING>>&[target=<VECTOR3>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("shoot")] [Reflection.NameAttribute("shoot")] SHOOT,

            [CommandInputSyntax(
                "<command=softban>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<action=<ban|unban|list|import|export>>&action=ban,unban:[avatars=<UUID|STRING[,UUID|STRING...]>]&action=ban:[eject=<BOOL>]&action=import,export:<entity=<group|mute>>&entity=mute:[flags=MuteFlags]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group | (ulong) Configuration.Permissions.Mute)] [CorradeCommand("softban")] [Reflection.NameAttribute("softban")] SOFTBAN,

            [Reflection.NameAttribute("soft")] SOFT,
            [Reflection.NameAttribute("restored")] RESTORED,

            [CommandInputSyntax(
                "<command=getconferencemembersdata>&<group=<UUID|STRING>>&<password=<STRING>>&<session=<UUID>>&<data=<ChatSessionMember[,ChatSessionMember...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("getconferencemembersdata")] [Reflection.NameAttribute("getconferencemembersdata")] GETCONFERENCEMEMBERSDATA,

            [CommandInputSyntax(
                "<command=getconferencememberdata>&<group=<UUID|STRING>>&<password=<STRING>>&<session=<UUID>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<ChatSessionMember[,ChatSessionMember...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("getconferencememberdata")] [Reflection.NameAttribute("getconferencememberdata")] GETCONFERENCEMEMBERDATA,

            [CommandInputSyntax(
                "<command=conference>&<group=<UUID|STRING>>&<password=<STRING>>>&<action=<start|detail|list>>&action=start:<avatars=<UUID|STRING[,UUID|STRING...]>>&action=detail:<session=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("conference")] [Reflection.NameAttribute("conference")] CONFERENCE,

            [Reflection.NameAttribute("parent")] PARENT,

            [CommandInputSyntax(
                "<command=importxml>&<group=<UUID|STRING>>&<password=<STRING>>>&<type=<zip|xml>>&<data=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask(
                    (ulong) Configuration.Permissions.Interact | (ulong) Configuration.Permissions.Economy)] [CorradeCommand("importxml")] [Reflection.NameAttribute("importxml")] IMPORTXML,

            [CommandInputSyntax(
                "<command=getgridlivedatafeeddata>&<group=<UUID|STRING>>&<password=<STRING>>>&<entity=<STRING>>&<data=<STRING[,STRING...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getgridlivedatafeeddata")] [Reflection.NameAttribute("getgridlivedatafeeddata")] GETGRIDLIVEDATAFEEDDATA,

            [CommandInputSyntax(
                "<command=readfile>&<group=<UUID|STRING>>&<password=<STRING>>>&<path=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("readfile")] [Reflection.NameAttribute("readfile")] READFILE,

            [CommandInputSyntax(
                "<command=writefile>&<group=<UUID|STRING>>&<password=<STRING>>>&<path=<STRING>>&<action=<append|create>>&<data=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("writefile")] [Reflection.NameAttribute("writefile")] WRITEFILE,

            [CommandInputSyntax(
                "<command=getavatargroupsdata>&<group=<UUID|STRING>>&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<AvatarGroup[,AvatarGroup...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatargroupsdata")] [Reflection.NameAttribute("getavatargroupsdata")] GETAVATARGROUPSDATA,

            [CommandInputSyntax(
                "<command=setestatecovenant>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setestatecovenant")] [Reflection.NameAttribute("setestatecovenant")] SETESTATECOVENANT,

            [CommandInputSyntax(
                "<command=divorce>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("divorce")] [Reflection.NameAttribute("divorce")] DIVORCE,

            [CommandInputSyntax(
                "<command=marry>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&<action=<propose|revoke|accept|reject>>&action=propose:<message=<STRING>>&action=propose:<name=<STRING>>&action=accept:[message=<STRING>]&action=reject:[message=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("marry")] [Reflection.NameAttribute("marry")] MARRY,

            [Reflection.NameAttribute("verify")] VERIFY,

            [CommandInputSyntax(
                "<command=modifyevent>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&<id=<INTEGER>>&[name=<STRING>]&[description=<STRING>]&[date=<DateTime>]&[time=<DateTime>]&[duration=<INTEGER>]&[location=<STRING>]&[category=<INTEGER>]&[amount=<INTEGER>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("modifyevent")] [Reflection.NameAttribute("modifyevent")] MODIFYEVENT,

            [CommandInputSyntax(
                "<command=deleteevent>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&[amount=<INTEGER>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("deleteevent")] [Reflection.NameAttribute("deleteevent")] DELETEEVENT,

            [CommandInputSyntax(
                "<command=addevent>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&<name=<STRING>>&<description=<STRING>>&<date=<DateTime>>&<time=<DateTime>>&<duration=<INTEGER>>&<location=<STRING>>&<category=<INTEGER>>&[amount=<INTEGER>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("addevent")] [Reflection.NameAttribute("addevent")] ADDEVENT,

            [Reflection.NameAttribute("location")] LOCATION,
            [Reflection.NameAttribute("category")] CATEGORY,

            [CommandInputSyntax(
                "<command=geteventformdata>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&[data=<EventFormData[,EventFormData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("geteventformdata")] [Reflection.NameAttribute("geteventformdata")] GETEVENTFORMDATA,

            [CommandInputSyntax(
                "<command=getaccounttransactionsdata>&<group=<UUID|STRING>>&<password=<STRING>>&[firstname=<STRING>]&[lastname=<STRING>]&<secret=<STRING>>&[data=<Transaction[,Transaction...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getaccounttransactionsdata")] [Reflection.NameAttribute("getaccounttransactionsdata")] GETACCOUNTTRANSACTIONSDATA,

            [CommandInputSyntax(
                "<command=getobjectsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<range|parcel|region|avatar>>&entity=range:[range=<FLOAT>]&entity=parcel:[position=<VECTOR2>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[data=<Primitive[,Primitive...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getobjectsdata")] [Reflection.NameAttribute("getobjectsdata")] GETOBJECTSDATA,

            [CommandInputSyntax(
                "<command=getobjectdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<data=<Primitive[,Primitive...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getobjectdata")] [Reflection.NameAttribute("getobjectdata")] GETOBJECTDATA,

            [CommandInputSyntax(
                "<command=getgroupmembersdata>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<data=<GroupMember[,GroupMember...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getgroupmembersdata")] [Reflection.NameAttribute("getgroupmembersdata")] GETGROUPMEMBERSDATA,

            [CommandInputSyntax(
                "<command=language>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<detect>>&action=detect:<message=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("language")] [Reflection.NameAttribute("language")] LANGUAGE,

            [Reflection.NameAttribute("online")] ONLINE,
            [Reflection.NameAttribute("dialog")] DIALOG,

            [CommandInputSyntax(
                "<command=getgroupmemberdata>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<GroupMember[,GroupMember...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getgroupmemberdata")] [Reflection.NameAttribute("getgroupmemberdata")] GETGROUPMEMBERDATA,

            [CommandInputSyntax(
                "<command=getcurrentgroupsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Group[,Group...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getcurrentgroupsdata")] [Reflection.NameAttribute("getcurrentgroupsdata")] GETCURRENTGROUPSDATA,

            [CommandInputSyntax(
                "<command=getcurrentgroups>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getcurrentgroups")] [Reflection.NameAttribute("getcurrentgroups")] GETCURRENTGROUPS,

            [CommandInputSyntax(
                "<command=getgroupsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<target=<UUID|STRING,...>>&<data=<Group[,Group...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getgroupsdata")] [Reflection.NameAttribute("getgroupsdata")] GETGROUPSDATA,

            [CommandInputSyntax(
                "<command=facebook>&<group=<UUID|STRING>>&<password=<STRING>>&<token=<USER_ACCESS_TOKEN>>&<action=<post>>&action=post:[ID=<STRING>]&action=post:[message=<STRING>]&action=post:[name=<STRING>]&action=post:[URL=<STRING>]&action=post:[description=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("facebook")] [Reflection.NameAttribute("facebook")] FACEBOOK,

            [CommandInputSyntax(
                "<command=twitter>&<group=<UUID|STRING>>&<password=<STRING>>&<key=<CONSUMER_KEY>>&<secret=<CONSUMER_SECRET>>&<token=<ACCESS_TOKEN>>&<access=<TOKEN_SECRET>>&<action=<post>>&action=post:<message=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("twitter")] [Reflection.NameAttribute("twitter")] TWITTER,

            [Reflection.NameAttribute("secret")] SECRET,
            [Reflection.NameAttribute("token")] TOKEN,
            [Reflection.NameAttribute("access")] ACCESS,
            [Reflection.NameAttribute("date")] DATE,
            [Reflection.NameAttribute("summary")] SUMMARY,

            [CommandInputSyntax(
                "<command=feed>&<group=<UUID|STRING>>&<password=<STRING>>&<URL=<STRING>>&<action=<add|remove|list>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Feed)] [CorradeCommand("feed")] [Reflection.NameAttribute("feed")] FEED,

            [CommandInputSyntax(
                "<command=setrolepowers>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<role=<UUID|STRING>>&<powers=<GroupPowers[,GroupPowers...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("setrolepowers")] [Reflection.NameAttribute("setrolepowers")] SETROLEPOWERS,

            /// <remarks>
            ///     This command is disabled because libopenmetaverse does not support managing the parcel lists.
            /// </remarks>
            /* [IsCorradeCommand(true)]
            [CommandInputSyntax(
                "<command=setparcellist>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )]
            [CommandPermissionMask((UInt64)Configuration.Permissions.Land)]
            [CorradeCommand("setparcellist")]
            [wasSharp.Reflection.NameAttribute("setparcellist")]
            SETPARCELLIST, */
            [Reflection.NameAttribute("creator")] CREATOR,
            [Reflection.NameAttribute("slot")] SLOT,

            [CommandInputSyntax(
                "<command=configuration>&<group=<UUID|STRING>>&<password=<STRING>>&<action=read|write|get|set>&action=write:<data=STRING>&action=write:<data=<STRING>>&action=get:<path=<STRING>>&action=set:<path=<STRING>>&action=set:<data=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("configuration")] [Reflection.NameAttribute("configuration")] CONFIGURATION,

            [CommandInputSyntax(
                "<command=getparcelinfodata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<ParcelInfo[,ParcelInfo...]>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getparcelinfodata")] [Reflection.NameAttribute("getparcelinfodata")] GETPARCELINFODATA,

            [Reflection.NameAttribute("degrees")] DEGREES,

            [CommandInputSyntax(
                "<command=turn>&<group=<UUID|STRING>>&<password=<STRING>>&<direction=<left|right>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("turn")] [Reflection.NameAttribute("turn")] TURN,

            [Reflection.NameAttribute("SQL")] SQL,

            [CommandInputSyntax(
                "<command=logs>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<group|message|local|region>>&<action=<get|search>>&entity=group|message|local|region,action=get:[from=<DateTime>]&entity=group|message|local|region,action=get:[to=<DateTime>]&entity=group|message|local|region,action=get:[firstname=<STRING>]&entity=group|message|local|region,action=get:[lastname=<STRING>]&entity=group|message|local|region,action=get:[message=<STRING>]&entity=local|region,action=get:[region=<STRING>]&entity=local:[type=<ChatType>]&action=search:<data=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("logs")] [Reflection.NameAttribute("logs")] LOGS,

            [Reflection.NameAttribute("from")] FROM,
            [Reflection.NameAttribute("to")] TO,
            [Reflection.NameAttribute("deanimate")] DEANIMATE,

            [CommandInputSyntax(
                "<command=terraform>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR2>>&<height=<FLOAT>>&<width=<FLOAT>>&<amount=<FLOAT>>&<brush=<TerraformBrushSize>>&<action=<TerraformAction>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("terraform")] [Reflection.NameAttribute("terraform")] TERRAFORM,

            [Reflection.NameAttribute("height")] HEIGHT,
            [Reflection.NameAttribute("width")] WIDTH,
            [Reflection.NameAttribute("brush")] BRUSH,

            [CommandInputSyntax(
                "<command=getestatecovenant>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getestatecovenant")] [Reflection.NameAttribute("getestatecovenant")] GETESTATECOVENANT,

            [CommandInputSyntax(
                "<command=estateteleportusershome>&<group=<UUID|STRING>>&<password=<STRING>>&[avatars=<UUID|STRING[,UUID|STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("estateteleportusershome")] [Reflection.NameAttribute("estateteleportusershome")] ESTATETELEPORTUSERSHOME,

            [CommandInputSyntax(
                "<command=setregionterrainvariables>&<group=<UUID|STRING>>&<password=<STRING>>&[waterheight=<FLOAT>]&[terrainraiselimit=<FLOAT>]&[terrainlowerlimit=<FLOAT>]&[usestatesun=<BOOL>]&[fixedsun=<BOOL>]&[sunposition=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setregionterrainvariables")] [Reflection.NameAttribute("setregionterrainvariables")] SETREGIONTERRAINVARIABLES,

            [Reflection.NameAttribute("useestatesun")] USEESTATESUN,
            [Reflection.NameAttribute("terrainraiselimit")] TERRAINRAISELIMIT,
            [Reflection.NameAttribute("terrainlowerlimit")] TERRAINLOWERLIMIT,
            [Reflection.NameAttribute("sunposition")] SUNPOSITION,
            [Reflection.NameAttribute("fixedsun")] FIXEDSUN,
            [Reflection.NameAttribute("waterheight")] WATERHEIGHT,

            [CommandInputSyntax(
                "<command=getregionterrainheights>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getregionterrainheights")] [Reflection.NameAttribute("getregionterrainheights")] GETREGIONTERRAINHEIGHTS,

            [CommandInputSyntax(
                "<command=setregionterrainheights>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<FLOAT[,FLOAT...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setregionterrainheights")] [Reflection.NameAttribute("setregionterrainheights")] SETREGIONTERRAINHEIGHTS,

            [CommandInputSyntax(
                "<command=getregionterraintextures>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getregionterraintextures")] [Reflection.NameAttribute("getregionterraintextures")] GETREGIONTERRAINTEXTURES,

            [CommandInputSyntax(
                "<command=setregionterraintextures>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<UUID|STRING[,UUID|STRING...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setregionterraintextures")] [Reflection.NameAttribute("setregionterraintextures")] SETREGIONTERRAINTEXTURES,

            [CommandInputSyntax(
                "<command=setregioninfo>&<group=<UUID|STRING>>&<password=<STRING>>&[terraform=<BOOL>]&[fly=<BOOL>]&[damage=<BOOL>]&[resell=<BOOL>]&[push=<BOOL>]&[parcel=<BOOL>]&[limit=<FLOAT>]&[bonus=<FLOAT>]&[mature=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setregioninfo")] [Reflection.NameAttribute("setregioninfo")] SETREGIONINFO,

            [Reflection.NameAttribute("bonus")] BONUS,
            [Reflection.NameAttribute("damage")] DAMAGE,
            [Reflection.NameAttribute("limit")] LIMIT,
            [Reflection.NameAttribute("mature")] MATURE,
            [Reflection.NameAttribute("parcel")] PARCEL,
            [Reflection.NameAttribute("push")] PUSH,
            [Reflection.NameAttribute("resell")] RESELL,

            [CommandInputSyntax(
                "<command=setcameradata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement.AgentCamera[,AgentManager.AgentMovement.AgentCamera...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("setcameradata")] [Reflection.NameAttribute("setcameradata")] SETCAMERADATA,

            [CommandInputSyntax(
                "<command=getcameradata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement.AgentCamera[,AgentManager.AgentMovement.AgentCamera...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getcameradata")] [Reflection.NameAttribute("getcameradata")] GETCAMERADATA,

            [CommandInputSyntax(
                "<command=setmovementdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement[,AgentManager.AgentMovement...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("setmovementdata")] [Reflection.NameAttribute("setmovementdata")] SETMOVEMENTDATA,

            [CommandInputSyntax(
                "<command=getmovementdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement[,AgentManager.AgentMovement...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getmovementdata")] [Reflection.NameAttribute("getmovementdata")] GETMOVEMENTDATA,

            [CommandInputSyntax(
                "<command=at>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<add|get|remove|list>>&action=add:<time=<Timestamp>>&action=add:<data=<STRING>>&action=get|remove:<index=<INTEGER>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Schedule)] [CorradeCommand("at")] [Reflection.NameAttribute("at")] AT,

            [CommandInputSyntax(
                "<command=flyto>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR3>>&[duration=<INTGEGER>]&[affinity=<INTEGER>]&[fly=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("flyto")] [Reflection.NameAttribute("flyto")] FLYTO,

            [Reflection.NameAttribute("vicinity")] VICINITY,
            [Reflection.NameAttribute("affinity")] AFFINITY,

            [CommandInputSyntax(
                "<command=batchmute>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<mute|unmute>>&[mutes=<STRING|UUID[,STRING|UUID...]>]&action=mute:[type=MuteType]&action=mute:[flags=MuteFlags]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("batchmute")] [Reflection.NameAttribute("batchmute")] BATCHMUTE,

            [Reflection.NameAttribute("mutes")] MUTES,

            [CommandInputSyntax(
                "<command=setconfigurationdata>&<group=<UUID|STRING>>&<password=<STRING>>&[data=<CorradeConfiguration,[Configuration...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("setconfigurationdata")] [Reflection.NameAttribute("setconfigurationdata")] SETCONFIGURATIONDATA,

            [CommandInputSyntax(
                "<command=getconfigurationdata>&<group=<UUID|STRING>>&<password=<STRING>>&[data=<CorradeConfiguration,[Configuration...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("getconfigurationdata")] [Reflection.NameAttribute("getconfigurationdata")] GETCONFIGURATIONDATA,

            [CommandInputSyntax(
                "<command=ban>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<action=<ban|unban|list>>&action=ban,unban:[avatars=<UUID|STRING[,UUID|STRING...]>]&action=ban:[eject=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("ban")] [Reflection.NameAttribute("ban")] BAN,

            [CommandInputSyntax("<command=ping>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.None)] [CorradeCommand("ping")] [Reflection.NameAttribute("ping")] PING,
            [Reflection.NameAttribute("pong")] PONG,

            [CommandInputSyntax(
                "<command=batcheject>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[avatars=<UUID|STRING[,UUID|STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("batcheject")] [Reflection.NameAttribute("batcheject")] BATCHEJECT,

            [CommandInputSyntax(
                "<command=batchinvite>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[role=<UUID[,STRING...]>]&[avatars=<UUID|STRING[,UUID|STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("batchinvite")] [Reflection.NameAttribute("batchinvite")] BATCHINVITE,

            [Reflection.NameAttribute("avatars")] AVATARS,

            [CommandInputSyntax(
                "<command=setobjectmediadata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<face=<INTEGER>>&[data=<MediaEntry[,MediaEntry...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectmediadata")] [Reflection.NameAttribute("setobjectmediadata")] SETOBJECTMEDIADATA,

            [CommandInputSyntax(
                "<command=getobjectmediadata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<MediaEntry[,MediaEntry...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getobjectmediadata")] [Reflection.NameAttribute("getobjectmediadata")] GETOBJECTMEDIADATA,

            [CommandInputSyntax(
                "<command=setprimitivematerial>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[material=<Material>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivematerial")] [Reflection.NameAttribute("setprimitivematerial")] SETPRIMITIVEMATERIAL,

            [Reflection.NameAttribute("material")] MATERIAL,

            [CommandInputSyntax(
                "<command=setprimitivelightdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<LightData[,LightData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivelightdata")] [Reflection.NameAttribute("setprimitivelightdata")] SETPRIMITIVELIGHTDATA,

            [CommandInputSyntax(
                "<command=getprimitivelightdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<LightData [,LightData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivelightdata")] [Reflection.NameAttribute("getprimitivelightdata")] GETPRIMITIVELIGHTDATA,

            [CommandInputSyntax(
                "<command=setprimitiveflexibledata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<FlexibleData[,FlexibleData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitiveflexibledata")] [Reflection.NameAttribute("setprimitiveflexibledata")] SETPRIMITIVEFLEXIBLEDATA,

            [CommandInputSyntax(
                "<command=getprimitiveflexibledata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<FlexibleData[,FlexibleData ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitiveflexibledata")] [Reflection.NameAttribute("getprimitiveflexibledata")] GETPRIMITIVEFLEXIBLEDATA,

            [CommandInputSyntax(
                "<command=creategrass>&<group=<UUID|STRING>>&<password=<STRING>>>&[region=<STRING>]&<position=<VECTOR3>>&[rotation=<Quaternion>]&<type=<Grass>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("creategrass")] [Reflection.NameAttribute("creategrass")] CREATEGRASS,

            [CommandInputSyntax(
                "<command=getstatus>&<group=<UUID|STRING>>&<password=<STRING>>&<status=<INTEGER>>&<entity=<description>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.None)] [CorradeCommand("getstatus")] [Reflection.NameAttribute("getstatus")] GETSTATUS,

            [CommandInputSyntax(
                "<command=getprimitivebodytypes>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivebodytypes")] [Reflection.NameAttribute("getprimitivebodytypes")] GETPRIMITIVEBODYTYPES,

            [CommandInputSyntax(
                "<command=getprimitivephysicsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<PhysicsProperties[,PhysicsProperties ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivephysicsdata")] [Reflection.NameAttribute("getprimitivephysicsdata")] GETPRIMITIVEPHYSICSDATA,

            [CommandInputSyntax(
                "<command=getprimitivepropertiesdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<ObjectProperties[,ObjectProperties ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivepropertiesdata")] [Reflection.NameAttribute("getprimitivepropertiesdata")] GETPRIMITIVEPROPERTIESDATA,

            [CommandInputSyntax(
                "<command=setprimitiveflags>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<SINGLE>]&[temporary=<BOOL>]&[shadows=<BOOL>]&[restitution=<SINGLE>]&[phantom=<BOOL>]&[gravity=<SINGLE>]&[friction=<SINGLE>]&[density=<SINGLE>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitiveflags")] [Reflection.NameAttribute("setprimitiveflags")] SETPRIMITIVEFLAGS,

            [Reflection.NameAttribute("temporary")] TEMPORARY,
            [Reflection.NameAttribute("shadows")] SHADOWS,
            [Reflection.NameAttribute("restitution")] RESTITUTION,
            [Reflection.NameAttribute("phantom")] PHANTOM,
            [Reflection.NameAttribute("gravity")] GRAVITY,
            [Reflection.NameAttribute("friction")] FRICTION,
            [Reflection.NameAttribute("density")] DENSITY,

            [CommandInputSyntax(
                "<command=grab>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&<item=<UUID|STRING>>&[range=<FLOAT>]&<texture=<VECTOR3>&<surface=<VECTOR3>>&<normal=<VECTOR3>>&<binormal=<VECTOR3>>&<face=<INTEGER>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("grab")] [Reflection.NameAttribute("grab")] GRAB,

            [Reflection.NameAttribute("texture")] TEXTURE,
            [Reflection.NameAttribute("surface")] SURFACE,
            [Reflection.NameAttribute("normal")] NORMAL,
            [Reflection.NameAttribute("binormal")] BINORMAL,
            [Reflection.NameAttribute("face")] FACE,

            [CommandInputSyntax(
                "<command=createtree>&<group=<UUID|STRING>>&<password=<STRING>>>&[region=<STRING>]&<position=<VECTOR3>>&[rotation=<Quaternion>]&<type=<Tree>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("createtree")] [Reflection.NameAttribute("createtree")] CREATETREE,

            [CommandInputSyntax(
                "<command=setprimitivetexturedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[index=<INTEGER>]&[data=<TextureEntryFace [,TextureEntryFace ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivetexturedata")] [Reflection.NameAttribute("setprimitivetexturedata")] SETPRIMITIVETEXTUREDATA,

            [CommandInputSyntax(
                "<command=getprimitivetexturedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<TextureEntry[,TextureEntry ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivetexturedata")] [Reflection.NameAttribute("getprimitivetexturedata")] GETPRIMITIVETEXTUREDATA,

            [CommandInputSyntax(
                "<command=setprimitivesculptdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<SculptData[,SculptData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivesculptdata")] [Reflection.NameAttribute("setprimitivesculptdata")] SETPRIMITIVESCULPTDATA,

            [CommandInputSyntax(
                "<command=getprimitivesculptdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<SculptData[,SculptData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivesculptdata")] [Reflection.NameAttribute("getprimitivesculptdata")] GETPRIMITIVESCULPTDATA,

            [CommandInputSyntax(
                "<command=setprimitiveshapedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[type=<CorradePrimitiveShape>]&[data=<ConstructionData[,ConstructionData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitiveshapedata")] [Reflection.NameAttribute("setprimitiveshapedata")] SETPRIMITIVESHAPEDATA,

            [CommandInputSyntax(
                "<command=getprimitiveshapedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<ConstructionData[,ConstructionData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitiveshapedata")] [Reflection.NameAttribute("getprimitiveshapedata")] GETPRIMITIVESHAPEDATA,

            [CommandInputSyntax(
                "<command=createprimitive>&<group=<UUID|STRING>>&<password=<STRING>>>&[region=<STRING>]&<position=<VECTOR3>>&[rotation=<Quaternion>]&[type=<CorradePrimitiveShape>]&[data=<ConstructionData>]&[flags=<PrimFlags>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("createprimitive")] [Reflection.NameAttribute("createprimitive")] CREATEPRIMITIVE,

            [Reflection.NameAttribute("flags")] FLAGS,
            [Reflection.NameAttribute("take")] TAKE,
            [Reflection.NameAttribute("pass")] PASS,
            [Reflection.NameAttribute("controls")] CONTROLS,
            [Reflection.NameAttribute("afterburn")] AFTERBURN,

            [CommandInputSyntax(
                "<command=getprimitivepayprices>&<group=<UUID|STRING>>&<password=<STRING>>>&item=<STRING|UUID>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivepayprices")] [Reflection.NameAttribute("getprimitivepayprices")] GETPRIMITIVEPAYPRICES,

            [CommandInputSyntax(
                "<command=primitivebuy>&<group=<UUID|STRING>>&<password=<STRING>>>&item=<STRING|UUID>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask(
                    (ulong) Configuration.Permissions.Interact | (ulong) Configuration.Permissions.Economy)
                   ] [CorradeCommand("primitivebuy")] [Reflection.NameAttribute("primitivebuy")] PRIMITIVEBUY,

            [CommandInputSyntax(
                "<command=changeprimitivelink>&<group=<UUID|STRING>>&<password=<STRING>>>&<action=<link|delink>>&action=link:<item=<STRING|UUID,STRING|UUID[,STRING|UUID...>>&action=delink:<item=<STRING|UUID[,STRING|UUID...>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("changeprimitivelink")] [Reflection.NameAttribute("changeprimitivelink")] CHANGEPRIMITIVELINK,

            [CommandInputSyntax(
                "<command=getavatargroupdata>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<AvatarGroup[,AvatarGroup...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatargroupdata")] [Reflection.NameAttribute("getavatargroupdata")] GETAVATARGROUPDATA,

            [CommandInputSyntax(
                "<command=getcommand>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<entity=<syntax|permission>>&entity=syntax:<type=<input>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.None)] [CorradeCommand("getcommand")] [Reflection.NameAttribute("getcommand")] GETCOMMAND,
            [CommandInputSyntax("<command=listcommands>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.None)] [CorradeCommand("listcommands")] [Reflection.NameAttribute("listcommands")] LISTCOMMANDS,

            [CommandInputSyntax(
                "<command=getconnectedregions>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getconnectedregions")] [Reflection.NameAttribute("getconnectedregions")] GETCONNECTEDREGIONS,

            [CommandInputSyntax(
                "<command=getnetworkdata>&<group=<UUID|STRING>>&<password=<STRING>>&[data=<NetworkManager[,NetworkManager...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getnetworkdata")] [Reflection.NameAttribute("getnetworkdata")] GETNETWORKDATA,

            [CommandInputSyntax(
                "<command=typing>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("typing")] [Reflection.NameAttribute("typing")] TYPING,

            [CommandInputSyntax(
                "<command=busy>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("busy")] [Reflection.NameAttribute("busy")] BUSY,

            [CommandInputSyntax(
                "<command=away>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("away")] [Reflection.NameAttribute("away")] AWAY,

            [CommandInputSyntax(
                "<command=getobjectpermissions>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getobjectpermissions")] [Reflection.NameAttribute("getobjectpermissions")] GETOBJECTPERMISSIONS,
            [Reflection.NameAttribute("scale")] SCALE,
            [Reflection.NameAttribute("uniform")] UNIFORM,

            [CommandInputSyntax(
                "<command=setobjectscale>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<scale=<FLOAT>>&[uniform=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectscale")] [Reflection.NameAttribute("setobjectscale")] SETOBJECTSCALE,

            [CommandInputSyntax(
                "<command=setprimitivescale>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<scale=<FLOAT>>&[uniform=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivescale")] [Reflection.NameAttribute("setprimitivescale")] SETPRIMITIVESCALE,

            [CommandInputSyntax(
                "<command=setprimitiverotation>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<rotation=<QUATERNION>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitiverotation")] [Reflection.NameAttribute("setprimitiverotation")] SETPRIMITIVEROTATION,

            [CommandInputSyntax(
                "<command=setprimitiveposition>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<position=<VECTOR3>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitiveposition")] [Reflection.NameAttribute("setprimitiveposition")] SETPRIMITIVEPOSITION,

            [CommandInputSyntax(
                "<command=exportdae>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&[format=<ImageFormat>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("exportdae")] [Reflection.NameAttribute("exportdae")] EXPORTDAE,

            [CommandInputSyntax(
                "<command=exportxml>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&[format=<ImageFormat>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("exportxml")] [Reflection.NameAttribute("exportxml")] EXPORTXML,

            [CommandInputSyntax(
                "<command=getprimitivesdata>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<range|parcel|region|avatar>>&entity=range:[range=<FLOAT>]&entity=parcel:[position=<VECTOR2>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[data=<Primitive[,Primitive...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivesdata")] [Reflection.NameAttribute("getprimitivesdata")] GETPRIMITIVESDATA,

            [CommandInputSyntax(
                "<command=getavatarsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<range|parcel|region|avatar>>&entity=range:[range=<FLOAT>]&entity=parcel:[position=<VECTOR2>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[data=<Avatar[,Avatar...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatarsdata")] [Reflection.NameAttribute("getavatarsdata")] GETAVATARSDATA,
            [Reflection.NameAttribute("format")] FORMAT,
            [Reflection.NameAttribute("volume")] VOLUME,
            [Reflection.NameAttribute("audible")] AUDIBLE,
            [Reflection.NameAttribute("path")] PATH,

            [CommandInputSyntax(
                "<command=inventory>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<ls|cwd|cd|mkdir|chmod|rm|cp|mv|ln>>&action=ls|mkdir|chmod:[path=<STRING>]&action=cd,action=rm:<path=<STRING>>&action=mkdir:<name=<STRING>>&action=chmod:<permissions=<STRING>>&action=cp|mv|ln:<source=<STRING>>&action=cp|mv|ln:<target=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("inventory")] [Reflection.NameAttribute("inventory")] INVENTORY,
            [Reflection.NameAttribute("offset")] OFFSET,
            [Reflection.NameAttribute("alpha")] ALPHA,
            [Reflection.NameAttribute("color")] COLOR,

            [CommandInputSyntax(
                "<command=deleteviewereffect>&<group=<UUID|STRING>>&<password=<STRING>>&<effect=<Look|Point>>&<id=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("deleteviewereffect")] [Reflection.NameAttribute("deleteviewereffect")] DELETEVIEWEREFFECT,

            [CommandInputSyntax(
                "<command=getviewereffects>&<group=<UUID|STRING>>&<password=<STRING>>&<effect=<Look|Point|Sphere|Beam>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getviewereffects")] [Reflection.NameAttribute("getviewereffects")] GETVIEWEREFFECTS,

            [CommandInputSyntax(
                "<command=setviewereffect>&<group=<UUID|STRING>>&<password=<STRING>>&<effect=<Look|Point|Sphere|Beam>>&effect=Look:<item=<UUID|STRING>&<range=<FLOAT>>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&effect=Look:<offset=<VECTOR3>>&effect=Look:<type=LookAt>&effect=Point:<item=<UUID|STRING>&<range=<FLOAT>>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&effect=Point:<offset=<VECTOR3>>&effect=Point:<type=PointAt>&effect=Beam:<item=<UUID|STRING>&<range=<FLOAT>>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&effect=Beam:<color=<VECTOR3>>&effect=Beam:<alpha=<FLOAT>>&effect=Beam:<duration=<FLOAT>>&effect=Beam:<offset=<VECTOR3>>&effect=Sphere:<color=<VECTOR3>>&effect=Sphere:<alpha=<FLOAT>>&effect=Sphere:<duration=<FLOAT>>&effect=Sphere:<offset=<VECTOR3>>&[id=<UUID>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setviewereffect")] [Reflection.NameAttribute("setviewereffect")] SETVIEWEREFFECT,

            [CommandInputSyntax(
                "<command=ai>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<process|enable|disable|rebuild>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("ai")] [Reflection.NameAttribute("ai")] AI,

            [CommandInputSyntax(
                "<command=gettitles>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("gettitles")] [Reflection.NameAttribute("gettitles")] GETTITLES,

            [CommandInputSyntax(
                "<command=tag>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&action=<set|get>&action=set:<title=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("tag")] [Reflection.NameAttribute("tag")] TAG,

            [CommandInputSyntax(
                "<command=filter>&<group=<UUID|STRING>>&<password=<STRING>>&action=<set|get>&action=get:<type=<input|output>>&action=set:<input=<STRING>>&action=set:<output=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Filter)] [CorradeCommand("filter")] [Reflection.NameAttribute("filter")] FILTER,

            [CommandInputSyntax(
                "<command=run>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )
            ] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("run")] [Reflection.NameAttribute("run")] RUN,
            [CommandInputSyntax("<command=relax>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("relax")] [Reflection.NameAttribute("relax")] RELAX,
            [Reflection.NameAttribute("sift")] SIFT,

            [CommandInputSyntax(
                "<command=rlv>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable>>&[callback=<STRING>]")
            ] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("rlv")] [Reflection.NameAttribute("rlv")] RLV,

            [CommandInputSyntax(
                "<command=getinventorypath>&<group=<UUID|STRING>>&<password=<STRING>>&<pattern=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("getinventorypath")] [Reflection.NameAttribute("getinventorypath")] GETINVENTORYPATH,
            [Reflection.NameAttribute("committed")] COMMITTED,
            [Reflection.NameAttribute("credit")] CREDIT,
            [Reflection.NameAttribute("success")] SUCCESS,
            [Reflection.NameAttribute("transaction")] TRANSACTION,

            [CommandInputSyntax(
                "<command=getscriptdialogs>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getscriptdialogs")] [Reflection.NameAttribute("getscriptdialogs")] GETSCRIPTDIALOGS,

            [CommandInputSyntax(
                "<command=getscriptpermissionrequests>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getscriptpermissionrequests")] [Reflection.NameAttribute("getscriptpermissionrequests")] GETSCRIPTPERMISSIONREQUESTS,

            [CommandInputSyntax(
                "<command=getteleportlures>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("getteleportlures")] [Reflection.NameAttribute("getteleportlures")] GETTELEPORTLURES,

            [CommandInputSyntax(
                "<command=replytogroupinvite>&<group=<UUID|STRING>>&<password=<STRING>>&[action=<accept|decline>]&<session=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group | (ulong) Configuration.Permissions.Economy)] [CorradeCommand("replytogroupinvite")] [Reflection.NameAttribute("replytogroupinvite")] REPLYTOGROUPINVITE,

            [CommandInputSyntax(
                "<command=getgroupinvites>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getgroupinvites")] [Reflection.NameAttribute("getgroupinvites")] GETGROUPINVITES,

            [CommandInputSyntax(
                "<command=getmemberroles>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getmemberroles")] [Reflection.NameAttribute("getmemberroles")] GETMEMBERROLES,

            [CommandInputSyntax(
                "<command=execute>&<group=<UUID|STRING>>&<password=<STRING>>&<file=<STRING>>&<shell=<BOOL>>&<window=<BOOL>>&&[parameter=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Execute)] [CorradeCommand("execute")] [Reflection.NameAttribute("execute")] EXECUTE,
            [Reflection.NameAttribute("parameter")] PARAMETER,
            [Reflection.NameAttribute("file")] FILE,

            [CommandInputSyntax(
                "<command=cache>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<purge|load|save>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("cache")] [Reflection.NameAttribute("cache")] CACHE,

            [CommandInputSyntax(
                "<command=getgridregiondata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<GridRegion[,GridRegion...]>>&[region=<STRING|UUID>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getgridregiondata")] [Reflection.NameAttribute("getgridregiondata")] GETGRIDREGIONDATA,

            [CommandInputSyntax(
                "<command=getregionparcelsboundingbox>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getregionparcelsboundingbox")] [Reflection.NameAttribute("getregionparcelsboundingbox")] GETREGIONPARCELSBOUNDINGBOX,
            [Reflection.NameAttribute("pattern")] PATTERN,

            [CommandInputSyntax(
                "<command=searchinventory>&<group=<UUID|STRING>>&<password=<STRING>>&<pattern=<STRING>>&[type=<AssetType>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("searchinventory")] [Reflection.NameAttribute("searchinventory")] SEARCHINVENTORY,

            [CommandInputSyntax(
                "<command=getterrainheight>&<group=<UUID|STRING>>&<password=<STRING>>&[southwest=<VECTOR>]&[northwest=<VECTOR>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getterrainheight")] [Reflection.NameAttribute("getterrainheight")] GETTERRAINHEIGHT,
            [Reflection.NameAttribute("northeast")] NORTHEAST,
            [Reflection.NameAttribute("southwest")] SOUTHWEST,

            [CommandInputSyntax(
                "<command=upload>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<type=<Texture|Sound|Animation|Clothing|Bodypart|Landmark|Gesture|Notecard|LSLText>>&type=Clothing:[wear=<WearableType>]&type=Bodypart:[wear=<WearableType>]&<data=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask(
                    (ulong) Configuration.Permissions.Inventory | (ulong) Configuration.Permissions.Economy
                    )] [CorradeCommand("upload")] [Reflection.NameAttribute("upload")] UPLOAD,

            [CommandInputSyntax(
                "<command=download>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<type=<Texture|Sound|Animation|Clothing|Bodypart|Landmark|Gesture|Notecard|LSLText>>&type=Texture,Sound:[format=<STRING>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact | (ulong) Configuration.Permissions.System
                    )] [CorradeCommand("download")] [Reflection.NameAttribute("download")] DOWNLOAD,

            [CommandInputSyntax(
                "<command=setparceldata>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR>]&[data=<Parcel[,Parcel...]>]&[region=<STRING>]&[callback=<STRING>]"
                )
            ] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setparceldata")] [Reflection.NameAttribute("setparceldata")] SETPARCELDATA,
            [Reflection.NameAttribute("new")] NEW,
            [Reflection.NameAttribute("old")] OLD,
            [Reflection.NameAttribute("aggressor")] AGGRESSOR,
            [Reflection.NameAttribute("magnitude")] MAGNITUDE,
            [Reflection.NameAttribute("time")] TIME,
            [Reflection.NameAttribute("victim")] VICTIM,

            [CommandInputSyntax(
                "<command=playgesture>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("playgesture")] [Reflection.NameAttribute("playgesture")] PLAYGESTURE,

            [CommandInputSyntax(
                "<command=jump>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("jump")] [Reflection.NameAttribute("jump")] JUMP,

            [CommandInputSyntax(
                "<command=crouch>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("crouch")] [Reflection.NameAttribute("crouch")] CROUCH,

            [CommandInputSyntax(
                "<command=turnto>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR3>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("turnto")] [Reflection.NameAttribute("turnto")] TURNTO,

            [CommandInputSyntax(
                "<command=nudge>&<group=<UUID|STRING>>&<password=<STRING>>&<direction=<left|right|up|down|back|forward>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("nudge")] [Reflection.NameAttribute("nudge")] NUDGE,

            [CommandInputSyntax(
                "<command=createnotecard>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[text=<STRING>]&[description=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("createnotecard")] [Reflection.NameAttribute("createnotecard")] CREATENOTECARD,
            [Reflection.NameAttribute("direction")] DIRECTION,
            [Reflection.NameAttribute("agent")] AGENT,

            [CommandInputSyntax(
                "<command=replytoinventoryoffer>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<accept|decline>>&<session=<UUID>>&[folder=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("replytoinventoryoffer")] [Reflection.NameAttribute("replytoinventoryoffer")] REPLYTOINVENTORYOFFER,

            [CommandInputSyntax(
                "<command=getinventoryoffers>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("getinventoryoffers")] [Reflection.NameAttribute("getinventoryoffers")] GETINVENTORYOFFERS,

            [CommandInputSyntax(
                "<command=updateprimitiveinventory>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<add|remove|take>>&action=add:<entity=<UUID|STRING>>&action=remove:<entity=<UUID|STRING>>&action=take:<entity=<UUID|STRING>>&action=take:<folder=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("updateprimitiveinventory")] [Reflection.NameAttribute("updateprimitiveinventory")] UPDATEPRIMITIVEINVENTORY,
            [CommandInputSyntax("<command=version>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.None)] [CorradeCommand("version")] [Reflection.NameAttribute("version")] VERSION,

            [CommandInputSyntax(
                "<command=playsound>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&<item=<UUID|STRING>>&[gain=<FLOAT>]&[position=<VECTOR3>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("playsound")] [Reflection.NameAttribute("playsound")] PLAYSOUND,
            [Reflection.NameAttribute("gain")] GAIN,

            [CommandInputSyntax(
                "<command=getrolemembers>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getrolemembers")] [Reflection.NameAttribute("getrolemembers")] GETROLEMEMBERS,
            [Reflection.NameAttribute("status")] STATUS,

            [CommandInputSyntax(
                "<command=getmembers>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getmembers")] [Reflection.NameAttribute("getmembers")] GETMEMBERS,

            [CommandInputSyntax(
                "<command=replytoteleportlure>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<session=<UUID>>&<action=<accept|decline>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("replytoteleportlure")] [Reflection.NameAttribute("replytoteleportlure")] REPLYTOTELEPORTLURE,
            [Reflection.NameAttribute("session")] SESSION,

            [CommandInputSyntax(
                "<command=replytoscriptpermissionrequest>&<group=<UUID|STRING>>&<password=<STRING>>&<task=<UUID>>&<item=<UUID>>&<permissions=<ScriptPermission>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("replytoscriptpermissionrequest")] [Reflection.NameAttribute("replytoscriptpermissionrequest")] REPLYTOSCRIPTPERMISSIONREQUEST,
            [Reflection.NameAttribute("task")] TASK,

            [CommandInputSyntax(
                "<command=getparcellist>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getparcellist")] [Reflection.NameAttribute("getparcellist")] GETPARCELLIST,

            [CommandInputSyntax(
                "<command=parcelrelease>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("parcelrelease")] [Reflection.NameAttribute("parcelrelease")] PARCELRELEASE,

            [CommandInputSyntax(
                "<command=parcelbuy>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[forgroup=<BOOL>]&[removecontribution=<BOOL>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land | (ulong) Configuration.Permissions.Economy)] [CorradeCommand("parcelbuy")] [Reflection.NameAttribute("parcelbuy")] PARCELBUY,
            [Reflection.NameAttribute("removecontribution")] REMOVECONTRIBUTION,
            [Reflection.NameAttribute("forgroup")] FORGROUP,

            [CommandInputSyntax(
                "<command=parceldeed>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("parceldeed")] [Reflection.NameAttribute("parceldeed")] PARCELDEED,

            [CommandInputSyntax(
                "<command=parcelreclaim>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("parcelreclaim")] [Reflection.NameAttribute("parcelreclaim")] PARCELRECLAIM,

            [CommandInputSyntax(
                "<command=unwear>&<group=<UUID|STRING>>&<password=<STRING>>&<wearables=<STRING[,UUID...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("unwear")] [Reflection.NameAttribute("unwear")] UNWEAR,

            [CommandInputSyntax(
                "<command=wear>&<group=<UUID|STRING>>&<password=<STRING>>&<wearables=<STRING[,UUID...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("wear")] [Reflection.NameAttribute("wear")] WEAR,
            [Reflection.NameAttribute("wearables")] WEARABLES,
            [CommandInputSyntax("<command=getwearables>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getwearables")] [Reflection.NameAttribute("getwearables")] GETWEARABLES,

            [CommandInputSyntax(
                "<command=changeappearance>&<group=<UUID|STRING>>&<password=<STRING>>&<folder=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("changeappearance")] [Reflection.NameAttribute("changeappearance")] CHANGEAPPEARANCE,
            [Reflection.NameAttribute("folder")] FOLDER,
            [Reflection.NameAttribute("replace")] REPLACE,

            [CommandInputSyntax(
                "<command=setobjectrotation>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<rotation=<QUARTERNION>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectrotation")] [Reflection.NameAttribute("setobjectrotation")] SETOBJECTROTATION,

            [CommandInputSyntax(
                "<command=setprimitivedescription>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<description=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivedescription")] [Reflection.NameAttribute("setprimitivedescription")] SETPRIMITIVEDESCRIPTION,

            [CommandInputSyntax(
                "<command=setprimitivename>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setprimitivename")] [Reflection.NameAttribute("setprimitivename")] SETPRIMITIVENAME,

            [CommandInputSyntax(
                "<command=setobjectposition>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<position=<VECTOR3>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectposition")] [Reflection.NameAttribute("setobjectposition")] SETOBJECTPOSITION,

            [CommandInputSyntax(
                "<command=setobjectsaleinfo>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<price=<INTEGER>>&<type=<SaleType>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectsaleinfo")] [Reflection.NameAttribute("setobjectsaleinfo")] SETOBJECTSALEINFO,

            [CommandInputSyntax(
                "<command=setobjectgroup>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectgroup")] [Reflection.NameAttribute("setobjectgroup")] SETOBJECTGROUP,

            [CommandInputSyntax(
                "<command=objectdeed>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("objectdeed")] [Reflection.NameAttribute("objectdeed")] OBJECTDEED,

            [CommandInputSyntax(
                "<command=setobjectpermissions>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<permissions=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setobjectpermissions")] [Reflection.NameAttribute("setobjectpermissions")] SETOBJECTPERMISSIONS,
            [Reflection.NameAttribute("permissions")] PERMISSIONS,

            [CommandInputSyntax(
                "<command=getavatarpositions>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<region|parcel>>&entity=parcel:<position=<VECTOR2>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatarpositions")] [Reflection.NameAttribute("getavatarpositions")] GETAVATARPOSITIONS,
            [Reflection.NameAttribute("delay")] DELAY,
            [Reflection.NameAttribute("asset")] ASSET,

            [CommandInputSyntax(
                "<command=setregiondebug>&<group=<UUID|STRING>>&<password=<STRING>>&<scripts=<BOOL>>&<collisions=<BOOL>>&<physics=<BOOL>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setregiondebug")] [Reflection.NameAttribute("setregiondebug")] SETREGIONDEBUG,
            [Reflection.NameAttribute("scripts")] SCRIPTS,
            [Reflection.NameAttribute("collisions")] COLLISIONS,
            [Reflection.NameAttribute("physics")] PHYSICS,

            [CommandInputSyntax(
                "<command=getmapavatarpositions>&<group=<UUID|STRING>>&<password=<STRING>>&<region=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getmapavatarpositions")] [Reflection.NameAttribute("getmapavatarpositions")] GETMAPAVATARPOSITIONS,

            [CommandInputSyntax(
                "<command=mapfriend>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("mapfriend")] [Reflection.NameAttribute("mapfriend")] MAPFRIEND,

            [CommandInputSyntax(
                "<command=replytofriendshiprequest>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<action=<accept|decline>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("replytofriendshiprequest")] [Reflection.NameAttribute("replytofriendshiprequest")] REPLYTOFRIENDSHIPREQUEST,

            [CommandInputSyntax(
                "<command=getfriendshiprequests>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("getfriendshiprequests")] [Reflection.NameAttribute("getfriendshiprequests")] GETFRIENDSHIPREQUESTS,

            [CommandInputSyntax(
                "<command=grantfriendrights>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<rights=<FriendRights>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("grantfriendrights")] [Reflection.NameAttribute("grantfriendrights")] GRANTFRIENDRIGHTS,
            [Reflection.NameAttribute("rights")] RIGHTS,

            [CommandInputSyntax("<command=getfriendslist>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("getfriendslist")] [Reflection.NameAttribute("getfriendslist")] GETFRIENDSLIST,

            [CommandInputSyntax(
                "<command=terminatefriendship>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("terminatefriendship")] [Reflection.NameAttribute("terminatefriendship")] TERMINATEFRIENDSHIP,

            [CommandInputSyntax(
                "<command=offerfriendship>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("offerfriendship")] [Reflection.NameAttribute("offerfriendship")] OFFERFRIENDSHIP,

            [CommandInputSyntax(
                "<command=getfrienddata>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<FriendInfo[,FriendInfo...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Friendship)] [CorradeCommand("getfrienddata")] [Reflection.NameAttribute("getfrienddata")] GETFRIENDDATA,
            [Reflection.NameAttribute("days")] DAYS,
            [Reflection.NameAttribute("interval")] INTERVAL,

            [CommandInputSyntax(
                "<command=getgroupaccountsummarydata>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<data=<GroupAccountSummary[,GroupAccountSummary...]>>&<days=<INTEGER>>&<interval=<INTEGER>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getgroupaccountsummarydata")] [Reflection.NameAttribute("getgroupaccountsummarydata")] GETGROUPACCOUNTSUMMARYDATA,

            [CommandInputSyntax(
                "<command=getselfdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager[,AgentManager...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getselfdata")] [Reflection.NameAttribute("getselfdata")] GETSELFDATA,

            [CommandInputSyntax(
                "<command=deleteclassified>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("deleteclassified")] [Reflection.NameAttribute("deleteclassified")] DELETECLASSIFIED,

            [CommandInputSyntax(
                "<command=addclassified>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<price=<INTEGER>>&<type=<Any|Shopping|LandRental|PropertyRental|SpecialAttraction|NewProducts|Employment|Wanted|Service|Personal>>&[item=<UUID|STRING>]&[description=<STRING>]&[renew=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask(
                    (ulong) Configuration.Permissions.Grooming | (ulong) Configuration.Permissions.Economy)
                   ] [CorradeCommand("addclassified")] [Reflection.NameAttribute("addclassified")] ADDCLASSIFIED,
            [Reflection.NameAttribute("price")] PRICE,
            [Reflection.NameAttribute("renew")] RENEW,
            [CommandInputSyntax("<command=logout>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.System)] [CorradeCommand("logout")] [Reflection.NameAttribute("logout")] LOGOUT,

            [CommandInputSyntax(
                "<command=displayname>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<get|set>>&action=set:<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("displayname")] [Reflection.NameAttribute("displayname")] DISPLAYNAME,

            [CommandInputSyntax(
                "<command=returnprimitives>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<entity=<parcel|estate>>&<type=<Owner|Group|Other|Sell|ReturnScripted|ReturnOnOthersLand|ReturnScriptedAndOnOthers>>&type=Owner|GroupUUID|Other|Sell:[position=<VECTOR2>]&type=ReturnScripted|ReturnOnOthersLand|ReturnScriptedAndOnOthers:[all=<BOOL>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("returnprimitives")] [Reflection.NameAttribute("returnprimitives")] RETURNPRIMITIVES,

            [CommandInputSyntax(
                "<command=getgroupdata>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[target=<STRING|UUID>]&<data=<Group[,Group...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getgroupdata")] [Reflection.NameAttribute("getgroupdata")] GETGROUPDATA,

            [CommandInputSyntax(
                "<command=getavatardata>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<Avatar[,Avatar...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getavatardata")] [Reflection.NameAttribute("getavatardata")] GETAVATARDATA,

            [CommandInputSyntax(
                "<command=getprimitiveinventory>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitiveinventory")] [Reflection.NameAttribute("getprimitiveinventory")] GETPRIMITIVEINVENTORY,

            [CommandInputSyntax(
                "<command=getinventorydata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&<data=<InventoryItem[,InventoryItem...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("getinventorydata")] [Reflection.NameAttribute("getinventorydata")] GETINVENTORYDATA,

            [CommandInputSyntax(
                "<command=getprimitiveinventorydata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<data=<InventoryItem[,InventoryItem...]>>&<entity=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitiveinventorydata")] [Reflection.NameAttribute("getprimitiveinventorydata")] GETPRIMITIVEINVENTORYDATA,

            [CommandInputSyntax(
                "<command=getscriptrunning>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<entity=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getscriptrunning")] [Reflection.NameAttribute("getscriptrunning")] GETSCRIPTRUNNING,

            [CommandInputSyntax(
                "<command=setscriptrunning>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<entity=<STRING|UUID>>&<action=<start|stop>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("setscriptrunning")] [Reflection.NameAttribute("setscriptrunning")] SETSCRIPTRUNNING,

            [CommandInputSyntax(
                "<command=derez>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[folder=<STRING|UUID>]&[type=<DeRezDestination>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("derez")] [Reflection.NameAttribute("derez")] DEREZ,

            [CommandInputSyntax(
                "<command=getparceldata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Parcel[,Parcel...]>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getparceldata")] [Reflection.NameAttribute("getparceldata")] GETPARCELDATA,

            [CommandInputSyntax(
                "<command=rez>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR2>>&<item=<UUID|STRING>&[rotation=<QUARTERNION>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("rez")] [Reflection.NameAttribute("rez")] REZ,
            [Reflection.NameAttribute("rotation")] ROTATION,
            [Reflection.NameAttribute("index")] INDEX,

            [CommandInputSyntax(
                "<command=replytoscriptdialog>&<group=<UUID|STRING>>&<password=<STRING>>&<channel=<INTEGER>>&<index=<INTEGER>&<button=<STRING>>&<item=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("replytoscriptdialog")] [Reflection.NameAttribute("replytoscriptdialog")] REPLYTOSCRIPTDIALOG,
            [Reflection.NameAttribute("owner")] OWNER,
            [Reflection.NameAttribute("button")] BUTTON,

            [CommandInputSyntax("<command=getanimations>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")
            ] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getanimations")] [Reflection.NameAttribute("getanimations")] GETANIMATIONS,

            [CommandInputSyntax(
                "<command=animation>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&<action=<start|stop>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("animation")] [Reflection.NameAttribute("animation")] ANIMATION,

            [CommandInputSyntax(
                "<command=setestatelist>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<ban|group|manager|user>>&<action=<add|remove>>&type=ban|manager|user,action=add|remove:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&type=group,action=add|remove:<target=<STRING|UUID>>&[all=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("setestatelist")] [Reflection.NameAttribute("setestatelist")] SETESTATELIST,

            [CommandInputSyntax(
                "<command=getestatelist>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<ban|group|manager|user>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getestatelist")] [Reflection.NameAttribute("getestatelist")] GETESTATELIST,
            [Reflection.NameAttribute("all")] ALL,

            [CommandInputSyntax(
                "<command=getregiontop>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<scripts|colliders>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getregiontop")] [Reflection.NameAttribute("getregiontop")] GETREGIONTOP,

            [CommandInputSyntax(
                "<command=restartregion>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<scripts|colliders>>&[delay=<INTEGER>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("restartregion")] [Reflection.NameAttribute("restartregion")] RESTARTREGION,

            [CommandInputSyntax(
                "<command=directorysearch>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<classified|event|group|land|people|places>>&type=classified:<data=<Classified[,Classified...]>>&type=classified:<name=<STRING>>&type=event:<data=<EventsSearchData[,EventSearchData...]>>&type=event:<name=<STRING>>&type=group:<data=<GroupSearchData[,GroupSearchData...]>>&type=land:<data=<DirectoryParcel[,DirectoryParcel...]>>&type=people:<data=<AgentSearchData[,AgentSearchData...]>>&type=places:<data=<DirectoryParcel[,DirectoryParcel...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Directory)] [CorradeCommand("directorysearch")] [Reflection.NameAttribute("directorysearch")] DIRECTORYSEARCH,

            [CommandInputSyntax(
                "<command=getprofiledata>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<AvatarProperties[,AvatarProperties...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprofiledata")] [Reflection.NameAttribute("getprofiledata")] GETPROFILEDATA,

            [CommandInputSyntax(
                "<command=getparticlesystem>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getparticlesystem")] [Reflection.NameAttribute("getparticlesystem")] GETPARTICLESYSTEM,
            [Reflection.NameAttribute("data")] DATA,
            [Reflection.NameAttribute("range")] RANGE,
            [Reflection.NameAttribute("balance")] BALANCE,
            [Reflection.NameAttribute("key")] KEY,
            [Reflection.NameAttribute("value")] VALUE,

            [CommandInputSyntax(
                "<command=database>&<group=<UUID|STRING>>&<password=<STRING>>&<SQL=<string>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Database)] [CorradeCommand("database")] [Reflection.NameAttribute("database")] DATABASE,

            [Reflection.NameAttribute("text")] TEXT,
            [Reflection.NameAttribute("quorum")] QUORUM,
            [Reflection.NameAttribute("majority")] MAJORITY,

            [CommandInputSyntax(
                "<command=startproposal>&<group=<UUID|STRING>>&<password=<STRING>>&<duration=<INTEGER>>&<majority=<FLOAT>>&<quorum=<INTEGER>>&<text=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("startproposal")] [Reflection.NameAttribute("startproposal")] STARTPROPOSAL,
            [Reflection.NameAttribute("duration")] DURATION,
            [Reflection.NameAttribute("action")] ACTION,

            [CommandInputSyntax(
                "<command=deletefromrole>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("deletefromrole")] [Reflection.NameAttribute("deletefromrole")] DELETEFROMROLE,

            [CommandInputSyntax(
                "<command=addtorole>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("addtorole")] [Reflection.NameAttribute("addtorole")] ADDTOROLE,

            [CommandInputSyntax(
                "<command=leave>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("leave")] [Reflection.NameAttribute("leave")] LEAVE,

            [CommandInputSyntax(
                "<command=setgroupdata>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<data=<Group[,GroupUUID...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("setgroupdata")] [Reflection.NameAttribute("setgroupdata")] SETGROUPDATA,

            [CommandInputSyntax(
                "<command=eject>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("eject")] [Reflection.NameAttribute("eject")] EJECT,

            [CommandInputSyntax(
                "<command=invite>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[role=<UUID[,STRING...]>]&[verify=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("invite")] [Reflection.NameAttribute("invite")] INVITE,

            [CommandInputSyntax(
                "<command=join>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Group | (ulong) Configuration.Permissions.Economy)] [CorradeCommand("join")] [Reflection.NameAttribute("join")] JOIN,
            [Reflection.NameAttribute("callback")] CALLBACK,
            [Reflection.NameAttribute("group")] GROUP,
            [Reflection.NameAttribute("password")] PASSWORD,
            [Reflection.NameAttribute("firstname")] FIRSTNAME,
            [Reflection.NameAttribute("lastname")] LASTNAME,
            [Reflection.NameAttribute("command")] COMMAND,
            [Reflection.NameAttribute("role")] ROLE,
            [Reflection.NameAttribute("title")] TITLE,

            [CommandInputSyntax(
                "<command=tell>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<local|group|avatar|estate|region>>&entity=local:<type=<Normal|Whisper|Shout>>&entity=local,type=Normal|Whisper|Shout:[channel=<INTEGER>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&entity=group:[target=<UUID>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Talk)] [CorradeCommand("tell")] [Reflection.NameAttribute("tell")] TELL,

            [CommandInputSyntax(
                "<command=notice>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<action=<send|list|accept|decline>>&action=send:<message=<STRING>>&action=send:[subject=<STRING>]&action=send:[item=<UUID|STRING>]&action=send:[permissions=<STRING>]&action=accept|decline:<<notice=<UUID>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<session=<UUID>>&<folder=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("notice")] [Reflection.NameAttribute("notice")] NOTICE,
            [Reflection.NameAttribute("message")] MESSAGE,
            [Reflection.NameAttribute("subject")] SUBJECT,
            [Reflection.NameAttribute("item")] ITEM,

            [CommandInputSyntax(
                "<command=pay>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<avatar|object|group>>&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&entity=object:<target=<UUID>>&[reason=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Economy)] [CorradeCommand("pay")] [Reflection.NameAttribute("pay")] PAY,
            [Reflection.NameAttribute("amount")] AMOUNT,
            [Reflection.NameAttribute("target")] TARGET,
            [Reflection.NameAttribute("reason")] REASON,
            [CommandInputSyntax("<command=getbalance>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Economy)] [CorradeCommand("getbalance")] [Reflection.NameAttribute("getbalance")] GETBALANCE,

            [CommandInputSyntax(
                "<command=teleport>&<group=<UUID|STRING>>&<password=<STRING>>&<region=<STRING>>&[position=<VECTOR3>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("teleport")] [Reflection.NameAttribute("teleport")] TELEPORT,
            [Reflection.NameAttribute("region")] REGION,
            [Reflection.NameAttribute("position")] POSITION,

            [CommandInputSyntax(
                "<command=getregiondata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Simulator[,Simulator...]>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getregiondata")] [Reflection.NameAttribute("getregiondata")] GETREGIONDATA,

            [CommandInputSyntax(
                "<command=sit>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("sit")] [Reflection.NameAttribute("sit")] SIT,
            [CommandInputSyntax("<command=stand>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("stand")] [Reflection.NameAttribute("stand")] STAND,

            [CommandInputSyntax(
                "<command=parceleject>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[ban=<BOOL>]&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("parceleject")] [Reflection.NameAttribute("parceleject")] PARCELEJECT,

            [CommandInputSyntax(
                "<command=creategroup>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<data=<Group[,GroupUUID...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group | (ulong) Configuration.Permissions.Economy)] [CorradeCommand("creategroup")] [Reflection.NameAttribute("creategroup")] CREATEGROUP,

            [CommandInputSyntax(
                "<command=parcelfreeze>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[freeze=<BOOL>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("parcelfreeze")] [Reflection.NameAttribute("parcelfreeze")] PARCELFREEZE,

            [CommandInputSyntax(
                "<command=createrole>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<role=<STRING>>&[powers=<GroupPowers[,GroupPowers...]>]&[title=<STRING>]&[description=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("createrole")] [Reflection.NameAttribute("createrole")] CREATEROLE,

            [CommandInputSyntax(
                "<command=deleterole>&<group=<UUID|STRING>>&<password=<STRING>>&<role=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("deleterole")] [Reflection.NameAttribute("deleterole")] DELETEROLE,

            [CommandInputSyntax(
                "<command=getrolesmembers>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getrolesmembers")] [Reflection.NameAttribute("getrolesmembers")] GETROLESMEMBERS,

            [CommandInputSyntax(
                "<command=getroles>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getroles")] [Reflection.NameAttribute("getroles")] GETROLES,

            [CommandInputSyntax(
                "<command=getrolepowers>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("getrolepowers")] [Reflection.NameAttribute("getrolepowers")] GETROLEPOWERS,
            [Reflection.NameAttribute("powers")] POWERS,

            [CommandInputSyntax(
                "<command=lure>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("lure")] [Reflection.NameAttribute("lure")] LURE,
            [Reflection.NameAttribute("URL")] URL,
            [CommandInputSyntax("<command=sethome>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("sethome")] [Reflection.NameAttribute("sethome")] SETHOME,
            [CommandInputSyntax("<command=gohome>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("gohome")] [Reflection.NameAttribute("gohome")] GOHOME,

            [CommandInputSyntax(
                "<command=setprofiledata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AvatarProperties[,AvatarProperties...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("setprofiledata")] [Reflection.NameAttribute("setprofiledata")] SETPROFILEDATA,

            [CommandInputSyntax(
                "<command=give>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<entity=<avatar|object>>&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&entity=avatar:<item=<UUID|STRING>&entity=object:<item=<UUID|STRING>&entity=object:[range=<FLOAT>]&entity=object:<target=<UUID|STRING>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("give")] [Reflection.NameAttribute("give")] GIVE,

            [CommandInputSyntax(
                "<command=trashitem>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("trashitem")] [Reflection.NameAttribute("trashitem")] TRASHITEM,

            [CommandInputSyntax("<command=emptytrash>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Inventory)] [CorradeCommand("emptytrash")] [Reflection.NameAttribute("emptytrash")] EMPTYTRASH,

            [CommandInputSyntax(
                "<command=fly>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("fly")] [Reflection.NameAttribute("fly")] FLY,

            [CommandInputSyntax(
                "<command=addpick>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[description=<STRING>]&[item=<STRING|UUID>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("addpick")] [Reflection.NameAttribute("addpick")] ADDPICK,

            [CommandInputSyntax(
                "<command=deletepick>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("deletepick")] [Reflection.NameAttribute("deletepick")] DELETEPICK,

            [CommandInputSyntax(
                "<command=touch>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("touch")] [Reflection.NameAttribute("touch")] TOUCH,

            [CommandInputSyntax(
                "<command=moderate>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<type=<voice|text>>&<silence=<BOOL>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Group)] [CorradeCommand("moderate")] [Reflection.NameAttribute("moderate")] MODERATE,
            [Reflection.NameAttribute("type")] TYPE,
            [Reflection.NameAttribute("silence")] SILENCE,
            [Reflection.NameAttribute("freeze")] FREEZE,
            [CommandInputSyntax("<command=rebake>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("rebake")] [Reflection.NameAttribute("rebake")] REBAKE,

            [CommandInputSyntax("<command=getattachments>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("getattachments")] [Reflection.NameAttribute("getattachments")] GETATTACHMENTS,

            [CommandInputSyntax(
                "<command=attach>&<group=<UUID|STRING>>&<password=<STRING>>&<attachments=<AttachmentPoint<,<UUID|STRING>>[,AttachmentPoint<,<UUID|STRING>>...]>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("attach")] [Reflection.NameAttribute("attach")] ATTACH,
            [Reflection.NameAttribute("attachments")] ATTACHMENTS,

            [CommandInputSyntax(
                "<command=detach>&<group=<UUID|STRING>>&<password=<STRING>>&<attachments=<STRING[,UUID...]>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("detach")] [Reflection.NameAttribute("detach")] DETACH,

            [CommandInputSyntax(
                "<command=getprimitiveowners>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("getprimitiveowners")] [Reflection.NameAttribute("getprimitiveowners")] GETPRIMITIVEOWNERS,
            [Reflection.NameAttribute("entity")] ENTITY,
            [Reflection.NameAttribute("channel")] CHANNEL,
            [Reflection.NameAttribute("name")] NAME,
            [Reflection.NameAttribute("description")] DESCRIPTION,

            [CommandInputSyntax(
                "<command=getprimitivedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<data=<Primitive[,Primitive...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Interact)] [CorradeCommand("getprimitivedata")] [Reflection.NameAttribute("getprimitivedata")] GETPRIMITIVEDATA,

            [CommandInputSyntax(
                "<command=activate>&<group=<UUID|STRING>>&[target=<UUID>]&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Grooming)] [CorradeCommand("activate")] [Reflection.NameAttribute("activate")] ACTIVATE,

            [CommandInputSyntax(
                "<command=autopilot>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR2>>&<action=<start|stop>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Movement)] [CorradeCommand("autopilot")] [Reflection.NameAttribute("autopilot")] AUTOPILOT,

            [CommandInputSyntax(
                "<command=mute>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<MuteType>>&type=Resident:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&type=Group:<target=<UUID|STRING>>&type=ByName:<name=<STRING>>&type=Object:<name=<STRING>>&type=Object:<target=<UUID>>&type=External:<name=<STRING>>&type=External:<target=<UUID>>&action=mute:[flags=MuteFlags]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Mute)] [CorradeCommand("mute")] [Reflection.NameAttribute("mute")] MUTE,

            [CommandInputSyntax("<command=getmutes>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((ulong) Configuration.Permissions.Mute)] [CorradeCommand("getmutes")] [Reflection.NameAttribute("getmutes")] GETMUTES,

            [CommandInputSyntax(
                "<command=notify>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<add|set|remove|list|clear|purge>>&action=add|set|remove|clear:<type=<STRING[,STRING...]>>&action=add|set|remove:<URL=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Notifications)] [CorradeCommand("notify")] [Reflection.NameAttribute("notify")] NOTIFY,
            [Reflection.NameAttribute("source")] SOURCE,
            [Reflection.NameAttribute("effect")] EFFECT,
            [Reflection.NameAttribute("id")] ID,

            [CommandInputSyntax(
                "<command=terrain>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<set|get>>&action=set:<data=<STRING>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((ulong) Configuration.Permissions.Land)] [CorradeCommand("terrain")] [Reflection.NameAttribute("terrain")] TERRAIN,
            [Reflection.NameAttribute("output")] OUTPUT,
            [Reflection.NameAttribute("input")] INPUT
        }

        /// <summary>
        ///     Possible sifting actions for post-processing data.
        /// </summary>
        public enum Sift : uint
        {
            [Reflection.NameAttribute("none")] NONE = 0,
            [Reflection.NameAttribute("take")] TAKE,
            [Reflection.NameAttribute("each")] EACH,
            [Reflection.NameAttribute("match")] MATCH,
            [Reflection.NameAttribute("skip")] SKIP,
            [Reflection.NameAttribute("js")] JS
        }


        /// <summary>
        ///     A structure holding Corrade command parameters.
        /// </summary>
        public struct CorradeCommandParameters
        {
            [Reflection.NameAttribute("group")] public Configuration.Group Group;
            [Reflection.NameAttribute("identifier")] public string Identifier;
            [Reflection.NameAttribute("message")] public string Message;
            [Reflection.NameAttribute("sender")] public string Sender;
        }

        public class CorradeCommandAttribute : Attribute
        {
            public CorradeCommandAttribute(string command)
            {
                CorradeCommand = Corrade.corradeCommands[command];
            }

            public Action<CorradeCommandParameters, Dictionary<string, string>> CorradeCommand { get; }
        }

        /// <summary>
        ///     The status for an error message.
        /// </summary>
        public class StatusAttribute : Attribute
        {
            protected readonly uint statusCode;

            public StatusAttribute(uint statusCode)
            {
                this.statusCode = statusCode;
            }

            public uint Status => statusCode;
        }

        /// <summary>
        ///     The syntax for a command.
        /// </summary>
        public class CommandInputSyntaxAttribute : Attribute
        {
            public CommandInputSyntaxAttribute(string syntax)
            {
                Syntax = syntax;
            }

            public string Syntax { get; }
        }

        /// <summary>
        ///     The permission mask of a command.
        /// </summary>
        public class CommandPermissionMaskAttribute : Attribute
        {
            public CommandPermissionMaskAttribute(ulong permissionMask)
            {
                PermissionMask = permissionMask;
            }

            public ulong PermissionMask { get; }
        }


        /// <summary>
        ///     An exception thrown on script errors.
        /// </summary>
        [Serializable]
        public class ScriptException : Exception
        {
            public ScriptException(Enumerations.ScriptError error)
                : base(Reflection.GetDescriptionFromEnumValue(error))
            {
                Status = Reflection.GetAttributeFromEnumValue<StatusAttribute>(error).Status;
            }

            protected ScriptException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public uint Status { get; }
        }

        /// <summary>
        ///     A structure for group scheduled commands.
        /// </summary>
        [Serializable]
        public struct GroupSchedule
        {
            [Reflection.NameAttribute("at")] public DateTime At;
            [Reflection.NameAttribute("group")] public Configuration.Group Group;
            [Reflection.NameAttribute("identifier")] public string Identifier;
            [Reflection.NameAttribute("message")] public string Message;
            [Reflection.NameAttribute("sender")] public string Sender;
        }
    }
}