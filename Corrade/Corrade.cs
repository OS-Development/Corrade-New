///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AIMLbot;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using Parallel = System.Threading.Tasks.Parallel;
using Path = System.IO.Path;
using ThreadState = System.Threading.ThreadState;

#endregion

namespace Corrade
{
    public partial class Corrade : ServiceBase
    {
        public delegate bool EventHandler(NativeMethods.CtrlType ctrlType);

        /// <summary>
        ///     The type of threads managed by Corrade.
        /// </summary>
        public enum CorradeThreadType : uint
        {
            [Description("command")] COMMAND = 1,
            [Description("rlv")] RLV = 2,
            [Description("notification")] NOTIFICATION = 3,
            [Description("instant message")] INSTANT_MESSAGE = 4,
            [Description("log")] LOG = 5,
            [Description("post")] POST = 6
        };

        /// <summary>
        ///     Possible input and output filters.
        /// </summary>
        public enum Filter : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE = 0,
            [XmlEnum(Name = "RFC1738")] [Description("RFC1738")] RFC1738,
            [XmlEnum(Name = "RFC3986")] [Description("RFC3986")] RFC3986,
            [XmlEnum(Name = "ENIGMA")] [Description("ENIGMA")] ENIGMA,
            [XmlEnum(Name = "VIGENERE")] [Description("VIGENERE")] VIGENERE,
            [XmlEnum(Name = "ATBASH")] [Description("ATBASH")] ATBASH,
            [XmlEnum(Name = "BASE64")] [Description("BASE64")] BASE64,
            [XmlEnum(Name = "AES")] [Description("AES")] AES
        }

        /// <summary>
        ///     An enumeration of various compression methods
        ///     supproted by Corrade's internal HTTP server.
        /// </summary>
        public enum HTTPCompressionMethod : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE,
            [XmlEnum(Name = "deflate")] [Description("deflate")] DEFLATE,
            [XmlEnum(Name = "gzip")] [Description("gzip")] GZIP
        }

        /// <summary>
        ///     Corrade notification types.
        /// </summary>
        [Flags]
        public enum Notifications : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] NONE = 0,
            [XmlEnum(Name = "alert")] [Description("alert")] AlertMessage = 1,
            [XmlEnum(Name = "region")] [Description("region")] RegionMessage = 2,
            [XmlEnum(Name = "group")] [Description("group")] GroupMessage = 4,
            [XmlEnum(Name = "balance")] [Description("balance")] Balance = 8,
            [XmlEnum(Name = "message")] [Description("message")] InstantMessage = 16,
            [XmlEnum(Name = "notice")] [Description("notice")] GroupNotice = 32,
            [XmlEnum(Name = "local")] [Description("local")] LocalChat = 64,
            [XmlEnum(Name = "dialog")] [Description("dialog")] ScriptDialog = 128,
            [XmlEnum(Name = "friendship")] [Description("friendship")] Friendship = 256,
            [XmlEnum(Name = "inventory")] [Description("inventory")] Inventory = 512,
            [XmlEnum(Name = "permission")] [Description("permission")] ScriptPermission = 1024,
            [XmlEnum(Name = "lure")] [Description("lure")] TeleportLure = 2048,
            [XmlEnum(Name = "effect")] [Description("effect")] ViewerEffect = 4096,
            [XmlEnum(Name = "collision")] [Description("collision")] MeanCollision = 8192,
            [XmlEnum(Name = "crossing")] [Description("crossing")] RegionCrossed = 16384,
            [XmlEnum(Name = "terse")] [Description("terse")] TerseUpdates = 32768,
            [XmlEnum(Name = "typing")] [Description("typing")] Typing = 65536,
            [XmlEnum(Name = "invite")] [Description("invite")] GroupInvite = 131072,
            [XmlEnum(Name = "economy")] [Description("economy")] Economy = 262144,
            [XmlEnum(Name = "membership")] [Description("membership")] GroupMembership = 524288,
            [XmlEnum(Name = "url")] [Description("url")] LoadURL = 1048576,
            [XmlEnum(Name = "ownersay")] [Description("ownersay")] OwnerSay = 2097152,
            [XmlEnum(Name = "regionsayto")] [Description("regionsayto")] RegionSayTo = 4194304,
            [XmlEnum(Name = "objectim")] [Description("objectim")] ObjectInstantMessage = 8388608,
            [XmlEnum(Name = "rlv")] [Description("rlv")] RLVMessage = 16777216,
            [XmlEnum(Name = "debug")] [Description("debug")] DebugMessage = 33554432,
            [XmlEnum(Name = "avatars")] [Description("avatars")] RadarAvatars = 67108864,
            [XmlEnum(Name = "primitives")] [Description("primitives")] RadarPrimitives = 134217728,
            [XmlEnum(Name = "control")] [Description("control")] ScriptControl = 268435456
        }

        /// <summary>
        ///     Corrade permissions.
        /// </summary>
        [Flags]
        public enum Permissions : uint
        {
            [XmlEnum(Name = "none")] [Description("none")] None = 0,
            [XmlEnum(Name = "movement")] [Description("movement")] Movement = 1,
            [XmlEnum(Name = "economy")] [Description("economy")] Economy = 2,
            [XmlEnum(Name = "land")] [Description("land")] Land = 4,
            [XmlEnum(Name = "grooming")] [Description("grooming")] Grooming = 8,
            [XmlEnum(Name = "inventory")] [Description("inventory")] Inventory = 16,
            [XmlEnum(Name = "interact")] [Description("interact")] Interact = 32,
            [XmlEnum(Name = "mute")] [Description("mute")] Mute = 64,
            [XmlEnum(Name = "database")] [Description("database")] Database = 128,
            [XmlEnum(Name = "notifications")] [Description("notifications")] Notifications = 256,
            [XmlEnum(Name = "talk")] [Description("talk")] Talk = 512,
            [XmlEnum(Name = "directory")] [Description("directory")] Directory = 1024,
            [XmlEnum(Name = "system")] [Description("system")] System = 2048,
            [XmlEnum(Name = "friendship")] [Description("friendship")] Friendship = 4096,
            [XmlEnum(Name = "execute")] [Description("execute")] Execute = 8192,
            [XmlEnum(Name = "group")] [Description("group")] Group = 16384,
            [XmlEnum(Name = "filter")] [Description("filter")] Filter = 32768,
            [XmlEnum(Name = "schedule")] [Description("schedule")] Schedule = 65536
        }

        /// <summary>
        ///     Structure containing errors returned to scripts.
        /// </summary>
        /// <remarks>
        ///     Status is generated by:
        ///     1.) jot -r 900 0 65535 | uniq | xargs printf "%05d\n" | pbcopy
        ///     2.) paste codes.txt status.txt | awk -F"\n" '{print $1,$2}' | pbcopy
        ///     Removals: 43508 - could not get land users
        /// </remarks>
        public enum ScriptError : uint
        {
            [Status(0)] [Description("none")] NONE = 0,
            [Status(35392)] [Description("could not join group")] COULD_NOT_JOIN_GROUP,
            [Status(20900)] [Description("could not leave group")] COULD_NOT_LEAVE_GROUP,
            [Status(57961)] [Description("agent not found")] AGENT_NOT_FOUND,
            [Status(28002)] [Description("group not found")] GROUP_NOT_FOUND,
            [Status(15345)] [Description("already in group")] ALREADY_IN_GROUP,
            [Status(11502)] [Description("not in group")] NOT_IN_GROUP,
            [Status(32472)] [Description("role not found")] ROLE_NOT_FOUND,
            [Status(08653)] [Description("command not found")] COMMAND_NOT_FOUND,
            [Status(14634)] [Description("could not eject agent")] COULD_NOT_EJECT_AGENT,
            [Status(30473)] [Description("no group power for command")] NO_GROUP_POWER_FOR_COMMAND,
            [Status(27605)] [Description("cannot eject owners")] CANNOT_EJECT_OWNERS,
            [Status(25984)] [Description("inventory item not found")] INVENTORY_ITEM_NOT_FOUND,
            [Status(43982)] [Description("invalid amount")] INVALID_AMOUNT,
            [Status(02169)] [Description("insufficient funds")] INSUFFICIENT_FUNDS,
            [Status(47624)] [Description("invalid pay target")] INVALID_PAY_TARGET,
            [Status(32164)] [Description("teleport failed")] TELEPORT_FAILED,
            [Status(22693)] [Description("primitive not found")] PRIMITIVE_NOT_FOUND,
            [Status(28613)] [Description("could not sit")] COULD_NOT_SIT,
            [Status(48467)] [Description("no Corrade permissions")] NO_CORRADE_PERMISSIONS,
            [Status(54214)] [Description("could not create group")] COULD_NOT_CREATE_GROUP,
            [Status(11287)] [Description("could not create role")] COULD_NOT_CREATE_ROLE,
            [Status(12758)] [Description("no role name specified")] NO_ROLE_NAME_SPECIFIED,
            [Status(34084)] [Description("timeout getting group roles members")] TIMEOUT_GETING_GROUP_ROLES_MEMBERS,
            [Status(11050)] [Description("timeout getting group roles")] TIMEOUT_GETTING_GROUP_ROLES,
            [Status(39016)] [Description("timeout getting role powers")] TIMEOUT_GETTING_ROLE_POWERS,
            [Status(64390)] [Description("could not find parcel")] COULD_NOT_FIND_PARCEL,
            [Status(17019)] [Description("unable to set home")] UNABLE_TO_SET_HOME,
            [Status(31493)] [Description("unable to go home")] UNABLE_TO_GO_HOME,
            [Status(32923)] [Description("timeout getting profile")] TIMEOUT_GETTING_PROFILE,
            [Status(56462)] [Description("texture not found")] TEXTURE_NOT_FOUND,
            [Status(36068)] [Description("type can only be voice or text")] TYPE_CAN_BE_VOICE_OR_TEXT,
            [Status(19862)] [Description("agent not in group")] AGENT_NOT_IN_GROUP,
            [Status(29345)] [Description("empty attachments")] EMPTY_ATTACHMENTS,
            [Status(48899)] [Description("empty pick name")] EMPTY_PICK_NAME,
            [Status(22733)] [Description("unable to join group chat")] UNABLE_TO_JOIN_GROUP_CHAT,
            [Status(59524)] [Description("invalid position")] INVALID_POSITION,
            [Status(02707)] [Description("could not find title")] COULD_NOT_FIND_TITLE,
            [Status(43713)] [Description("fly action can only be start or stop")] FLY_ACTION_START_OR_STOP,
            [Status(64868)] [Description("invalid proposal text")] INVALID_PROPOSAL_TEXT,
            [Status(03098)] [Description("invalid proposal quorum")] INVALID_PROPOSAL_QUORUM,
            [Status(41810)] [Description("invalid proposal majority")] INVALID_PROPOSAL_MAJORITY,
            [Status(07628)] [Description("invalid proposal duration")] INVALID_PROPOSAL_DURATION,
            [Status(64123)] [Description("invalid mute target")] INVALID_MUTE_TARGET,
            [Status(59526)] [Description("unknown action")] UNKNOWN_ACTION,
            [Status(28087)] [Description("no database file configured")] NO_DATABASE_FILE_CONFIGURED,
            [Status(12181)] [Description("no database key specified")] NO_DATABASE_KEY_SPECIFIED,
            [Status(44994)] [Description("no database value specified")] NO_DATABASE_VALUE_SPECIFIED,
            [Status(19142)] [Description("unknown database action")] UNKNOWN_DATABASE_ACTION,
            [Status(01253)] [Description("cannot remove owner role")] CANNOT_REMOVE_OWNER_ROLE,
            [Status(47808)] [Description("cannot remove user from owner role")] CANNOT_REMOVE_USER_FROM_OWNER_ROLE,
            [Status(47469)] [Description("timeout getting picks")] TIMEOUT_GETTING_PICKS,
            [Status(41256)] [Description("maximum number of roles exceeded")] MAXIMUM_NUMBER_OF_ROLES_EXCEEDED,
            [Status(40908)] [Description("cannot delete a group member from the everyone role")] CANNOT_DELETE_A_GROUP_MEMBER_FROM_THE_EVERYONE_ROLE,
            [Status(00458)] [Description("group members are by default in the everyone role")] GROUP_MEMBERS_ARE_BY_DEFAULT_IN_THE_EVERYONE_ROLE,
            [Status(33413)] [Description("cannot delete the everyone role")] CANNOT_DELETE_THE_EVERYONE_ROLE,
            [Status(65303)] [Description("invalid url provided")] INVALID_URL_PROVIDED,
            [Status(65327)] [Description("invalid notification types")] INVALID_NOTIFICATION_TYPES,
            [Status(49640)] [Description("notification not allowed")] NOTIFICATION_NOT_ALLOWED,
            [Status(44447)] [Description("unknown directory search type")] UNKNOWN_DIRECTORY_SEARCH_TYPE,
            [Status(65101)] [Description("no search text provided")] NO_SEARCH_TEXT_PROVIDED,
            [Status(14337)] [Description("unknown restart action")] UNKNOWN_RESTART_ACTION,
            [Status(28429)] [Description("unknown move action")] UNKNOWN_MOVE_ACTION,
            [Status(20541)] [Description("timeout getting top scripts")] TIMEOUT_GETTING_TOP_SCRIPTS,
            [Status(47172)] [Description("timeout getting top colliders")] TIMEOUT_GETTING_TOP_COLLIDERS,
            [Status(57429)] [Description("timeout waiting for estate list")] TIMEOUT_WAITING_FOR_ESTATE_LIST,
            [Status(41676)] [Description("unknown top type")] UNKNOWN_TOP_TYPE,
            [Status(25897)] [Description("unknown estate list action")] UNKNOWN_ESTATE_LIST_ACTION,
            [Status(46990)] [Description("unknown estate list")] UNKNOWN_ESTATE_LIST,
            [Status(43156)] [Description("no item specified")] NO_ITEM_SPECIFIED,
            [Status(09348)] [Description("unknown animation action")] UNKNOWN_ANIMATION_ACTION,
            [Status(42216)] [Description("no channel specified")] NO_CHANNEL_SPECIFIED,
            [Status(31049)] [Description("no button index specified")] NO_BUTTON_INDEX_SPECIFIED,
            [Status(38931)] [Description("no button specified")] NO_BUTTON_SPECIFIED,
            [Status(19059)] [Description("no land rights")] NO_LAND_RIGHTS,
            [Status(61113)] [Description("unknown entity")] UNKNOWN_ENTITY,
            [Status(58183)] [Description("invalid rotation")] INVALID_ROTATION,
            [Status(45364)] [Description("could not set script state")] COULD_NOT_SET_SCRIPT_STATE,
            [Status(50218)] [Description("item is not a script")] ITEM_IS_NOT_A_SCRIPT,
            [Status(49722)] [Description("failed to get display name")] FAILED_TO_GET_DISPLAY_NAME,
            [Status(40665)] [Description("no name provided")] NO_NAME_PROVIDED,
            [Status(35198)] [Description("could not set display name")] COULD_NOT_SET_DISPLAY_NAME,
            [Status(63713)] [Description("timeout joining group")] TIMEOUT_JOINING_GROUP,
            [Status(32404)] [Description("timeout creating group")] TIMEOUT_CREATING_GROUP,
            [Status(00616)] [Description("timeout ejecting agent")] TIMEOUT_EJECTING_AGENT,
            [Status(25426)] [Description("timeout getting group role members")] TIMEOUT_GETTING_GROUP_ROLE_MEMBERS,
            [Status(31237)] [Description("timeout leaving group")] TIMEOUT_LEAVING_GROUP,
            [Status(14951)] [Description("timeout joining group chat")] TIMEOUT_JOINING_GROUP_CHAT,
            [Status(43780)] [Description("timeout during teleport")] TIMEOUT_DURING_TELEPORT,
            [Status(46316)] [Description("timeout requesting sit")] TIMEOUT_REQUESTING_SIT,
            [Status(09111)] [Description("timeout getting land users")] TIMEOUT_GETTING_LAND_USERS,
            [Status(23364)] [Description("timeout getting script state")] TIMEOUT_GETTING_SCRIPT_STATE,
            [Status(26393)] [Description("timeout updating mute list")] TIMEOUT_UPDATING_MUTE_LIST,
            [Status(32362)] [Description("timeout getting parcels")] TIMEOUT_GETTING_PARCELS,
            [Status(46942)] [Description("empty classified name")] EMPTY_CLASSIFIED_NAME,
            [Status(38184)] [Description("invalid price")] INVALID_PRICE,
            [Status(59103)] [Description("timeout getting classifieds")] TIMEOUT_GETTING_CLASSIFIEDS,
            [Status(08241)] [Description("could not find classified")] COULD_NOT_FIND_CLASSIFIED,
            [Status(53947)] [Description("invalid days")] INVALID_DAYS,
            [Status(18490)] [Description("invalid interval")] INVALID_INTERVAL,
            [Status(53829)] [Description("timeout getting group account summary")] TIMEOUT_GETTING_GROUP_ACCOUNT_SUMMARY,
            [Status(30207)] [Description("friend not found")] FRIEND_NOT_FOUND,
            [Status(32366)] [Description("the agent already is a friend")] AGENT_ALREADY_FRIEND,
            [Status(04797)] [Description("no friendship offer found")] NO_FRIENDSHIP_OFFER_FOUND,
            [Status(65003)] [Description("friend does not allow mapping")] FRIEND_DOES_NOT_ALLOW_MAPPING,
            [Status(10691)] [Description("timeout mapping friend")] TIMEOUT_MAPPING_FRIEND,
            [Status(23309)] [Description("friend offline")] FRIEND_OFFLINE,
            [Status(34964)] [Description("timeout getting region")] TIMEOUT_GETTING_REGION,
            [Status(35447)] [Description("region not found")] REGION_NOT_FOUND,
            [Status(00337)] [Description("no map items found")] NO_MAP_ITEMS_FOUND,
            [Status(53549)] [Description("no description provided")] NO_DESCRIPTION_PROVIDED,
            [Status(43982)] [Description("no folder specified")] NO_FOLDER_SPECIFIED,
            [Status(29512)] [Description("empty wearables")] EMPTY_WEARABLES,
            [Status(35316)] [Description("parcel not for sale")] PARCEL_NOT_FOR_SALE,
            [Status(42051)] [Description("unknown access list type")] UNKNOWN_ACCESS_LIST_TYPE,
            [Status(29438)] [Description("no task specified")] NO_TASK_SPECIFIED,
            [Status(37470)] [Description("timeout getting group members")] TIMEOUT_GETTING_GROUP_MEMBERS,
            [Status(24939)] [Description("group not open")] GROUP_NOT_OPEN,
            [Status(30384)] [Description("timeout downloading terrain")] TIMEOUT_DOWNLOADING_ASSET,
            [Status(57005)] [Description("timeout uploading terrain")] TIMEOUT_UPLOADING_ASSET,
            [Status(16667)] [Description("empty terrain data")] EMPTY_ASSET_DATA,
            [Status(34749)] [Description("the specified folder contains no equipable items")] NO_EQUIPABLE_ITEMS,
            [Status(42249)] [Description("inventory offer not found")] INVENTORY_OFFER_NOT_FOUND,
            [Status(23805)] [Description("no session specified")] NO_SESSION_SPECIFIED,
            [Status(61018)] [Description("folder not found")] FOLDER_NOT_FOUND,
            [Status(37211)] [Description("timeout creating item")] TIMEOUT_CREATING_ITEM,
            [Status(09541)] [Description("timeout uploading item")] TIMEOUT_UPLOADING_ITEM,
            [Status(36684)] [Description("unable to upload item")] UNABLE_TO_UPLOAD_ITEM,
            [Status(05034)] [Description("unable to create item")] UNABLE_TO_CREATE_ITEM,
            [Status(44397)] [Description("timeout uploading item data")] TIMEOUT_UPLOADING_ITEM_DATA,
            [Status(12320)] [Description("unable to upload item data")] UNABLE_TO_UPLOAD_ITEM_DATA,
            [Status(55979)] [Description("unknown direction")] UNKNOWN_DIRECTION,
            [Status(22576)] [Description("timeout requesting to set home")] TIMEOUT_REQUESTING_TO_SET_HOME,
            [Status(07255)] [Description("timeout transferring asset")] TIMEOUT_TRANSFERRING_ASSET,
            [Status(60269)] [Description("asset upload failed")] ASSET_UPLOAD_FAILED,
            [Status(57085)] [Description("failed to download asset")] FAILED_TO_DOWNLOAD_ASSET,
            [Status(60025)] [Description("unknown asset type")] UNKNOWN_ASSET_TYPE,
            [Status(59048)] [Description("invalid asset data")] INVALID_ASSET_DATA,
            [Status(32709)] [Description("unknown wearable type")] UNKNOWN_WEARABLE_TYPE,
            [Status(06097)] [Description("unknown inventory type")] UNKNOWN_INVENTORY_TYPE,
            [Status(64698)] [Description("could not compile regular expression")] COULD_NOT_COMPILE_REGULAR_EXPRESSION,
            [Status(18680)] [Description("no pattern provided")] NO_PATTERN_PROVIDED,
            [Status(11910)] [Description("no executable file provided")] NO_EXECUTABLE_FILE_PROVIDED,
            [Status(31381)] [Description("timeout waiting for execution")] TIMEOUT_WAITING_FOR_EXECUTION,
            [Status(04541)] [Description("unknown group invite session")] UNKNOWN_GROUP_INVITE_SESSION,
            [Status(38125)] [Description("unable to obtain money balance")] UNABLE_TO_OBTAIN_MONEY_BALANCE,
            [Status(20048)] [Description("timeout getting avatar data")] TIMEOUT_GETTING_AVATAR_DATA,
            [Status(13712)] [Description("timeout retrieving estate list")] TIMEOUT_RETRIEVING_ESTATE_LIST,
            [Status(37559)] [Description("destination too close")] DESTINATION_TOO_CLOSE,
            [Status(11229)] [Description("timeout getting group titles")] TIMEOUT_GETTING_GROUP_TITLES,
            [Status(47101)] [Description("no message provided")] NO_MESSAGE_PROVIDED,
            [Status(04075)] [Description("could not remove brain file")] COULD_NOT_REMOVE_BRAIN_FILE,
            [Status(54456)] [Description("unknown effect")] UNKNOWN_EFFECT,
            [Status(48775)] [Description("no effect UUID provided")] NO_EFFECT_UUID_PROVIDED,
            [Status(38858)] [Description("effect not found")] EFFECT_NOT_FOUND,
            [Status(16572)] [Description("invalid viewer effect")] INVALID_VIEWER_EFFECT,
            [Status(19011)] [Description("ambiguous path")] AMBIGUOUS_PATH,
            [Status(53066)] [Description("path not found")] PATH_NOT_FOUND,
            [Status(13857)] [Description("unexpected item in path")] UNEXPECTED_ITEM_IN_PATH,
            [Status(59282)] [Description("no path provided")] NO_PATH_PROVIDED,
            [Status(26623)] [Description("unable to create folder")] UNABLE_TO_CREATE_FOLDER,
            [Status(28866)] [Description("no permissions provided")] NO_PERMISSIONS_PROVIDED,
            [Status(43615)] [Description("setting permissions failed")] SETTING_PERMISSIONS_FAILED,
            [Status(36716)] [Description("timeout retrieving item")] TIMEOUT_RETRIEVING_ITEM,
            [Status(39391)] [Description("expected item as source")] EXPECTED_ITEM_AS_SOURCE,
            [Status(22655)] [Description("expected folder as target")] EXPECTED_FOLDER_AS_TARGET,
            [Status(63024)] [Description("unable to load configuration")] UNABLE_TO_LOAD_CONFIGURATION,
            [Status(33564)] [Description("unable to save configuration")] UNABLE_TO_SAVE_CONFIGURATION,
            [Status(20900)] [Description("invalid xml path")] INVALID_XML_PATH,
            [Status(03638)] [Description("no data provided")] NO_DATA_PROVIDED,
            [Status(42903)] [Description("unknown image format requested")] UNKNOWN_IMAGE_FORMAT_REQUESTED,
            [Status(02380)] [Description("unknown image format provided")] UNKNOWN_IMAGE_FORMAT_PROVIDED,
            [Status(04994)] [Description("unable to decode asset data")] UNABLE_TO_DECODE_ASSET_DATA,
            [Status(61067)] [Description("unable to convert to requested format")] UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT,
            [Status(08411)] [Description("could not start process")] COULD_NOT_START_PROCESS,
            [Status(11869)] [Description("timeout getting primitive data")] TIMEOUT_GETTING_PRIMITIVE_DATA,
            [Status(22737)] [Description("item is not an object")] ITEM_IS_NOT_AN_OBJECT,
            [Status(19143)] [Description("timeout meshmerizing object")] COULD_NOT_MESHMERIZE_OBJECT,
            [Status(37841)] [Description("could not get primitive properties")] COULD_NOT_GET_PRIMITIVE_PROPERTIES,
            [Status(54854)] [Description("avatar not in range")] AVATAR_NOT_IN_RANGE,
            [Status(03475)] [Description("invalid scale")] INVALID_SCALE,
            [Status(30129)] [Description("could not get current groups")] COULD_NOT_GET_CURRENT_GROUPS,
            [Status(39613)] [Description("maximum number of groups reached")] MAXIMUM_NUMBER_OF_GROUPS_REACHED,
            [Status(43003)] [Description("unknown syntax type")] UNKNOWN_SYNTAX_TYPE,
            [Status(13053)] [Description("too many characters for group name")] TOO_MANY_CHARACTERS_FOR_GROUP_NAME,
            [Status(19325)] [Description("too many characters for group title")] TOO_MANY_CHARACTERS_FOR_GROUP_TITLE,
            [Status(26178)] [Description("too many characters for notice message")] TOO_MANY_CHARACTERS_FOR_NOTICE_MESSAGE,
            [Status(35277)] [Description("notecard message body too large")] NOTECARD_MESSAGE_BODY_TOO_LARGE,
            [Status(47571)] [Description("too many or too few characters for display name")] TOO_MANY_OR_TOO_FEW_CHARACTERS_FOR_DISPLAY_NAME,
            [Status(30293)] [Description("name too large")] NAME_TOO_LARGE,
            [Status(60515)] [Description("position would exceed maximum rez altitude")] POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE,
            [Status(43683)] [Description("description too large")] DESCRIPTION_TOO_LARGE,
            [Status(54154)] [Description("scale would exceed building constraints")] SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS,
            [Status(29745)] [Description("attachments would exceed maximum attachment limit")] ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT,
            [Status(52299)] [Description("too many or too few characters in message")] TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE,
            [Status(50593)] [Description("maximum ban list length reached")] MAXIMUM_BAN_LIST_LENGTH_REACHED,
            [Status(09935)] [Description("maximum group list length reached")] MAXIMUM_GROUP_LIST_LENGTH_REACHED,
            [Status(42536)] [Description("maximum user list length reached")] MAXIMUM_USER_LIST_LENGTH_REACHED,
            [Status(28625)] [Description("maximum manager list length reached")] MAXIMUM_MANAGER_LIST_LENGTH_REACHED,
            [Status(28126)] [Description("auto return time outside limit range")] AUTO_RETURN_TIME_OUTSIDE_LIMIT_RANGE,
            [Status(56379)] [Description("second life text too large")] SECOND_LIFE_TEXT_TOO_LARGE,
            [Status(09924)] [Description("first life text too large")] FIRST_LIFE_TEXT_TOO_LARGE,
            [Status(50405)] [Description("maximum amount of picks reached")] MAXIMUM_AMOUNT_OF_PICKS_REACHED,
            [Status(17894)] [Description("description would exceed maximum size")] DESCRIPTION_WOULD_EXCEED_MAXIMUM_SIZE,
            [Status(28247)] [Description("maximum amount of classifieds reached")] MAXIMUM_AMOUNT_OF_CLASSIFIEDS_REACHED,
            [Status(38609)] [Description("timeout changing links")] TIMEOUT_CHANGING_LINKS,
            [Status(45074)] [Description("link would exceed maximum link limit")] LINK_WOULD_EXCEED_MAXIMUM_LINK_LIMIT,
            [Status(40773)] [Description("invalid number of items specified")] INVALID_NUMBER_OF_ITEMS_SPECIFIED,
            [Status(52751)] [Description("timeout requesting price")] TIMEOUT_REQUESTING_PRICE,
            [Status(01536)] [Description("primitive not for sale")] PRIMITIVE_NOT_FOR_SALE,
            [Status(36123)] [Description("teleport throttled")] TELEPORT_THROTTLED,
            [Status(06617)] [Description("no matching dialog found")] NO_MATCHING_DIALOG_FOUND,
            [Status(08842)] [Description("unknown tree type")] UNKNOWN_TREE_TYPE,
            [Status(20238)] [Description("parcel must be owned")] PARCEL_MUST_BE_OWNED,
            [Status(62130)] [Description("invalid texture coordinates")] INVALID_TEXTURE_COORDINATES,
            [Status(10945)] [Description("invalid surface coordinates")] INVALID_SURFACE_COORDINATES,
            [Status(28487)] [Description("invalid normal vector")] INVALID_NORMAL_VECTOR,
            [Status(13296)] [Description("invalid binormal vector")] INVALID_BINORMAL_VECTOR,
            [Status(44554)] [Description("primitives not in same region")] PRIMITIVES_NOT_IN_SAME_REGION,
            [Status(38798)] [Description("invalid face specified")] INVALID_FACE_SPECIFIED,
            [Status(61473)] [Description("invalid status supplied")] INVALID_STATUS_SUPPLIED,
            [Status(13764)] [Description("status not found")] STATUS_NOT_FOUND,
            [Status(30556)] [Description("no description for status")] NO_DESCRIPTION_FOR_STATUS,
            [Status(64368)] [Description("unknown grass type")] UNKNOWN_GRASS_TYPE,
            [Status(53274)] [Description("unknown material type")] UNKNOWN_MATERIAL_TYPE,
            [Status(18463)] [Description("could not retrieve object media")] COULD_NOT_RETRIEVE_OBJECT_MEDIA,
            [Status(02193)] [Description("no avatars to ban or unban")] NO_AVATARS_TO_BAN_OR_UNBAN,
            [Status(45568)] [Description("could not retrieve broup ban list")] COULD_NOT_RETRIEVE_GROUP_BAN_LIST,
            [Status(15719)] [Description("timeout retrieving group ban list")] TIMEOUT_RETRIEVING_GROUP_BAN_LIST,
            [Status(26749)] [Description("timeout modifying group ban list")] TIMEOUT_MODIFYING_GROUP_BAN_LIST,
            [Status(26715)] [Description("mute entry not found")] MUTE_ENTRY_NOT_FOUND,
            [Status(51086)] [Description("no name or UUID provided")] NO_NAME_OR_UUID_PROVIDED,
            [Status(16450)] [Description("could not retrieve mute list")] COULD_NOT_RETRIEVE_MUTE_LIST,
            [Status(39647)] [Description("mute entry already exists")] MUTE_ENTRY_ALREADY_EXISTS,
            [Status(22961)] [Description("could not add mute entry")] COULD_NOT_ADD_MUTE_ENTRY,
            [Status(39787)] [Description("timeout reaching destination")] TIMEOUT_REACHING_DESTINATION,
            [Status(10776)] [Description("group schedules exceeded")] GROUP_SCHEDULES_EXCEEDED,
            [Status(36896)] [Description("no index provided")] NO_INDEX_PROVIDED,
            [Status(56094)] [Description("no schedule found")] NO_SCHEDULE_FOUND,
            [Status(41612)] [Description("unknown date time stamp")] UNKNOWN_DATE_TIME_STAMP,
            [Status(07457)] [Description("no permissions for item")] NO_PERMISSIONS_FOR_ITEM,
            [Status(10374)] [Description("timeout retrieving estate covenant")] TIMEOUT_RETRIEVING_ESTATE_COVENANT,
            [Status(56901)] [Description("no terraform action specified")] NO_TERRAFORM_ACTION_SPECIFIED,
            [Status(41211)] [Description("no terraform brush specified")] NO_TERRAFORM_BRUSH_SPECIFIED,
            [Status(63486)] [Description("invalid height")] INVALID_HEIGHT,
            [Status(20547)] [Description("invalid width")] INVALID_WIDTH,
            [Status(28891)] [Description("invalid terraform action")] INVALID_TERRAFORM_ACTION,
            [Status(41190)] [Description("invalid terraform brush")] INVALID_TERRAFORM_BRUSH,
            [Status(58619)] [Description("could not terraform")] COULD_NOT_TERRAFORM
        }

        /// <summary>
        ///     Semaphores that sense the state of the connection. When any of these semaphores fail,
        ///     Corrade does not consider itself connected anymore and terminates.
        /// </summary>
        private static readonly Dictionary<char, ManualResetEvent> ConnectionSemaphores = new Dictionary
            <char, ManualResetEvent>
        {
            {'l', new ManualResetEvent(false)},
            {'s', new ManualResetEvent(false)},
            {'u', new ManualResetEvent(false)}
        };

        public static string InstalledServiceName;
        private static CorradeConfiguration corradeConfiguration = new CorradeConfiguration();
        private static Thread programThread;
        private static Thread HTTPListenerThread;
        private static Thread TCPNotificationsThread;
        private static TcpListener TCPListener;
        private static HttpListener HTTPListener;
        private static Thread EffectsExpirationThread;
        private static Thread GroupSchedulesThread;
        private static readonly Random CorradeRandom = new Random();
        private static readonly EventLog CorradeEventLog = new EventLog();
        private static readonly GridClient Client = new GridClient();

        private static readonly Bot AIMLBot = new Bot
        {
            TrustAIML = false
        };

        private static readonly User AIMLBotUser = new User(CORRADE_CONSTANTS.CORRADE, AIMLBot);
        private static readonly FileSystemWatcher AIMLBotConfigurationWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher ConfigurationWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher NotificationsWatcher = new FileSystemWatcher();
        private static readonly FileSystemWatcher SchedulesWatcher = new FileSystemWatcher();
        private static readonly object AIMLBotLock = new object();
        private static readonly object ClientInstanceGroupsLock = new object();
        private static readonly object ClientInstanceInventoryLock = new object();
        private static readonly object ClientInstanceAvatarsLock = new object();
        private static readonly object ClientInstanceSelfLock = new object();
        private static readonly object ClientInstanceConfigurationLock = new object();
        private static readonly object ClientInstanceParcelsLock = new object();
        private static readonly object ClientInstanceNetworkLock = new object();
        private static readonly object ClientInstanceGridLock = new object();
        private static readonly object ClientInstanceDirectoryLock = new object();
        private static readonly object ClientInstanceEstateLock = new object();
        private static readonly object ClientInstanceObjectsLock = new object();
        private static readonly object ClientInstanceFriendsLock = new object();
        private static readonly object ClientInstanceAssetsLock = new object();
        private static readonly object ClientInstanceAppearanceLock = new object();
        private static readonly object ConfigurationFileLock = new object();
        private static readonly object ClientLogFileLock = new object();
        private static readonly object GroupLogFileLock = new object();
        private static readonly object LocalLogFileLock = new object();
        private static readonly object RegionLogFileLock = new object();
        private static readonly object InstantMessageLogFileLock = new object();
        private static readonly object DatabaseFileLock = new object();

        private static readonly wasTimedThrottle TimedTeleportThrottle =
            new wasTimedThrottle(LINDEN_CONSTANTS.TELEPORTS.THROTTLE.MAX_TELEPORTS,
                LINDEN_CONSTANTS.TELEPORTS.THROTTLE.GRACE_SECONDS);

        private static readonly Dictionary<string, object> DatabaseLocks = new Dictionary<string, object>();
        private static readonly object GroupNotificationsLock = new object();
        public static HashSet<Notification> GroupNotifications = new HashSet<Notification>();

        private static readonly SerializableDictionary<InventoryObjectOfferedEventArgs, ManualResetEvent>
            InventoryOffers =
                new SerializableDictionary<InventoryObjectOfferedEventArgs, ManualResetEvent>();

        private static readonly object InventoryOffersLock = new object();

        private static readonly BlockingQueue<CallbackQueueElement> CallbackQueue =
            new BlockingQueue<CallbackQueueElement>();

        private static readonly BlockingQueue<NotificationQueueElement> NotificationQueue =
            new BlockingQueue<NotificationQueueElement>();

        private static readonly BlockingQueue<NotificationTCPQueueElement> NotificationTCPQueue =
            new BlockingQueue<NotificationTCPQueueElement>();

        private static readonly HashSet<GroupInvite> GroupInvites = new HashSet<GroupInvite>();
        private static readonly object GroupInviteLock = new object();
        private static readonly HashSet<TeleportLure> TeleportLures = new HashSet<TeleportLure>();
        private static readonly object TeleportLureLock = new object();
        // permission requests can be identical
        private static readonly List<ScriptPermissionRequest> ScriptPermissionRequests =
            new List<ScriptPermissionRequest>();

        private static readonly object ScriptPermissionRequestLock = new object();
        // script dialogs can be identical
        private static readonly List<ScriptDialog> ScriptDialogs = new List<ScriptDialog>();
        private static readonly object ScriptDialogLock = new object();

        private static readonly SerializableDictionary<UUID, HashSet<UUID>> GroupMembers =
            new SerializableDictionary<UUID, HashSet<UUID>>();

        private static readonly object GroupMembersLock = new object();
        private static readonly Hashtable GroupWorkers = new Hashtable();
        private static readonly object GroupWorkersLock = new object();
        private static readonly Hashtable GroupDirectoryTrackers = new Hashtable();
        private static readonly object GroupDirectoryTrackersLock = new object();
        private static readonly HashSet<LookAtEffect> LookAtEffects = new HashSet<LookAtEffect>();
        private static readonly HashSet<PointAtEffect> PointAtEffects = new HashSet<PointAtEffect>();
        private static readonly HashSet<SphereEffect> SphereEffects = new HashSet<SphereEffect>();
        private static readonly object SphereEffectsLock = new object();
        private static readonly HashSet<BeamEffect> BeamEffects = new HashSet<BeamEffect>();
        private static readonly Dictionary<UUID, Primitive> RadarObjects = new Dictionary<UUID, Primitive>();
        private static readonly object RadarObjectsLock = new object();
        private static readonly object BeamEffectsLock = new object();
        private static readonly object InputFiltersLock = new object();
        private static readonly object OutputFiltersLock = new object();
        private static readonly HashSet<GroupSchedule> GroupSchedules = new HashSet<GroupSchedule>();
        private static readonly object GroupSchedulesLock = new object();
        private static volatile bool AIMLBotBrainCompiled;

        /// <summary>
        ///     The various types of threads created by Corrade.
        /// </summary>
        private static readonly Dictionary<CorradeThreadType, CorradeThread> CorradeThreadPool =
            new Dictionary<CorradeThreadType, CorradeThread>
            {
                {CorradeThreadType.COMMAND, new CorradeThread(CorradeThreadType.COMMAND)},
                {CorradeThreadType.RLV, new CorradeThread(CorradeThreadType.RLV)},
                {CorradeThreadType.NOTIFICATION, new CorradeThread(CorradeThreadType.NOTIFICATION)},
                {CorradeThreadType.INSTANT_MESSAGE, new CorradeThread(CorradeThreadType.INSTANT_MESSAGE)},
                {CorradeThreadType.LOG, new CorradeThread(CorradeThreadType.LOG)},
                {CorradeThreadType.POST, new CorradeThread(CorradeThreadType.POST)}
            };

        /// <summary>
        ///     Group membership sweep thread.
        /// </summary>
        private static Thread GroupMembershipSweepThread;

        /// <summary>
        ///     Group membership sweep thread starter.
        /// </summary>
        private static readonly System.Action StartGroupMembershipSweepThread = () =>
        {
            if (GroupMembershipSweepThread != null &&
                (GroupMembershipSweepThread.ThreadState.Equals(ThreadState.Running) ||
                 GroupMembershipSweepThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                return;
            runGroupMembershipSweepThread = true;
            GroupMembershipSweepThread = new Thread(GroupMembershipSweep)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            GroupMembershipSweepThread.Start();
        };

        /// <summary>
        ///     Group membership sweep thread stopper.
        /// </summary>
        private static readonly System.Action StopGroupMembershipSweepThread = () =>
        {
            // Stop the notification thread.
            runGroupMembershipSweepThread = false;
            if (GroupMembershipSweepThread == null ||
                (!GroupMembershipSweepThread.ThreadState.Equals(ThreadState.Running) &&
                 !GroupMembershipSweepThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                return;
            if (GroupMembershipSweepThread.Join(1000)) return;
            try
            {
                GroupMembershipSweepThread.Abort();
                GroupMembershipSweepThread.Join();
            }
            catch (ThreadStateException)
            {
            }
        };

        /// <summary>
        ///     Schedules a load of the configuration file.
        /// </summary>
        private static readonly Timer ConfigurationChangedTimer =
            new Timer(ConfigurationChanged =>
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.CONFIGURATION_FILE_MODIFIED));
                lock (ConfigurationFileLock)
                {
                    corradeConfiguration.Load(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                }
            });

        /// <summary>
        ///     Schedules a load of the notifications file.
        /// </summary>
        private static readonly Timer NotificationsChangedTimer =
            new Timer(NotificationsChanged =>
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.NOTIFICATIONS_FILE_MODIFIED));
                lock (GroupNotificationsLock)
                {
                    LoadNotificationState.Invoke();
                }
            });

        /// <summary>
        ///     Schedules a load of the AIML configuration file.
        /// </summary>
        private static readonly Timer AIMLConfigurationChangedTimer =
            new Timer(AIMLConfigurationChanged =>
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.AIML_CONFIGURATION_MODIFIED));
                new Thread(
                    () =>
                    {
                        lock (AIMLBotLock)
                        {
                            LoadChatBotFiles.Invoke();
                        }
                    })
                {IsBackground = true}.Start();
            });

        /// <summary>
        ///     Schedules a load of the group schedules file.
        /// </summary>
        private static readonly Timer GroupSchedulesChangedTimer =
            new Timer(GroupSchedulesChanged =>
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.GROUP_SCHEDULES_FILE_MODIFIED));
                lock (GroupSchedulesLock)
                {
                    LoadGroupSchedulesState.Invoke();
                }
            });

        /// <summary>
        ///     Global rebake timer.
        /// </summary>
        private static readonly Timer RebakeTimer = new Timer(Rebake =>
        {
            lock (ClientInstanceAppearanceLock)
            {
                ManualResetEvent AppearanceSetEvent = new ManualResetEvent(false);
                EventHandler<AppearanceSetEventArgs> HandleAppearanceSet = (sender, args) => AppearanceSetEvent.Set();
                Client.Appearance.AppearanceSet += HandleAppearanceSet;
                Client.Appearance.RequestSetAppearance(true);
                AppearanceSetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false);
                Client.Appearance.AppearanceSet -= HandleAppearanceSet;
            }
        });

        /// <summary>
        ///     Current land group activation timer.
        /// </summary>
        private static readonly Timer ActivateCurrentLandGroupTimer =
            new Timer(ActivateCurrentLandGroup =>
            {
                Parcel parcel = null;
                if (!GetParcelAtPosition(Client.Network.CurrentSim, Client.Self.SimPosition, ref parcel)) return;
                Group landGroup =
                    corradeConfiguration.Groups.AsParallel().FirstOrDefault(o => o.UUID.Equals(parcel.GroupID));
                if (landGroup.UUID.Equals(UUID.Zero)) return;
                Client.Groups.ActivateGroup(landGroup.UUID);
            });

        public static EventHandler ConsoleEventHandler;

        /// <summary>
        ///     Corrade's input filter function.
        /// </summary>
        private static readonly Func<string, string> wasInput = o =>
        {
            if (string.IsNullOrEmpty(o)) return string.Empty;

            List<Filter> safeFilters;
            lock (InputFiltersLock)
            {
                safeFilters = corradeConfiguration.InputFilters;
            }
            foreach (Filter filter in safeFilters)
            {
                switch (filter)
                {
                    case Filter.RFC1738:
                        o = wasURLUnescapeDataString(o);
                        break;
                    case Filter.RFC3986:
                        o = wasURIUnescapeDataString(o);
                        break;
                    case Filter.ENIGMA:
                        o = wasEnigma(o, corradeConfiguration.ENIGMA.rotors.ToArray(),
                            corradeConfiguration.ENIGMA.plugs.ToArray(),
                            corradeConfiguration.ENIGMA.reflector);
                        break;
                    case Filter.VIGENERE:
                        o = wasDecryptVIGENERE(o, corradeConfiguration.VIGENERESecret);
                        break;
                    case Filter.ATBASH:
                        o = wasATBASH(o);
                        break;
                    case Filter.AES:
                        o = wasAESDecrypt(o, corradeConfiguration.AESKey, corradeConfiguration.AESIV);
                        break;
                    case Filter.BASE64:
                        o = Encoding.UTF8.GetString(Convert.FromBase64String(o));
                        break;
                }
            }
            return o;
        };

        /// <summary>
        ///     Corrade's output filter function.
        /// </summary>
        private static readonly Func<string, string> wasOutput = o =>
        {
            if (string.IsNullOrEmpty(o)) return string.Empty;

            List<Filter> safeFilters;
            lock (OutputFiltersLock)
            {
                safeFilters = corradeConfiguration.OutputFilters;
            }
            foreach (Filter filter in safeFilters)
            {
                switch (filter)
                {
                    case Filter.RFC1738:
                        o = wasURLEscapeDataString(o);
                        break;
                    case Filter.RFC3986:
                        o = wasURIEscapeDataString(o);
                        break;
                    case Filter.ENIGMA:
                        o = wasEnigma(o, corradeConfiguration.ENIGMA.rotors.ToArray(),
                            corradeConfiguration.ENIGMA.plugs.ToArray(),
                            corradeConfiguration.ENIGMA.reflector);
                        break;
                    case Filter.VIGENERE:
                        o = wasEncryptVIGENERE(o, corradeConfiguration.VIGENERESecret);
                        break;
                    case Filter.ATBASH:
                        o = wasATBASH(o);
                        break;
                    case Filter.AES:
                        o = wasAESEncrypt(o, corradeConfiguration.AESKey, corradeConfiguration.AESIV);
                        break;
                    case Filter.BASE64:
                        o = Convert.ToBase64String(Encoding.UTF8.GetBytes(o));
                        break;
                }
            }
            return o;
        };

        /// <summary>
        ///     Determines whether a string is a Corrade command.
        /// </summary>
        /// <returns>true if the string is a Corrade command</returns>
        private static readonly Func<string, bool> IsCorradeCommand = o =>
        {
            Dictionary<string, string> data = wasKeyValueDecode(o);
            return data.Any() && data.ContainsKey(wasGetDescriptionFromEnumValue(ScriptKeys.COMMAND)) &&
                   data.ContainsKey(wasGetDescriptionFromEnumValue(ScriptKeys.GROUP)) &&
                   data.ContainsKey(wasGetDescriptionFromEnumValue(ScriptKeys.PASSWORD));
        };

        /// <summary>
        ///     Gets the first name and last name from an avatar name.
        /// </summary>
        /// <returns>the firstname and the lastname or Resident</returns>
        private static readonly Func<string, IEnumerable<string>> GetAvatarNames =
            o => !string.IsNullOrEmpty(o)
                ? CORRADE_CONSTANTS.AvatarFullNameRegex.Matches(o)
                    .Cast<Match>()
                    .ToDictionary(p => new[]
                    {
                        p.Groups["first"].Value,
                        p.Groups["last"].Value
                    })
                    .SelectMany(
                        p =>
                            new[]
                            {
                                p.Key[0].Trim(),
                                !string.IsNullOrEmpty(p.Key[1])
                                    ? p.Key[1].Trim()
                                    : LINDEN_CONSTANTS.AVATARS.LASTNAME_PLACEHOLDER
                            })
                : null;

        /// <summary>
        ///     Updates the inventory starting from a folder recursively.
        /// </summary>
        private static readonly Action<InventoryFolder> UpdateInventoryRecursive = o =>
        {
            Thread updateInventoryRecursiveThread = new Thread(() =>
            {
                try
                {
                    // Create the queue of folders.
                    // Enqueue the first folder (as the root).
                    Dictionary<UUID, ManualResetEvent> inventoryFolders = new Dictionary<UUID, ManualResetEvent>
                    {
                        {o.UUID, new ManualResetEvent(false)}
                    };
                    // Create a stopwatch for the root folder.
                    Dictionary<UUID, Stopwatch> inventoryStopwatch = new Dictionary<UUID, Stopwatch>
                    {
                        {o.UUID, new Stopwatch()}
                    };

                    HashSet<long> times = new HashSet<long>(new[] {(long) Client.Settings.CAPS_TIMEOUT});

                    object LockObject = new object();

                    EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) =>
                    {
                        // Enqueue all the new folders.
                        Parallel.ForEach(Client.Inventory.Store.GetContents(q.FolderID), r =>
                        {
                            if (r is InventoryFolder)
                            {
                                UUID inventoryFolderUUID = (r as InventoryFolder).UUID;
                                if (Client.Inventory.Store.GetNodeFor(inventoryFolderUUID).NeedsUpdate)
                                {
                                    lock (LockObject)
                                    {
                                        if (!inventoryFolders.ContainsKey(inventoryFolderUUID))
                                        {
                                            inventoryFolders.Add(inventoryFolderUUID, new ManualResetEvent(false));
                                        }
                                    }
                                    lock (LockObject)
                                    {
                                        if (!inventoryStopwatch.ContainsKey(inventoryFolderUUID))
                                        {
                                            inventoryStopwatch.Add(inventoryFolderUUID, new Stopwatch());
                                        }
                                    }
                                }
                            }
                            lock (LockObject)
                            {
                                inventoryStopwatch[q.FolderID].Stop();
                                times.Add(inventoryStopwatch[q.FolderID].ElapsedMilliseconds);
                                inventoryFolders[q.FolderID].Set();
                            }
                        });
                    };

                    do
                    {
                        // Don't choke the chicken.
                        Thread.Yield();
                        Dictionary<UUID, ManualResetEvent> closureFolders;
                        lock (LockObject)
                        {
                            closureFolders =
                                new Dictionary<UUID, ManualResetEvent>(
                                    inventoryFolders.Where(p => !p.Key.Equals(UUID.Zero))
                                        .ToDictionary(p => p.Key, q => q.Value));
                        }
                        lock (ClientInstanceInventoryLock)
                        {
                            Parallel.ForEach(closureFolders, p =>
                            {
                                Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                                lock (LockObject)
                                {
                                    inventoryStopwatch[p.Key].Start();
                                }
                                Client.Inventory.RequestFolderContents(p.Key, Client.Self.AgentID, true, true,
                                    InventorySortOrder.ByDate);
                                ManualResetEvent folderEvent;
                                int averageTime;
                                lock (LockObject)
                                {
                                    folderEvent = closureFolders[p.Key];
                                    averageTime = (int) times.Average();
                                }
                                folderEvent.WaitOne(averageTime, false);
                                Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                            });
                        }
                        Parallel.ForEach(closureFolders, p =>
                        {
                            lock (LockObject)
                            {
                                if (inventoryFolders.ContainsKey(p.Key))
                                {
                                    inventoryFolders.Remove(p.Key);
                                }
                            }
                            lock (LockObject)
                            {
                                if (inventoryStopwatch.ContainsKey(p.Key))
                                {
                                    inventoryStopwatch.Remove(p.Key);
                                }
                            }
                        });
                    } while (inventoryFolders.Any());
                }
                catch (Exception)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_UPDATING_INVENTORY));
                }
            })
            {IsBackground = true};

            updateInventoryRecursiveThread.Start();
            updateInventoryRecursiveThread.Join(Timeout.Infinite);
        };

        /// <summary>
        ///     Loads the OpenMetaverse inventory cache.
        /// </summary>
        private static readonly System.Action LoadInventoryCache = () =>
        {
            int itemsLoaded;
            lock (ClientInstanceInventoryLock)
            {
                itemsLoaded = Client.Inventory.Store.RestoreFromDisk(Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                    CORRADE_CONSTANTS.INVENTORY_CACHE_FILE));
            }

            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.INVENTORY_CACHE_ITEMS_LOADED),
                itemsLoaded < 0 ? "0" : itemsLoaded.ToString(Utils.EnUsCulture));
        };

        /// <summary>
        ///     Saves the OpenMetaverse inventory cache.
        /// </summary>
        private static readonly System.Action SaveInventoryCache = () =>
        {
            string path = Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                CORRADE_CONSTANTS.INVENTORY_CACHE_FILE);
            int itemsSaved;
            lock (ClientInstanceInventoryLock)
            {
                itemsSaved = Client.Inventory.Store.Items.Count;
                Client.Inventory.Store.SaveToDisk(path);
            }

            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.INVENTORY_CACHE_ITEMS_SAVED),
                itemsSaved.ToString(Utils.EnUsCulture));
        };

        /// <summary>
        ///     Loads Corrade's caches.
        /// </summary>
        private static readonly System.Action LoadCorradeCache = () =>
        {
            Cache.AgentCache =
                Cache.Load(Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.AGENT_CACHE_FILE),
                    Cache.AgentCache);

            Cache.GroupCache =
                Cache.Load(Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.GROUP_CACHE_FILE),
                    Cache.GroupCache);
        };

        /// <summary>
        ///     Saves Corrade's caches.
        /// </summary>
        private static readonly System.Action SaveCorradeCache = () =>
        {
            Cache.Save(Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.AGENT_CACHE_FILE),
                Cache.AgentCache);

            Cache.Save(Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY, CORRADE_CONSTANTS.GROUP_CACHE_FILE),
                Cache.GroupCache);
        };

        /// <summary>
        ///     Saves Corrade group members.
        /// </summary>
        private static readonly System.Action SaveGroupMembersState = () =>
        {
            try
            {
                using (
                    StreamWriter writer =
                        new StreamWriter(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.GROUP_MEMBERS_STATE_FILE), false, Encoding.UTF8))
                {
                    XmlSerializer serializer =
                        new XmlSerializer(typeof (SerializableDictionary<UUID, HashSet<UUID>>));
                    lock (GroupMembersLock)
                    {
                        serializer.Serialize(writer, GroupMembers);
                    }
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_SAVE_GROUP_MEMBERS_STATE),
                    e.Message);
            }
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly System.Action LoadGroupMembersState = () =>
        {
            string groupMembersStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.GROUP_MEMBERS_STATE_FILE);
            if (File.Exists(groupMembersStateFile))
            {
                try
                {
                    using (StreamReader stream = new StreamReader(groupMembersStateFile, Encoding.UTF8))
                    {
                        XmlSerializer serializer =
                            new XmlSerializer(typeof (SerializableDictionary<UUID, HashSet<UUID>>));
                        Parallel.ForEach((SerializableDictionary<UUID, HashSet<UUID>>) serializer.Deserialize(stream),
                            o =>
                            {
                                if (!corradeConfiguration.Groups.AsParallel().Any(p => p.UUID.Equals(o.Key)) ||
                                    GroupMembers.Contains(o))
                                    return;
                                lock (GroupMembersLock)
                                {
                                    GroupMembers.Add(o.Key, o.Value);
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_LOAD_GROUP_MEMBERS_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade notifications.
        /// </summary>
        private static readonly System.Action SaveGroupSchedulesState = () =>
        {
            SchedulesWatcher.EnableRaisingEvents = false;
            try
            {
                using (
                    StreamWriter writer =
                        new StreamWriter(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.GROUP_SCHEDULES_STATE_FILE), false, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (HashSet<GroupSchedule>));
                    lock (GroupSchedulesLock)
                    {
                        serializer.Serialize(writer, GroupSchedules);
                    }
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_SAVE_CORRADE_GROUP_SCHEDULES_STATE),
                    e.Message);
            }
            SchedulesWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly System.Action LoadGroupSchedulesState = () =>
        {
            string groupSchedulesStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.GROUP_SCHEDULES_STATE_FILE);
            if (File.Exists(groupSchedulesStateFile))
            {
                try
                {
                    using (StreamReader stream = new StreamReader(groupSchedulesStateFile, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (HashSet<GroupSchedule>));
                        Parallel.ForEach((HashSet<GroupSchedule>) serializer.Deserialize(stream),
                            o =>
                            {
                                if (
                                    !corradeConfiguration.Groups.AsParallel()
                                        .Any(
                                            p =>
                                                p.UUID.Equals(o.Group.UUID) &&
                                                !(p.PermissionMask & (uint) Permissions.Schedule).Equals(0) &&
                                                !p.Schedules.Equals(0)))
                                    return;
                                lock (GroupSchedulesLock)
                                {
                                    GroupSchedules.Add(o);
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_LOAD_CORRADE_GROUP_SCHEDULES_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade notifications.
        /// </summary>
        private static readonly System.Action SaveNotificationState = () =>
        {
            NotificationsWatcher.EnableRaisingEvents = false;
            try
            {
                using (
                    StreamWriter writer =
                        new StreamWriter(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.NOTIFICATIONS_STATE_FILE), false, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (HashSet<Notification>));
                    lock (GroupNotificationsLock)
                    {
                        serializer.Serialize(writer, GroupNotifications);
                    }
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_SAVE_CORRADE_NOTIFICATIONS_STATE),
                    e.Message);
            }
            NotificationsWatcher.EnableRaisingEvents = true;
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly System.Action LoadNotificationState = () =>
        {
            string groupNotificationsStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.NOTIFICATIONS_STATE_FILE);
            if (File.Exists(groupNotificationsStateFile))
            {
                try
                {
                    using (StreamReader stream = new StreamReader(groupNotificationsStateFile, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (HashSet<Notification>));
                        Parallel.ForEach((HashSet<Notification>) serializer.Deserialize(stream),
                            o =>
                            {
                                if (!corradeConfiguration.Groups.AsParallel().Any(p => p.Name.Equals(o.GroupName)) ||
                                    GroupNotifications.Contains(o))
                                    return;
                                lock (GroupNotificationsLock)
                                {
                                    GroupNotifications.Add(o);
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_LOAD_CORRADE_NOTIFICATIONS_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Saves Corrade notifications.
        /// </summary>
        private static readonly System.Action SaveMovementState = () =>
        {
            try
            {
                using (
                    StreamWriter writer =
                        new StreamWriter(Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                            CORRADE_CONSTANTS.MOVEMENT_STATE_FILE), false, Encoding.UTF8))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (AgentMovement));
                    AgentMovement movement;
                    lock (ClientInstanceSelfLock)
                    {
                        movement = new AgentMovement
                        {
                            AlwaysRun = Client.Self.Movement.AlwaysRun,
                            AutoResetControls = Client.Self.Movement.AutoResetControls,
                            Away = Client.Self.Movement.Away,
                            BodyRotation = Client.Self.Movement.BodyRotation,
                            Flags = Client.Self.Movement.Flags,
                            Fly = Client.Self.Movement.Fly,
                            HeadRotation = Client.Self.Movement.HeadRotation,
                            Mouselook = Client.Self.Movement.Mouselook,
                            SitOnGround = Client.Self.Movement.SitOnGround,
                            StandUp = Client.Self.Movement.StandUp,
                            State = Client.Self.Movement.State
                        };
                    }
                    serializer.Serialize(writer, movement);
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_SAVE_CORRADE_MOVEMENT_STATE),
                    e.Message);
            }
        };

        /// <summary>
        ///     Loads Corrade notifications.
        /// </summary>
        private static readonly System.Action LoadMovementState = () =>
        {
            string movementStateFile = Path.Combine(CORRADE_CONSTANTS.STATE_DIRECTORY,
                CORRADE_CONSTANTS.MOVEMENT_STATE_FILE);
            if (File.Exists(movementStateFile))
            {
                try
                {
                    using (StreamReader stream = new StreamReader(movementStateFile, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (AgentMovement));
                        AgentMovement movement = (AgentMovement) serializer.Deserialize(stream);
                        Client.Self.Movement.AlwaysRun = movement.AlwaysRun;
                        Client.Self.Movement.AutoResetControls = movement.AutoResetControls;
                        Client.Self.Movement.Away = movement.Away;
                        Client.Self.Movement.BodyRotation = movement.BodyRotation;
                        Client.Self.Movement.Flags = movement.Flags;
                        Client.Self.Movement.Fly = movement.Fly;
                        Client.Self.Movement.HeadRotation = movement.HeadRotation;
                        Client.Self.Movement.Mouselook = movement.Mouselook;
                        Client.Self.Movement.SitOnGround = movement.SitOnGround;
                        Client.Self.Movement.StandUp = movement.StandUp;
                        Client.Self.Movement.State = movement.State;
                    }
                }
                catch (Exception ex)
                {
                    Feedback(
                        wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_LOAD_CORRADE_MOVEMENT_STATE),
                        ex.Message);
                }
            }
        };

        /// <summary>
        ///     Loads the chatbot configuration and AIML files.
        /// </summary>
        private static readonly System.Action LoadChatBotFiles = () =>
        {
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.READING_AIML_BOT_CONFIGURATION));
            try
            {
                AIMLBot.isAcceptingUserInput = false;
                AIMLBot.loadSettings(wasPathCombine(
                    Directory.GetCurrentDirectory(), AIML_BOT_CONSTANTS.DIRECTORY,
                    AIML_BOT_CONSTANTS.CONFIG.DIRECTORY, AIML_BOT_CONSTANTS.CONFIG.SETTINGS_FILE));
                string AIMLBotBrain =
                    wasPathCombine(
                        Directory.GetCurrentDirectory(), AIML_BOT_CONSTANTS.DIRECTORY,
                        AIML_BOT_CONSTANTS.BRAIN.DIRECTORY, AIML_BOT_CONSTANTS.BRAIN_FILE);
                switch (File.Exists(AIMLBotBrain))
                {
                    case true:
                        AIMLBot.loadFromBinaryFile(AIMLBotBrain);
                        break;
                    default:
                        AIMLBot.loadAIMLFromFiles();
                        AIMLBot.saveToBinaryFile(AIMLBotBrain);
                        break;
                }
                string AIMLBotUserBrain =
                    wasPathCombine(
                        Directory.GetCurrentDirectory(), AIML_BOT_CONSTANTS.DIRECTORY,
                        AIML_BOT_CONSTANTS.BRAIN.DIRECTORY, AIML_BOT_CONSTANTS.BRAIN_SESSION_FILE);
                if (File.Exists(AIMLBotUserBrain))
                {
                    AIMLBotUser.Predicates.loadSettings(AIMLBotUserBrain);
                }
                AIMLBot.isAcceptingUserInput = true;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_LOADING_AIML_BOT_FILES), ex.Message);
                return;
            }
            finally
            {
                AIMLBotBrainCompiled = true;
            }
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.READ_AIML_BOT_CONFIGURATION));
        };

        /// <summary>
        ///     Saves the chatbot configuration and AIML files.
        /// </summary>
        private static readonly System.Action SaveChatBotFiles = () =>
        {
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.WRITING_AIML_BOT_CONFIGURATION));
            try
            {
                AIMLBot.isAcceptingUserInput = false;
                AIMLBotUser.Predicates.DictionaryAsXML.Save(wasPathCombine(
                    Directory.GetCurrentDirectory(), AIML_BOT_CONSTANTS.DIRECTORY,
                    AIML_BOT_CONSTANTS.BRAIN.DIRECTORY, AIML_BOT_CONSTANTS.BRAIN_SESSION_FILE));
                AIMLBot.isAcceptingUserInput = true;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_SAVING_AIML_BOT_FILES), ex.Message);
                return;
            }
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.WROTE_AIML_BOT_CONFIGURATION));
        };

        private static volatile bool runHTTPServer;
        private static volatile bool runTCPNotificationsServer;
        private static volatile bool runCallbackThread = true;
        private static volatile bool runNotificationThread = true;
        private static volatile bool runGroupSchedulesThread;
        private static volatile bool runGroupMembershipSweepThread;
        private static volatile bool runEffectsExpirationThread;

        public Corrade()
        {
            if (Environment.UserInteractive) return;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    try
                    {
                        InstalledServiceName = (string)
                            new ManagementObjectSearcher("SELECT * FROM Win32_Service where ProcessId = " +
                                                         Process.GetCurrentProcess().Id).Get()
                                .Cast<ManagementBaseObject>()
                                .First()["Name"];
                    }
                    catch (Exception)
                    {
                        InstalledServiceName = CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME;
                    }
                    break;
                default:
                    InstalledServiceName = CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME;
                    break;
            }
            CorradeEventLog.Source = InstalledServiceName;
            CorradeEventLog.Log = CORRADE_CONSTANTS.LOG_FACILITY;
            CorradeEventLog.BeginInit();
            if (!EventLog.SourceExists(CorradeEventLog.Source))
            {
                EventLog.CreateEventSource(CorradeEventLog.Source, CorradeEventLog.Log);
            }
            CorradeEventLog.EndInit();
        }

        /// <summary>
        ///     Sweep for group members.
        /// </summary>
        private static void GroupMembershipSweep()
        {
            Queue<UUID> groupUUIDs = new Queue<UUID>();
            Queue<int> memberCount = new Queue<int>();
            // The total list of members.
            HashSet<UUID> groupMembers = new HashSet<UUID>();
            // New members that have joined the group.
            HashSet<UUID> joinedMembers = new HashSet<UUID>();
            // Members that have parted the group.
            HashSet<UUID> partedMembers = new HashSet<UUID>();

            ManualResetEvent GroupMembersReplyEvent = new ManualResetEvent(false);
            EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
            {
                lock (GroupMembersLock)
                {
                    if (GroupMembers.ContainsKey(args.GroupID))
                    {
                        object LockObject = new object();
                        Parallel.ForEach(
                            args.Members.Values,
                            o =>
                            {
                                if (GroupMembers[args.GroupID].Contains(o.ID)) return;
                                lock (LockObject)
                                {
                                    joinedMembers.Add(o.ID);
                                }
                            });
                        Parallel.ForEach(
                            GroupMembers[args.GroupID],
                            o =>
                            {
                                if (args.Members.Values.Any(p => p.ID.Equals(o))) return;
                                lock (LockObject)
                                {
                                    partedMembers.Add(o);
                                }
                            });
                    }
                }
                groupMembers.UnionWith(args.Members.Values.Select(o => o.ID));
                GroupMembersReplyEvent.Set();
            };

            while (runGroupMembershipSweepThread)
            {
                Thread.Sleep((int) corradeConfiguration.MembershipSweepInterval);
                if (!Client.Network.Connected) continue;

                IEnumerable<UUID> groups = Enumerable.Empty<UUID>();
                if (!GetCurrentGroups(corradeConfiguration.ServicesTimeout, ref groups))
                    continue;

                // Enqueue configured groups that are currently joined groups.
                groupUUIDs.Clear();
                object LockObject = new object();
                HashSet<UUID> currentGroups = new HashSet<UUID>(groups);
                Parallel.ForEach(
                    corradeConfiguration.Groups.AsParallel().Select(o => new {group = o, groupUUID = o.UUID})
                        .Where(p => currentGroups.Contains(p.groupUUID))
                        .Select(o => o.group), o =>
                        {
                            lock (LockObject)
                            {
                                groupUUIDs.Enqueue(o.UUID);
                            }
                        });


                // Bail if no configured groups are also joined.
                if (!groupUUIDs.Any()) continue;

                // Get the last member count.
                memberCount.Clear();
                lock (GroupMembersLock)
                {
                    Parallel.ForEach(GroupMembers.AsParallel().SelectMany(
                        members => groupUUIDs,
                        (members, groupUUID) => new {members, groupUUID})
                        .Where(o => o.groupUUID.Equals(o.members.Key))
                        .Select(p => p.members), o =>
                        {
                            lock (LockObject)
                            {
                                memberCount.Enqueue(o.Value.Count);
                            }
                        });
                }

                do
                {
                    // Pause a second between group sweeps.
                    Thread.Yield();
                    // Dequeue the first group.
                    UUID groupUUID = groupUUIDs.Dequeue();
                    // Clear the total list of members.
                    groupMembers.Clear();
                    // Clear the members that have joined the group.
                    joinedMembers.Clear();
                    // Clear the members that have left the group.
                    partedMembers.Clear();
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                        GroupMembersReplyEvent.Reset();
                        Client.Groups.RequestGroupMembers(groupUUID);
                        if (!GroupMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                            continue;
                        }
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    }

                    if (!GroupMembers.ContainsKey(groupUUID))
                    {
                        lock (GroupMembersLock)
                        {
                            GroupMembers.Add(groupUUID, new HashSet<UUID>(groupMembers));
                        }
                        continue;
                    }

                    if (memberCount.Any())
                    {
                        if (!memberCount.Dequeue().Equals(groupMembers.Count))
                        {
                            if (joinedMembers.Any())
                            {
                                Parallel.ForEach(
                                    joinedMembers,
                                    o =>
                                    {
                                        string agentName = string.Empty;
                                        string groupName = string.Empty;
                                        if (AgentUUIDToName(
                                            o,
                                            corradeConfiguration.ServicesTimeout,
                                            ref agentName) &&
                                            GroupUUIDToName(groupUUID, corradeConfiguration.ServicesTimeout,
                                                ref groupName))
                                        {
                                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                                () => SendNotification(
                                                    Notifications.GroupMembership,
                                                    new GroupMembershipEventArgs
                                                    {
                                                        AgentName = agentName,
                                                        AgentUUID = o,
                                                        Action = Action.JOINED,
                                                        GroupName = groupName,
                                                        GroupUUID = groupUUID
                                                    }),
                                                corradeConfiguration.MaximumNotificationThreads);
                                        }
                                    });

                                joinedMembers.Clear();
                            }
                            if (partedMembers.Any())
                            {
                                Parallel.ForEach(
                                    partedMembers,
                                    o =>
                                    {
                                        string agentName = string.Empty;
                                        string groupName = string.Empty;
                                        if (AgentUUIDToName(
                                            o,
                                            corradeConfiguration.ServicesTimeout,
                                            ref agentName) &&
                                            GroupUUIDToName(groupUUID, corradeConfiguration.ServicesTimeout,
                                                ref groupName))
                                        {
                                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                                () => SendNotification(
                                                    Notifications.GroupMembership,
                                                    new GroupMembershipEventArgs
                                                    {
                                                        AgentName = agentName,
                                                        AgentUUID = o,
                                                        Action = Action.PARTED,
                                                        GroupName = groupName,
                                                        GroupUUID = groupUUID
                                                    }),
                                                corradeConfiguration.MaximumNotificationThreads);
                                        }
                                    });
                                partedMembers.Clear();
                            }
                        }
                    }
                    lock (GroupMembersLock)
                    {
                        GroupMembers[groupUUID].Clear();
                        Parallel.ForEach(groupMembers, o =>
                        {
                            lock (LockObject)
                            {
                                GroupMembers[groupUUID].Add(o);
                            }
                        });
                    }
                    groupMembers.Clear();
                } while (groupUUIDs.Any() && runGroupMembershipSweepThread);
            }
        }

        private static bool ConsoleCtrlCheck(NativeMethods.CtrlType ctrlType)
        {
            // Set the user disconnect semaphore.
            ConnectionSemaphores['u'].Set();
            // Wait for threads to finish.
            Thread.Sleep((int) corradeConfiguration.ServicesTimeout);
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Serialize an RLV message to a string.
        /// </summary>
        /// <returns>in order: behaviours, options, parameters</returns>
        private static IEnumerable<string> wasRLVToString(string message)
        {
            if (string.IsNullOrEmpty(message)) yield break;

            // Split all commands.
            string[] unpack = message.Split(RLV_CONSTANTS.CSV_DELIMITER[0]);
            // Pop first command to process.
            string first = unpack.First();
            // Remove command.
            unpack = unpack.AsParallel().Where(o => !o.Equals(first)).ToArray();
            // Keep rest of message.
            message = string.Join(RLV_CONSTANTS.CSV_DELIMITER, unpack);

            Match match = RLV_CONSTANTS.RLVRegEx.Match(first);
            if (!match.Success) goto CONTINUE;

            yield return match.Groups["behaviour"].ToString().ToLowerInvariant();
            yield return match.Groups["option"].ToString().ToLowerInvariant();
            yield return match.Groups["param"].ToString().ToLowerInvariant();

            CONTINUE:
            foreach (string slice in wasRLVToString(message))
            {
                yield return slice;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Combine multiple paths.
        /// </summary>
        /// <param name="paths">an array of paths</param>
        /// <returns>a combined path</returns>
        private static string wasPathCombine(params string[] paths)
        {
            return paths.Any()
                ? paths.Length < 2
                    ? paths[0]
                    : Path.Combine(Path.Combine(paths[0], paths[1]), wasPathCombine(paths.Skip(2).ToArray()))
                : string.Empty;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Retrieves an attribute of type T from an enumeration.
        /// </summary>
        /// <returns>an attribute of type T</returns>
        private static T wasGetAttributeFromEnumValue<T>(Enum value)
        {
            return (T) value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof (T), false)
                .SingleOrDefault();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns all the attributes of type T of an enumeration.
        /// </summary>
        /// <typeparam name="T">the attribute to retrieve</typeparam>
        /// <returns>a list of attributes</returns>
        private static IEnumerable<T> wasGetEnumAttributes<T>(Enum e)
        {
            return e.GetType().GetFields(BindingFlags.Static | BindingFlags.Public)
                .AsParallel().Select(o => wasGetAttributeFromEnumValue<T>((Enum) o.GetValue(null)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns all the field descriptions of an enumeration.
        /// </summary>
        /// <returns>the field descriptions</returns>
        private static IEnumerable<string> wasGetEnumDescriptions<T>()
        {
            return typeof (T).GetFields(BindingFlags.Static | BindingFlags.Public)
                .AsParallel().Select(o => wasGetDescriptionFromEnumValue((Enum) o.GetValue(null)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the description from an enumeration value.
        /// </summary>
        /// <param name="value">an enumeration value</param>
        /// <returns>the description or the empty string</returns>
        private static string wasGetDescriptionFromEnumValue(Enum value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof (DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute != null ? attribute.Description : string.Empty;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get enumeration value from its description.
        /// </summary>
        /// <typeparam name="T">the enumeration type</typeparam>
        /// <param name="description">the description of a member</param>
        /// <returns>the value or the default of T if case no description found</returns>
        private static T wasGetEnumValueFromDescription<T>(string description)
        {
            var field = typeof (T).GetFields()
                .AsParallel().SelectMany(f => f.GetCustomAttributes(
                    typeof (DescriptionAttribute), false), (
                        f, a) => new {Field = f, Att = a}).SingleOrDefault(a => ((DescriptionAttribute) a.Att)
                            .Description.Equals(description));
            return field != null ? (T) field.Field.GetRawConstantValue() : default(T);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the description of structure member.
        /// </summary>
        /// <typeparam name="T">the type of the structure to search</typeparam>
        /// <param name="structure">the structure to search</param>
        /// <param name="item">the value of the item to search</param>
        /// <returns>the description or the empty string</returns>
        private static string wasGetStructureMemberDescription<T>(T structure, object item) where T : struct
        {
            var field = typeof (T).GetFields()
                .AsParallel().SelectMany(f => f.GetCustomAttributes(typeof (DescriptionAttribute), false),
                    (f, a) => new {Field = f, Att = a}).SingleOrDefault(f => f.Field.GetValue(structure).Equals(item));
            return field != null ? ((DescriptionAttribute) field.Att).Description : string.Empty;
        }

        /// <summary>
        ///     Can an inventory item be worn?
        /// </summary>
        /// <param name="item">item to check</param>
        /// <returns>true if the inventory item can be worn</returns>
        private static bool CanBeWorn(InventoryBase item)
        {
            return item is InventoryWearable || item is InventoryAttachment || item is InventoryObject;
        }

        /// <summary>
        ///     Resolves inventory links and returns a real inventory item that
        ///     the link is pointing to
        /// </summary>
        /// <param name="item">a link or inventory item</param>
        /// <returns>the real inventory item</returns>
        private static InventoryItem ResolveItemLink(InventoryItem item)
        {
            return item.IsLink() && Client.Inventory.Store.Contains(item.AssetUUID) &&
                   Client.Inventory.Store[item.AssetUUID] is InventoryItem
                ? Client.Inventory.Store[item.AssetUUID] as InventoryItem
                : item;
        }

        /// <summary>
        ///     Get current outfit folder links.
        /// </summary>
        /// <returns>a list of inventory items that can be part of appearance (attachments, wearables)</returns>
        private static HashSet<InventoryItem> GetCurrentOutfitFolderLinks(InventoryFolder outfitFolder)
        {
            HashSet<InventoryItem> ret = new HashSet<InventoryItem>();
            if (outfitFolder == null) return ret;

            object LockObject = new object();
            Parallel.ForEach(
                Client.Inventory.Store.GetContents(outfitFolder)
                    .AsParallel()
                    .Where(o => CanBeWorn(o) && ((InventoryItem) o).AssetType.Equals(AssetType.Link)),
                o =>
                {
                    lock (LockObject)
                    {
                        ret.Add((InventoryItem) o);
                    }
                });

            return ret;
        }

        private static void Attach(InventoryItem item, AttachmentPoint point, bool replace)
        {
            lock (ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(item);
                if (realItem == null) return;
                Client.Appearance.Attach(realItem, point, replace);
                AddLink(realItem,
                    Client.Inventory.Store[Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)] as
                        InventoryFolder);
            }
        }

        private static void Detach(InventoryItem item)
        {
            lock (ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(item);
                if (realItem == null) return;
                RemoveLink(realItem,
                    Client.Inventory.Store[Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)] as
                        InventoryFolder);
                Client.Appearance.Detach(realItem);
            }
        }

        private static void Wear(InventoryItem item, bool replace)
        {
            lock (ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(item);
                if (realItem == null) return;
                Client.Appearance.AddToOutfit(realItem, replace);
                AddLink(realItem,
                    Client.Inventory.Store[Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)] as
                        InventoryFolder);
            }
        }

        private static void UnWear(InventoryItem item)
        {
            lock (ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(item);
                if (realItem == null) return;
                Client.Appearance.RemoveFromOutfit(realItem);
                InventoryItem link = GetCurrentOutfitFolderLinks(
                    Client.Inventory.Store[Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)] as
                        InventoryFolder)
                    .AsParallel()
                    .FirstOrDefault(o => o.AssetType.Equals(AssetType.Link) && o.Name.Equals(item.Name));
                if (link == null) return;
                RemoveLink(link,
                    Client.Inventory.Store[Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)] as
                        InventoryFolder);
            }
        }

        /// <summary>
        ///     Is the item a body part?
        /// </summary>
        /// <param name="item">the item to check</param>
        /// <returns>true if the item is a body part</returns>
        private static bool IsBodyPart(InventoryItem item)
        {
            InventoryItem realItem = ResolveItemLink(item);
            if (!(realItem is InventoryWearable)) return false;
            WearableType t = ((InventoryWearable) realItem).WearableType;
            return t.Equals(WearableType.Shape) ||
                   t.Equals(WearableType.Skin) ||
                   t.Equals(WearableType.Eyes) ||
                   t.Equals(WearableType.Hair);
        }

        /// <summary>
        ///     Creates a new current outfit folder link.
        /// </summary>
        /// <param name="item">item to be linked</param>
        /// <param name="outfitFolder">the outfit folder</param>
        private static void AddLink(InventoryItem item, InventoryFolder outfitFolder)
        {
            if (outfitFolder == null) return;

            /* If the link already exists, then don't add another one. */
            if (GetCurrentOutfitFolderLinks(outfitFolder).AsParallel().Any(o => o.AssetUUID.Equals(item.UUID))) return;

            string description = (item.InventoryType.Equals(InventoryType.Wearable) && !IsBodyPart(item))
                ? string.Format("@{0}{1:00}", (int) ((InventoryWearable) item).WearableType, 0)
                : string.Empty;
            lock (ClientInstanceInventoryLock)
            {
                Client.Inventory.CreateLink(Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder), item.UUID,
                    item.Name, description, AssetType.Link,
                    item.InventoryType, UUID.Random(), (success, newItem) =>
                    {
                        if (success)
                        {
                            Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                        }
                    });
            }
        }

        /// <summary>
        ///     Remove current outfit folder links for multiple specified inventory item.
        /// </summary>
        /// <param name="item">the item whose link should be removed</param>
        /// <param name="outfitFolder">the outfit folder</param>
        private static void RemoveLink(InventoryItem item, InventoryFolder outfitFolder)
        {
            if (outfitFolder == null) return;

            HashSet<UUID> removeItems = new HashSet<UUID>();
            object LockOject = new object();
            Parallel.ForEach(GetCurrentOutfitFolderLinks(outfitFolder).AsParallel().Where(o =>
                o.AssetUUID.Equals(item is InventoryWearable ? item.AssetUUID : item.UUID)), o =>
                {
                    lock (LockOject)
                    {
                        removeItems.Add(o.UUID);
                    }
                });

            lock (ClientInstanceInventoryLock)
            {
                Client.Inventory.Remove(removeItems.ToList(), null);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Swaps two integers passed by reference using XOR.
        /// </summary>
        /// <param name="q">first integer to swap</param>
        /// <param name="p">second integer to swap</param>
        private static void wasXORSwap(ref int q, ref int p)
        {
            q ^= p;
            p ^= q;
            q ^= p;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Enumerates the fields of an object along with the child objects,
        ///     provided that all child objects are part of a specified namespace.
        /// </summary>
        /// <param name="object">the object to enumerate</param>
        /// <param name="namespace">the namespace to enumerate in</param>
        /// <returns>child objects of the object</returns>
        private static IEnumerable<KeyValuePair<FieldInfo, object>> wasGetFields(object @object, string @namespace)
        {
            if (@object == null) yield break;

            foreach (FieldInfo fi in @object.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (fi.FieldType.FullName.Split('.', '+')
                    .Contains(@namespace, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (KeyValuePair<FieldInfo, object> sf in wasGetFields(fi.GetValue(@object), @namespace))
                    {
                        yield return sf;
                    }
                }
                yield return new KeyValuePair<FieldInfo, object>(fi, @object);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Enumerates the properties of an object along with the child objects,
        ///     provided that all child objects are part of a specified namespace.
        /// </summary>
        /// <param name="object">the object to enumerate</param>
        /// <param name="namespace">the namespace to enumerate in</param>
        /// <returns>child objects of the object</returns>
        private static IEnumerable<KeyValuePair<PropertyInfo, object>> wasGetProperties(object @object,
            string @namespace)
        {
            if (@object == null) yield break;

            foreach (PropertyInfo pi in @object.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (pi.PropertyType.FullName.Split('.', '+')
                    .Contains(@namespace, StringComparer.OrdinalIgnoreCase))
                {
                    MethodInfo getMethod = pi.GetGetMethod();
                    if (getMethod.ReturnType.IsArray)
                    {
                        var array = (Array) getMethod.Invoke(@object, null);
                        foreach (KeyValuePair<PropertyInfo, object> sp in
                            array.Cast<object>().SelectMany(element => wasGetProperties(element, @namespace)))
                        {
                            yield return sp;
                        }
                    }
                    foreach (
                        KeyValuePair<PropertyInfo, object> sp in
                            wasGetProperties(pi.GetValue(@object, null), @namespace))
                    {
                        yield return sp;
                    }
                }
                yield return new KeyValuePair<PropertyInfo, object>(pi, @object);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     This is a wrapper for both FieldInfo and PropertyInfo SetValue.
        /// </summary>
        /// <param name="info">either a FieldInfo or PropertyInfo</param>
        /// <param name="object">the object to set the value on</param>
        /// <param name="value">the value to set</param>
        private static void wasSetInfoValue<TK, TV>(TK info, ref TV @object, object value)
        {
            object o = @object;
            FieldInfo fi = (object) info as FieldInfo;
            if (fi != null)
            {
                fi.SetValue(o, value);
                @object = (TV) o;
                return;
            }
            PropertyInfo pi = (object) info as PropertyInfo;
            if (pi != null)
            {
                pi.SetValue(o, value, null);
                @object = (TV) o;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     This is a wrapper for both FieldInfo and PropertyInfo GetValue.
        /// </summary>
        /// <param name="info">either a FieldInfo or PropertyInfo</param>
        /// <param name="value">the object to get from</param>
        /// <returns>the value of the field or property</returns>
        private static object wasGetInfoValue<T>(T info, object value)
        {
            FieldInfo fi = (object) info as FieldInfo;
            if (fi != null)
            {
                return fi.GetValue(value);
            }
            PropertyInfo pi = (object) info as PropertyInfo;
            if (pi != null)
            {
                if (pi.GetIndexParameters().Any())
                {
                    return value;
                }
                return pi.GetValue(value, null);
            }
            return null;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     The function gets the value from FieldInfo or PropertyInfo.
        /// </summary>
        /// <param name="info">a FieldInfo or PropertyInfo structure</param>
        /// <param name="value">the value to get</param>
        /// <returns>the value or values as a string</returns>
        private static IEnumerable<string> wasGetInfo(object info, object value)
        {
            if (info == null) yield break;
            object data = wasGetInfoValue(info, value);
            if (data == null) yield break;
            // Handle arrays and lists
            if (data is Array || data is IList)
            {
                IList iList = (IList) data;
                foreach (object item in iList.Cast<object>().Where(o => o != null))
                {
                    // These are index collections so pre-prend an index.
                    yield return "Index";
                    yield return iList.IndexOf(item).ToString();
                    foreach (KeyValuePair<FieldInfo, object> fi in wasGetFields(item, item.GetType().Name))
                    {
                        if (fi.Key != null)
                        {
                            foreach (string fieldString in wasGetInfo(fi.Key, fi.Value))
                            {
                                yield return fi.Key.Name;
                                yield return fieldString;
                            }
                        }
                    }
                    foreach (KeyValuePair<PropertyInfo, object> pi in wasGetProperties(item, item.GetType().Name))
                    {
                        if (pi.Key != null)
                        {
                            foreach (string propertyString in wasGetInfo(pi.Key, pi.Value))
                            {
                                yield return pi.Key.Name;
                                yield return propertyString;
                            }
                        }
                    }
                    // Don't bother with primitive types.
                    if (item.GetType().IsPrimitive)
                    {
                        yield return item.ToString();
                    }
                }
                yield break;
            }
            // Handle Dictionary
            if (data is IDictionary)
            {
                IDictionary dictionary = (IDictionary) data;
                foreach (DictionaryEntry entry in dictionary)
                {
                    // First the keys.
                    foreach (KeyValuePair<FieldInfo, object> fi in wasGetFields(entry.Key, entry.Key.GetType().Name))
                    {
                        if (fi.Key != null)
                        {
                            foreach (string fieldString in wasGetInfo(fi.Key, fi.Value))
                            {
                                yield return fi.Key.Name;
                                yield return fieldString;
                            }
                        }
                    }
                    foreach (
                        KeyValuePair<PropertyInfo, object> pi in wasGetProperties(entry.Key, entry.Key.GetType().Name))
                    {
                        if (pi.Key != null)
                        {
                            foreach (string propertyString in wasGetInfo(pi.Key, pi.Value))
                            {
                                yield return pi.Key.Name;
                                yield return propertyString;
                            }
                        }
                    }
                    // Then the values.
                    foreach (KeyValuePair<FieldInfo, object> fi in wasGetFields(entry.Value, entry.Value.GetType().Name)
                        )
                    {
                        if (fi.Key != null)
                        {
                            foreach (string fieldString in wasGetInfo(fi.Key, fi.Value))
                            {
                                yield return fi.Key.Name;
                                yield return fieldString;
                            }
                        }
                    }
                    foreach (
                        KeyValuePair<PropertyInfo, object> pi in
                            wasGetProperties(entry.Value, entry.Value.GetType().Name))
                    {
                        if (pi.Key != null)
                        {
                            foreach (string propertyString in wasGetInfo(pi.Key, pi.Value))
                            {
                                yield return pi.Key.Name;
                                yield return propertyString;
                            }
                        }
                    }
                }
                yield break;
            }
            // Handle InternalDictionary
            FieldInfo internalDictionaryInfo = data.GetType()
                .GetField("Dictionary",
                    BindingFlags.Default | BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic);
            if (internalDictionaryInfo != null)
            {
                IDictionary iDictionary = internalDictionaryInfo.GetValue(data) as IDictionary;
                if (iDictionary == null) yield break;
                foreach (DictionaryEntry entry in iDictionary)
                {
                    // First the keys.
                    foreach (KeyValuePair<FieldInfo, object> fi in wasGetFields(entry.Key, entry.Key.GetType().Name))
                    {
                        if (fi.Key != null)
                        {
                            foreach (string fieldString in wasGetInfo(fi.Key, fi.Value))
                            {
                                yield return fi.Key.Name;
                                yield return fieldString;
                            }
                        }
                    }
                    foreach (
                        KeyValuePair<PropertyInfo, object> pi in wasGetProperties(entry.Key, entry.Key.GetType().Name))
                    {
                        if (pi.Key != null)
                        {
                            foreach (string propertyString in wasGetInfo(pi.Key, pi.Value))
                            {
                                yield return pi.Key.Name;
                                yield return propertyString;
                            }
                        }
                    }
                    // Then the values.
                    foreach (KeyValuePair<FieldInfo, object> fi in wasGetFields(entry.Value, entry.Value.GetType().Name)
                        )
                    {
                        if (fi.Key != null)
                        {
                            foreach (string fieldString in wasGetInfo(fi.Key, fi.Value))
                            {
                                yield return fi.Key.Name;
                                yield return fieldString;
                            }
                        }
                    }
                    foreach (
                        KeyValuePair<PropertyInfo, object> pi in
                            wasGetProperties(entry.Value, entry.Value.GetType().Name))
                    {
                        if (pi.Key != null)
                        {
                            foreach (string propertyString in wasGetInfo(pi.Key, pi.Value))
                            {
                                yield return pi.Key.Name;
                                yield return propertyString;
                            }
                        }
                    }
                }
                yield break;
            }
            // Handle date and time as an LSL timestamp
            if (data is DateTime)
            {
                yield return ((DateTime) data).ToString(LINDEN_CONSTANTS.LSL.DATE_TIME_STAMP);
            }

            string @string = data.ToString();
            if (string.IsNullOrEmpty(@string)) yield break;
            yield return @string;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sets the value of FieldInfo or PropertyInfo.
        /// </summary>
        /// <typeparam name="T">the type to set</typeparam>
        /// <param name="info">a FieldInfo or PropertyInfo object</param>
        /// <param name="value">the object's value</param>
        /// <param name="setting">the value to set to</param>
        /// <param name="object">the object to set the values for</param>
        private static void wasSetInfo<T>(object info, object value, string setting, ref T @object)
        {
            if (info == null) return;

            // OpenMetaverse particular flags.
            if (wasGetInfoValue(info, value) is ParcelFlags)
            {
                uint parcelFlags;
                switch (!uint.TryParse(setting, out parcelFlags))
                {
                    case true:
                        Parallel.ForEach(wasCSVToEnumerable(setting).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                            o =>
                            {
                                Parallel.ForEach(
                                    typeof (ParcelFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    p => { parcelFlags |= ((uint) p.GetValue(null)); });
                            });
                        break;
                }
                wasSetInfoValue(info, ref @object, parcelFlags);
                return;
            }
            if (wasGetInfoValue(info, value) is GroupPowers)
            {
                uint groupPowers;
                switch (!uint.TryParse(setting, out groupPowers))
                {
                    case true:
                        Parallel.ForEach(wasCSVToEnumerable(setting).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                            o =>
                            {
                                Parallel.ForEach(
                                    typeof (GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                    p => { groupPowers |= ((uint) p.GetValue(null)); });
                            });
                        break;
                }
                wasSetInfoValue(info, ref @object, groupPowers);
                return;
            }
            if (wasGetInfoValue(info, value) is AttachmentPoint)
            {
                byte attachmentPoint;
                switch (!byte.TryParse(setting, out attachmentPoint))
                {
                    case true:
                        FieldInfo attachmentPointFieldInfo =
                            typeof (AttachmentPoint).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (attachmentPointFieldInfo == null) break;
                        attachmentPoint = (byte) attachmentPointFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, attachmentPoint);
                return;
            }
            if (wasGetInfoValue(info, value) is Tree)
            {
                byte tree;
                switch (!byte.TryParse(setting, out tree))
                {
                    case true:
                        FieldInfo treeFieldInfo = typeof (Tree).GetFields(BindingFlags.Public |
                                                                          BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (treeFieldInfo == null) break;
                        tree = (byte) treeFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, tree);
                return;
            }
            if (wasGetInfoValue(info, value) is Material)
            {
                byte material;
                switch (!byte.TryParse(setting, out material))
                {
                    case true:
                        FieldInfo materialFieldInfo = typeof (Material).GetFields(BindingFlags.Public |
                                                                                  BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (materialFieldInfo == null) break;
                        material = (byte) materialFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, material);
                return;
            }
            if (wasGetInfoValue(info, value) is PathCurve)
            {
                byte pathCurve;
                switch (!byte.TryParse(setting, out pathCurve))
                {
                    case true:
                        FieldInfo pathCurveFieldInfo = typeof (PathCurve).GetFields(BindingFlags.Public |
                                                                                    BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (pathCurveFieldInfo == null) break;
                        pathCurve = (byte) pathCurveFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, pathCurve);
                return;
            }
            if (wasGetInfoValue(info, value) is PCode)
            {
                byte pCode;
                switch (!byte.TryParse(setting, out pCode))
                {
                    case true:
                        FieldInfo pCodeFieldInfo = typeof (PCode).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (pCodeFieldInfo == null) break;
                        pCode = (byte) pCodeFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, pCode);
                return;
            }
            if (wasGetInfoValue(info, value) is ProfileCurve)
            {
                byte profileCurve;
                switch (!byte.TryParse(setting, out profileCurve))
                {
                    case true:
                        FieldInfo profileCurveFieldInfo =
                            typeof (ProfileCurve).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (profileCurveFieldInfo == null) break;
                        profileCurve = (byte) profileCurveFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, profileCurve);
                return;
            }
            if (wasGetInfoValue(info, value) is HoleType)
            {
                byte holeType;
                switch (!byte.TryParse(setting, out holeType))
                {
                    case true:
                        FieldInfo holeTypeFieldInfo = typeof (HoleType).GetFields(BindingFlags.Public |
                                                                                  BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (holeTypeFieldInfo == null) break;
                        holeType = (byte) holeTypeFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, holeType);
                return;
            }
            if (wasGetInfoValue(info, value) is SculptType)
            {
                byte sculptType;
                switch (!byte.TryParse(setting, out sculptType))
                {
                    case true:
                        FieldInfo sculptTypeFieldInfo = typeof (SculptType).GetFields(BindingFlags.Public |
                                                                                      BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(setting, StringComparison.Ordinal));
                        if (sculptTypeFieldInfo == null) break;
                        sculptType = (byte) sculptTypeFieldInfo.GetValue(null);
                        break;
                }
                wasSetInfoValue(info, ref @object, sculptType);
                return;
            }
            // OpenMetaverse Primitive Types
            if (wasGetInfoValue(info, value) is UUID)
            {
                UUID UUIDData;
                if (!UUID.TryParse(setting, out UUIDData))
                {
                    InventoryItem item = FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                        setting).FirstOrDefault() as InventoryItem;
                    if (item == null)
                    {
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    UUIDData = item.UUID;
                }
                switch (!UUIDData.Equals(UUID.Zero))
                {
                    case true:
                        wasSetInfoValue(info, ref @object, UUIDData);
                        return;
                    default:
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                }
            }
            if (wasGetInfoValue(info, value) is Vector3)
            {
                Vector3 vector3Data;
                if (Vector3.TryParse(setting, out vector3Data))
                {
                    wasSetInfoValue(info, ref @object, vector3Data);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is Vector2)
            {
                Vector3 vector2Data;
                if (Vector2.TryParse(setting, out vector2Data))
                {
                    wasSetInfoValue(info, ref @object, vector2Data);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is Vector3d)
            {
                Vector3d vector3DData;
                if (Vector3d.TryParse(setting, out vector3DData))
                {
                    wasSetInfoValue(info, ref @object, vector3DData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is Vector4)
            {
                Vector4 vector4Data;
                if (Vector4.TryParse(setting, out vector4Data))
                {
                    wasSetInfoValue(info, ref @object, vector4Data);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is Quaternion)
            {
                Quaternion quaternionData;
                if (Quaternion.TryParse(setting, out quaternionData))
                {
                    wasSetInfoValue(info, ref @object, quaternionData);
                    return;
                }
            }
            // Primitive types.
            if (wasGetInfoValue(info, value) is bool)
            {
                bool boolData;
                if (bool.TryParse(setting, out boolData))
                {
                    wasSetInfoValue(info, ref @object, boolData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is char)
            {
                char charData;
                if (char.TryParse(setting, out charData))
                {
                    wasSetInfoValue(info, ref @object, charData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is decimal)
            {
                decimal decimalData;
                if (decimal.TryParse(setting, out decimalData))
                {
                    wasSetInfoValue(info, ref @object, decimalData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is byte)
            {
                byte byteData;
                if (byte.TryParse(setting, out byteData))
                {
                    wasSetInfoValue(info, ref @object, byteData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is int)
            {
                int intData;
                if (int.TryParse(setting, out intData))
                {
                    wasSetInfoValue(info, ref @object, intData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is uint)
            {
                uint uintData;
                if (uint.TryParse(setting, out uintData))
                {
                    wasSetInfoValue(info, ref @object, uintData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is float)
            {
                float floatData;
                if (float.TryParse(setting, out floatData))
                {
                    wasSetInfoValue(info, ref @object, floatData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is long)
            {
                long longData;
                if (long.TryParse(setting, out longData))
                {
                    wasSetInfoValue(info, ref @object, longData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is float)
            {
                float singleData;
                if (float.TryParse(setting, out singleData))
                {
                    wasSetInfoValue(info, ref @object, singleData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is DateTime)
            {
                DateTime dateTimeData;
                if (DateTime.TryParse(setting, out dateTimeData))
                {
                    wasSetInfoValue(info, ref @object, dateTimeData);
                    return;
                }
            }
            if (wasGetInfoValue(info, value) is string)
            {
                wasSetInfoValue(info, ref @object, setting);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sets permissions on inventory items using the [WaS] permission mask format.
        /// </summary>
        /// <param name="inventoryItem">an inventory item</param>
        /// <param name="wasPermissions">the string permissions to set</param>
        /// <returns>true in case the permissions were set successfully</returns>
        private static bool wasSetInventoryItemPermissions(InventoryItem inventoryItem, string wasPermissions)
        {
            // Update the object.
            OpenMetaverse.Permissions permissions = wasStringToPermissions(wasPermissions);
            inventoryItem.Permissions = permissions;
            lock (ClientInstanceInventoryLock)
            {
                Client.Inventory.RequestUpdateItem(inventoryItem);
            }
            // Now check that the permissions were set.
            bool succeeded = false;
            ManualResetEvent ItemReceivedEvent = new ManualResetEvent(false);
            EventHandler<ItemReceivedEventArgs> ItemReceivedEventHandler =
                (sender, args) =>
                {
                    if (!args.Item.UUID.Equals(inventoryItem.UUID)) return;
                    succeeded = args.Item.Permissions.Equals(permissions);
                    ItemReceivedEvent.Set();
                };
            lock (ClientInstanceInventoryLock)
            {
                Client.Inventory.ItemReceived += ItemReceivedEventHandler;
                Client.Inventory.RequestFetchInventory(inventoryItem.UUID, inventoryItem.OwnerID);
                if (
                    !ItemReceivedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                {
                    Client.Inventory.ItemReceived -= ItemReceivedEventHandler;
                    succeeded = false;
                }
                Client.Inventory.ItemReceived -= ItemReceivedEventHandler;
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts Linden item permissions to a formatted string:
        ///     CDEMVT - Copy, Damage, Export, Modify, Move, Transfer
        ///     BBBBBBEEEEEEGGGGGGNNNNNNOOOOOO - Base, Everyone, Group, Next, Owner
        /// </summary>
        /// <param name="permissions">the item permissions</param>
        /// <returns>the literal permissions for an item</returns>
        private static string wasPermissionsToString(OpenMetaverse.Permissions permissions)
        {
            Func<PermissionMask, string> segment = o =>
            {
                StringBuilder seg = new StringBuilder();

                switch (!((uint) o & (uint) PermissionMask.Copy).Equals(0))
                {
                    case true:
                        seg.Append("c");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Damage).Equals(0))
                {
                    case true:
                        seg.Append("d");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Export).Equals(0))
                {
                    case true:
                        seg.Append("e");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Modify).Equals(0))
                {
                    case true:
                        seg.Append("m");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Move).Equals(0))
                {
                    case true:
                        seg.Append("v");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Transfer).Equals(0))
                {
                    case true:
                        seg.Append("t");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                return seg.ToString();
            };

            StringBuilder x = new StringBuilder();
            x.Append(segment(permissions.BaseMask));
            x.Append(segment(permissions.EveryoneMask));
            x.Append(segment(permissions.GroupMask));
            x.Append(segment(permissions.NextOwnerMask));
            x.Append(segment(permissions.OwnerMask));
            return x.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a formatted string to item permissions:
        ///     CDEMVT - Copy, Damage, Export, Modify, Move, Transfer
        ///     BBBBBBEEEEEEGGGGGGNNNNNNOOOOOO - Base, Everyone, Group, Next, Owner
        /// </summary>
        /// <param name="permissions">the item permissions</param>
        /// <returns>the permissions for an item</returns>
        private static OpenMetaverse.Permissions wasStringToPermissions(string permissions)
        {
            Func<string, uint> segment = o =>
            {
                uint r = 0;
                switch (!char.ToLower(o[0]).Equals('c'))
                {
                    case false:
                        r |= (uint) PermissionMask.Copy;
                        break;
                }

                switch (!char.ToLower(o[1]).Equals('d'))
                {
                    case false:
                        r |= (uint) PermissionMask.Damage;
                        break;
                }

                switch (!char.ToLower(o[2]).Equals('e'))
                {
                    case false:
                        r |= (uint) PermissionMask.Export;
                        break;
                }

                switch (!char.ToLower(o[3]).Equals('m'))
                {
                    case false:
                        r |= (uint) PermissionMask.Modify;
                        break;
                }

                switch (!char.ToLower(o[4]).Equals('v'))
                {
                    case false:
                        r |= (uint) PermissionMask.Move;
                        break;
                }

                switch (!char.ToLower(o[5]).Equals('t'))
                {
                    case false:
                        r |= (uint) PermissionMask.Transfer;
                        break;
                }

                return r;
            };

            return new OpenMetaverse.Permissions(segment(permissions.Substring(0, 6)),
                segment(permissions.Substring(6, 6)), segment(permissions.Substring(12, 6)),
                segment(permissions.Substring(18, 6)), segment(permissions.Substring(24, 6)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether an agent has a set of powers for a group.
        /// </summary>
        /// <param name="agentUUID">the agent UUID</param>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="powers">a GroupPowers structure</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout in millisecons for each data burst</param>
        /// <returns>true if the agent has the powers</returns>
        private static bool HasGroupPowers(UUID agentUUID, UUID groupUUID, GroupPowers powers, uint millisecondsTimeout,
            uint dataTimeout)
        {
            List<AvatarGroup> avatarGroups = new List<AvatarGroup>();
            wasAdaptiveAlarm AvatarGroupsReceivedAlarm = new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
            object LockObject = new object();
            EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
            {
                AvatarGroupsReceivedAlarm.Alarm(dataTimeout);
                lock (LockObject)
                {
                    avatarGroups.AddRange(args.Groups);
                }
            };
            lock (ClientInstanceAvatarsLock)
            {
                Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                Client.Avatars.RequestAvatarProperties(agentUUID);
                if (!AvatarGroupsReceivedAlarm.Signal.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                    return false;
                }
                Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
            }
            return
                avatarGroups.AsParallel()
                    .Any(o => o.GroupID.Equals(groupUUID) && !(o.GroupPowers & powers).Equals(GroupPowers.None));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Attempts to join the group chat for a given group.
        /// </summary>
        /// <param name="groupUUID">the UUID of the group to join the group chat for</param>
        /// <param name="millisecondsTimeout">timeout for joining the group chat</param>
        /// <returns>true if the group chat was joined</returns>
        private static bool JoinGroupChat(UUID groupUUID, uint millisecondsTimeout)
        {
            bool succeeded = false;
            ManualResetEvent GroupChatJoinedEvent = new ManualResetEvent(false);
            EventHandler<GroupChatJoinedEventArgs> GroupChatJoinedEventHandler =
                (sender, args) =>
                {
                    succeeded = args.Success;
                    GroupChatJoinedEvent.Set();
                };
            lock (ClientInstanceSelfLock)
            {
                Client.Self.GroupChatJoined += GroupChatJoinedEventHandler;
                Client.Self.RequestJoinGroupChat(groupUUID);
                if (!GroupChatJoinedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                    return false;
                }
                Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether an agent referenced by an UUID is in a group
        ///     referenced by an UUID.
        /// </summary>
        /// <param name="agentUUID">the UUID of the agent</param>
        /// <param name="groupUUID">the UUID of the groupt</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <returns>true if the agent is in the group</returns>
        private static bool AgentInGroup(UUID agentUUID, UUID groupUUID, uint millisecondsTimeout)
        {
            ManualResetEvent groupMembersReceivedEvent = new ManualResetEvent(false);
            Dictionary<UUID, GroupMember> groupMembers = null;
            EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
            {
                groupMembers = args.Members;
                groupMembersReceivedEvent.Set();
            };
            lock (ClientInstanceGroupsLock)
            {
                Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                Client.Groups.RequestGroupMembers(groupUUID);
                if (!groupMembersReceivedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    return false;
                }
                Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
            }
            return groupMembers != null && groupMembers.AsParallel().Any(o => o.Value.ID.Equals(agentUUID));
        }

        /// <summary>
        ///     Used to check whether a group name matches a group password.
        /// </summary>
        /// <param name="group">the name of the group</param>
        /// <param name="password">the password for the group</param>
        /// <returns>true if the agent has authenticated</returns>
        private static bool Authenticate(string group, string password)
        {
            UUID groupUUID;
            return UUID.TryParse(group, out groupUUID)
                ? corradeConfiguration.Groups.AsParallel().Any(
                    o =>
                        groupUUID.Equals(o.UUID) &&
                        password.Equals(o.Password, StringComparison.Ordinal))
                : corradeConfiguration.Groups.AsParallel().Any(
                    o =>
                        o.Name.Equals(group, StringComparison.OrdinalIgnoreCase) &&
                        password.Equals(o.Password, StringComparison.Ordinal));
        }

        /// <summary>
        ///     Used to check whether a group has certain permissions for Corrade.
        /// </summary>
        /// <param name="group">the name of the group</param>
        /// <param name="permission">the numeric Corrade permission</param>
        /// <returns>true if the group has permission</returns>
        private static bool HasCorradePermission(string group, int permission)
        {
            UUID groupUUID;
            return !permission.Equals(0) && UUID.TryParse(group, out groupUUID)
                ? corradeConfiguration.Groups.AsParallel()
                    .Any(o => groupUUID.Equals(o.UUID) && !(o.PermissionMask & permission).Equals(0))
                : corradeConfiguration.Groups.AsParallel().Any(
                    o =>
                        o.Name.Equals(group, StringComparison.OrdinalIgnoreCase) &&
                        !(o.PermissionMask & permission).Equals(0));
        }

        /// <summary>
        ///     Fetches a Corrade group from a key-value formatted message message.
        /// </summary>
        /// <param name="message">the message to inspect</param>
        /// <returns>the configured group</returns>
        private static Group GetCorradeGroupFromMessage(string message)
        {
            string group =
                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.GROUP)), message));
            UUID groupUUID;
            return UUID.TryParse(group, out groupUUID)
                ? corradeConfiguration.Groups.AsParallel().FirstOrDefault(o => o.UUID.Equals(groupUUID))
                : corradeConfiguration.Groups.AsParallel()
                    .FirstOrDefault(o => o.Name.Equals(group, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Used to check whether a group has a certain notification for Corrade.
        /// </summary>
        /// <param name="group">the name of the group</param>
        /// <param name="notification">the numeric Corrade notification</param>
        /// <returns>true if the group has the notification</returns>
        private static bool GroupHasNotification(string group, uint notification)
        {
            UUID groupUUID;
            return !notification.Equals(0) && UUID.TryParse(group, out groupUUID)
                ? corradeConfiguration.Groups.AsParallel().Any(
                    o => groupUUID.Equals(o.UUID) &&
                         !(o.NotificationMask & notification).Equals(0))
                : corradeConfiguration.Groups.AsParallel().Any(
                    o => o.Name.Equals(group, StringComparison.OrdinalIgnoreCase) &&
                         !(o.NotificationMask & notification).Equals(0));
        }

        /// <summary>
        ///     Used to determine whether the current grid is Second Life.
        /// </summary>
        /// <returns>true if the connected grid is Second Life</returns>
        private static bool IsSecondLife()
        {
            return Client.Network.CurrentSim.SimVersion.Contains(LINDEN_CONSTANTS.GRID.SECOND_LIFE);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches a group.
        /// </summary>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="group">a group object to store the group profile</param>
        /// <returns>true if the group was found and false otherwise</returns>
        private static bool RequestGroup(UUID groupUUID, uint millisecondsTimeout, ref OpenMetaverse.Group group)
        {
            OpenMetaverse.Group localGroup = new OpenMetaverse.Group();
            ManualResetEvent GroupProfileEvent = new ManualResetEvent(false);
            EventHandler<GroupProfileEventArgs> GroupProfileDelegate = (sender, args) =>
            {
                localGroup = args.Group;
                GroupProfileEvent.Set();
            };
            lock (ClientInstanceGroupsLock)
            {
                Client.Groups.GroupProfile += GroupProfileDelegate;
                Client.Groups.RequestGroupProfile(groupUUID);
                if (!GroupProfileEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Groups.GroupProfile -= GroupProfileDelegate;
                    return false;
                }
                Client.Groups.GroupProfile -= GroupProfileDelegate;
            }
            group = localGroup;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the parcel of a simulator given a position.
        /// </summary>
        /// <param name="simulator">the simulator containing the parcel</param>
        /// <param name="position">a position within the parcel</param>
        /// <param name="parcel">a parcel object where to store the found parcel</param>
        /// <returns>true if the parcel could be found</returns>
        private static bool GetParcelAtPosition(Simulator simulator, Vector3 position,
            ref Parcel parcel)
        {
            ManualResetEvent RequestAllSimParcelsEvent = new ManualResetEvent(false);
            EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedDelegate =
                (sender, args) => RequestAllSimParcelsEvent.Set();
            lock (ClientInstanceParcelsLock)
            {
                Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedDelegate;
                switch (!simulator.IsParcelMapFull())
                {
                    case true:
                        Client.Parcels.RequestAllSimParcels(simulator);
                        break;
                    default:
                        RequestAllSimParcelsEvent.Set();
                        break;
                }
                if (!RequestAllSimParcelsEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                {
                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
                    return false;
                }
                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedDelegate;
            }
            HashSet<Parcel> localParcels = new HashSet<Parcel>();
            object LockObject = new object();
            simulator.Parcels.ForEach(o =>
            {
                if (!(position.X >= o.AABBMin.X) || !(position.X <= o.AABBMax.X) ||
                    !(position.Y >= o.AABBMin.Y) || !(position.Y <= o.AABBMax.Y))
                    return;
                lock (LockObject)
                {
                    localParcels.Add(o);
                }
            });
            Parcel localParcel = localParcels.OrderBy(o => Vector3.Distance(o.AABBMin, o.AABBMax)).FirstOrDefault();
            switch (localParcel != null)
            {
                case true:
                    parcel = localParcel;
                    return true;
                default:
                    return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines whether a vector falls within a parcel.
        /// </summary>
        /// <param name="position">a 3D vector</param>
        /// <param name="parcel">a parcel</param>
        /// <returns>true if the vector falls within the parcel bounds</returns>
        private static bool IsVectorInParcel(Vector3 position, Parcel parcel)
        {
            return position.X >= parcel.AABBMin.X && position.X <= parcel.AABBMax.X &&
                   position.Y >= parcel.AABBMin.Y && position.Y <= parcel.AABBMax.Y;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find a named primitive in range (whether attachment or in-world).
        /// </summary>
        /// <param name="item">the name or UUID of the primitive</param>
        /// <param name="range">the range in meters to search for the object</param>
        /// <param name="primitive">a primitive object to store the result</param>
        /// <param name="millisecondsTimeout">the services timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>true if the primitive could be found</returns>
        private static bool FindPrimitive<T>(T item, float range, ref Primitive primitive, uint millisecondsTimeout,
            uint dataTimeout)
        {
            HashSet<Primitive> selectedPrimitives = new HashSet<Primitive>();
            HashSet<Primitive> objectsPrimitives =
                new HashSet<Primitive>(GetPrimitives(range, millisecondsTimeout, dataTimeout));
            HashSet<Avatar> objectsAvatars = new HashSet<Avatar>(GetAvatars(range, millisecondsTimeout, dataTimeout));
            object LockObject = new object();
            Parallel.ForEach(objectsPrimitives, o =>
            {
                switch (o.ParentID)
                {
                    // primitive is a parent and it is in range
                    case 0:
                        if (Vector3.Distance(o.Position, Client.Self.SimPosition) <= range)
                        {
                            if (item is UUID)
                            {
                                switch (!o.ID.Equals(item))
                                {
                                    case false:
                                        lock (LockObject)
                                        {
                                            selectedPrimitives.Add(o);
                                        }
                                        break;
                                }
                                break;
                            }
                            if (item is string)
                            {
                                lock (LockObject)
                                {
                                    selectedPrimitives.Add(o);
                                }
                            }
                        }
                        break;
                    // primitive is a child
                    default:
                        // find the parent of the primitive
                        Primitive primitiveParent = objectsPrimitives.FirstOrDefault(p => p.LocalID.Equals(o.ParentID));
                        // if the primitive has a parent
                        if (primitiveParent != null)
                        {
                            // if the parent is in range, add the child
                            if (Vector3.Distance(primitiveParent.Position, Client.Self.SimPosition) <= range)
                            {
                                if (item is UUID)
                                {
                                    switch (!o.ID.Equals(item))
                                    {
                                        case false:
                                            lock (LockObject)
                                            {
                                                selectedPrimitives.Add(o);
                                            }
                                            break;
                                    }
                                    break;
                                }
                                if (item is string)
                                {
                                    lock (LockObject)
                                    {
                                        selectedPrimitives.Add(o);
                                    }
                                }
                                break;
                            }
                        }
                        // check if an avatar is the parent of the parent primitive
                        Avatar avatarParent =
                            objectsAvatars.FirstOrDefault(p => p.LocalID.Equals(o.ParentID));
                        // parent avatar not found, this should not happen
                        if (avatarParent != null)
                        {
                            // check whether the avatar is sitting
                            Primitive avatarParentPrimitive =
                                objectsPrimitives.FirstOrDefault(p => p.LocalID.Equals(avatarParent.ParentID));
                            switch (avatarParentPrimitive != null)
                            {
                                case true: // the avatar is sitting, if the sit root is in range, add the primitive
                                    if (Vector3.Distance(avatarParentPrimitive.Position, Client.Self.SimPosition) <=
                                        range)
                                    {
                                        if (item is UUID)
                                        {
                                            switch (!o.ID.Equals(item))
                                            {
                                                case false:
                                                    lock (LockObject)
                                                    {
                                                        selectedPrimitives.Add(o);
                                                    }
                                                    break;
                                            }
                                            break;
                                        }
                                        if (item is string)
                                        {
                                            lock (LockObject)
                                            {
                                                selectedPrimitives.Add(o);
                                            }
                                        }
                                    }
                                    break;
                                default: // the avatar is not sitting
                                    // check if the avatar is in range
                                    if (Vector3.Distance(avatarParent.Position, Client.Self.SimPosition) <= range)
                                    {
                                        if (item is UUID)
                                        {
                                            switch (!o.ID.Equals(item))
                                            {
                                                case false:
                                                    lock (LockObject)
                                                    {
                                                        selectedPrimitives.Add(o);
                                                    }
                                                    break;
                                            }
                                            break;
                                        }
                                        if (item is string)
                                        {
                                            lock (LockObject)
                                            {
                                                selectedPrimitives.Add(o);
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        break;
                }
            });
            if (!selectedPrimitives.Any()) return false;
            if (!UpdatePrimitives(ref selectedPrimitives, dataTimeout))
                return false;
            primitive =
                selectedPrimitives.FirstOrDefault(
                    o =>
                        (item is UUID && o.ID.Equals(item)) ||
                        (item is string && (item as string).Equals(o.Properties.Name, StringComparison.Ordinal)));
            return primitive != null;
        }

        /// <summary>
        ///     Creates a faceted mesh from a primitive.
        /// </summary>
        /// <param name="primitive">the primitive to convert</param>
        /// <param name="mesher">the mesher to use</param>
        /// <param name="facetedMesh">a reference to an output facted mesh object</param>
        /// <param name="millisecondsTimeout">the services timeout</param>
        /// <returns>true if the mesh could be created successfully</returns>
        private static bool MakeFacetedMesh(Primitive primitive, MeshmerizerR mesher, ref FacetedMesh facetedMesh,
            uint millisecondsTimeout)
        {
            if (primitive.Sculpt == null || primitive.Sculpt.SculptTexture.Equals(UUID.Zero))
            {
                facetedMesh = mesher.GenerateFacetedMesh(primitive, DetailLevel.Highest);
                return true;
            }
            if (!primitive.Sculpt.Type.Equals(SculptType.Mesh))
            {
                byte[] assetData = null;
                switch (!Client.Assets.Cache.HasAsset(primitive.Sculpt.SculptTexture))
                {
                    case true:
                        lock (ClientInstanceAssetsLock)
                        {
                            ManualResetEvent ImageDownloadedEvent = new ManualResetEvent(false);
                            Client.Assets.RequestImage(primitive.Sculpt.SculptTexture, (state, args) =>
                            {
                                if (!state.Equals(TextureRequestState.Finished)) return;
                                assetData = args.AssetData;
                                ImageDownloadedEvent.Set();
                            });
                            if (!ImageDownloadedEvent.WaitOne((int) millisecondsTimeout, false))
                                return false;
                        }
                        Client.Assets.Cache.SaveAssetToCache(primitive.Sculpt.SculptTexture, assetData);
                        break;
                    default:
                        assetData = Client.Assets.Cache.GetCachedAssetBytes(primitive.Sculpt.SculptTexture);
                        break;
                }
                Image image;
                ManagedImage managedImage;
                switch (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                {
                    case true:
                        return false;
                    default:
                        if ((managedImage.Channels & ManagedImage.ImageChannels.Alpha) != 0)
                        {
                            managedImage.ConvertChannels(managedImage.Channels & ~ManagedImage.ImageChannels.Alpha);
                        }
                        image = LoadTGAClass.LoadTGA(new MemoryStream(managedImage.ExportTGA()));
                        break;
                }
                facetedMesh = mesher.GenerateFacetedSculptMesh(primitive, (Bitmap) image, DetailLevel.Highest);
                return true;
            }
            FacetedMesh localFacetedMesh = null;
            ManualResetEvent MeshDownloadedEvent = new ManualResetEvent(false);
            lock (ClientInstanceAssetsLock)
            {
                Client.Assets.RequestMesh(primitive.Sculpt.SculptTexture, (success, meshAsset) =>
                {
                    FacetedMesh.TryDecodeFromAsset(primitive, meshAsset, DetailLevel.Highest, out localFacetedMesh);
                    MeshDownloadedEvent.Set();
                });

                if (!MeshDownloadedEvent.WaitOne((int) millisecondsTimeout, false))
                    return false;
            }

            switch (localFacetedMesh != null)
            {
                case true:
                    facetedMesh = localFacetedMesh;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Generates a Collada DAE XML Document.
        /// </summary>
        /// <param name="facetedMeshSet">the faceted meshes</param>
        /// <param name="textures">a dictionary of UUID to texture names</param>
        /// <param name="imageFormat">the image export format</param>
        /// <returns>the DAE document</returns>
        /// <remarks>
        ///     This function is a branch-in of several functions of the Radegast Viewer with some changes by Wizardry and
        ///     Steamworks.
        /// </remarks>
        private static XmlDocument GenerateCollada(IEnumerable<FacetedMesh> facetedMeshSet,
            Dictionary<UUID, string> textures, string imageFormat)
        {
            List<MaterialInfo> AllMeterials = new List<MaterialInfo>();

            XmlDocument Doc = new XmlDocument();
            var root = Doc.AppendChild(Doc.CreateElement("COLLADA"));
            root.Attributes.Append(Doc.CreateAttribute("xmlns")).Value = "http://www.collada.org/2005/11/COLLADASchema";
            root.Attributes.Append(Doc.CreateAttribute("version")).Value = "1.4.1";

            var asset = root.AppendChild(Doc.CreateElement("asset"));
            var contributor = asset.AppendChild(Doc.CreateElement("contributor"));
            contributor.AppendChild(Doc.CreateElement("author")).InnerText = "Radegast User";
            contributor.AppendChild(Doc.CreateElement("authoring_tool")).InnerText = "Radegast Collada Export";

            asset.AppendChild(Doc.CreateElement("created")).InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            asset.AppendChild(Doc.CreateElement("modified")).InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            var unit = asset.AppendChild(Doc.CreateElement("unit"));
            unit.Attributes.Append(Doc.CreateAttribute("name")).Value = "meter";
            unit.Attributes.Append(Doc.CreateAttribute("meter")).Value = "1";

            asset.AppendChild(Doc.CreateElement("up_axis")).InnerText = "Z_UP";

            var images = root.AppendChild(Doc.CreateElement("library_images"));
            var geomLib = root.AppendChild(Doc.CreateElement("library_geometries"));
            var effects = root.AppendChild(Doc.CreateElement("library_effects"));
            var materials = root.AppendChild(Doc.CreateElement("library_materials"));
            var scene = root.AppendChild(Doc.CreateElement("library_visual_scenes"))
                .AppendChild(Doc.CreateElement("visual_scene"));
            scene.Attributes.Append(Doc.CreateAttribute("id")).InnerText = "Scene";
            scene.Attributes.Append(Doc.CreateAttribute("name")).InnerText = "Scene";

            foreach (string name in textures.Values)
            {
                string colladaName = name + "_" + imageFormat.ToLower();
                var image = images.AppendChild(Doc.CreateElement("image"));
                image.Attributes.Append(Doc.CreateAttribute("id")).InnerText = colladaName;
                image.Attributes.Append(Doc.CreateAttribute("name")).InnerText = colladaName;
                image.AppendChild(Doc.CreateElement("init_from")).InnerText =
                    wasURIUnescapeDataString(name + "." + imageFormat.ToLower());
            }

            Func<XmlNode, string, string, List<float>, bool> addSource = (mesh, src_id, param, vals) =>
            {
                var source = mesh.AppendChild(Doc.CreateElement("source"));
                source.Attributes.Append(Doc.CreateAttribute("id")).InnerText = src_id;
                var src_array = source.AppendChild(Doc.CreateElement("float_array"));

                src_array.Attributes.Append(Doc.CreateAttribute("id")).InnerText = string.Format("{0}-{1}", src_id,
                    "array");
                src_array.Attributes.Append(Doc.CreateAttribute("count")).InnerText = vals.Count.ToString();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < vals.Count; i++)
                {
                    sb.Append(vals[i].ToString(Utils.EnUsCulture));
                    if (i != vals.Count - 1)
                    {
                        sb.Append(" ");
                    }
                }
                src_array.InnerText = sb.ToString();

                var acc = source.AppendChild(Doc.CreateElement("technique_common"))
                    .AppendChild(Doc.CreateElement("accessor"));
                acc.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}", src_id,
                    "array");
                acc.Attributes.Append(Doc.CreateAttribute("count")).InnerText = (vals.Count/param.Length).ToString();
                acc.Attributes.Append(Doc.CreateAttribute("stride")).InnerText = param.Length.ToString();

                foreach (char c in param)
                {
                    var pX = acc.AppendChild(Doc.CreateElement("param"));
                    pX.Attributes.Append(Doc.CreateAttribute("name")).InnerText = c.ToString();
                    pX.Attributes.Append(Doc.CreateAttribute("type")).InnerText = "float";
                }

                return true;
            };

            Func<Primitive.TextureEntryFace, MaterialInfo> getMaterial = o =>
            {
                MaterialInfo ret = AllMeterials.FirstOrDefault(mat => mat.Matches(o));

                if (ret != null) return ret;
                ret = new MaterialInfo
                {
                    TextureID = o.TextureID,
                    Color = o.RGBA,
                    Name = string.Format("Material{0}", AllMeterials.Count)
                };
                AllMeterials.Add(ret);

                return ret;
            };

            Func<FacetedMesh, List<MaterialInfo>> getMaterials = o =>
            {
                var ret = new List<MaterialInfo>();

                for (int face_num = 0; face_num < o.Faces.Count; face_num++)
                {
                    var te = o.Faces[face_num].TextureFace;
                    if (te.RGBA.A < 0.01f)
                    {
                        continue;
                    }
                    var mat = getMaterial.Invoke(te);
                    if (!ret.Contains(mat))
                    {
                        ret.Add(mat);
                    }
                }
                return ret;
            };

            Func<XmlNode, string, string, FacetedMesh, List<int>, bool> addPolygons =
                (mesh, geomID, materialID, obj, faces_to_include) =>
                {
                    var polylist = mesh.AppendChild(Doc.CreateElement("polylist"));
                    polylist.Attributes.Append(Doc.CreateAttribute("material")).InnerText = materialID;

                    // Vertices semantic
                    {
                        var input = polylist.AppendChild(Doc.CreateElement("input"));
                        input.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "VERTEX";
                        input.Attributes.Append(Doc.CreateAttribute("offset")).InnerText = "0";
                        input.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}",
                            geomID, "vertices");
                    }

                    // Normals semantic
                    {
                        var input = polylist.AppendChild(Doc.CreateElement("input"));
                        input.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "NORMAL";
                        input.Attributes.Append(Doc.CreateAttribute("offset")).InnerText = "0";
                        input.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}",
                            geomID, "normals");
                    }

                    // UV semantic
                    {
                        var input = polylist.AppendChild(Doc.CreateElement("input"));
                        input.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "TEXCOORD";
                        input.Attributes.Append(Doc.CreateAttribute("offset")).InnerText = "0";
                        input.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format("#{0}-{1}",
                            geomID, "map0");
                    }

                    // Save indices
                    var vcount = polylist.AppendChild(Doc.CreateElement("vcount"));
                    var p = polylist.AppendChild(Doc.CreateElement("p"));
                    int index_offset = 0;
                    int num_tris = 0;
                    StringBuilder pBuilder = new StringBuilder();
                    StringBuilder vcountBuilder = new StringBuilder();

                    for (int face_num = 0; face_num < obj.Faces.Count; face_num++)
                    {
                        var face = obj.Faces[face_num];
                        if (faces_to_include == null || faces_to_include.Contains(face_num))
                        {
                            for (int i = 0; i < face.Indices.Count; i++)
                            {
                                int index = index_offset + face.Indices[i];
                                pBuilder.Append(index);
                                pBuilder.Append(" ");
                                if (i%3 == 0)
                                {
                                    vcountBuilder.Append("3 ");
                                    num_tris++;
                                }
                            }
                        }
                        index_offset += face.Vertices.Count;
                    }

                    p.InnerText = pBuilder.ToString().TrimEnd();
                    vcount.InnerText = vcountBuilder.ToString().TrimEnd();
                    polylist.Attributes.Append(Doc.CreateAttribute("count")).InnerText = num_tris.ToString();

                    return true;
                };

            Func<FacetedMesh, MaterialInfo, List<int>> getFacesWithMaterial = (obj, mat) =>
            {
                var ret = new List<int>();
                for (int face_num = 0; face_num < obj.Faces.Count; face_num++)
                {
                    if (mat == getMaterial.Invoke(obj.Faces[face_num].TextureFace))
                    {
                        ret.Add(face_num);
                    }
                }
                return ret;
            };

            Func<Vector3, Quaternion, Vector3, float[]> createSRTMatrix = (scale, q, pos) =>
            {
                float[] mat = new float[16];

                // Transpose the quaternion (don't ask me why)
                q.X = q.X*-1f;
                q.Y = q.Y*-1f;
                q.Z = q.Z*-1f;

                float x2 = q.X + q.X;
                float y2 = q.Y + q.Y;
                float z2 = q.Z + q.Z;
                float xx = q.X*x2;
                float xy = q.X*y2;
                float xz = q.X*z2;
                float yy = q.Y*y2;
                float yz = q.Y*z2;
                float zz = q.Z*z2;
                float wx = q.W*x2;
                float wy = q.W*y2;
                float wz = q.W*z2;

                mat[0] = (1.0f - (yy + zz))*scale.X;
                mat[1] = (xy - wz)*scale.X;
                mat[2] = (xz + wy)*scale.X;
                mat[3] = 0.0f;

                mat[4] = (xy + wz)*scale.Y;
                mat[5] = (1.0f - (xx + zz))*scale.Y;
                mat[6] = (yz - wx)*scale.Y;
                mat[7] = 0.0f;

                mat[8] = (xz - wy)*scale.Z;
                mat[9] = (yz + wx)*scale.Z;
                mat[10] = (1.0f - (xx + yy))*scale.Z;
                mat[11] = 0.0f;

                //Positional parts
                mat[12] = pos.X;
                mat[13] = pos.Y;
                mat[14] = pos.Z;
                mat[15] = 1.0f;

                return mat;
            };

            Func<XmlNode, bool> generateEffects = o =>
            {
                // Effects (face color, alpha)
                foreach (var mat in AllMeterials)
                {
                    var color = mat.Color;
                    var effect = effects.AppendChild(Doc.CreateElement("effect"));
                    effect.Attributes.Append(Doc.CreateAttribute("id")).InnerText = mat.Name + "-fx";
                    var profile = effect.AppendChild(Doc.CreateElement("profile_COMMON"));
                    string colladaName = null;

                    KeyValuePair<UUID, string> kvp = textures.FirstOrDefault(p => p.Key.Equals(mat.TextureID));

                    if (!kvp.Equals(default(KeyValuePair<UUID, string>)))
                    {
                        UUID textID = kvp.Key;
                        colladaName = textures[textID] + "_" + imageFormat.ToLower();
                        var newparam = profile.AppendChild(Doc.CreateElement("newparam"));
                        newparam.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = colladaName + "-surface";
                        var surface = newparam.AppendChild(Doc.CreateElement("surface"));
                        surface.Attributes.Append(Doc.CreateAttribute("type")).InnerText = "2D";
                        surface.AppendChild(Doc.CreateElement("init_from")).InnerText = colladaName;
                        newparam = profile.AppendChild(Doc.CreateElement("newparam"));
                        newparam.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = colladaName + "-sampler";
                        newparam.AppendChild(Doc.CreateElement("sampler2D"))
                            .AppendChild(Doc.CreateElement("source"))
                            .InnerText = colladaName + "-surface";
                    }

                    var t = profile.AppendChild(Doc.CreateElement("technique"));
                    t.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = "common";
                    var phong = t.AppendChild(Doc.CreateElement("phong"));

                    var diffuse = phong.AppendChild(Doc.CreateElement("diffuse"));
                    // Only one <color> or <texture> can appear inside diffuse element
                    if (colladaName != null)
                    {
                        var txtr = diffuse.AppendChild(Doc.CreateElement("texture"));
                        txtr.Attributes.Append(Doc.CreateAttribute("texture")).InnerText = colladaName + "-sampler";
                        txtr.Attributes.Append(Doc.CreateAttribute("texcoord")).InnerText = colladaName;
                    }
                    else
                    {
                        var diffuseColor = diffuse.AppendChild(Doc.CreateElement("color"));
                        diffuseColor.Attributes.Append(Doc.CreateAttribute("sid")).InnerText = "diffuse";
                        diffuseColor.InnerText = string.Format("{0} {1} {2} {3}",
                            color.R.ToString(Utils.EnUsCulture),
                            color.G.ToString(Utils.EnUsCulture),
                            color.B.ToString(Utils.EnUsCulture),
                            color.A.ToString(Utils.EnUsCulture));
                    }

                    phong.AppendChild(Doc.CreateElement("transparency"))
                        .AppendChild(Doc.CreateElement("float"))
                        .InnerText = color.A.ToString(Utils.EnUsCulture);
                }

                return true;
            };

            int prim_nr = 0;
            foreach (var obj in facetedMeshSet)
            {
                int total_num_vertices = 0;
                string name = string.Format("prim{0}", prim_nr++);
                string geomID = name;

                var geom = geomLib.AppendChild(Doc.CreateElement("geometry"));
                geom.Attributes.Append(Doc.CreateAttribute("id")).InnerText = string.Format("{0}-{1}", geomID, "mesh");
                var mesh = geom.AppendChild(Doc.CreateElement("mesh"));

                List<float> position_data = new List<float>();
                List<float> normal_data = new List<float>();
                List<float> uv_data = new List<float>();

                int num_faces = obj.Faces.Count;

                for (int face_num = 0; face_num < num_faces; face_num++)
                {
                    var face = obj.Faces[face_num];
                    total_num_vertices += face.Vertices.Count;

                    foreach (var v in face.Vertices)
                    {
                        position_data.Add(v.Position.X);
                        position_data.Add(v.Position.Y);
                        position_data.Add(v.Position.Z);

                        normal_data.Add(v.Normal.X);
                        normal_data.Add(v.Normal.Y);
                        normal_data.Add(v.Normal.Z);

                        uv_data.Add(v.TexCoord.X);
                        uv_data.Add(v.TexCoord.Y);
                    }
                }

                addSource.Invoke(mesh, string.Format("{0}-{1}", geomID, "positions"), "XYZ", position_data);
                addSource.Invoke(mesh, string.Format("{0}-{1}", geomID, "normals"), "XYZ", normal_data);
                addSource.Invoke(mesh, string.Format("{0}-{1}", geomID, "map0"), "ST", uv_data);

                // Add the <vertices> element
                {
                    var verticesNode = mesh.AppendChild(Doc.CreateElement("vertices"));
                    verticesNode.Attributes.Append(Doc.CreateAttribute("id")).InnerText = string.Format("{0}-{1}",
                        geomID, "vertices");
                    var verticesInput = verticesNode.AppendChild(Doc.CreateElement("input"));
                    verticesInput.Attributes.Append(Doc.CreateAttribute("semantic")).InnerText = "POSITION";
                    verticesInput.Attributes.Append(Doc.CreateAttribute("source")).InnerText = string.Format(
                        "#{0}-{1}", geomID, "positions");
                }

                var objMaterials = getMaterials.Invoke(obj);

                // Add triangles
                foreach (var objMaterial in objMaterials)
                {
                    addPolygons.Invoke(mesh, geomID, objMaterial.Name + "-material", obj,
                        getFacesWithMaterial.Invoke(obj, objMaterial));
                }

                var node = scene.AppendChild(Doc.CreateElement("node"));
                node.Attributes.Append(Doc.CreateAttribute("type")).InnerText = "NODE";
                node.Attributes.Append(Doc.CreateAttribute("id")).InnerText = geomID;
                node.Attributes.Append(Doc.CreateAttribute("name")).InnerText = geomID;

                // Set tranform matrix (node position, rotation and scale)
                var matrix = node.AppendChild(Doc.CreateElement("matrix"));

                var srt = createSRTMatrix.Invoke(obj.Prim.Scale, obj.Prim.Rotation, obj.Prim.Position);
                string matrixVal = string.Empty;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        matrixVal += srt[j*4 + i].ToString(Utils.EnUsCulture) + " ";
                    }
                }
                matrix.InnerText = matrixVal.TrimEnd();

                // Geometry of the node
                var nodeGeometry = node.AppendChild(Doc.CreateElement("instance_geometry"));

                // Bind materials
                var tq = nodeGeometry.AppendChild(Doc.CreateElement("bind_material"))
                    .AppendChild(Doc.CreateElement("technique_common"));
                foreach (var objMaterial in objMaterials)
                {
                    var instanceMaterial = tq.AppendChild(Doc.CreateElement("instance_material"));
                    instanceMaterial.Attributes.Append(Doc.CreateAttribute("symbol")).InnerText =
                        string.Format("{0}-{1}", objMaterial.Name, "material");
                    instanceMaterial.Attributes.Append(Doc.CreateAttribute("target")).InnerText =
                        string.Format("#{0}-{1}", objMaterial.Name, "material");
                }

                nodeGeometry.Attributes.Append(Doc.CreateAttribute("url")).InnerText = string.Format("#{0}-{1}", geomID,
                    "mesh");
            }

            generateEffects.Invoke(effects);

            // Materials
            foreach (var objMaterial in AllMeterials)
            {
                var mat = materials.AppendChild(Doc.CreateElement("material"));
                mat.Attributes.Append(Doc.CreateAttribute("id")).InnerText = objMaterial.Name + "-material";
                var matEffect = mat.AppendChild(Doc.CreateElement("instance_effect"));
                matEffect.Attributes.Append(Doc.CreateAttribute("url")).InnerText = string.Format("#{0}-{1}",
                    objMaterial.Name, "fx");
            }

            root.AppendChild(Doc.CreateElement("scene"))
                .AppendChild(Doc.CreateElement("instance_visual_scene"))
                .Attributes.Append(Doc.CreateAttribute("url")).InnerText = "#Scene";

            return Doc;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches all the avatars in-range.
        /// </summary>
        /// <param name="range">the range to extend or contract to</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>the avatars in range</returns>
        private static IEnumerable<Avatar> GetAvatars(float range, uint millisecondsTimeout, uint dataTimeout)
        {
            switch (Client.Self.Movement.Camera.Far < range)
            {
                case true:
                    IEnumerable<Avatar> avatars;
                    wasAdaptiveAlarm RangeUpdateAlarm = new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
                    EventHandler<AvatarUpdateEventArgs> AvatarUpdateEventHandler =
                        (sender, args) =>
                        {
                            // ignore if this is not a new avatar being added
                            if (!args.IsNew) return;
                            RangeUpdateAlarm.Alarm(dataTimeout);
                        };
                    lock (ClientInstanceObjectsLock)
                    {
                        Client.Objects.AvatarUpdate += AvatarUpdateEventHandler;
                        lock (ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = range;
                        }
                        RangeUpdateAlarm.Alarm(dataTimeout);
                        RangeUpdateAlarm.Signal.WaitOne((int) millisecondsTimeout, false);
                        avatars =
                            Client.Network.Simulators.AsParallel().Select(o => o.ObjectsAvatars)
                                .Select(o => o.Copy().Values)
                                .SelectMany(o => o);
                        lock (ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = corradeConfiguration.Range;
                        }
                        Client.Objects.AvatarUpdate -= AvatarUpdateEventHandler;
                    }
                    return avatars;
                default:
                    return Client.Network.Simulators.AsParallel().Select(o => o.ObjectsAvatars)
                        .Select(o => o.Copy().Values)
                        .SelectMany(o => o);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches all the primitives in-range.
        /// </summary>
        /// <param name="range">the range to extend or contract to</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        /// <param name="dataTimeout">the data timeout in milliseconds</param>
        /// <returns>the primitives in range</returns>
        private static IEnumerable<Primitive> GetPrimitives(float range, uint millisecondsTimeout, uint dataTimeout)
        {
            switch (Client.Self.Movement.Camera.Far < range)
            {
                case true:
                    IEnumerable<Primitive> primitives;
                    wasAdaptiveAlarm RangeUpdateAlarm = new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
                    EventHandler<PrimEventArgs> ObjectUpdateEventHandler =
                        (sender, args) =>
                        {
                            // ignore if this is not a new primitive being added
                            if (!args.IsNew) return;
                            RangeUpdateAlarm.Alarm(dataTimeout);
                        };
                    lock (ClientInstanceObjectsLock)
                    {
                        Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                        lock (ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = range;
                        }
                        RangeUpdateAlarm.Alarm(dataTimeout);
                        RangeUpdateAlarm.Signal.WaitOne((int) millisecondsTimeout, false);
                        primitives =
                            Client.Network.Simulators.AsParallel().Select(o => o.ObjectsPrimitives)
                                .Select(o => o.Copy().Values).ToList()
                                .SelectMany(o => o);
                        lock (ClientInstanceConfigurationLock)
                        {
                            Client.Self.Movement.Camera.Far = corradeConfiguration.Range;
                        }
                        Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                    }
                    return primitives;
                default:
                    return Client.Network.Simulators.AsParallel().Select(o => o.ObjectsPrimitives)
                        .Select(o => o.Copy().Values).ToList()
                        .SelectMany(o => o);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates a set of primitives by scanning their properties.
        /// </summary>
        /// <param name="primitives">a list of primitives to update</param>
        /// <param name="dataTimeout">the timeout for receiving data from the grid</param>
        /// <returns>a list of updated primitives</returns>
        private static bool UpdatePrimitives(ref HashSet<Primitive> primitives, uint dataTimeout)
        {
            HashSet<Primitive> scansPrimitives = new HashSet<Primitive>(primitives);
            HashSet<Primitive> localPrimitives = new HashSet<Primitive>();
            Dictionary<UUID, ManualResetEvent> primitiveEvents =
                new Dictionary<UUID, ManualResetEvent>(
                    scansPrimitives
                        .AsParallel().ToDictionary(o => o.ID, p => new ManualResetEvent(false)));
            Dictionary<UUID, Stopwatch> stopWatch = new Dictionary<UUID, Stopwatch>(
                scansPrimitives
                    .AsParallel().ToDictionary(o => o.ID, p => new Stopwatch()));
            HashSet<long> times = new HashSet<long>(new[] {(long) dataTimeout});
            object LockObject = new object();
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler = (sender, args) =>
            {
                KeyValuePair<UUID, ManualResetEvent> queueElement =
                    primitiveEvents.AsParallel().FirstOrDefault(o => o.Key.Equals(args.Properties.ObjectID));
                switch (!queueElement.Equals(default(KeyValuePair<UUID, ManualResetEvent>)))
                {
                    case true:
                        Primitive updatedPrimitive =
                            scansPrimitives.AsParallel().FirstOrDefault(o => o.ID.Equals(args.Properties.ObjectID));
                        if (updatedPrimitive == null) return;
                        updatedPrimitive.Properties = args.Properties;
                        lock (LockObject)
                        {
                            localPrimitives.Add(updatedPrimitive);
                            stopWatch[queueElement.Key].Stop();
                            times.Add(stopWatch[queueElement.Key].ElapsedMilliseconds);
                            queueElement.Value.Set();
                        }
                        break;
                }
            };
            lock (ClientInstanceObjectsLock)
            {
                Parallel.ForEach(primitiveEvents, o =>
                {
                    Primitive queryPrimitive =
                        scansPrimitives.AsParallel().SingleOrDefault(p => p.ID.Equals(o.Key));
                    if (queryPrimitive == null) return;
                    lock (LockObject)
                    {
                        stopWatch[queryPrimitive.ID].Start();
                    }
                    Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                    Client.Objects.SelectObject(
                        Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(p => p.Handle.Equals(queryPrimitive.RegionHandle)),
                        queryPrimitive.LocalID,
                        true);
                    ManualResetEvent primitiveEvent;
                    int averageTime;
                    lock (LockObject)
                    {
                        primitiveEvent = primitiveEvents[queryPrimitive.ID];
                        averageTime = (int) times.Average();
                    }
                    primitiveEvent.WaitOne(averageTime != 0 ? averageTime : (int) dataTimeout, false);
                    Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
                });
            }
            if (!scansPrimitives.Count.Equals(localPrimitives.Count))
                return false;
            primitives = localPrimitives;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates a set of avatars by scanning their profile data.
        /// </summary>
        /// <param name="avatars">a list of avatars to update</param>
        /// <param name="millisecondsTimeout">the amount of time in milliseconds to timeout</param>
        /// <param name="dataTimeout">the data timeout</param>
        /// <returns>true if any avatars were updated</returns>
        private static bool UpdateAvatars(ref HashSet<Avatar> avatars, uint millisecondsTimeout,
            uint dataTimeout)
        {
            HashSet<Avatar> scansAvatars = new HashSet<Avatar>(avatars);
            Dictionary<UUID, wasAdaptiveAlarm> avatarAlarms =
                new Dictionary<UUID, wasAdaptiveAlarm>(scansAvatars.AsParallel()
                    .ToDictionary(o => o.ID, p => new wasAdaptiveAlarm(corradeConfiguration.DataDecayType)));
            Dictionary<UUID, Avatar> avatarUpdates = new Dictionary<UUID, Avatar>(scansAvatars.AsParallel()
                .ToDictionary(o => o.ID, p => p));
            object LockObject = new object();
            EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReplyEventHandler = (sender, args) =>
            {
                lock (LockObject)
                {
                    avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    avatarUpdates[args.AvatarID].ProfileInterests = args.Interests;
                }
            };
            EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesReplyEventHandler =
                (sender, args) =>
                {
                    lock (LockObject)
                    {
                        avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                        avatarUpdates[args.AvatarID].ProfileProperties = args.Properties;
                    }
                };
            EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
            {
                lock (LockObject)
                {
                    avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    avatarUpdates[args.AvatarID].Groups.AddRange(args.Groups.Select(o => o.GroupID));
                }
            };
            EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReplyEventHandler =
                (sender, args) =>
                {
                    lock (LockObject)
                    {
                        avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    }
                };
            EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedReplyEventHandler =
                (sender, args) =>
                {
                    lock (LockObject)
                    {
                        avatarAlarms[args.AvatarID].Alarm(dataTimeout);
                    }
                };
            lock (ClientInstanceAvatarsLock)
            {
                Parallel.ForEach(scansAvatars, o =>
                {
                    Client.Avatars.AvatarInterestsReply += AvatarInterestsReplyEventHandler;
                    Client.Avatars.AvatarPropertiesReply += AvatarPropertiesReplyEventHandler;
                    Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                    Client.Avatars.AvatarPicksReply += AvatarPicksReplyEventHandler;
                    Client.Avatars.AvatarClassifiedReply += AvatarClassifiedReplyEventHandler;
                    Client.Avatars.RequestAvatarProperties(o.ID);
                    Client.Avatars.RequestAvatarPicks(o.ID);
                    Client.Avatars.RequestAvatarClassified(o.ID);
                    wasAdaptiveAlarm avatarAlarm;
                    lock (LockObject)
                    {
                        avatarAlarm = avatarAlarms[o.ID];
                    }
                    avatarAlarm.Signal.WaitOne((int) millisecondsTimeout, false);
                    Client.Avatars.AvatarInterestsReply -= AvatarInterestsReplyEventHandler;
                    Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesReplyEventHandler;
                    Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                    Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                    Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedReplyEventHandler;
                });
            }

            switch (
                avatarUpdates.Values.AsParallel()
                    .Any(
                        o =>
                            o != null && !o.ProfileInterests.Equals(default(Avatar.Interests)) &&
                            !o.ProfileProperties.Equals(default(Avatar.AvatarProperties))))
            {
                case true:
                    avatars = new HashSet<Avatar>(avatarUpdates.Values);
                    return true;
                default:
                    return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Requests the UUIDs of all the current groups.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groups">a hashset where to store the UUIDs</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool directGetCurrentGroups(uint millisecondsTimeout, ref IEnumerable<UUID> groups)
        {
            ManualResetEvent CurrentGroupsReceivedEvent = new ManualResetEvent(false);
            Dictionary<UUID, OpenMetaverse.Group> currentGroups = null;
            EventHandler<CurrentGroupsEventArgs> CurrentGroupsEventHandler = (sender, args) =>
            {
                currentGroups = args.Groups;
                CurrentGroupsReceivedEvent.Set();
            };
            Client.Groups.CurrentGroups += CurrentGroupsEventHandler;
            Client.Groups.RequestCurrentGroups();
            if (!CurrentGroupsReceivedEvent.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Groups.CurrentGroups -= CurrentGroupsEventHandler;
                return false;
            }
            Client.Groups.CurrentGroups -= CurrentGroupsEventHandler;
            switch (currentGroups.Any())
            {
                case true:
                    groups = currentGroups.Keys;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     A wrapper for retrieveing all the current groups that implements caching.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groups">a hashset where to store the UUIDs</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool GetCurrentGroups(uint millisecondsTimeout, ref IEnumerable<UUID> groups)
        {
            if (Cache.CurrentGroupsCache.Any())
            {
                groups = Cache.CurrentGroupsCache;
                return true;
            }
            bool succeeded;
            lock (ClientInstanceGroupsLock)
            {
                succeeded = directGetCurrentGroups(millisecondsTimeout, ref groups);
            }
            if (succeeded)
            {
                Cache.CurrentGroupsCache = new HashSet<UUID>(groups);
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Requests the UUIDs of all the current groups.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="mutes">an enumerable where to store mute entries</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool directGetMutes(uint millisecondsTimeout, ref IEnumerable<MuteEntry> mutes)
        {
            ManualResetEvent MuteListUpdatedEvent = new ManualResetEvent(false);
            EventHandler<EventArgs> MuteListUpdatedEventHandler =
                (sender, args) => MuteListUpdatedEvent.Set();
            lock (ClientInstanceSelfLock)
            {
                Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                Client.Self.RequestMuteList();
                MuteListUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
            }
            mutes = Client.Self.MuteList.Copy().Values;
            return true;
        }

        /// <summary>
        ///     A wrapper for retrieveing all the current groups that implements caching.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="mutes">an enumerable where to store mute entries</param>
        /// <returns>true if the current groups could be fetched</returns>
        private static bool GetMutes(uint millisecondsTimeout, ref IEnumerable<MuteEntry> mutes)
        {
            if (Cache.MutesCache != null)
            {
                mutes = Cache.MutesCache;
                return true;
            }
            bool succeeded;
            lock (ClientInstanceSelfLock)
            {
                succeeded = directGetMutes(millisecondsTimeout, ref mutes);
            }
            if (succeeded)
            {
                switch (Cache.MutesCache != null)
                {
                    case true:
                        Cache.MutesCache.UnionWith(mutes);
                        break;
                    default:
                        Cache.MutesCache = new HashSet<MuteEntry>(mutes);
                        break;
                }
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get all worn attachments.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">the alarm timeout for receiving object properties</param>
        /// <returns>attachment points by primitives</returns>
        private static IEnumerable<KeyValuePair<Primitive, AttachmentPoint>> GetAttachments(
            uint millisecondsTimeout, uint dataTimeout)
        {
            HashSet<Primitive> primitives;
            lock (ClientInstanceNetworkLock)
            {
                primitives =
                    new HashSet<Primitive>(Client.Network.CurrentSim.ObjectsPrimitives.FindAll(
                        o => o.ParentID.Equals(Client.Self.LocalID)));
            }
            Dictionary<UUID, uint> primitiveQueue = primitives.ToDictionary(o => o.ID, o => o.LocalID);
            object LockObject = new object();
            wasAdaptiveAlarm ObjectPropertiesAlarm = new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
            EventHandler<ObjectPropertiesEventArgs> ObjectPropertiesEventHandler = (sender, args) =>
            {
                ObjectPropertiesAlarm.Alarm(dataTimeout);
                lock (LockObject)
                {
                    primitiveQueue.Remove(args.Properties.ObjectID);
                    if (!primitiveQueue.Any()) ObjectPropertiesAlarm.Signal.Set();
                }
            };
            lock (ClientInstanceObjectsLock)
            {
                Client.Objects.ObjectProperties += ObjectPropertiesEventHandler;
                Client.Objects.SelectObjects(Client.Network.CurrentSim, primitiveQueue.Values.ToArray(),
                    true);
                ObjectPropertiesAlarm.Signal.WaitOne((int) millisecondsTimeout, false);
                Client.Objects.ObjectProperties -= ObjectPropertiesEventHandler;
            }
            return primitives
                .AsParallel()
                .Select(
                    o =>
                        new KeyValuePair<Primitive, AttachmentPoint>(o,
                            (AttachmentPoint) (((o.PrimData.State & 0xF0) >> 4) |
                                               ((o.PrimData.State & ~0xF0) << 4))));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the inventory wearables that are currently being worn.
        /// </summary>
        /// <param name="root">the folder to start the search from</param>
        /// <returns>key value pairs of wearables by name</returns>
        private static IEnumerable<KeyValuePair<AppearanceManager.WearableData, WearableType>> GetWearables(
            InventoryNode root)
        {
            InventoryFolder inventoryFolder = Client.Inventory.Store[root.Data.UUID] as InventoryFolder;
            if (inventoryFolder == null)
            {
                InventoryItem inventoryItem = Client.Inventory.Store[root.Data.UUID] as InventoryItem;
                if (inventoryItem != null)
                {
                    WearableType wearableType = Client.Appearance.IsItemWorn(inventoryItem);
                    if (!wearableType.Equals(WearableType.Invalid))
                    {
                        foreach (
                            KeyValuePair<WearableType, AppearanceManager.WearableData> wearable in
                                Client.Appearance.GetWearables()
                                    .AsParallel().Where(o => o.Value.ItemID.Equals(inventoryItem.UUID)))
                        {
                            yield return
                                new KeyValuePair<AppearanceManager.WearableData, WearableType>(wearable.Value,
                                    wearable.Key);
                        }
                    }
                    yield break;
                }
            }
            foreach (
                KeyValuePair<AppearanceManager.WearableData, WearableType> item in
                    root.Nodes.Values.AsParallel().SelectMany(GetWearables))
            {
                yield return item;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// ///
        /// <summary>
        ///     Fetches items by searching the inventory starting with an inventory
        ///     node where the search criteria finds:
        ///     - name as string
        ///     - name as Regex
        ///     - UUID as UUID
        /// </summary>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">the name, UUID or Regex of the item to be found</param>
        /// <returns>a list of items matching the item name</returns>
        private static IEnumerable<T> FindInventory<T>(InventoryNode root, object criteria)
        {
            if ((criteria is Regex && (criteria as Regex).IsMatch(root.Data.Name)) ||
                (criteria is string &&
                 (criteria as string).Equals(root.Data.Name, StringComparison.Ordinal)) ||
                (criteria is UUID &&
                 (criteria.Equals(root.Data.UUID) ||
                  (Client.Inventory.Store[root.Data.UUID] is InventoryItem &&
                   (Client.Inventory.Store[root.Data.UUID] as InventoryItem).AssetUUID.Equals(criteria)))))
            {
                if (typeof (T) == typeof (InventoryNode))
                {
                    yield return (T) (object) root;
                }
                if (typeof (T) == typeof (InventoryBase))
                {
                    yield return (T) (object) Client.Inventory.Store[root.Data.UUID];
                }
            }
            foreach (T item in root.Nodes.Values.AsParallel().SelectMany(node => FindInventory<T>(node, criteria)))
            {
                yield return item;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// ///
        /// <summary>
        ///     Fetches items and their full path from the inventory starting with
        ///     an inventory node where the search criteria finds:
        ///     - name as string
        ///     - name as Regex
        ///     - UUID as UUID
        /// </summary>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">the name, UUID or Regex of the item to be found</param>
        /// <param name="prefix">any prefix to append to the found paths</param>
        /// <returns>items matching criteria and their full inventoy path</returns>
        private static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(
            InventoryNode root, object criteria, LinkedList<string> prefix)
        {
            if ((criteria is Regex && (criteria as Regex).IsMatch(root.Data.Name)) ||
                (criteria is string &&
                 (criteria as string).Equals(root.Data.Name, StringComparison.Ordinal)) ||
                (criteria is UUID &&
                 (criteria.Equals(root.Data.UUID) ||
                  (Client.Inventory.Store[root.Data.UUID] is InventoryItem &&
                   (Client.Inventory.Store[root.Data.UUID] as InventoryItem).AssetUUID.Equals(criteria)))))
            {
                if (typeof (T) == typeof (InventoryBase))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) Client.Inventory.Store[root.Data.UUID],
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
                if (typeof (T) == typeof (InventoryNode))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) root,
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
            }
            foreach (
                KeyValuePair<T, LinkedList<string>> o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => FindInventoryPath<T>(o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] {root.Data.Name})))))
            {
                yield return o;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets all the items from an inventory folder and returns the items.
        /// </summary>
        /// <param name="rootFolder">a folder from which to search</param>
        /// <param name="folder">the folder to search for</param>
        /// <returns>a list of items from the folder</returns>
        private static IEnumerable<T> GetInventoryFolderContents<T>(InventoryNode rootFolder,
            string folder)
        {
            foreach (
                InventoryNode node in
                    rootFolder.Nodes.Values.AsParallel()
                        .Where(
                            node =>
                                node.Data is InventoryFolder && node.Data.Name.Equals(folder, StringComparison.Ordinal))
                )
            {
                foreach (InventoryNode item in node.Nodes.Values)
                {
                    if (typeof (T) == typeof (InventoryNode))
                    {
                        yield return (T) (object) item;
                    }
                    if (typeof (T) == typeof (InventoryBase))
                    {
                        yield return (T) (object) Client.Inventory.Store[item.Data.UUID];
                    }
                }
                break;
            }
        }

        /// <summary>
        ///     Posts messages to console or log-files.
        /// </summary>
        /// <param name="messages">a list of messages</param>
        private static void Feedback(params string[] messages)
        {
            CorradeThreadPool[CorradeThreadType.LOG].Spawn(
                () =>
                {
                    List<string> output = new List<string>
                    {
                        !string.IsNullOrEmpty(InstalledServiceName)
                            ? InstalledServiceName
                            : CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME,
                        string.Format(Utils.EnUsCulture, "[{0}]",
                            DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                Utils.EnUsCulture.DateTimeFormat))
                    };

                    output.AddRange(messages.Select(message => message));

                    // Attempt to write to log file,
                    if (corradeConfiguration.ClientLogEnabled)
                    {
                        try
                        {
                            lock (ClientLogFileLock)
                            {
                                using (
                                    StreamWriter logWriter =
                                        new StreamWriter(corradeConfiguration.ClientLogFile, true, Encoding.UTF8))
                                {
                                    logWriter.WriteLine(string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR, output.ToArray()));
                                    //logWriter.Flush();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // or fail and append the fail message.
                            output.Add(string.Format(Utils.EnUsCulture, "{0} {1}",
                                wasGetDescriptionFromEnumValue(
                                    ConsoleError.COULD_NOT_WRITE_TO_CLIENT_LOG_FILE),
                                ex.Message));
                        }
                    }

                    if (!Environment.UserInteractive)
                    {
                        switch (Environment.OSVersion.Platform)
                        {
                            case PlatformID.Win32NT:
                                CorradeEventLog.WriteEntry(
                                    string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR, output.ToArray()),
                                    EventLogEntryType.Information);
                                break;
                        }
                        return;
                    }
                    Console.WriteLine(string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR, output.ToArray()));
                },
                corradeConfiguration.MaximumLogThreads);
        }

        /// <summary>
        ///     Posts messages to console or log-files.
        /// </summary>
        /// <param name="multiline">whether to treat the messages as separate lines</param>
        /// <param name="messages">a list of messages</param>
        private static void Feedback(bool multiline, params string[] messages)
        {
            if (!multiline)
            {
                Feedback(messages);
                return;
            }
            CorradeThreadPool[CorradeThreadType.LOG].Spawn(
                () =>
                {
                    List<string> output =
                        new List<string>(
                            messages.Select(
                                o => string.Format(Utils.EnUsCulture, "{0}{1}[{2}]{3}{4}",
                                    !string.IsNullOrEmpty(InstalledServiceName)
                                        ? InstalledServiceName
                                        : CORRADE_CONSTANTS.DEFAULT_SERVICE_NAME, CORRADE_CONSTANTS.ERROR_SEPARATOR,
                                    DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                        Utils.EnUsCulture.DateTimeFormat),
                                    CORRADE_CONSTANTS.ERROR_SEPARATOR,
                                    o)));

                    // Attempt to write to log file,
                    if (corradeConfiguration.ClientLogEnabled)
                    {
                        try
                        {
                            lock (ClientLogFileLock)
                            {
                                using (
                                    StreamWriter logWriter =
                                        new StreamWriter(corradeConfiguration.ClientLogFile, true, Encoding.UTF8))
                                {
                                    foreach (string message in output)
                                    {
                                        logWriter.WriteLine(message);
                                    }
                                    //logWriter.Flush();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // or fail and append the fail message.
                            output.Add(string.Format(Utils.EnUsCulture, "{0} {1}",
                                wasGetDescriptionFromEnumValue(
                                    ConsoleError.COULD_NOT_WRITE_TO_CLIENT_LOG_FILE),
                                ex.Message));
                        }
                    }

                    if (!Environment.UserInteractive)
                    {
                        switch (Environment.OSVersion.Platform)
                        {
                            case PlatformID.Win32NT:
                                foreach (string message in output)
                                {
                                    CorradeEventLog.WriteEntry(message, EventLogEntryType.Information);
                                }
                                break;
                        }
                        return;
                    }

                    foreach (string message in output)
                    {
                        Console.WriteLine(message);
                    }
                },
                corradeConfiguration.MaximumLogThreads);
        }

        public static int Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Any())
                {
                    string action = string.Empty;
                    for (int i = 0; i < args.Length; ++i)
                    {
                        switch (args[i].ToUpper())
                        {
                            case "/INSTALL":
                                action = "INSTALL";
                                break;
                            case "/UNINSTALL":
                                action = "UNINSTALL";
                                break;
                            case "/NAME":
                                if (args.Length > i + 1)
                                {
                                    InstalledServiceName = args[++i];
                                }
                                break;
                        }
                    }

                    switch (action)
                    {
                        case "INSTALL":
                            return InstallService();
                        case "UNINSTALL":
                            return UninstallService();
                    }
                }
                // run interactively and log to console
                Corrade corrade = new Corrade();
                corrade.OnStart(null);
                return 0;
            }

            // run as a standard service
            Run(new Corrade());
            return 0;
        }

        private static int InstallService()
        {
            try
            {
                // install the service with the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new[] {Assembly.GetExecutingAssembly().Location});
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.GetType() == typeof (Win32Exception))
                {
                    Win32Exception we = (Win32Exception) ex.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service already installed!", we.ErrorCode);
                    return we.ErrorCode;
                }
                Console.WriteLine(ex.ToString());
                return -1;
            }

            return 0;
        }

        private static int UninstallService()
        {
            try
            {
                // uninstall the service from the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new[] {"/u", Assembly.GetExecutingAssembly().Location});
            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof (Win32Exception))
                {
                    Win32Exception we = (Win32Exception) ex.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service not installed!", we.ErrorCode);
                    return we.ErrorCode;
                }
                Console.WriteLine(ex.ToString());
                return -1;
            }

            return 0;
        }

        protected override void OnStop()
        {
            base.OnStop();
            ConnectionSemaphores['u'].Set();
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            //Debugger.Break();
            programThread = new Thread(new Corrade().Program);
            programThread.Start();
        }

        // Main entry point.
        public void Program()
        {
            // Set the current directory to the service directory.
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            // Load the configuration file.
            lock (ConfigurationFileLock)
            {
                corradeConfiguration.Load(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
            }
            // Write the logo.
            Feedback(true, CORRADE_CONSTANTS.LOGO.ToArray());
            // Branch on platform and set-up termination handlers.
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    if (Environment.UserInteractive)
                    {
                        // Setup console handler.
                        ConsoleEventHandler += ConsoleCtrlCheck;
                        NativeMethods.SetConsoleCtrlHandler(ConsoleEventHandler, true);
                        if (Environment.UserInteractive)
                        {
                            Console.CancelKeyPress +=
                                (sender, args) => ConnectionSemaphores['u'].Set();
                        }
                    }
                    break;
            }
            // Set-up watcher for dynamically reading the configuration file.
            FileSystemEventHandler HandleConfigurationFileChanged = null;
            try
            {
                ConfigurationWatcher.Path = Directory.GetCurrentDirectory();
                ConfigurationWatcher.Filter = CORRADE_CONSTANTS.CONFIGURATION_FILE;
                ConfigurationWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleConfigurationFileChanged = (sender, args) => ConfigurationChangedTimer.Change(1000, 0);
                ConfigurationWatcher.Changed += HandleConfigurationFileChanged;
                ConfigurationWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_SETTING_UP_CONFIGURATION_WATCHER), ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the notifications file.
            FileSystemEventHandler HandleNotificationsFileChanged = null;
            try
            {
                NotificationsWatcher.Path = wasPathCombine(Directory.GetCurrentDirectory(),
                    CORRADE_CONSTANTS.STATE_DIRECTORY);
                NotificationsWatcher.Filter = CORRADE_CONSTANTS.NOTIFICATIONS_STATE_FILE;
                NotificationsWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleNotificationsFileChanged = (sender, args) => NotificationsChangedTimer.Change(1000, 0);
                NotificationsWatcher.Changed += HandleNotificationsFileChanged;
                NotificationsWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_SETTING_UP_NOTIFICATIONS_WATCHER), ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up watcher for dynamically reading the group schedules file.
            FileSystemEventHandler HandleGroupSchedulesFileChanged = null;
            try
            {
                SchedulesWatcher.Path = wasPathCombine(Directory.GetCurrentDirectory(),
                    CORRADE_CONSTANTS.STATE_DIRECTORY);
                SchedulesWatcher.Filter = CORRADE_CONSTANTS.GROUP_SCHEDULES_STATE_FILE;
                SchedulesWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleGroupSchedulesFileChanged = (sender, args) => GroupSchedulesChangedTimer.Change(1000, 0);
                SchedulesWatcher.Changed += HandleGroupSchedulesFileChanged;
                SchedulesWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_SETTING_UP_SCHEDULES_WATCHER), ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Set-up the AIML bot in case it has been enabled.
            FileSystemEventHandler HandleAIMLBotConfigurationChanged = null;
            try
            {
                AIMLBotConfigurationWatcher.Path = wasPathCombine(Directory.GetCurrentDirectory(),
                    AIML_BOT_CONSTANTS.DIRECTORY);
                AIMLBotConfigurationWatcher.NotifyFilter = NotifyFilters.LastWrite;
                HandleAIMLBotConfigurationChanged = (sender, args) => AIMLConfigurationChangedTimer.Change(1000, 0);
                AIMLBotConfigurationWatcher.Changed += HandleAIMLBotConfigurationChanged;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_SETTING_UP_AIML_CONFIGURATION_WATCHER),
                    ex.Message);
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Suppress standard OpenMetaverse logs, we have better ones.
            Settings.LOG_LEVEL = Helpers.LogLevel.None;
            Client.Settings.ALWAYS_REQUEST_PARCEL_ACL = true;
            Client.Settings.ALWAYS_DECODE_OBJECTS = true;
            Client.Settings.ALWAYS_REQUEST_OBJECTS = true;
            Client.Settings.SEND_AGENT_APPEARANCE = true;
            Client.Settings.AVATAR_TRACKING = true;
            Client.Settings.OBJECT_TRACKING = true;
            Client.Settings.PARCEL_TRACKING = true;
            Client.Settings.ALWAYS_REQUEST_PARCEL_DWELL = true;
            Client.Settings.ALWAYS_REQUEST_PARCEL_ACL = true;
            Client.Settings.SEND_AGENT_UPDATES = true;
            // Smoother movement for autopilot.
            Client.Settings.DISABLE_AGENT_UPDATE_DUPLICATE_CHECK = true;
            Client.Settings.ENABLE_CAPS = true;
            // Set the asset cache directory.
            Client.Settings.ASSET_CACHE_DIR = Path.Combine(CORRADE_CONSTANTS.CACHE_DIRECTORY,
                CORRADE_CONSTANTS.ASSET_CACHE_DIRECTORY);
            Client.Settings.USE_ASSET_CACHE = true;
            Client.Assets.Cache.AutoPruneEnabled = false;
            // More precision for object and avatar tracking updates.
            Client.Settings.USE_INTERPOLATION_TIMER = true;
            Client.Settings.FETCH_MISSING_INVENTORY = true;
            Client.Settings.HTTP_INVENTORY = true;
            Settings.SORT_INVENTORY = true;
            // Transfer textures over HTTP if possible.
            Client.Settings.USE_HTTP_TEXTURES = true;
            // Needed for commands dealing with terrain height.
            Client.Settings.STORE_LAND_PATCHES = true;
            // Decode simulator statistics.
            Client.Settings.ENABLE_SIMSTATS = true;
            // Send pings for lag measurement.
            Client.Settings.SEND_PINGS = true;
            // Throttling.
            Client.Settings.THROTTLE_OUTGOING_PACKETS = false;
            Client.Settings.SEND_AGENT_THROTTLE = true;
            // Enable multiple simulators
            Client.Settings.MULTIPLE_SIMS = true;
            // Check TOS
            if (!corradeConfiguration.TOSAccepted)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TOS_NOT_ACCEPTED));
                Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
            }
            // Proceed to log-in.
            LoginParams login = new LoginParams(
                Client,
                corradeConfiguration.FirstName,
                corradeConfiguration.LastName,
                corradeConfiguration.Password,
                CORRADE_CONSTANTS.CLIENT_CHANNEL,
                CORRADE_CONSTANTS.CORRADE_VERSION.ToString(Utils.EnUsCulture),
                corradeConfiguration.LoginURL)
            {
                Author = CORRADE_CONSTANTS.WIZARDRY_AND_STEAMWORKS,
                AgreeToTos = corradeConfiguration.TOSAccepted,
                Start = corradeConfiguration.StartLocation,
                UserAgent = CORRADE_CONSTANTS.USER_AGENT
            };
            // Set the outgoing IP address if specified in the configuration file.
            if (!string.IsNullOrEmpty(corradeConfiguration.BindIPAddress))
            {
                try
                {
                    Settings.BIND_ADDR = IPAddress.Parse(corradeConfiguration.BindIPAddress);
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNKNOWN_IP_ADDRESS), ex.Message);
                    Environment.Exit(corradeConfiguration.ExitCodeAbnormal);
                }
            }
            // Set the ID0 if specified in the configuration file.
            if (!string.IsNullOrEmpty(corradeConfiguration.DriveIdentifierHash))
            {
                login.ID0 = Utils.MD5String(corradeConfiguration.DriveIdentifierHash);
            }
            // Set the MAC if specified in the configuration file.
            if (!string.IsNullOrEmpty(corradeConfiguration.NetworkCardMAC))
            {
                login.MAC = Utils.MD5String(corradeConfiguration.NetworkCardMAC);
            }
            // Load Corrade caches.
            LoadCorradeCache.Invoke();
            // Load group members.
            LoadGroupMembersState.Invoke();
            // Load notification state.
            LoadNotificationState.Invoke();
            // Load group scheduls state.
            LoadGroupSchedulesState.Invoke();
            // Load movement state.
            LoadMovementState.Invoke();
            // Start the callback thread to send callbacks.
            Thread CallbackThread = new Thread(() =>
            {
                do
                {
                    try
                    {
                        CallbackQueueElement callbackQueueElement = new CallbackQueueElement();
                        if (CallbackQueue.Dequeue((int) corradeConfiguration.CallbackThrottle, ref callbackQueueElement))
                        {
                            CorradeThreadPool[CorradeThreadType.POST].Spawn(
                                () => wasPOST(callbackQueueElement.URL, callbackQueueElement.message,
                                    corradeConfiguration.CallbackTimeout), corradeConfiguration.MaximumPOSTThreads);
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.CALLBACK_ERROR),
                            ex.Message);
                    }
                } while (runCallbackThread);
            })
            {IsBackground = true};
            CallbackThread.Start();
            // Start the notification thread for notifications.
            Thread NotificationThread = new Thread(() =>
            {
                do
                {
                    try
                    {
                        NotificationQueueElement notificationQueueElement = new NotificationQueueElement();
                        if (NotificationQueue.Dequeue((int) corradeConfiguration.NotificationThrottle,
                            ref notificationQueueElement))
                        {
                            CorradeThreadPool[CorradeThreadType.POST].Spawn(
                                () => wasPOST(notificationQueueElement.URL, notificationQueueElement.message,
                                    corradeConfiguration.NotificationTimeout), corradeConfiguration.MaximumPOSTThreads);
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.NOTIFICATION_ERROR),
                            ex.Message);
                    }
                } while (runNotificationThread);
            })
            {IsBackground = true};
            NotificationThread.Start();
            // Install non-dynamic global event handlers.
            Client.Inventory.InventoryObjectOffered += HandleInventoryObjectOffered;
            Client.Network.LoginProgress += HandleLoginProgress;
            Client.Appearance.AppearanceSet += HandleAppearanceSet;
            Client.Network.SimConnected += HandleSimulatorConnected;
            Client.Network.Disconnected += HandleDisconnected;
            Client.Network.SimDisconnected += HandleSimulatorDisconnected;
            Client.Network.EventQueueRunning += HandleEventQueueRunning;
            Client.Self.TeleportProgress += HandleTeleportProgress;
            Client.Self.ChatFromSimulator += HandleChatFromSimulator;
            Client.Groups.GroupJoinedReply += HandleGroupJoined;
            Client.Groups.GroupLeaveReply += HandleGroupLeave;
            // Each Instant Message is processed in its own thread.
            Client.Self.IM += (sender, args) => CorradeThreadPool[CorradeThreadType.INSTANT_MESSAGE].Spawn(
                () => HandleSelfIM(sender, args),
                corradeConfiguration.MaximumInstantMessageThreads);
            // Log-in to the grid.
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.LOGGING_IN));
            Client.Network.BeginLogin(login);
            /*
             * The main thread spins around waiting for the semaphores to become invalidated,
             * at which point Corrade will consider its connection to the grid severed and
             * will terminate.
             *
             */
            WaitHandle.WaitAny(ConnectionSemaphores.Values.Select(o => (WaitHandle) o).ToArray());
            // Now log-out.
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.LOGGING_OUT));
            // Uninstall all installed handlers
            Client.Self.IM -= HandleSelfIM;
            Client.Network.SimChanged -= HandleRadarObjects;
            Client.Objects.AvatarUpdate -= HandleAvatarUpdate;
            Client.Objects.ObjectUpdate -= HandleObjectUpdate;
            Client.Objects.KillObject -= HandleKillObject;
            Client.Self.LoadURL -= HandleLoadURL;
            Client.Self.ScriptControlChange -= HandleScriptControlChange;
            Client.Self.MoneyBalanceReply -= HandleMoneyBalance;
            Client.Network.SimChanged -= HandleSimChanged;
            Client.Self.RegionCrossed -= HandleRegionCrossed;
            Client.Self.MeanCollision -= HandleMeanCollision;
            Client.Avatars.ViewerEffectLookAt -= HandleViewerEffect;
            Client.Avatars.ViewerEffectPointAt -= HandleViewerEffect;
            Client.Avatars.ViewerEffect -= HandleViewerEffect;
            Client.Objects.TerseObjectUpdate -= HandleTerseObjectUpdate;
            Client.Self.ScriptDialog -= HandleScriptDialog;
            Client.Self.ChatFromSimulator -= HandleChatFromSimulator;
            Client.Self.MoneyBalance -= HandleMoneyBalance;
            Client.Self.AlertMessage -= HandleAlertMessage;
            Client.Self.ScriptQuestion -= HandleScriptQuestion;
            Client.Self.TeleportProgress -= HandleTeleportProgress;
            Client.Friends.FriendRightsUpdate -= HandleFriendRightsUpdate;
            Client.Friends.FriendOffline -= HandleFriendOnlineStatus;
            Client.Friends.FriendOnline -= HandleFriendOnlineStatus;
            Client.Friends.FriendshipResponse -= HandleFriendShipResponse;
            Client.Friends.FriendshipOffered -= HandleFriendshipOffered;
            Client.Network.EventQueueRunning -= HandleEventQueueRunning;
            Client.Network.SimDisconnected -= HandleSimulatorDisconnected;
            Client.Network.Disconnected -= HandleDisconnected;
            Client.Network.SimConnected -= HandleSimulatorConnected;
            Client.Appearance.AppearanceSet -= HandleAppearanceSet;
            Client.Network.LoginProgress -= HandleLoginProgress;
            Client.Inventory.InventoryObjectOffered -= HandleInventoryObjectOffered;
            // Save notification states.
            SaveNotificationState.Invoke();
            // Save group members.
            SaveGroupMembersState.Invoke();
            // Save group schedules.
            SaveGroupSchedulesState.Invoke();
            // Save movement state.
            SaveMovementState.Invoke();
            // Save Corrade caches.
            SaveCorradeCache.Invoke();
            // Stop the sphere effects expiration thread.
            runEffectsExpirationThread = false;
            if (EffectsExpirationThread != null)
            {
                try
                {
                    if (
                        (EffectsExpirationThread.ThreadState.Equals(ThreadState.Running) ||
                         EffectsExpirationThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                    {
                        if (!EffectsExpirationThread.Join(1000))
                        {
                            EffectsExpirationThread.Abort();
                            EffectsExpirationThread.Join();
                        }
                    }
                }
                catch (Exception)
                {
                    /* We are going down and we do not care. */
                }
                finally
                {
                    EffectsExpirationThread = null;
                }
            }
            // Stop the group member sweep thread.
            StopGroupMembershipSweepThread.Invoke();
            // Stop the notification thread.
            try
            {
                runNotificationThread = false;
                if (
                    (NotificationThread.ThreadState.Equals(ThreadState.Running) ||
                     NotificationThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                {
                    if (!NotificationThread.Join(1000))
                    {
                        NotificationThread.Abort();
                        NotificationThread.Join();
                    }
                }
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            finally
            {
                NotificationThread = null;
            }

            // Stop the callback thread.
            try
            {
                runCallbackThread = false;
                if (
                    (CallbackThread.ThreadState.Equals(ThreadState.Running) ||
                     CallbackThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                {
                    if (!CallbackThread.Join(1000))
                    {
                        CallbackThread.Abort();
                        CallbackThread.Join();
                    }
                }
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            finally
            {
                NotificationThread = null;
            }

            // Close HTTP server
            if (HttpListener.IsSupported && corradeConfiguration.EnableHTTPServer)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.STOPPING_HTTP_SERVER));
                runHTTPServer = false;
                try
                {
                    if (HTTPListenerThread != null)
                    {
                        HTTPListener.Stop();
                        if (
                            (HTTPListenerThread.ThreadState.Equals(ThreadState.Running) ||
                             HTTPListenerThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                        {
                            if (!HTTPListenerThread.Join(1000))
                            {
                                HTTPListenerThread.Abort();
                                HTTPListenerThread.Join();
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    /* We are going down and we do not care. */
                }
                finally
                {
                    HTTPListenerThread = null;
                }
            }
            // Reject any inventory that has not been accepted.
            lock (InventoryOffersLock)
            {
                Parallel.ForEach(InventoryOffers, o =>
                {
                    o.Key.Accept = false;
                    o.Value.Set();
                });
            }
            // Disable the configuration watcher.
            try
            {
                ConfigurationWatcher.EnableRaisingEvents = false;
                ConfigurationWatcher.Changed -= HandleConfigurationFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the notifications watcher.
            try
            {
                NotificationsWatcher.EnableRaisingEvents = false;
                NotificationsWatcher.Changed -= HandleNotificationsFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the group schedule watcher.
            try
            {
                SchedulesWatcher.EnableRaisingEvents = false;
                SchedulesWatcher.Changed -= HandleGroupSchedulesFileChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Disable the AIML bot configuration watcher.
            try
            {
                AIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                AIMLBotConfigurationWatcher.Changed -= HandleAIMLBotConfigurationChanged;
            }
            catch (Exception)
            {
                /* We are going down and we do not care. */
            }
            // Save the AIML user session.
            lock (AIMLBotLock)
            {
                if (AIMLBotBrainCompiled)
                {
                    SaveChatBotFiles.Invoke();
                }
            }
            // Logout
            if (Client.Network.Connected)
            {
                // Full speed ahead; do not even attempt to grab a lock.
                ManualResetEvent LoggedOutEvent = new ManualResetEvent(false);
                EventHandler<LoggedOutEventArgs> LoggedOutEventHandler = (sender, args) => LoggedOutEvent.Set();
                Client.Network.LoggedOut += LoggedOutEventHandler;
                Client.Network.RequestLogout();
                if (!LoggedOutEvent.WaitOne((int) corradeConfiguration.LogoutGrace, false))
                {
                    Client.Network.LoggedOut -= LoggedOutEventHandler;
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TIMEOUT_LOGGING_OUT));
                }
                Client.Network.LoggedOut -= LoggedOutEventHandler;
            }
            if (Client.Network.Connected)
            {
                Client.Network.Shutdown(NetworkManager.DisconnectType.ClientInitiated);
            }

            // Terminate.
            Environment.Exit(corradeConfiguration.ExitCodeExpected);
        }

        private static void HandleAvatarUpdate(object sender, AvatarUpdateEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.RadarAvatars, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleObjectUpdate(object sender, PrimEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.RadarPrimitives, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleKillObject(object sender, KillObjectEventArgs e)
        {
            KeyValuePair<UUID, Primitive> tracked;
            lock (RadarObjectsLock)
            {
                tracked =
                    RadarObjects.AsParallel().FirstOrDefault(o => o.Value.LocalID.Equals(e.ObjectLocalID));
            }
            switch (!tracked.Equals(default(KeyValuePair<UUID, Primitive>)))
            {
                case true:
                    switch (tracked.Value is Avatar)
                    {
                        case true:
                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Notifications.RadarAvatars, e),
                                corradeConfiguration.MaximumNotificationThreads);
                            break;
                        default:
                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Notifications.RadarPrimitives, e),
                                corradeConfiguration.MaximumNotificationThreads);
                            break;
                    }
                    break;
            }
        }

        private static void HandleGroupJoined(object sender, GroupOperationEventArgs e)
        {
            // Add the group to the cache.
            if (!Cache.CurrentGroupsCache.Contains(e.GroupID))
            {
                Cache.CurrentGroupsCache.Add(e.GroupID);
            }

            // Join group chat if possible.
            if (!Client.Self.GroupChatSessions.ContainsKey(e.GroupID) &&
                HasGroupPowers(Client.Self.AgentID, e.GroupID, GroupPowers.JoinChat,
                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
            {
                JoinGroupChat(e.GroupID, corradeConfiguration.ServicesTimeout);
            }
        }

        private static void HandleGroupLeave(object sender, GroupOperationEventArgs e)
        {
            // Remove the group from the cache.
            Cache.CurrentGroupsCache.Remove(e.GroupID);
        }

        private static void HandleLoadURL(object sender, LoadUrlEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.LoadURL, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleScriptControlChange(object sender, ScriptControlEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.ScriptControl, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleAppearanceSet(object sender, AppearanceSetEventArgs e)
        {
            switch (e.Success)
            {
                case true:
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.APPEARANCE_SET_SUCCEEDED));
                    break;
                default:
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.APPEARANCE_SET_FAILED));
                    break;
            }
        }

        private static void HandleRegionCrossed(object sender, RegionCrossedEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.RegionCrossed, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleMeanCollision(object sender, MeanCollisionEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.MeanCollision, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleViewerEffect(object sender, object e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.ViewerEffect, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        /// <summary>
        ///     Processes HTTP POST web-requests.
        /// </summary>
        /// <param name="ar">the async HTTP listener object</param>
        private static void ProcessHTTPRequest(IAsyncResult ar)
        {
            // We need to grab the context and everything else outside of the main request.
            HttpListenerContext httpContext = null;
            HttpListenerRequest httpRequest;
            string message;
            Group commandGroup;
            // Now grab the message and check that the group is set or abandon.
            try
            {
                HttpListener httpListener = (HttpListener) ar.AsyncState;
                // bail if we are not listening
                if (httpListener == null || !httpListener.IsListening) return;
                httpContext = httpListener.EndGetContext(ar);
                if (httpContext.Request == null) throw new HTTPCommandException();
                httpRequest = httpContext.Request;
                // only accept POST requests
                if (!httpRequest.HttpMethod.Equals(WebRequestMethods.Http.Post, StringComparison.OrdinalIgnoreCase))
                    throw new HTTPCommandException();
                // only accept connected remote endpoints
                if (httpRequest.RemoteEndPoint == null) throw new HTTPCommandException();
                // Retrieve the message sent even if it is a compressed stream.
                switch (httpRequest.ContentEncoding.EncodingName.ToLower())
                {
                    case "gzip":
                        using (MemoryStream inputStream = new MemoryStream())
                        {
                            using (GZipStream dataGZipStream = new GZipStream(httpRequest.InputStream,
                                CompressionMode.Decompress, false))
                            {
                                dataGZipStream.CopyTo(inputStream);
                                dataGZipStream.Flush();
                            }
                            message = inputStream.ToString();
                        }
                        break;
                    case "deflate":
                        using (MemoryStream inputStream = new MemoryStream())
                        {
                            using (
                                DeflateStream dataDeflateStream = new DeflateStream(httpRequest.InputStream,
                                    CompressionMode.Decompress, false))
                            {
                                dataDeflateStream.CopyTo(inputStream);
                                dataDeflateStream.Flush();
                            }
                            message = inputStream.ToString();
                        }
                        break;
                    default:
                        using (
                            StreamReader reader = new StreamReader(httpRequest.InputStream,
                                httpRequest.ContentEncoding))
                        {
                            message = reader.ReadToEnd();
                        }
                        break;
                }
                // ignore empty messages right-away.
                if (string.IsNullOrEmpty(message)) throw new HTTPCommandException();
                commandGroup = GetCorradeGroupFromMessage(message);
                // do not process anything from unknown groups.
                if (commandGroup.Equals(default(Group))) throw new HTTPCommandException();
            }
            catch (HTTPCommandException)
            {
                /* Close the connection and bail if the preconditions are not satisifed for running the command. */
                httpContext?.Response.Close();
                return;
            }
            catch (HttpListenerException)
            {
                /* This happens when the server goes down, so do not scare the user since it is completely harmelss. */
                return;
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.HTTP_SERVER_PROCESSING_ABORTED), ex.Message);
                return;
            }

            // We have the group so schedule the Corrade command though the group scheduler.
            CorradeThreadPool[CorradeThreadType.COMMAND].Spawn(() =>
            {
                try
                {
                    Dictionary<string, string> result = HandleCorradeCommand(message,
                        CORRADE_CONSTANTS.WEB_REQUEST,
                        httpRequest.RemoteEndPoint.ToString(), commandGroup);
                    using (HttpListenerResponse response = httpContext.Response)
                    {
                        // set the content type based on chosen output filers
                        switch (corradeConfiguration.OutputFilters.Last())
                        {
                            case Filter.RFC1738:
                                response.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED;
                                break;
                            default:
                                response.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.TEXT_PLAIN;
                                break;
                        }
                        response.StatusCode = (int) HttpStatusCode.OK;
                        response.StatusDescription = "OK";
                        response.SendChunked = true;
                        switch (corradeConfiguration.HTTPServerKeepAlive)
                        {
                            case true:
                                response.ProtocolVersion = HttpVersion.Version11;
                                break;
                            default:
                                response.ProtocolVersion = HttpVersion.Version10;
                                response.KeepAlive = false;
                                break;
                        }
                        byte[] data = Encoding.UTF8.GetBytes(wasKeyValueEncode(wasKeyValueEscape(result)));
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            switch (corradeConfiguration.HTTPServerCompression)
                            {
                                case HTTPCompressionMethod.GZIP:
                                    using (GZipStream dataGZipStream = new GZipStream(outputStream,
                                        CompressionMode.Compress, false))
                                    {
                                        dataGZipStream.Write(data, 0, data.Length);
                                        dataGZipStream.Flush();
                                    }
                                    response.AddHeader("Content-Encoding", "gzip");
                                    data = outputStream.ToArray();
                                    break;
                                case HTTPCompressionMethod.DEFLATE:
                                    using (
                                        DeflateStream dataDeflateStream = new DeflateStream(outputStream,
                                            CompressionMode.Compress, false))
                                    {
                                        dataDeflateStream.Write(data, 0, data.Length);
                                        dataDeflateStream.Flush();
                                    }
                                    response.AddHeader("Content-Encoding", "deflate");
                                    data = outputStream.ToArray();
                                    break;
                                default:
                                    response.AddHeader("Content-Encoding", "UTF-8");
                                    break;
                            }
                        }
                        using (Stream responseStream = response.OutputStream)
                        {
                            using (BinaryWriter responseStreamWriter = new BinaryWriter(responseStream))
                            {
                                responseStreamWriter.Write(data);
                            }
                        }
                    }
                }
                catch (HttpListenerException)
                {
                    /* This happens when the server goes down, so do not scare the user since it is completely harmless. */
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.HTTP_SERVER_PROCESSING_ABORTED), ex.Message);
                }
                finally
                {
                    /* Close the connection. */
                    httpContext?.Response.Close();
                }
            }, corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                corradeConfiguration.SchedulerExpiration);
        }

        /// <summary>
        ///     Sends a notification to each group with a configured and installed notification.
        /// </summary>
        /// <param name="notification">the notification to send</param>
        /// <param name="args">the event arguments</param>
        private static void SendNotification(Notifications notification, object args)
        {
            // Create a list of groups that have the notification installed.
            List<Notification> notifyGroups = new List<Notification>();
            lock (GroupNotificationsLock)
            {
                if (GroupNotifications.Any())
                {
                    notifyGroups.AddRange(GroupNotifications.AsParallel()
                        .Where(
                            o =>
                                !(o.NotificationMask & (uint) notification).Equals(0) &&
                                corradeConfiguration.Groups.AsParallel().Any(
                                    p => p.Name.Equals(o.GroupName, StringComparison.OrdinalIgnoreCase) &&
                                         !(p.NotificationMask & (uint) notification).Equals(0))));
                }
            }

            // No groups to notify so bail directly.
            if (!notifyGroups.Any()) return;

            // For each group build the notification.
            Parallel.ForEach(notifyGroups, z =>
            {
                // Set the notification type
                Dictionary<string, string> notificationData = new Dictionary<string, string>();

                // Create the executable delegate.
                System.Action execute;

                // Build the notification data
                switch (notification)
                {
                    case Notifications.ScriptDialog:
                        execute = () =>
                        {
                            ScriptDialogEventArgs scriptDialogEventArgs = (ScriptDialogEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(scriptDialogEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                scriptDialogEventArgs.Message);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                scriptDialogEventArgs.FirstName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                scriptDialogEventArgs.LastName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.CHANNEL),
                                scriptDialogEventArgs.Channel.ToString(Utils.EnUsCulture));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                scriptDialogEventArgs.ObjectName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                scriptDialogEventArgs.ObjectID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                scriptDialogEventArgs.OwnerID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.BUTTON),
                                wasEnumerableToCSV(scriptDialogEventArgs.ButtonLabels));
                        };
                        break;
                    case Notifications.LocalChat:
                        execute = () =>
                        {
                            ChatEventArgs localChatEventArgs = (ChatEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(localChatEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(localChatEventArgs.FromName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                localChatEventArgs.Message);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                localChatEventArgs.OwnerID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                localChatEventArgs.SourceID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                localChatEventArgs.Position.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                Enum.GetName(typeof (ChatSourceType), localChatEventArgs.SourceType));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AUDIBLE),
                                Enum.GetName(typeof (ChatAudibleLevel), localChatEventArgs.AudibleLevel));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.VOLUME),
                                Enum.GetName(typeof (ChatType), localChatEventArgs.Type));
                        };
                        break;
                    case Notifications.Balance:
                        execute = () =>
                        {
                            BalanceEventArgs balanceEventArgs = (BalanceEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(balanceEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.BALANCE),
                                balanceEventArgs.Balance.ToString(Utils.EnUsCulture));
                        };
                        break;
                    case Notifications.AlertMessage:
                        execute = () =>
                        {
                            AlertMessageEventArgs alertMessageEventArgs = (AlertMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(alertMessageEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                alertMessageEventArgs.Message);
                        };
                        break;
                    case Notifications.Inventory:
                        execute = () =>
                        {
                            System.Type inventoryOfferedType = args.GetType();
                            if (inventoryOfferedType == typeof (InstantMessageEventArgs))
                            {
                                InstantMessageEventArgs inventoryOfferEventArgs = (InstantMessageEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(inventoryOfferEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                List<string> inventoryObjectOfferedName =
                                    new List<string>(CORRADE_CONSTANTS.AvatarFullNameRegex.Matches(
                                        inventoryOfferEventArgs.IM.FromAgentName)
                                        .Cast<Match>()
                                        .ToDictionary(p => new[]
                                        {
                                            p.Groups["first"].Value,
                                            p.Groups["last"].Value
                                        })
                                        .SelectMany(
                                            p =>
                                                new[]
                                                {
                                                    p.Key[0].Trim(),
                                                    !string.IsNullOrEmpty(p.Key[1])
                                                        ? p.Key[1].Trim()
                                                        : string.Empty
                                                }));
                                switch (!string.IsNullOrEmpty(inventoryObjectOfferedName.Last()))
                                {
                                    case true:
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                            inventoryObjectOfferedName.First());
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                            inventoryObjectOfferedName.Last());
                                        break;
                                    default:
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                            inventoryObjectOfferedName.First());
                                        break;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                    inventoryOfferEventArgs.IM.FromAgentID.ToString());
                                switch (inventoryOfferEventArgs.IM.Dialog)
                                {
                                    case InstantMessageDialog.InventoryAccepted:
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                            wasGetDescriptionFromEnumValue(Action.ACCEPT));
                                        break;
                                    case InstantMessageDialog.InventoryDeclined:
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                            wasGetDescriptionFromEnumValue(Action.DECLINE));
                                        break;
                                    case InstantMessageDialog.TaskInventoryOffered:
                                    case InstantMessageDialog.InventoryOffered:
                                        lock (InventoryOffersLock)
                                        {
                                            KeyValuePair<InventoryObjectOfferedEventArgs, ManualResetEvent>
                                                inventoryObjectOfferedEventArgs =
                                                    InventoryOffers.AsParallel().FirstOrDefault(p =>
                                                        p.Key.Offer.IMSessionID.Equals(
                                                            inventoryOfferEventArgs.IM.IMSessionID));
                                            if (
                                                !inventoryObjectOfferedEventArgs.Equals(
                                                    default(
                                                        KeyValuePair
                                                            <InventoryObjectOfferedEventArgs, ManualResetEvent>)))
                                            {
                                                switch (inventoryObjectOfferedEventArgs.Key.Accept)
                                                {
                                                    case true:
                                                        notificationData.Add(
                                                            wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                                            wasGetDescriptionFromEnumValue(Action.ACCEPT));
                                                        break;
                                                    default:
                                                        notificationData.Add(
                                                            wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                                            wasGetDescriptionFromEnumValue(Action.DECLINE));
                                                        break;
                                                }
                                            }
                                            GroupCollection groups =
                                                CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                                    inventoryObjectOfferedEventArgs.Key.Offer.Message).Groups;
                                            if (groups.Count > 0)
                                            {
                                                notificationData.Add(
                                                    wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                                    groups[1].Value);
                                            }
                                            InventoryOffers.Remove(inventoryObjectOfferedEventArgs.Key);
                                        }
                                        break;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DIRECTION),
                                    wasGetDescriptionFromEnumValue(Action.REPLY));
                                return;
                            }
                            if (inventoryOfferedType == typeof (InventoryObjectOfferedEventArgs))
                            {
                                InventoryObjectOfferedEventArgs inventoryObjectOfferedEventArgs =
                                    (InventoryObjectOfferedEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(inventoryObjectOfferedEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                List<string> inventoryObjectOfferedName =
                                    new List<string>(CORRADE_CONSTANTS.AvatarFullNameRegex.Matches(
                                        inventoryObjectOfferedEventArgs.Offer.FromAgentName)
                                        .Cast<Match>()
                                        .ToDictionary(p => new[]
                                        {
                                            p.Groups["first"].Value,
                                            p.Groups["last"].Value
                                        })
                                        .SelectMany(
                                            p =>
                                                new[]
                                                {
                                                    p.Key[0],
                                                    !string.IsNullOrEmpty(p.Key[1])
                                                        ? p.Key[1]
                                                        : string.Empty
                                                }));
                                switch (!string.IsNullOrEmpty(inventoryObjectOfferedName.Last()))
                                {
                                    case true:
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                            inventoryObjectOfferedName.First());
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                            inventoryObjectOfferedName.Last());
                                        break;
                                    default:
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                            inventoryObjectOfferedName.First());
                                        break;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                    inventoryObjectOfferedEventArgs.Offer.FromAgentID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ASSET),
                                    inventoryObjectOfferedEventArgs.AssetType.ToString());
                                GroupCollection groups =
                                    CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                        inventoryObjectOfferedEventArgs.Offer.Message).Groups;
                                if (groups.Count > 0)
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                        groups[1].Value);
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SESSION),
                                    inventoryObjectOfferedEventArgs.Offer.IMSessionID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DIRECTION),
                                    wasGetDescriptionFromEnumValue(Action.OFFER));
                            }
                        };
                        break;
                    case Notifications.ScriptPermission:
                        execute = () =>
                        {
                            ScriptQuestionEventArgs scriptQuestionEventArgs = (ScriptQuestionEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(scriptQuestionEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                scriptQuestionEventArgs.ItemID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TASK),
                                scriptQuestionEventArgs.TaskID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.PERMISSIONS),
                                wasEnumerableToCSV(typeof (ScriptPermission).GetFields(BindingFlags.Public |
                                                                                       BindingFlags.Static)
                                    .AsParallel().Where(
                                        p =>
                                            !(((int) p.GetValue(null) &
                                               (int) scriptQuestionEventArgs.Questions)).Equals(0))
                                    .Select(p => p.Name)));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.REGION),
                                scriptQuestionEventArgs.Simulator.Name);
                        };
                        break;
                    case Notifications.Friendship:
                        execute = () =>
                        {
                            System.Type friendshipNotificationType = args.GetType();
                            if (friendshipNotificationType == typeof (FriendInfoEventArgs))
                            {
                                FriendInfoEventArgs friendInfoEventArgs = (FriendInfoEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(friendInfoEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                IEnumerable<string> name = GetAvatarNames(friendInfoEventArgs.Friend.Name);
                                if (name != null)
                                {
                                    List<string> fullName = new List<string>(name);
                                    if (fullName.Count.Equals(2))
                                    {
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                            fullName.First());
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                            fullName.Last());
                                    }
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                    friendInfoEventArgs.Friend.UUID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.STATUS),
                                    friendInfoEventArgs.Friend.IsOnline
                                        ? wasGetDescriptionFromEnumValue(Action.ONLINE)
                                        : wasGetDescriptionFromEnumValue(Action.OFFLINE));
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.RIGHTS),
                                    // Return the friend rights as a nice CSV string.
                                    wasEnumerableToCSV(typeof (FriendRights).GetFields(BindingFlags.Public |
                                                                                       BindingFlags.Static)
                                        .AsParallel().Where(
                                            p =>
                                                !(((int) p.GetValue(null) &
                                                   (int) friendInfoEventArgs.Friend.MyFriendRights))
                                                    .Equals(
                                                        0))
                                        .Select(p => p.Name)));
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.UPDATE));
                                return;
                            }
                            if (friendshipNotificationType == typeof (FriendshipResponseEventArgs))
                            {
                                FriendshipResponseEventArgs friendshipResponseEventArgs =
                                    (FriendshipResponseEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(friendshipResponseEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                IEnumerable<string> name = GetAvatarNames(friendshipResponseEventArgs.AgentName);
                                if (name != null)
                                {
                                    List<string> fullName = new List<string>(name);
                                    if (fullName.Count.Equals(2))
                                    {
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                            fullName.First());
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                            fullName.Last());
                                    }
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                    friendshipResponseEventArgs.AgentID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.RESPONSE));
                                return;
                            }
                            if (friendshipNotificationType == typeof (FriendshipOfferedEventArgs))
                            {
                                FriendshipOfferedEventArgs friendshipOfferedEventArgs =
                                    (FriendshipOfferedEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(friendshipOfferedEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                IEnumerable<string> name = GetAvatarNames(friendshipOfferedEventArgs.AgentName);
                                if (name != null)
                                {
                                    List<string> fullName = new List<string>(name);
                                    if (fullName.Count.Equals(2))
                                    {
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                            fullName.First());
                                        notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                            fullName.Last());
                                    }
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                    friendshipOfferedEventArgs.AgentID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.REQUEST));
                            }
                        };
                        break;
                    case Notifications.TeleportLure:
                        execute = () =>
                        {
                            InstantMessageEventArgs teleportLureEventArgs = (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(teleportLureEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(teleportLureEventArgs.IM.FromAgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                teleportLureEventArgs.IM.FromAgentID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SESSION),
                                teleportLureEventArgs.IM.IMSessionID.ToString());
                        };
                        break;
                    case Notifications.GroupNotice:
                        execute = () =>
                        {
                            InstantMessageEventArgs notificationGroupNoticeEventArgs =
                                (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationGroupNoticeEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(notificationGroupNoticeEventArgs.IM.FromAgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                notificationGroupNoticeEventArgs.IM.FromAgentID.ToString());
                            string[] noticeData = notificationGroupNoticeEventArgs.IM.Message.Split('|');
                            if (noticeData.Length > 0 && !string.IsNullOrEmpty(noticeData[0]))
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SUBJECT),
                                    noticeData[0]);
                            }
                            if (noticeData.Length > 1 && !string.IsNullOrEmpty(noticeData[1]))
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                    noticeData[1]);
                            }
                            switch (notificationGroupNoticeEventArgs.IM.Dialog)
                            {
                                case InstantMessageDialog.GroupNoticeInventoryAccepted:
                                case InstantMessageDialog.GroupNoticeInventoryDeclined:
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                        !notificationGroupNoticeEventArgs.IM.Dialog.Equals(
                                            InstantMessageDialog.GroupNoticeInventoryAccepted)
                                            ? wasGetDescriptionFromEnumValue(Action.DECLINE)
                                            : wasGetDescriptionFromEnumValue(Action.ACCEPT));
                                    break;
                                case InstantMessageDialog.GroupNotice:
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                        wasGetDescriptionFromEnumValue(Action.RECEIVED));
                                    break;
                            }
                        };
                        break;
                    case Notifications.InstantMessage:
                        execute = () =>
                        {
                            InstantMessageEventArgs notificationInstantMessage =
                                (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationInstantMessage,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(notificationInstantMessage.IM.FromAgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                notificationInstantMessage.IM.FromAgentID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                notificationInstantMessage.IM.Message);
                        };
                        break;
                    case Notifications.RegionMessage:
                        execute = () =>
                        {
                            InstantMessageEventArgs notificationRegionMessage =
                                (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationRegionMessage,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(notificationRegionMessage.IM.FromAgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                notificationRegionMessage.IM.FromAgentID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                notificationRegionMessage.IM.Message);
                        };
                        break;
                    case Notifications.GroupMessage:
                        execute = () =>
                        {
                            GroupMessageEventArgs notificationGroupMessage = (GroupMessageEventArgs) args;
                            // Set-up filters.
                            if (!notificationGroupMessage.GroupUUID.Equals(z.GroupUUID)) return;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationGroupMessage,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                notificationGroupMessage.FirstName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                notificationGroupMessage.LastName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                notificationGroupMessage.AgentUUID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.GROUP),
                                notificationGroupMessage.GroupName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                notificationGroupMessage.Message);
                        };
                        break;
                    case Notifications.ViewerEffect:
                        execute = () =>
                        {
                            System.Type viewerEffectType = args.GetType();
                            if (viewerEffectType == typeof (ViewerEffectEventArgs))
                            {
                                ViewerEffectEventArgs notificationViewerEffectEventArgs =
                                    (ViewerEffectEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(notificationViewerEffectEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.EFFECT),
                                    notificationViewerEffectEventArgs.Type.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SOURCE),
                                    notificationViewerEffectEventArgs.SourceID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET),
                                    notificationViewerEffectEventArgs.TargetID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    notificationViewerEffectEventArgs.TargetPosition.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DURATION),
                                    notificationViewerEffectEventArgs.Duration.ToString(
                                        Utils.EnUsCulture));
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    notificationViewerEffectEventArgs.EffectID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.GENERIC));
                                return;
                            }
                            if (viewerEffectType == typeof (ViewerEffectPointAtEventArgs))
                            {
                                ViewerEffectPointAtEventArgs notificationViewerPointAtEventArgs =
                                    (ViewerEffectPointAtEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(notificationViewerPointAtEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SOURCE),
                                    notificationViewerPointAtEventArgs.SourceID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET),
                                    notificationViewerPointAtEventArgs.TargetID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    notificationViewerPointAtEventArgs.TargetPosition.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DURATION),
                                    notificationViewerPointAtEventArgs.Duration.ToString(
                                        Utils.EnUsCulture));
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    notificationViewerPointAtEventArgs.EffectID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.POINT));
                                return;
                            }
                            if (viewerEffectType == typeof (ViewerEffectLookAtEventArgs))
                            {
                                ViewerEffectLookAtEventArgs notificationViewerLookAtEventArgs =
                                    (ViewerEffectLookAtEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(notificationViewerLookAtEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SOURCE),
                                    notificationViewerLookAtEventArgs.SourceID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET),
                                    notificationViewerLookAtEventArgs.TargetID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    notificationViewerLookAtEventArgs.TargetPosition.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DURATION),
                                    notificationViewerLookAtEventArgs.Duration.ToString(
                                        Utils.EnUsCulture));
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    notificationViewerLookAtEventArgs.EffectID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.LOOK));
                            }
                        };
                        break;
                    case Notifications.MeanCollision:
                        execute = () =>
                        {
                            MeanCollisionEventArgs meanCollisionEventArgs =
                                (MeanCollisionEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(meanCollisionEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGGRESSOR),
                                meanCollisionEventArgs.Aggressor.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MAGNITUDE),
                                meanCollisionEventArgs.Magnitude.ToString(Utils.EnUsCulture));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TIME),
                                meanCollisionEventArgs.Time.ToLongDateString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                meanCollisionEventArgs.Type.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.VICTIM),
                                meanCollisionEventArgs.Victim.ToString());
                        };
                        break;
                    case Notifications.RegionCrossed:
                        execute = () =>
                        {
                            System.Type regionChangeType = args.GetType();
                            if (regionChangeType == typeof (SimChangedEventArgs))
                            {
                                SimChangedEventArgs simChangedEventArgs = (SimChangedEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(simChangedEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                if (simChangedEventArgs.PreviousSimulator != null)
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OLD),
                                        simChangedEventArgs.PreviousSimulator.Name);
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NEW),
                                    Client.Network.CurrentSim.Name);
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.CHANGED));
                                return;
                            }
                            if (regionChangeType == typeof (RegionCrossedEventArgs))
                            {
                                RegionCrossedEventArgs regionCrossedEventArgs =
                                    (RegionCrossedEventArgs) args;
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(regionCrossedEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                if (regionCrossedEventArgs.OldSimulator != null)
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OLD),
                                        regionCrossedEventArgs.OldSimulator.Name);
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NEW),
                                    regionCrossedEventArgs.NewSimulator.Name);
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.CROSSED));
                            }
                        };
                        break;
                    case Notifications.TerseUpdates:
                        execute = () =>
                        {
                            TerseObjectUpdateEventArgs terseObjectUpdateEventArgs =
                                (TerseObjectUpdateEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(terseObjectUpdateEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                terseObjectUpdateEventArgs.Prim.ID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                terseObjectUpdateEventArgs.Prim.Position.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION),
                                terseObjectUpdateEventArgs.Prim.Rotation.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                terseObjectUpdateEventArgs.Prim.PrimData.PCode.ToString());
                        };
                        break;
                    case Notifications.Typing:
                        execute = () =>
                        {
                            InstantMessageEventArgs notificationTypingMessageEventArgs =
                                (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationTypingMessageEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name =
                                GetAvatarNames(notificationTypingMessageEventArgs.IM.FromAgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                notificationTypingMessageEventArgs.IM.FromAgentID.ToString());
                            switch (notificationTypingMessageEventArgs.IM.Dialog)
                            {
                                case InstantMessageDialog.StartTyping:
                                case InstantMessageDialog.StopTyping:
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                        !notificationTypingMessageEventArgs.IM.Dialog.Equals(
                                            InstantMessageDialog.StartTyping)
                                            ? wasGetDescriptionFromEnumValue(Action.STOP)
                                            : wasGetDescriptionFromEnumValue(Action.START));
                                    break;
                            }
                        };
                        break;
                    case Notifications.GroupInvite:
                        execute = () =>
                        {
                            InstantMessageEventArgs notificationGroupInviteEventArgs =
                                (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationGroupInviteEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(notificationGroupInviteEventArgs.IM.FromAgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                notificationGroupInviteEventArgs.IM.FromAgentID.ToString());
                            lock (GroupInviteLock)
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.GROUP),
                                    GroupInvites.AsParallel().FirstOrDefault(
                                        p => p.Session.Equals(notificationGroupInviteEventArgs.IM.IMSessionID))
                                        .Group);
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SESSION),
                                notificationGroupInviteEventArgs.IM.IMSessionID.ToString());
                        };
                        break;
                    case Notifications.Economy:
                        execute = () =>
                        {
                            MoneyBalanceReplyEventArgs notificationMoneyBalanceEventArgs =
                                (MoneyBalanceReplyEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationMoneyBalanceEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.BALANCE),
                                notificationMoneyBalanceEventArgs.Balance.ToString(
                                    Utils.EnUsCulture));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DESCRIPTION),
                                notificationMoneyBalanceEventArgs.Description);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.COMMITTED),
                                notificationMoneyBalanceEventArgs.MetersCommitted.ToString(
                                    Utils.EnUsCulture));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.CREDIT),
                                notificationMoneyBalanceEventArgs.MetersCredit.ToString(
                                    Utils.EnUsCulture));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SUCCESS),
                                notificationMoneyBalanceEventArgs.Success.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                notificationMoneyBalanceEventArgs.TransactionID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AMOUNT),
                                notificationMoneyBalanceEventArgs.TransactionInfo.Amount.ToString(
                                    Utils.EnUsCulture));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET),
                                notificationMoneyBalanceEventArgs.TransactionInfo.DestID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.SOURCE),
                                notificationMoneyBalanceEventArgs.TransactionInfo.SourceID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TRANSACTION),
                                Enum.GetName(typeof (MoneyTransactionType),
                                    notificationMoneyBalanceEventArgs.TransactionInfo.TransactionType));
                        };
                        break;
                    case Notifications.GroupMembership:
                        execute = () =>
                        {
                            GroupMembershipEventArgs groupMembershipEventArgs = (GroupMembershipEventArgs) args;
                            // Set-up filters.
                            if (!groupMembershipEventArgs.GroupUUID.Equals(z.GroupUUID)) return;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(groupMembershipEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            IEnumerable<string> name = GetAvatarNames(groupMembershipEventArgs.AgentName);
                            if (name != null)
                            {
                                List<string> fullName = new List<string>(name);
                                if (fullName.Count.Equals(2))
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                        fullName.First());
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                        fullName.Last());
                                }
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT),
                                groupMembershipEventArgs.AgentUUID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.GROUP),
                                groupMembershipEventArgs.GroupName);
                            switch (groupMembershipEventArgs.Action)
                            {
                                case Action.JOINED:
                                case Action.PARTED:
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                        !groupMembershipEventArgs.Action.Equals(
                                            Action.JOINED)
                                            ? wasGetDescriptionFromEnumValue(Action.PARTED)
                                            : wasGetDescriptionFromEnumValue(Action.JOINED));
                                    break;
                            }
                        };
                        break;
                    case Notifications.LoadURL:
                        execute = () =>
                        {
                            LoadUrlEventArgs loadURLEventArgs = (LoadUrlEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(loadURLEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                loadURLEventArgs.ObjectName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                loadURLEventArgs.ObjectID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                loadURLEventArgs.OwnerID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.GROUP),
                                loadURLEventArgs.OwnerIsGroup.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                loadURLEventArgs.Message);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.URL),
                                loadURLEventArgs.URL);
                        };
                        break;
                    case Notifications.OwnerSay:
                        execute = () =>
                        {
                            ChatEventArgs ownerSayEventArgs = (ChatEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(ownerSayEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                ownerSayEventArgs.Message);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                ownerSayEventArgs.SourceID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                ownerSayEventArgs.FromName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                ownerSayEventArgs.Position.ToString());
                        };
                        break;
                    case Notifications.RegionSayTo:
                        execute = () =>
                        {
                            ChatEventArgs regionSayToEventArgs = (ChatEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(regionSayToEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                regionSayToEventArgs.Message);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                regionSayToEventArgs.OwnerID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                regionSayToEventArgs.SourceID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                regionSayToEventArgs.FromName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                regionSayToEventArgs.Position.ToString());
                        };
                        break;
                    case Notifications.ObjectInstantMessage:
                        execute = () =>
                        {
                            InstantMessageEventArgs notificationObjectInstantMessage =
                                (InstantMessageEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(notificationObjectInstantMessage,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                notificationObjectInstantMessage.IM.FromAgentID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                notificationObjectInstantMessage.IM.IMSessionID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                notificationObjectInstantMessage.IM.FromAgentName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                notificationObjectInstantMessage.IM.Message);
                        };
                        break;
                    case Notifications.RLVMessage:
                        execute = () =>
                        {
                            ChatEventArgs RLVEventArgs = (ChatEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(RLVEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                RLVEventArgs.SourceID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                RLVEventArgs.FromName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                RLVEventArgs.Position.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.RLV),
                                wasEnumerableToCSV(wasRLVToString(RLVEventArgs.Message)));
                        };
                        break;
                    case Notifications.DebugMessage:
                        execute = () =>
                        {
                            ChatEventArgs DebugEventArgs = (ChatEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(DebugEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM),
                                DebugEventArgs.SourceID.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.NAME),
                                DebugEventArgs.FromName);
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                DebugEventArgs.Position.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE),
                                DebugEventArgs.Message);
                        };
                        break;
                    case Notifications.RadarAvatars:
                        execute = () =>
                        {
                            System.Type radarAvatarsType = args.GetType();
                            if (radarAvatarsType == typeof (AvatarUpdateEventArgs))
                            {
                                AvatarUpdateEventArgs avatarUpdateEventArgs =
                                    (AvatarUpdateEventArgs) args;
                                lock (RadarObjectsLock)
                                {
                                    if (RadarObjects.ContainsKey(avatarUpdateEventArgs.Avatar.ID)) return;
                                    RadarObjects.Add(avatarUpdateEventArgs.Avatar.ID, avatarUpdateEventArgs.Avatar);
                                }
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(avatarUpdateEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                    avatarUpdateEventArgs.Avatar.FirstName);
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                    avatarUpdateEventArgs.Avatar.LastName);
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    avatarUpdateEventArgs.Avatar.ID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    avatarUpdateEventArgs.Avatar.Position.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION),
                                    avatarUpdateEventArgs.Avatar.Rotation.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                    avatarUpdateEventArgs.Avatar.PrimData.PCode.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.APPEAR));
                                return;
                            }
                            if (radarAvatarsType == typeof (KillObjectEventArgs))
                            {
                                KillObjectEventArgs killObjectEventArgs =
                                    (KillObjectEventArgs) args;
                                Avatar avatar;
                                lock (RadarObjectsLock)
                                {
                                    KeyValuePair<UUID, Primitive> tracked =
                                        RadarObjects.AsParallel().FirstOrDefault(
                                            p => p.Value.LocalID.Equals(killObjectEventArgs.ObjectLocalID));
                                    switch (!tracked.Equals(default(KeyValuePair<UUID, Primitive>)))
                                    {
                                        case true:
                                            RadarObjects.Remove(tracked.Key);
                                            break;
                                        default:
                                            return;
                                    }
                                    if (!(tracked.Value is Avatar)) return;
                                    avatar = tracked.Value as Avatar;
                                }
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(killObjectEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME),
                                    avatar.FirstName);
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME),
                                    avatar.LastName);
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    avatar.ID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    avatar.Position.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION),
                                    avatar.Rotation.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                    avatar.PrimData.PCode.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.VANISH));
                            }
                        };
                        break;
                    case Notifications.RadarPrimitives:
                        execute = () =>
                        {
                            System.Type radarPrimitivesType = args.GetType();
                            if (radarPrimitivesType == typeof (PrimEventArgs))
                            {
                                PrimEventArgs primEventArgs =
                                    (PrimEventArgs) args;
                                lock (RadarObjectsLock)
                                {
                                    if (RadarObjects.ContainsKey(primEventArgs.Prim.ID)) return;
                                    RadarObjects.Add(primEventArgs.Prim.ID, primEventArgs.Prim);
                                }
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(primEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                    primEventArgs.Prim.OwnerID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    primEventArgs.Prim.ID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    primEventArgs.Prim.Position.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION),
                                    primEventArgs.Prim.Rotation.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                    primEventArgs.Prim.PrimData.PCode.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.APPEAR));
                                return;
                            }
                            if (radarPrimitivesType == typeof (KillObjectEventArgs))
                            {
                                KillObjectEventArgs killObjectEventArgs =
                                    (KillObjectEventArgs) args;
                                Primitive prim;
                                lock (RadarObjectsLock)
                                {
                                    KeyValuePair<UUID, Primitive> tracked =
                                        RadarObjects.AsParallel().FirstOrDefault(
                                            p => p.Value.LocalID.Equals(killObjectEventArgs.ObjectLocalID));
                                    switch (!tracked.Equals(default(KeyValuePair<UUID, Primitive>)))
                                    {
                                        case true:
                                            RadarObjects.Remove(tracked.Key);
                                            prim = tracked.Value;
                                            break;
                                        default:
                                            return;
                                    }
                                }
                                // In case we should send specific data then query the structure and return.
                                if (z.Data != null && z.Data.Any())
                                {
                                    notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                        wasEnumerableToCSV(GetStructuredData(killObjectEventArgs,
                                            wasEnumerableToCSV(z.Data))));
                                    return;
                                }
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.OWNER),
                                    prim.OwnerID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ID),
                                    prim.ID.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION),
                                    prim.Position.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION),
                                    prim.Rotation.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY),
                                    prim.PrimData.PCode.ToString());
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION),
                                    wasGetDescriptionFromEnumValue(Action.VANISH));
                            }
                        };
                        break;
                    case Notifications.ScriptControl:
                        execute = () =>
                        {
                            ScriptControlEventArgs scriptControlEventArgs =
                                (ScriptControlEventArgs) args;
                            // In case we should send specific data then query the structure and return.
                            if (z.Data != null && z.Data.Any())
                            {
                                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                                    wasEnumerableToCSV(GetStructuredData(scriptControlEventArgs,
                                        wasEnumerableToCSV(z.Data))));
                                return;
                            }
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.CONTROLS),
                                wasEnumerableToCSV(typeof (ScriptControlChange).GetFields(BindingFlags.Public |
                                                                                          BindingFlags.Static)
                                    .AsParallel().Where(
                                        p =>
                                            !(((uint) p.GetValue(null) &
                                               (uint) scriptControlEventArgs.Controls)).Equals(0))
                                    .Select(p => p.Name)));
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.PASS),
                                scriptControlEventArgs.Pass.ToString());
                            notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TAKE),
                                scriptControlEventArgs.Take.ToString());
                        };
                        break;
                    default:
                        execute = () =>
                        {
                            throw new Exception(
                                wasGetDescriptionFromEnumValue(ConsoleError.UNKNOWN_NOTIFICATION_TYPE));
                        };
                        break;
                }

                try
                {
                    execute.Invoke();
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.NOTIFICATION_ERROR), ex.Message);
                    return;
                }

                // Do not send empty notifications.
                if (!notificationData.Any()) return;

                // Add the notification type.
                notificationData.Add(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE),
                    wasGetDescriptionFromEnumValue(notification));

                // Build the afterburn.
                if (z.Afterburn != null && z.Afterburn.Any())
                {
                    object LockObject = new object();
                    Parallel.ForEach(z.Afterburn, o =>
                    {
                        lock (LockObject)
                        {
                            notificationData.Add(o.Key, o.Value);
                        }
                    });
                }

                // Enqueue the notification for the group.
                if (z.NotificationURLDestination.Any())
                {
                    Parallel.ForEach(
                        z.NotificationURLDestination.AsParallel()
                            .Where(p => p.Key.Equals(notification))
                            .SelectMany(p => p.Value), p =>
                            {
                                // Check that the notification queue is not already full.
                                switch (NotificationQueue.Count <= corradeConfiguration.NotificationQueueLength)
                                {
                                    case true:
                                        NotificationQueue.Enqueue(new NotificationQueueElement
                                        {
                                            URL = p,
                                            message = wasKeyValueEscape(notificationData)
                                        });
                                        break;
                                    default:
                                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.NOTIFICATION_THROTTLED));
                                        break;
                                }
                            });
                }

                // Enqueue the TCP notification for the group.
                if (z.NotificationTCPDestination.Any())
                {
                    Parallel.ForEach(
                        z.NotificationTCPDestination.AsParallel()
                            .Where(p => p.Key.Equals(notification))
                            .SelectMany(p => p.Value), p =>
                            {
                                switch (NotificationTCPQueue.Count <= corradeConfiguration.TCPNotificationQueueLength)
                                {
                                    case true:
                                        NotificationTCPQueue.Enqueue(new NotificationTCPQueueElement
                                        {
                                            message = wasKeyValueEscape(notificationData),
                                            IPEndPoint = p
                                        });
                                        break;
                                    default:
                                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TCP_NOTIFICATION_THROTTLED));
                                        break;
                                }
                            });
                }
            });
        }

        private static void HandleScriptDialog(object sender, ScriptDialogEventArgs e)
        {
            lock (ScriptDialogLock)
            {
                ScriptDialogs.Add(new ScriptDialog
                {
                    Message = e.Message,
                    Agent = new Agent
                    {
                        FirstName = e.FirstName,
                        LastName = e.LastName,
                        UUID = e.OwnerID
                    },
                    Channel = e.Channel,
                    Name = e.ObjectName,
                    Item = e.ObjectID,
                    Button = e.ButtonLabels
                });
            }
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.ScriptDialog, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleChatFromSimulator(object sender, ChatEventArgs e)
        {
            // Ignore chat with no message (ie: start / stop typing)
            if (string.IsNullOrEmpty(e.Message)) return;
            // Check if message is from muted agent or object and ignore it.
            if (Cache.MutesCache != null && Cache.MutesCache.Any(o => o.ID.Equals(e.SourceID) || o.ID.Equals(e.OwnerID)))
                return;
            // Get the full name.
            List<string> fullName = new List<string>(GetAvatarNames(e.FromName));
            Group commandGroup;
            switch (e.Type)
            {
                case ChatType.OwnerSay:
                    // If this is a message from an agent, add the agent to the cache.
                    if (e.SourceType.Equals(ChatSourceType.Agent))
                    {
                        Cache.AddAgent(fullName.First(), fullName.Last(), e.SourceID);
                    }
                    // If RLV is enabled, process RLV and terminate.
                    if (corradeConfiguration.EnableRLV && e.Message.StartsWith(RLV_CONSTANTS.COMMAND_OPERATOR))
                    {
                        // Send RLV message notifications.
                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                            () => SendNotification(Notifications.RLVMessage, e),
                            corradeConfiguration.MaximumNotificationThreads);
                        CorradeThreadPool[CorradeThreadType.RLV].Spawn(
                            () => HandleRLVBehaviour(e.Message.Substring(1, e.Message.Length - 1), e.SourceID),
                            corradeConfiguration.MaximumRLVThreads);
                        break;
                    }
                    // If this is a Corrade command, process it and terminate.
                    if (IsCorradeCommand(e.Message))
                    {
                        // If the group was not set properly, then bail.
                        commandGroup = GetCorradeGroupFromMessage(e.Message);
                        switch (!commandGroup.Equals(default(Group)))
                        {
                            case false:
                                return;
                        }
                        // Spawn the command.
                        CorradeThreadPool[CorradeThreadType.COMMAND].Spawn(
                            () => HandleCorradeCommand(e.Message, e.FromName, e.OwnerID.ToString(), commandGroup),
                            corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                            corradeConfiguration.SchedulerExpiration);
                        return;
                    }
                    // Otherwise, send llOwnerSay notifications.
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.OwnerSay, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
                case ChatType.Debug:
                    // Send debug notifications.
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.DebugMessage, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    break;
                case ChatType.Normal:
                case ChatType.Shout:
                case ChatType.Whisper:
                    // If this is a message from an agent, add the agent to the cache.
                    if (e.SourceType.Equals(ChatSourceType.Agent))
                    {
                        Cache.AddAgent(fullName.First(), fullName.Last(), e.SourceID);
                    }
                    // Send chat notifications.
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.LocalChat, e),
                        corradeConfiguration.MaximumNotificationThreads);
                    // Log local chat,
                    if (corradeConfiguration.LocalMessageLogEnabled)
                    {
                        CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                        {
                            try
                            {
                                lock (LocalLogFileLock)
                                {
                                    using (
                                        StreamWriter logWriter =
                                            new StreamWriter(
                                                wasPathCombine(corradeConfiguration.LocalMessageLogDirectory,
                                                    Client.Network.CurrentSim.Name) +
                                                "." +
                                                CORRADE_CONSTANTS.LOG_FILE_EXTENSION, true, Encoding.UTF8))
                                    {
                                        logWriter.WriteLine("[{0}] {1} {2} ({3}) : {4}",
                                            DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                Utils.EnUsCulture.DateTimeFormat),
                                            fullName.First(), fullName.Last(),
                                            Enum.GetName(typeof (ChatType), e.Type),
                                            e.Message);
                                        //logWriter.Flush();
                                        //logWriter.Close();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // or fail and append the fail message.
                                Feedback(
                                    wasGetDescriptionFromEnumValue(
                                        ConsoleError.COULD_NOT_WRITE_TO_LOCAL_MESSAGE_LOG_FILE),
                                    ex.Message);
                            }
                        }, corradeConfiguration.MaximumLogThreads);
                    }
                    break;
                case (ChatType) 9:
                    // Send llRegionSayTo notification in case we do not have a command.
                    if (!IsCorradeCommand(e.Message))
                    {
                        // Send chat notifications.
                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                            () => SendNotification(Notifications.RegionSayTo, e),
                            corradeConfiguration.MaximumNotificationThreads);
                        break;
                    }
                    // If the group was not set properly, then bail.
                    commandGroup = GetCorradeGroupFromMessage(e.Message);
                    switch (!commandGroup.Equals(default(Group)))
                    {
                        case false:
                            return;
                    }
                    // Spawn the command.
                    CorradeThreadPool[CorradeThreadType.COMMAND].Spawn(
                        () => HandleCorradeCommand(e.Message, e.FromName, e.OwnerID.ToString(), commandGroup),
                        corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                        corradeConfiguration.SchedulerExpiration);
                    break;
            }
        }

        private static void HandleAlertMessage(object sender, AlertMessageEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.AlertMessage, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleInventoryObjectOffered(object sender, InventoryObjectOfferedEventArgs e)
        {
            // Send notification
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Inventory, e),
                corradeConfiguration.MaximumNotificationThreads);

            // Accept anything from master avatars.
            if (
                corradeConfiguration.Masters.AsParallel().Select(
                    o => string.Format(Utils.EnUsCulture, "{0} {1}", o.FirstName, o.LastName))
                    .Any(p => p.Equals(e.Offer.FromAgentName, StringComparison.OrdinalIgnoreCase)))
            {
                e.Accept = true;
                // It is accepted, so update the inventory.
                UpdateInventoryRecursive.Invoke(
                    Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(e.AssetType)].Data as
                        InventoryFolder);
                return;
            }

            // We need to block until we get a reply from a script.
            ManualResetEvent wait = new ManualResetEvent(false);
            // Add the inventory offer to the list of inventory items.
            lock (InventoryOffersLock)
            {
                InventoryOffers.Add(e, wait);
            }

            // It is temporary, so update the inventory.
            UpdateInventoryRecursive.Invoke(
                Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(e.AssetType)].Data as
                    InventoryFolder);

            // Find the item in the inventory.
            InventoryBase inventoryBaseItem;
            lock (ClientInstanceInventoryLock)
            {
                inventoryBaseItem = FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, ((Func<string>) (() =>
                {
                    GroupCollection groups =
                        CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(e.Offer.Message).Groups;
                    return groups.Count > 0 ? groups[1].Value : e.Offer.Message;
                }))()
                    ).FirstOrDefault();
            }

            if (inventoryBaseItem != null)
            {
                // Assume we do not want the item.
                lock (ClientInstanceInventoryLock)
                {
                    Client.Inventory.Move(
                        inventoryBaseItem,
                        Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(AssetType.TrashFolder)].Data as
                            InventoryFolder);
                }
            }

            // Wait for a reply.
            wait.WaitOne(Timeout.Infinite);

            if (!e.Accept) return;

            // If no folder UUID was specified, move it to the default folder for the asset type.
            if (inventoryBaseItem != null)
            {
                switch (!e.FolderID.Equals(UUID.Zero))
                {
                    case true:
                        InventoryBase inventoryBaseFolder;
                        lock (ClientInstanceInventoryLock)
                        {
                            // Locate the folder and move.
                            inventoryBaseFolder =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, e.FolderID
                                    ).FirstOrDefault();
                            if (inventoryBaseFolder != null)
                            {
                                Client.Inventory.Move(inventoryBaseItem, inventoryBaseFolder as InventoryFolder);
                            }
                        }
                        if (inventoryBaseFolder != null)
                        {
                            UpdateInventoryRecursive.Invoke(inventoryBaseFolder as InventoryFolder);
                        }
                        break;
                    default:
                        lock (ClientInstanceInventoryLock)
                        {
                            Client.Inventory.Move(
                                inventoryBaseItem,
                                Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(e.AssetType)].Data as
                                    InventoryFolder);
                        }
                        UpdateInventoryRecursive.Invoke(
                            Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(e.AssetType)].Data as
                                InventoryFolder);
                        break;
                }
            }
        }

        private static void HandleScriptQuestion(object sender, ScriptQuestionEventArgs e)
        {
            List<string> owner = new List<string>(GetAvatarNames(e.ObjectOwnerName));
            UUID ownerUUID = UUID.Zero;
            // Don't add permission requests from unknown agents.
            if (
                !AgentNameToUUID(owner.First(), owner.Last(), corradeConfiguration.ServicesTimeout,
                    corradeConfiguration.DataTimeout,
                    ref ownerUUID))
            {
                return;
            }

            lock (ScriptPermissionRequestLock)
            {
                ScriptPermissionRequests.Add(new ScriptPermissionRequest
                {
                    Name = e.ObjectName,
                    Agent = new Agent
                    {
                        FirstName = owner.First(),
                        LastName = owner.Last(),
                        UUID = ownerUUID
                    },
                    Item = e.ItemID,
                    Task = e.TaskID,
                    Permission = e.Questions,
                    Region = e.Simulator.Name
                });
            }
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.ScriptPermission, e),
                corradeConfiguration.MaximumNotificationThreads);

            // Handle RLV: acceptpermission
            lock (RLVRulesLock)
            {
                if (
                    !RLVRules.AsParallel()
                        .Any(o => o.Behaviour.Equals(wasGetDescriptionFromEnumValue(RLVBehaviour.ACCEPTPERMISSION))))
                    return;
                lock (ClientInstanceSelfLock)
                {
                    Client.Self.ScriptQuestionReply(e.Simulator, e.ItemID, e.TaskID, e.Questions);
                }
            }
        }

        private static void HandleDisconnected(object sender, DisconnectedEventArgs e)
        {
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.DISCONNECTED));
            ConnectionSemaphores['l'].Set();
        }

        private static void HandleEventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.EVENT_QUEUE_STARTED));
        }

        private static void HandleSimulatorConnected(object sender, SimConnectedEventArgs e)
        {
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.SIMULATOR_CONNECTED));
        }

        private static void HandleSimulatorDisconnected(object sender, SimDisconnectedEventArgs e)
        {
            // if any simulators are still connected, we are not disconnected
            if (Client.Network.Simulators.Any()) return;
            Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ALL_SIMULATORS_DISCONNECTED));
            ConnectionSemaphores['s'].Set();
        }

        private static void HandleLoginProgress(object sender, LoginProgressEventArgs e)
        {
            switch (e.Status)
            {
                case LoginStatus.Success:
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.LOGIN_SUCCEEDED));
                    // Start inventory update thread.
                    new Thread(() =>
                    {
                        // First load the caches.
                        LoadInventoryCache.Invoke();
                        // Update the inventory.
                        UpdateInventoryRecursive.Invoke(Client.Inventory.Store.RootFolder);
                        // Now save the caches.
                        SaveInventoryCache.Invoke();
                    })
                    {IsBackground = true}.Start();
                    // Set current group to land group.
                    new Thread(() =>
                    {
                        if (!corradeConfiguration.AutoActivateGroup) return;
                        ActivateCurrentLandGroupTimer.Change(corradeConfiguration.ActivateDelay, 0);
                    })
                    {IsBackground = true}.Start();
                    // Retrieve instant messages.
                    new Thread(() =>
                    {
                        lock (ClientInstanceSelfLock)
                        {
                            Client.Self.RetrieveInstantMessages();
                        }
                    })
                    {IsBackground = true}.Start();
                    // Request the mute list.
                    new Thread(() =>
                    {
                        IEnumerable<MuteEntry> mutes = Enumerable.Empty<MuteEntry>();
                        if (!GetMutes(corradeConfiguration.ServicesTimeout, ref mutes))
                            return;
                        Cache.MutesCache.UnionWith(mutes);
                    })
                    {IsBackground = true}.Start();
                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );
                    break;
                case LoginStatus.Failed:
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.LOGIN_FAILED), e.FailReason);
                    ConnectionSemaphores['l'].Set();
                    break;
            }
        }

        private static void HandleFriendOnlineStatus(object sender, FriendInfoEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleFriendRightsUpdate(object sender, FriendInfoEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleFriendShipResponse(object sender, FriendshipResponseEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleFriendshipOffered(object sender, FriendshipOfferedEventArgs e)
        {
            // Send friendship notifications
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Friendship, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleTeleportProgress(object sender, TeleportEventArgs e)
        {
            switch (e.Status)
            {
                case TeleportStatus.Finished:
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TELEPORT_SUCCEEDED));
                    // Set current group to land group.
                    new Thread(() =>
                    {
                        if (!corradeConfiguration.AutoActivateGroup) return;
                        ActivateCurrentLandGroupTimer.Change(corradeConfiguration.ActivateDelay, 0);
                    })
                    {IsBackground = true}.Start();
                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );
                    break;
                case TeleportStatus.Failed:
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TELEPORT_FAILED));
                    break;
            }
        }

        private static void HandleSelfIM(object sender, InstantMessageEventArgs args)
        {
            // Check if message is from muted agent and ignore it.
            if (Cache.MutesCache != null && Cache.MutesCache.Any(o => o.ID.Equals(args.IM.FromAgentID)))
                return;
            List<string> fullName =
                new List<string>(
                    GetAvatarNames(args.IM.FromAgentName));
            // Process dialog messages.
            switch (args.IM.Dialog)
            {
                // Send typing notification.
                case InstantMessageDialog.StartTyping:
                case InstantMessageDialog.StopTyping:
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.Typing, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    return;
                case InstantMessageDialog.FriendshipOffered:
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    // Accept friendships only from masters (for the time being)
                    if (
                        !corradeConfiguration.Masters.AsParallel().Any(
                            o =>
                                o.FirstName.Equals(fullName.First(), StringComparison.OrdinalIgnoreCase) &&
                                o.LastName.Equals(fullName.Last(), StringComparison.OrdinalIgnoreCase)))
                        return;
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ACCEPTED_FRIENDSHIP), args.IM.FromAgentName);
                    Client.Friends.AcceptFriendship(args.IM.FromAgentID, args.IM.IMSessionID);
                    break;
                case InstantMessageDialog.InventoryAccepted:
                case InstantMessageDialog.InventoryDeclined:
                case InstantMessageDialog.TaskInventoryOffered:
                case InstantMessageDialog.InventoryOffered:
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.Inventory, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    return;
                case InstantMessageDialog.MessageBox:
                    // Not used.
                    return;
                case InstantMessageDialog.RequestTeleport:
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    // Handle RLV: acccepttp
                    lock (RLVRulesLock)
                    {
                        if (
                            RLVRules.AsParallel()
                                .Any(o => o.Behaviour.Equals(wasGetDescriptionFromEnumValue(RLVBehaviour.ACCEPTTP))))
                        {
                            if (IsSecondLife() && !TimedTeleportThrottle.IsSafe)
                            {
                                // or fail and append the fail message.
                                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TELEPORT_THROTTLED));
                                return;
                            }
                            lock (ClientInstanceSelfLock)
                            {
                                Client.Self.TeleportLureRespond(args.IM.FromAgentID, args.IM.IMSessionID, true);
                            }
                            return;
                        }
                    }
                    // Store teleport lure.
                    lock (TeleportLureLock)
                    {
                        TeleportLures.Add(new TeleportLure
                        {
                            Agent = new Agent
                            {
                                FirstName = fullName.First(),
                                LastName = fullName.Last(),
                                UUID = args.IM.FromAgentID
                            },
                            Session = args.IM.IMSessionID
                        });
                    }
                    // Send teleport lure notification.
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.TeleportLure, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    // If we got a teleport request from a master, then accept it (for the moment).
                    lock (ClientInstanceConfigurationLock)
                    {
                        if (
                            !corradeConfiguration.Masters.AsParallel()
                                .Any(
                                    o =>
                                        o.FirstName.Equals(fullName.First(), StringComparison.OrdinalIgnoreCase) &&
                                        o.LastName.Equals(fullName.Last(), StringComparison.OrdinalIgnoreCase)))
                            return;
                    }
                    if (IsSecondLife() && !TimedTeleportThrottle.IsSafe)
                    {
                        // or fail and append the fail message.
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.TELEPORT_THROTTLED));
                        return;
                    }
                    lock (ClientInstanceSelfLock)
                    {
                        if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                        {
                            Client.Self.Stand();
                        }
                        // stop all non-built-in animations
                        HashSet<UUID> lindenAnimations = new HashSet<UUID>(typeof (Animations).GetProperties(
                            BindingFlags.Public |
                            BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));
                        Parallel.ForEach(Client.Self.SignaledAnimations.Copy().Keys, o =>
                        {
                            if (!lindenAnimations.Contains(o))
                                Client.Self.AnimationStop(o, true);
                        });
                        Client.Self.TeleportLureRespond(args.IM.FromAgentID, args.IM.IMSessionID, true);
                    }
                    return;
                // Group invitations received
                case InstantMessageDialog.GroupInvitation:
                    OpenMetaverse.Group inviteGroup = new OpenMetaverse.Group();
                    if (!RequestGroup(args.IM.FromAgentID, corradeConfiguration.ServicesTimeout, ref inviteGroup))
                        return;
                    // Add the group to the cache.
                    Cache.AddGroup(inviteGroup.Name, inviteGroup.ID);
                    UUID inviteGroupAgent = UUID.Zero;
                    if (
                        !AgentNameToUUID(fullName.First(), fullName.Last(),
                            corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            ref inviteGroupAgent))
                        return;
                    // Add the agent to the cache.
                    Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                    // Add the group invite - have to track them manually.
                    lock (GroupInviteLock)
                    {
                        GroupInvites.Add(new GroupInvite
                        {
                            Agent = new Agent
                            {
                                FirstName = fullName.First(),
                                LastName = fullName.Last(),
                                UUID = inviteGroupAgent
                            },
                            Group = inviteGroup.Name,
                            Session = args.IM.IMSessionID,
                            Fee = inviteGroup.MembershipFee
                        });
                    }
                    // Send group invitation notification.
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.GroupInvite, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    // If a master sends it, then accept.
                    lock (ClientInstanceConfigurationLock)
                    {
                        if (
                            !corradeConfiguration.Masters.AsParallel()
                                .Any(
                                    o =>
                                        o.FirstName.Equals(fullName.First(), StringComparison.OrdinalIgnoreCase) &&
                                        o.LastName.Equals(fullName.Last(), StringComparison.OrdinalIgnoreCase)))
                            return;
                    }
                    Client.Self.GroupInviteRespond(inviteGroup.ID, args.IM.IMSessionID, true);
                    return;
                // Group notice inventory accepted, declined or notice received.
                case InstantMessageDialog.GroupNoticeInventoryAccepted:
                case InstantMessageDialog.GroupNoticeInventoryDeclined:
                case InstantMessageDialog.GroupNotice:
                    CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                        () => SendNotification(Notifications.GroupNotice, args),
                        corradeConfiguration.MaximumNotificationThreads);
                    return;
                case InstantMessageDialog.SessionSend:
                case InstantMessageDialog.MessageFromAgent:
                    // Check if this is a group message.
                    // Note that this is a lousy way of doing it but libomv does not properly set the GroupIM field
                    // such that the only way to determine if we have a group message is to check that the UUID
                    // of the session is actually the UUID of a current group. Furthermore, what's worse is that
                    // group mesages can appear both through SessionSend and from MessageFromAgent. Hence the problem.
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        return;

                    if (new HashSet<UUID>(currentGroups).Contains(args.IM.IMSessionID))
                    {
                        Group messageGroup =
                            corradeConfiguration.Groups.AsParallel()
                                .FirstOrDefault(p => p.UUID.Equals(args.IM.IMSessionID));
                        if (!messageGroup.Equals(default(Group)))
                        {
                            // Add the group to the cache.
                            Cache.AddGroup(messageGroup.Name, messageGroup.UUID);
                            // Add the agent to the cache.
                            Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                            // Send group notice notifications.
                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                () =>
                                    SendNotification(Notifications.GroupMessage, new GroupMessageEventArgs
                                    {
                                        AgentUUID = args.IM.FromAgentID,
                                        FirstName = fullName.First(),
                                        LastName = fullName.Last(),
                                        GroupName = messageGroup.Name,
                                        GroupUUID = messageGroup.UUID,
                                        Message = args.IM.Message
                                    }),
                                corradeConfiguration.MaximumNotificationThreads);
                            // Log group messages
                            Parallel.ForEach(
                                corradeConfiguration.Groups.AsParallel().Where(
                                    o =>
                                        o.Name.Equals(messageGroup.Name, StringComparison.OrdinalIgnoreCase) &&
                                        o.ChatLogEnabled),
                                o =>
                                {
                                    // Attempt to write to log file,
                                    CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                                    {
                                        try
                                        {
                                            lock (GroupLogFileLock)
                                            {
                                                using (
                                                    StreamWriter logWriter = new StreamWriter(o.ChatLog, true,
                                                        Encoding.UTF8)
                                                    )
                                                {
                                                    logWriter.WriteLine("[{0}] {1} {2} : {3}",
                                                        DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                            Utils.EnUsCulture.DateTimeFormat),
                                                        fullName.First(),
                                                        fullName.Last(),
                                                        args.IM.Message);
                                                    //logWriter.Flush();
                                                    //logWriter.Close();
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // or fail and append the fail message.
                                            Feedback(
                                                wasGetDescriptionFromEnumValue(
                                                    ConsoleError.COULD_NOT_WRITE_TO_GROUP_CHAT_LOG_FILE),
                                                ex.Message);
                                        }
                                    }, corradeConfiguration.MaximumLogThreads);
                                });
                        }
                        return;
                    }
                    // Check if this is an instant message.
                    switch (!args.IM.ToAgentID.Equals(Client.Self.AgentID))
                    {
                        case false:
                            // Add the agent to the cache.
                            Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Notifications.InstantMessage, args),
                                corradeConfiguration.MaximumNotificationThreads);
                            // Check if we were ejected.
                            UUID groupUUID = UUID.Zero;
                            if (
                                GroupNameToUUID(
                                    CORRADE_CONSTANTS.EjectedFromGroupRegEx.Match(args.IM.Message).Groups[1].Value,
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    ref groupUUID))
                            {
                                // Remove the group from the cache.
                                Cache.CurrentGroupsCache.Remove(groupUUID);
                            }

                            // Log instant messages,
                            if (corradeConfiguration.InstantMessageLogEnabled)
                            {
                                CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (InstantMessageLogFileLock)
                                        {
                                            using (
                                                StreamWriter logWriter =
                                                    new StreamWriter(
                                                        wasPathCombine(corradeConfiguration.InstantMessageLogDirectory,
                                                            args.IM.FromAgentName) +
                                                        "." + CORRADE_CONSTANTS.LOG_FILE_EXTENSION, true, Encoding.UTF8)
                                                )
                                            {
                                                logWriter.WriteLine("[{0}] {1} {2} : {3}",
                                                    DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                        Utils.EnUsCulture.DateTimeFormat),
                                                    fullName.First(),
                                                    fullName.Last(),
                                                    args.IM.Message);
                                                //logWriter.Flush();
                                                //logWriter.Close();
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            wasGetDescriptionFromEnumValue(
                                                ConsoleError.COULD_NOT_WRITE_TO_INSTANT_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads);
                            }
                            return;
                    }
                    // Check if this is a region message.
                    switch (!args.IM.IMSessionID.Equals(UUID.Zero))
                    {
                        case false:
                            // Add the agent to the cache.
                            Cache.AddAgent(fullName.First(), fullName.Last(), args.IM.FromAgentID);
                            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                () => SendNotification(Notifications.RegionMessage, args),
                                corradeConfiguration.MaximumNotificationThreads);
                            // Log region messages,
                            if (corradeConfiguration.RegionMessageLogEnabled)
                            {
                                CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (RegionLogFileLock)
                                        {
                                            using (
                                                StreamWriter logWriter =
                                                    new StreamWriter(
                                                        wasPathCombine(corradeConfiguration.RegionMessageLogDirectory,
                                                            Client.Network.CurrentSim.Name) + "." +
                                                        CORRADE_CONSTANTS.LOG_FILE_EXTENSION, true, Encoding.UTF8))
                                            {
                                                logWriter.WriteLine("[{0}] {1} {2} : {3}",
                                                    DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                        Utils.EnUsCulture.DateTimeFormat),
                                                    fullName.First(),
                                                    fullName.Last(),
                                                    args.IM.Message);
                                                //logWriter.Flush();
                                                //logWriter.Close();
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            wasGetDescriptionFromEnumValue(
                                                ConsoleError.COULD_NOT_WRITE_TO_REGION_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads);
                            }
                            return;
                    }
                    break;
            }

            // We are now in a region of code where the message is an IM sent by an object.
            // Check if this is not a Corrade command and send an object IM notification.
            if (!IsCorradeCommand(args.IM.Message))
            {
                CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                    () => SendNotification(Notifications.ObjectInstantMessage, args),
                    corradeConfiguration.MaximumNotificationThreads);
                return;
            }

            // If the group was not set properly, then bail.
            Group commandGroup = GetCorradeGroupFromMessage(args.IM.Message);
            switch (!commandGroup.Equals(default(Group)))
            {
                case false:
                    return;
            }
            // Otherwise process the command.
            CorradeThreadPool[CorradeThreadType.COMMAND].Spawn(
                () =>
                    HandleCorradeCommand(args.IM.Message, args.IM.FromAgentName, args.IM.FromAgentID.ToString(),
                        commandGroup),
                corradeConfiguration.MaximumCommandThreads, commandGroup.UUID,
                corradeConfiguration.SchedulerExpiration);
        }

        /// <summary>
        ///     Processes a RLV behaviour.
        /// </summary>
        /// <param name="message">the RLV message to process</param>
        /// <param name="senderUUID">the UUID of the sender</param>
        private static void HandleRLVBehaviour(string message, UUID senderUUID)
        {
            if (string.IsNullOrEmpty(message)) return;

            // Split all commands.
            string[] unpack = message.Split(RLV_CONSTANTS.CSV_DELIMITER[0]);
            // Pop first command to process.
            string first = unpack.First();
            // Remove command.
            unpack = unpack.AsParallel().Where(o => !o.Equals(first)).ToArray();
            // Keep rest of message.
            message = string.Join(RLV_CONSTANTS.CSV_DELIMITER, unpack);

            Match match = RLV_CONSTANTS.RLVRegEx.Match(first);
            if (!match.Success) goto CONTINUE;

            RLVRule RLVrule = new RLVRule
            {
                Behaviour = match.Groups["behaviour"].ToString().ToLowerInvariant(),
                Option = match.Groups["option"].ToString().ToLowerInvariant(),
                Param = match.Groups["param"].ToString().ToLowerInvariant(),
                ObjectUUID = senderUUID
            };

            switch (RLVrule.Param)
            {
                case RLV_CONSTANTS.Y:
                case RLV_CONSTANTS.ADD:
                    if (string.IsNullOrEmpty(RLVrule.Option))
                    {
                        lock (RLVRulesLock)
                        {
                            RLVRules.RemoveWhere(
                                o =>
                                    o.Behaviour.Equals(
                                        RLVrule.Behaviour,
                                        StringComparison.OrdinalIgnoreCase) &&
                                    o.ObjectUUID.Equals(RLVrule.ObjectUUID));
                        }
                        goto CONTINUE;
                    }
                    lock (RLVRulesLock)
                    {
                        RLVRules.RemoveWhere(
                            o =>
                                o.Behaviour.Equals(
                                    RLVrule.Behaviour,
                                    StringComparison.OrdinalIgnoreCase) &&
                                o.ObjectUUID.Equals(RLVrule.ObjectUUID) &&
                                o.Option.Equals(RLVrule.Option, StringComparison.OrdinalIgnoreCase));
                    }
                    goto CONTINUE;
                case RLV_CONSTANTS.N:
                case RLV_CONSTANTS.REM:
                    lock (RLVRulesLock)
                    {
                        RLVRules.RemoveWhere(
                            o =>
                                o.Behaviour.Equals(
                                    RLVrule.Behaviour,
                                    StringComparison.OrdinalIgnoreCase) &&
                                o.Option.Equals(RLVrule.Option, StringComparison.OrdinalIgnoreCase) &&
                                o.ObjectUUID.Equals(RLVrule.ObjectUUID));
                        RLVRules.Add(RLVrule);
                    }
                    goto CONTINUE;
            }

            try
            {
                // Find command.
                RLVBehaviour RLVBehaviour = wasGetEnumValueFromDescription<RLVBehaviour>(RLVrule.Behaviour);
                IsRLVBehaviourAttribute isRLVBehaviourAttribute =
                    wasGetAttributeFromEnumValue<IsRLVBehaviourAttribute>(RLVBehaviour);
                if (isRLVBehaviourAttribute == null || !isRLVBehaviourAttribute.IsRLVBehaviour)
                {
                    throw new Exception(string.Join(CORRADE_CONSTANTS.ERROR_SEPARATOR,
                        wasGetDescriptionFromEnumValue(ConsoleError.BEHAVIOUR_NOT_IMPLEMENTED),
                        RLVrule.Behaviour));
                }
                RLVBehaviourAttribute execute =
                    wasGetAttributeFromEnumValue<RLVBehaviourAttribute>(RLVBehaviour);

                // Execute the command.
                execute.RLVBehaviour.Invoke(message, RLVrule, senderUUID);
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.FAILED_TO_MANIFEST_RLV_BEHAVIOUR), ex.Message);
            }

            CONTINUE:
            HandleRLVBehaviour(message, senderUUID);
        }

        private static Dictionary<string, string> HandleCorradeCommand(string message, string sender, string identifier,
            Group commandGroup)
        {
            // Get password.
            string password =
                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PASSWORD)), message));
            // Bail if no password set.
            if (string.IsNullOrEmpty(password)) return null;
            // Authenticate the request against the group password.
            if (!Authenticate(commandGroup.Name, password))
            {
                Feedback(commandGroup.Name, wasGetDescriptionFromEnumValue(ConsoleError.ACCESS_DENIED));
                return null;
            }
            // Censor password.
            message = wasKeyValueSet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PASSWORD)),
                CORRADE_CONSTANTS.PASSWORD_CENSOR, message);
            /*
             * OpenSim sends the primitive UUID through args.IM.FromAgentID while Second Life properly sends
             * the agent UUID - which just shows how crap and non-compliant OpenSim really is. This tries to
             * resolve args.IM.FromAgentID to a name, which is what Second Life does, otherwise it just sets
             * the name to the name of the primitive sending the message.
             */
            if (IsSecondLife())
            {
                UUID fromAgentID;
                if (UUID.TryParse(identifier, out fromAgentID))
                {
                    if (
                        !AgentUUIDToName(fromAgentID, corradeConfiguration.ServicesTimeout,
                            ref sender))
                    {
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.AGENT_NOT_FOUND),
                            fromAgentID.ToString());
                        return null;
                    }
                }
            }

            // Log the command.
            Feedback(string.Format(Utils.EnUsCulture, "{0} ({1}) : {2}", sender,
                identifier,
                message));

            // Initialize workers for the group if they are not set.
            lock (GroupWorkersLock)
            {
                if (!GroupWorkers.Contains(commandGroup.Name))
                {
                    GroupWorkers.Add(commandGroup.Name, 0u);
                }
            }

            // Check if the workers have not been exceeded.
            lock (GroupWorkersLock)
            {
                if ((uint) GroupWorkers[commandGroup.Name] >
                    corradeConfiguration.Groups.AsParallel().FirstOrDefault(
                        o => o.Name.Equals(commandGroup.Name, StringComparison.OrdinalIgnoreCase)).Workers)
                {
                    // And refuse to proceed if they have.
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.WORKERS_EXCEEDED),
                        commandGroup.Name);
                    return null;
                }
            }

            // Increment the group workers.
            lock (GroupWorkersLock)
            {
                GroupWorkers[commandGroup.Name] = ((uint) GroupWorkers[commandGroup.Name]) + 1;
            }
            // Perform the command.
            Dictionary<string, string> result = ProcessCommand(new CorradeCommandParameters
            {
                Message = message,
                Sender = sender,
                Identifier = identifier,
                Group = commandGroup
            });
            // Decrement the group workers.
            lock (GroupWorkersLock)
            {
                GroupWorkers[commandGroup.Name] = ((uint) GroupWorkers[commandGroup.Name]) - 1;
            }
            // do not send a callback if the callback queue is saturated
            if (CallbackQueue.Count >= corradeConfiguration.CallbackQueueLength)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.CALLBACK_THROTTLED));
                return result;
            }
            // send callback if registered
            string url =
                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.CALLBACK)), message));
            // if no url was provided, do not send the callback
            if (string.IsNullOrEmpty(url)) return result;
            CallbackQueue.Enqueue(new CallbackQueueElement
            {
                URL = url,
                message = wasKeyValueEscape(result)
            });
            return result;
        }

        /// <summary>
        ///     This function is responsible for processing commands.
        /// </summary>
        /// <param name="corradeCommandParameters">the command parameters</param>
        /// <returns>a dictionary of key-value pairs representing the results of the command</returns>
        private static Dictionary<string, string> ProcessCommand(CorradeCommandParameters corradeCommandParameters)
        {
            Dictionary<string, string> result = new Dictionary<string, string>
            {
                // add the command group to the response.
                {wasGetDescriptionFromEnumValue(ScriptKeys.GROUP), corradeCommandParameters.Group.Name}
            };

            // retrieve the command from the message.
            string command =
                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.COMMAND)),
                    corradeCommandParameters.Message));
            if (!string.IsNullOrEmpty(command))
            {
                result.Add(wasGetDescriptionFromEnumValue(ScriptKeys.COMMAND), command);
            }

            // execute command, sift data and check for errors
            bool success = false;
            try
            {
                // Find command.
                ScriptKeys scriptKey = wasGetEnumValueFromDescription<ScriptKeys>(command);
                IsCorradeCommandAttribute isCommandAttribute =
                    wasGetAttributeFromEnumValue<IsCorradeCommandAttribute>(scriptKey);
                if (isCommandAttribute == null || !isCommandAttribute.IsCorradeCorradeCommand)
                {
                    throw new ScriptException(ScriptError.COMMAND_NOT_FOUND);
                }
                CorradeCommandAttribute execute =
                    wasGetAttributeFromEnumValue<CorradeCommandAttribute>(scriptKey);

                // Execute the command.
                execute.CorradeCommand.Invoke(corradeCommandParameters, result);

                // Sift the results
                string pattern =
                    wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SIFT)),
                        corradeCommandParameters.Message));
                string data = string.Empty;
                switch (
                    !string.IsNullOrEmpty(pattern) &&
                    result.TryGetValue(wasGetDescriptionFromEnumValue(ResultKeys.DATA), out data) &&
                    !string.IsNullOrEmpty(data))
                {
                    case true:
                        data = wasEnumerableToCSV((((new Regex(pattern, RegexOptions.Compiled)).Matches(data)
                            .AsParallel()
                            .Cast<Match>()
                            .Select(m => m.Groups)).SelectMany(
                                matchGroups => Enumerable.Range(0, matchGroups.Count).Skip(1),
                                (matchGroups, i) => new {matchGroups, i})
                            .SelectMany(@t => Enumerable.Range(0, @t.matchGroups[@t.i].Captures.Count),
                                (@t, j) => @t.matchGroups[@t.i].Captures[j].Value)));
                        switch (!string.IsNullOrEmpty(data))
                        {
                            case true:
                                result[wasGetDescriptionFromEnumValue(ResultKeys.DATA)] = data;
                                break;
                            default:
                                result.Remove(wasGetDescriptionFromEnumValue(ResultKeys.DATA));
                                break;
                        }
                        break;
                }

                success = true;
            }
            catch (ScriptException sx)
            {
                // we have a script error so return a status as well
                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.ERROR), sx.Message);
                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.STATUS),
                    sx.Status.ToString());
            }
            catch (Exception ex)
            {
                // we have a generic exception so return the message
                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.ERROR), ex.Message);
            }

            // add the final success status
            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.SUCCESS),
                success.ToString(Utils.EnUsCulture));

            // add the time stamp
            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.TIME),
                DateTime.Now.ToUniversalTime().ToString(LINDEN_CONSTANTS.LSL.DATE_TIME_STAMP));

            // build afterburn
            object AfterBurnLock = new object();
            HashSet<string> resultKeys = new HashSet<string>(wasGetEnumDescriptions<ResultKeys>());
            HashSet<string> scriptKeys = new HashSet<string>(wasGetEnumDescriptions<ScriptKeys>());
            Parallel.ForEach(wasKeyValueDecode(corradeCommandParameters.Message), o =>
            {
                // remove keys that are script keys, result keys or invalid key-value pairs
                if (string.IsNullOrEmpty(o.Key) || resultKeys.Contains(wasInput(o.Key)) ||
                    scriptKeys.Contains(wasInput(o.Key)) ||
                    string.IsNullOrEmpty(o.Value))
                    return;
                lock (AfterBurnLock)
                {
                    result.Add(o.Key, o.Value);
                }
            });

            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the values from structures as strings.
        /// </summary>
        /// <typeparam name="T">the type of the structure</typeparam>
        /// <param name="structure">the structure</param>
        /// <param name="query">a CSV list of fields or properties to get</param>
        /// <returns>value strings</returns>
        private static IEnumerable<string> GetStructuredData<T>(T structure, string query)
        {
            HashSet<string[]> result = new HashSet<string[]>();
            switch (!structure.Equals(default(T)))
            {
                case false:
                    return result.SelectMany(o => o);
            }
            List<string> data;
            object LockObject = new object();
            Parallel.ForEach(wasCSVToEnumerable(query).AsParallel().Where(o => !string.IsNullOrEmpty(o)), name =>
            {
                KeyValuePair<FieldInfo, object> fi =
                    wasGetFields(structure, structure.GetType().Name).AsParallel()
                        .FirstOrDefault(o => o.Key.Name.Equals(name, StringComparison.Ordinal));

                lock (LockObject)
                {
                    data = new List<string> {name};
                    data.AddRange(wasGetInfo(fi.Key, fi.Value));
                    if (data.Count >= 2)
                    {
                        result.Add(data.ToArray());
                    }
                }

                KeyValuePair<PropertyInfo, object> pi =
                    wasGetProperties(structure, structure.GetType().Name).AsParallel().FirstOrDefault(
                        o => o.Key.Name.Equals(name, StringComparison.Ordinal));

                lock (LockObject)
                {
                    data = new List<string> {name};
                    data.AddRange(wasGetInfo(pi.Key, pi.Value));
                    if (data.Count >= 2)
                    {
                        result.Add(data.ToArray());
                    }
                }
            });
            return result.SelectMany(o => o);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Takes as input a CSV data values and sets the corresponding
        ///     structure's fields or properties from the CSV data.
        /// </summary>
        /// <typeparam name="T">the type of the structure</typeparam>
        /// <param name="data">a CSV string</param>
        /// <param name="structure">the structure to set the fields and properties for</param>
        private static void wasCSVToStructure<T>(string data, ref T structure)
        {
            foreach (
                KeyValuePair<string, string> match in
                    wasCSVToEnumerable(data).AsParallel().Select((o, p) => new {o, p})
                        .GroupBy(q => q.p/2, q => q.o)
                        .Select(o => o.ToList())
                        .TakeWhile(o => o.Count%2 == 0)
                        .Where(o => !string.IsNullOrEmpty(o.First()) || !string.IsNullOrEmpty(o.Last()))
                        .ToDictionary(o => o.First(), p => p.Last()))
            {
                KeyValuePair<string, string> localMatch = match;
                KeyValuePair<FieldInfo, object> fi =
                    wasGetFields(structure, structure.GetType().Name)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Key.Name.Equals(localMatch.Key,
                                    StringComparison.Ordinal));

                wasSetInfo(fi.Key, fi.Value, match.Value, ref structure);

                KeyValuePair<PropertyInfo, object> pi =
                    wasGetProperties(structure, structure.GetType().Name)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Key.Name.Equals(localMatch.Key,
                                    StringComparison.Ordinal));

                wasSetInfo(pi.Key, pi.Value, match.Value, ref structure);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Sends a post request to an URL with set key-value pairs.
        /// </summary>
        /// <param name="URL">the url to send the message to</param>
        /// <param name="message">key-value pairs to send</param>
        /// <param name="millisecondsTimeout">the time in milliseconds for the request to timeout</param>
        private static void wasPOST(string URL, Dictionary<string, string> message, uint millisecondsTimeout)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(URL);
                request.UserAgent = CORRADE_CONSTANTS.USER_AGENT;
                request.Proxy = WebRequest.DefaultWebProxy;
                request.Pipelined = true;
                request.KeepAlive = true;
                request.Timeout = (int) millisecondsTimeout;
                request.ReadWriteTimeout = (int) millisecondsTimeout;
                request.AllowAutoRedirect = true;
                request.AllowWriteStreamBuffering = true;
                request.Method = WebRequestMethods.Http.Post;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                // set the content type based on chosen output filers
                switch (corradeConfiguration.OutputFilters.Last())
                {
                    case Filter.RFC1738:
                        request.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.WWW_FORM_URLENCODED;
                        break;
                    default:
                        request.ContentType = CORRADE_CONSTANTS.CONTENT_TYPE.TEXT_PLAIN;
                        break;
                }
                // send request
                using (Stream requestStream = request.GetRequestStream())
                {
                    using (StreamWriter dataStream = new StreamWriter(requestStream))
                    {
                        dataStream.Write(wasKeyValueEncode(message));
                    }
                }
                // read response
                using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            using (StreamReader streamReader = new StreamReader(responseStream))
                            {
                                streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_MAKING_POST_REQUEST), URL, ex.Message);
            }
        }

        private static void HandleTerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.TerseUpdates, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleRadarObjects(object sender, SimChangedEventArgs e)
        {
            lock (RadarObjectsLock)
            {
                if (RadarObjects.Any())
                {
                    RadarObjects.Clear();
                }
            }
        }

        private static void HandleSimChanged(object sender, SimChangedEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.RegionCrossed, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleMoneyBalance(object sender, BalanceEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Balance, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        private static void HandleMoneyBalance(object sender, MoneyBalanceReplyEventArgs e)
        {
            CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                () => SendNotification(Notifications.Economy, e),
                corradeConfiguration.MaximumNotificationThreads);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>URI unescapes an RFC3986 URI escaped string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>the resulting string</returns>
        private static string wasURIUnescapeDataString(string data)
        {
            // Uri.UnescapeDataString can only handle 32766 characters at a time
            return string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766)
                .Select(o => Uri.UnescapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - (o*32766)))))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC3986 URI Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC3986 escaped string</returns>
        private static string wasURIEscapeDataString(string data)
        {
            // Uri.EscapeDataString can only handle 32766 characters at a time
            return string.Join("", Enumerable.Range(0, (data.Length + 32765)/32766)
                .Select(o => Uri.EscapeDataString(data.Substring(o*32766, Math.Min(32766, data.Length - (o*32766)))))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Escapes a string</summary>
        /// <param name="data">a string to escape</param>
        /// <returns>an RFC1738 escaped string</returns>
        private static string wasURLEscapeDataString(string data)
        {
            return HttpUtility.UrlEncode(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>RFC1738 URL Unescape a string</summary>
        /// <param name="data">a string to unescape</param>
        /// <returns>an RFC1738 unescaped string</returns>
        private static string wasURLUnescapeDataString(string data)
        {
            return HttpUtility.UrlDecode(data);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a list of string to a comma-separated values string.
        /// </summary>
        /// <param name="l">a list of strings</param>
        /// <returns>a commma-separated list of values</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static string wasEnumerableToCSV(IEnumerable<string> l)
        {
            string[] csv = l.Select(o => o.Clone() as string).ToArray();
            Parallel.ForEach(csv.Select((v, i) => new {i, v}), o =>
            {
                string cell = o.v.Replace("\"", "\"\"");
                switch (new[] {'"', ' ', ',', '\r', '\n'}.Any(p => cell.Contains(p)))
                {
                    case true:
                        csv[o.i] = "\"" + cell + "\"";
                        break;
                    default:
                        csv[o.i] = cell;
                        break;
                }
            });
            return string.Join(",", csv);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a comma-separated list of values to a list of strings.
        /// </summary>
        /// <param name="csv">a comma-separated list of values</param>
        /// <returns>a list of strings</returns>
        /// <remarks>compliant with RFC 4180</remarks>
        public static IEnumerable<string> wasCSVToEnumerable(string csv)
        {
            Stack<char> s = new Stack<char>();
            StringBuilder m = new StringBuilder();
            for (int i = 0; i < csv.Length; ++i)
            {
                switch (csv[i])
                {
                    case ',':
                        if (!s.Any() || !s.Peek().Equals('"'))
                        {
                            yield return m.ToString();
                            m = new StringBuilder();
                            continue;
                        }
                        m.Append(csv[i]);
                        continue;
                    case '"':
                        if (i + 1 < csv.Length && csv[i].Equals(csv[i + 1]))
                        {
                            m.Append(csv[i]);
                            ++i;
                            continue;
                        }
                        if (!s.Any() || !s.Peek().Equals(csv[i]))
                        {
                            s.Push(csv[i]);
                            continue;
                        }
                        s.Pop();
                        continue;
                }
                m.Append(csv[i]);
            }

            yield return m.ToString();
        }

        /// <summary>
        ///     Material information for Collada DAE Export.
        /// </summary>
        /// <remarks>This class is taken from the Radegast Viewer with changes by Wizardry and Steamworks.</remarks>
        private class MaterialInfo
        {
            public Color4 Color;
            public string Name;
            public UUID TextureID;

            public bool Matches(Primitive.TextureEntryFace TextureEntry)
            {
                return TextureID.Equals(TextureEntry.TextureID) && Color.Equals(TextureEntry.RGBA);
            }
        }

        /// <summary>
        ///     Constants for Corrade's integrated chat bot.
        /// </summary>
        private struct AIML_BOT_CONSTANTS
        {
            public const string DIRECTORY = @"AIMLBot";
            public const string BRAIN_FILE = @"AIMLBot.brain";
            public const string BRAIN_SESSION_FILE = @"AIMLbot.session";

            public struct AIML
            {
                public const string DIRECTORY = @"AIML";
            }

            public struct BRAIN
            {
                public const string DIRECTORY = @"brain";
            }

            public struct CONFIG
            {
                public const string DIRECTORY = @"config";
                public const string SETTINGS_FILE = @"Settings.xml";
                public const string NAME = @"NAME";
                public const string AIMLDIRECTORY = @"AIMLDIRECTORY";
                public const string CONFIGDIRECTORY = @"CONFIGDIRECTORY";
                public const string LOGDIRECTORY = @"LOGDIRECTORY";
            }

            public struct LOG
            {
                public const string DIRECTORY = @"logs";
            }
        }

        /// <summary>
        ///     Possible actions.
        /// </summary>
        private enum Action : uint
        {
            [Description("none")] NONE = 0,
            [Description("get")] GET,
            [Description("set")] SET,
            [Description("add")] ADD,
            [Description("remove")] REMOVE,
            [Description("start")] START,
            [Description("stop")] STOP,
            [Description("mute")] MUTE,
            [Description("unmute")] UNMUTE,
            [Description("restart")] RESTART,
            [Description("cancel")] CANCEL,
            [Description("accept")] ACCEPT,
            [Description("decline")] DECLINE,
            [Description("online")] ONLINE,
            [Description("offline")] OFFLINE,
            [Description("request")] REQUEST,
            [Description("response")] RESPONSE,
            [Description("delete")] DELETE,
            [Description("take")] TAKE,
            [Description("read")] READ,
            [Description("wrtie")] WRITE,
            [Description("purge")] PURGE,
            [Description("crossed")] CROSSED,
            [Description("changed")] CHANGED,
            [Description("reply")] REPLY,
            [Description("offer")] OFFER,
            [Description("generic")] GENERIC,
            [Description("point")] POINT,
            [Description("look")] LOOK,
            [Description("update")] UPDATE,
            [Description("received")] RECEIVED,
            [Description("joined")] JOINED,
            [Description("parted")] PARTED,
            [Description("save")] SAVE,
            [Description("load")] LOAD,
            [Description("enable")] ENABLE,
            [Description("disable")] DISABLE,
            [Description("process")] PROCESS,
            [Description("rebuild")] REBUILD,
            [Description("clear")] CLEAR,
            [Description("ls")] LS,
            [Description("cwd")] CWD,
            [Description("cd")] CD,
            [Description("mkdir")] MKDIR,
            [Description("chmod")] CHMOD,
            [Description("rm")] RM,
            [Description("ln")] LN,
            [Description("mv")] MV,
            [Description("cp")] CP,
            [Description("appear")] APPEAR,
            [Description("vanish")] VANISH,
            [Description("list")] LIST,
            [Description("link")] LINK,
            [Description("delink")] DELINK,
            [Description("ban")] BAN,
            [Description("unban")] UNBAN
        }

        /// <summary>
        ///     Agent structure.
        /// </summary>
        private struct Agent
        {
            [Description("firstname")] public string FirstName;
            [Description("lastname")] public string LastName;
            [Description("uuid")] public UUID UUID;
        }

        /// <summary>
        ///     A structure to track Beam effects.
        /// </summary>
        private struct BeamEffect
        {
            [Description("alpha")] public float Alpha;
            [Description("color")] public Vector3 Color;
            [Description("duration")] public float Duration;
            [Description("effect")] public UUID Effect;
            [Description("offset")] public Vector3d Offset;
            [Description("source")] public UUID Source;
            [Description("target")] public UUID Target;
            [Description("termination")] public DateTime Termination;
        }

        /// <summary>
        ///     Constants used by Corrade.
        /// </summary>
        private struct CORRADE_CONSTANTS
        {
            /// <summary>
            ///     Copyright.
            /// </summary>
            public const string COPYRIGHT = @"(c) Copyright 2013 Wizardry and Steamworks";

            public const string WIZARDRY_AND_STEAMWORKS = @"Wizardry and Steamworks";
            public const string CORRADE = @"Corrade";
            public const string WIZARDRY_AND_STEAMWORKS_WEBSITE = @"http://grimore.org";

            /// <summary>
            ///     Censor characters for passwords.
            /// </summary>
            public const string PASSWORD_CENSOR = "***";

            /// <summary>
            ///     Corrade channel sent to the simulator.
            /// </summary>
            public const string CLIENT_CHANNEL = @"[Wizardry and Steamworks]:Corrade";

            public const string CURRENT_OUTFIT_FOLDER_NAME = @"Current Outfit";
            public const string DEFAULT_SERVICE_NAME = @"Corrade";
            public const string LOG_FACILITY = @"Application";
            public const string WEB_REQUEST = @"Web Request";
            public const string CONFIGURATION_FILE = @"Corrade.ini";
            public const string DATE_TIME_STAMP = @"dd-MM-yyyy HH:mm:ss";
            public const string INVENTORY_CACHE_FILE = @"Inventory.cache";
            public const string AGENT_CACHE_FILE = @"Agent.cache";
            public const string GROUP_CACHE_FILE = @"Group.cache";
            public const string PATH_SEPARATOR = @"/";
            public const string ERROR_SEPARATOR = @" : ";
            public const string CACHE_DIRECTORY = @"cache";
            public const string ASSET_CACHE_DIRECTORY = @"assets";
            public const string LOG_FILE_EXTENSION = @"log";
            public const string STATE_DIRECTORY = @"state";
            public const string NOTIFICATIONS_STATE_FILE = @"Notifications.state";
            public const string GROUP_MEMBERS_STATE_FILE = @"GroupMembers.state";
            public const string GROUP_SCHEDULES_STATE_FILE = @"GroupSchedules.state";
            public const string MOVEMENT_STATE_FILE = @"Movement.state";
            public const string LIBS_DIRECTORY = @"libs";

            public static readonly Regex AvatarFullNameRegex = new Regex(@"^(?<first>.*?)([\s\.]|$)(?<last>.*?)$",
                RegexOptions.Compiled);

            public static readonly Regex OneOrMoRegex = new Regex(@".+?", RegexOptions.Compiled);

            public static readonly Regex InventoryOfferObjectNameRegEx = new Regex(@"^[']{0,1}(.+?)(('\s)|$)",
                RegexOptions.Compiled);

            public static readonly Regex EjectedFromGroupRegEx =
                new Regex(@"You have been ejected from '(.+?)' by .+?\.$", RegexOptions.Compiled);

            /// <summary>
            ///     Corrade version.
            /// </summary>
            public static readonly string CORRADE_VERSION = Assembly.GetEntryAssembly().GetName().Version.ToString();

            /// <summary>
            ///     Corrade user agent.
            /// </summary>
            public static readonly string USER_AGENT =
                $"{CORRADE}/{CORRADE_VERSION} ({WIZARDRY_AND_STEAMWORKS_WEBSITE})";

            /// <summary>
            ///     Corrade compile date.
            /// </summary>
            public static readonly string CORRADE_COMPILE_DATE = new DateTime(2000, 1, 1).Add(new TimeSpan(
                TimeSpan.TicksPerDay*Assembly.GetEntryAssembly().GetName().Version.Build + // days since 1 January 2000
                TimeSpan.TicksPerSecond*2*Assembly.GetEntryAssembly().GetName().Version.Revision)).ToLongDateString();

            /// <summary>
            ///     Corrade Logo.
            /// </summary>
            public static readonly List<string> LOGO = new List<string>
            {
                @"",
                @"       _..--=--..._  ",
                @"    .-'            '-.  .-.  ",
                @"   /.'              '.\/  /  ",
                @"  |=-     Corrade    -=| (  ",
                @"   \'.              .'/\  \  ",
                @"    '-.,_____ _____.-'  '-'  ",
                @"          [_____]=8  ",
                @"               \  ",
                @"                 Good day!  ",
                @"",
                string.Format(Utils.EnUsCulture, "Version: {0}, Compiled: {1}", CORRADE_VERSION,
                    CORRADE_COMPILE_DATE),
                string.Format(Utils.EnUsCulture, "Copyright: {0}", COPYRIGHT)
            };

            /// <summary>
            ///     Conten-types that Corrade can send and receive.
            /// </summary>
            public struct CONTENT_TYPE
            {
                public const string TEXT_PLAIN = @"text/plain";
                public const string WWW_FORM_URLENCODED = @"application/x-www-form-urlencoded";
            }

            public struct PERMISSIONS
            {
                public const string NONE = @"------------------------------";
            }

            public struct PRIMTIVE_BODIES
            {
                [Description("cube")] public static readonly Primitive.ConstructionData CUBE = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.Square,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("prism")] public static readonly Primitive.ConstructionData PRISM = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 0.0f,
                    PathScaleY = 1.0f,
                    PathShearX = -0.5f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.Square,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("pyramid")] public static readonly Primitive.ConstructionData PYRAMID = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 0.0f,
                    PathScaleY = 0.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.Square,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("tetrahedron")] public static readonly Primitive.ConstructionData TETRAHEDRON = new Primitive
                    .ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 0.0f,
                    PathScaleY = 0.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.EqualTriangle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("cylinder")] public static readonly Primitive.ConstructionData CYLINDER = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.Circle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("hemicylinder")] public static readonly Primitive.ConstructionData HEMICYLINDER = new Primitive
                    .ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0.0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.25f,
                    ProfileCurve = ProfileCurve.Circle,
                    ProfileEnd = 0.75f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("cone")] public static readonly Primitive.ConstructionData CONE = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 0.0f,
                    PathScaleY = 0.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.Circle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("hemicone")] public static readonly Primitive.ConstructionData HEMICONE = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0f,
                    PathCurve = PathCurve.Line,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 0.0f,
                    PathScaleY = 0.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.25f,
                    ProfileCurve = ProfileCurve.Circle,
                    ProfileEnd = 0.75f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("sphere")] public static readonly Primitive.ConstructionData SPHERE = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0.0f,
                    PathCurve = PathCurve.Circle,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.HalfCircle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("hemisphere")] public static readonly Primitive.ConstructionData HEMISPHERE = new Primitive
                    .ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0.0f,
                    PathCurve = PathCurve.Circle,
                    PathEnd = 0.5f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.HalfCircle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("torus")] public static readonly Primitive.ConstructionData TORUS = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0.0f,
                    PathCurve = PathCurve.Circle,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 0.25f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.Circle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };

                [Description("ring")] public static readonly Primitive.ConstructionData RING = new Primitive.
                    ConstructionData
                {
                    AttachmentPoint = AttachmentPoint.Default,
                    Material = Material.Wood,
                    PathBegin = 0.0f,
                    PathCurve = PathCurve.Circle,
                    PathEnd = 1.0f,
                    PathRadiusOffset = 0.0f,
                    PathRevolutions = 1.0f,
                    PathScaleX = 1.0f,
                    PathScaleY = 0.25f,
                    PathShearX = 0.0f,
                    PathShearY = 0.0f,
                    PathSkew = 0.0f,
                    PathTaperX = 0.0f,
                    PathTaperY = 0.0f,
                    PathTwistBegin = 0.0f,
                    PCode = PCode.Prim,
                    ProfileBegin = 0.0f,
                    ProfileCurve = ProfileCurve.EqualTriangle,
                    ProfileEnd = 1.0f,
                    ProfileHole = HoleType.Same,
                    ProfileHollow = 0.0f,
                    State = 0
                };
            }
        }

        /// <summary>
        ///     Corrade's caches.
        /// </summary>
        public struct Cache
        {
            private static HashSet<Agents> _agentCache = new HashSet<Agents>();
            private static HashSet<Groups> _groupCache = new HashSet<Groups>();
            private static HashSet<UUID> _currentGroupsCache = new HashSet<UUID>();
            private static HashSet<MuteEntry> _mutesCache;
            private static readonly object AgentCacheLock = new object();
            private static readonly object GroupCacheLock = new object();
            private static readonly object CurrentGroupsCacheLock = new object();
            private static readonly object MutesCacheLock = new object();

            public static HashSet<Agents> AgentCache
            {
                get
                {
                    lock (AgentCacheLock)
                    {
                        return _agentCache;
                    }
                }
                set
                {
                    lock (AgentCacheLock)
                    {
                        _agentCache = value;
                    }
                }
            }

            public static HashSet<Groups> GroupCache
            {
                get
                {
                    lock (GroupCacheLock)
                    {
                        return _groupCache;
                    }
                }
                set
                {
                    lock (AgentCacheLock)
                    {
                        _groupCache = value;
                    }
                }
            }

            public static HashSet<UUID> CurrentGroupsCache
            {
                get
                {
                    lock (CurrentGroupsCacheLock)
                    {
                        return _currentGroupsCache;
                    }
                }
                set
                {
                    lock (AgentCacheLock)
                    {
                        _currentGroupsCache = value;
                    }
                }
            }

            public static HashSet<MuteEntry> MutesCache
            {
                get
                {
                    lock (MutesCacheLock)
                    {
                        return _mutesCache;
                    }
                }
                set
                {
                    lock (AgentCacheLock)
                    {
                        _mutesCache = value;
                    }
                }
            }

            internal static void Purge()
            {
                AgentCache.Clear();
                GroupCache.Clear();
                CurrentGroupsCache.Clear();
                MutesCache.Clear();
            }

            public static void AddAgent(string FirstName, string LastName, UUID agentUUID)
            {
                Agents agent = new Agents
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    UUID = agentUUID
                };

                if (!AgentCache.Contains(agent))
                {
                    AgentCache.Add(agent);
                }
            }

            public static Agents GetAgent(string FirstName, string LastName)
            {
                return AgentCache.AsParallel().FirstOrDefault(
                    o =>
                        o.FirstName.Equals(FirstName, StringComparison.OrdinalIgnoreCase) &&
                        o.LastName.Equals(LastName, StringComparison.OrdinalIgnoreCase));
            }

            public static Agents GetAgent(UUID agentUUID)
            {
                return AgentCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(agentUUID));
            }

            public static void AddGroup(string GroupName, UUID GroupUUID)
            {
                Groups group = new Groups
                {
                    Name = GroupName,
                    UUID = GroupUUID
                };
                if (!GroupCache.Contains(group))
                {
                    GroupCache.Add(group);
                }
            }

            /// <summary>
            ///     Serializes to a file.
            /// </summary>
            /// <param name="FileName">File path of the new xml file</param>
            /// <param name="o">the object to save</param>
            public static void Save<T>(string FileName, T o)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(FileName, false, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (T));
                        serializer.Serialize(writer, o);
                        writer.Flush();
                    }
                }
                catch (Exception e)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_SAVE_CORRADE_CACHE), e.Message);
                }
            }

            /// <summary>
            ///     Load an object from an xml file
            /// </summary>
            /// <param name="FileName">Xml file name</param>
            /// <param name="o">the object to load to</param>
            /// <returns>The object created from the xml file</returns>
            public static T Load<T>(string FileName, T o)
            {
                if (!File.Exists(FileName)) return o;
                try
                {
                    using (StreamReader stream = new StreamReader(FileName, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (T));
                        return (T) serializer.Deserialize(stream);
                    }
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_LOAD_CORRADE_CACHE), ex.Message);
                }
                return o;
            }

            public struct Agents
            {
                public string FirstName;
                public string LastName;
                public UUID UUID;
            }

            public struct Groups
            {
                public string Name;
                public UUID UUID;
            }
        }

        /// <summary>
        ///     An element from the callback queue waiting to be dispatched.
        /// </summary>
        private struct CallbackQueueElement
        {
            public Dictionary<string, string> message;
            public string URL;
        }

        [Serializable]
        public class CorradeConfiguration
        {
            private uint _activateDelay = 5000;
            private byte[] _AESIV;
            private byte[] _AESKey;
            private bool _autoActivateGroup;
            private string _bindIPAddress = string.Empty;
            private uint _callbackQueueLength = 100;
            private uint _callbackThrottle = 1000;
            private uint _callbackTimeout = 5000;
            private UUID _clientIdentificationTag = new UUID("0705230f-cbd0-99bd-040b-28eb348b5255");
            private bool _clientLogEnabled = true;
            private string _clientLogFile = "logs/Corrade.log";
            private uint _connectionIdleTime = 900000;
            private uint _connectionLimit = 100;
            private wasAdaptiveAlarm.DECAY_TYPE _dataDecayType = wasAdaptiveAlarm.DECAY_TYPE.ARITHMETIC;
            private uint _dataTimeout = 2500;
            private string _driveIdentifierHash = string.Empty;
            private bool _enableAIML;
            private bool _enableHTTPServer;
            private bool _enableRLV;
            private bool _enableTCPNotificationsServer;

            private ENIGMA _enigma = new ENIGMA
            {
                rotors = new[] {'3', 'g', '1'},
                plugs = new[] {'z', 'p', 'q'},
                reflector = 'b'
            };

            private int _exitCodeAbnormal = -2;
            private int _exitCodeExpected = -1;
            private string _firstName = string.Empty;
            private uint _groupCreateFee = 100;
            private HashSet<Group> _groups = new HashSet<Group>();
            private uint _HTTPServerBodyTimeout = 5000;
            private HTTPCompressionMethod _HTTPServerCompression = HTTPCompressionMethod.NONE;
            private uint _HTTPServerDrainTimeout = 10000;
            private uint _HTTPServerHeaderTimeout = 2500;
            private uint _HTTPServerIdleTimeout = 2500;
            private bool _HTTPServerKeepAlive = true;
            private string _HTTPServerPrefix = @"http://+:8080/";
            private uint _HTTPServerQueueTimeout = 10000;
            private uint _HTTPServerTimeout = 5000;
            private List<Filter> _inputFilters = new List<Filter>();
            private string _instantMessageLogDirectory = @"logs/im";
            private bool _instantMessageLogEnabled;
            private string _lastName = string.Empty;
            private string _localMessageLogDirectory = @"logs/local";
            private bool _localMessageLogEnabled;
            private string _loginURL = @"https://login.agni.lindenlab.com/cgi-bin/login.cgi";
            private uint _logoutGrace = 2500;
            private HashSet<Master> _masters = new HashSet<Master>();
            private uint _maximumCommandThreads = 10;
            private uint _maximumInstantMessageThreads = 10;
            private uint _maximumLogThreads = 40;
            private uint _maximumNotificationThreads = 10;
            private uint _maximumPOSTThreads = 25;
            private uint _maximumRLVThreads = 10;
            private uint _membershipSweepInterval = 60000;
            private string _networkCardMAC = string.Empty;
            private uint _notificationQueueLength = 100;
            private uint _notificationThrottle = 1000;
            private uint _notificationTimeout = 5000;
            private List<Filter> _outputFilters = new List<Filter>();
            private string _password = string.Empty;
            private float _range = 64;
            private uint _rebakeDelay = 1000;
            private string _regionMessageLogDirectory = @"logs/region";
            private bool _regionMessageLogEnabled;
            private uint _schedulerExpiration = 60000;
            private uint _schedulesResolution = 1000;
            private uint _servicesTimeout = 60000;
            private string _startLocation = "last";
            private uint _TCPnotificationQueueLength = 100;
            private string _TCPNotificationsServerAddress = @"0.0.0.0";
            private uint _TCPNotificationsServerPort = 8095;
            private uint _TCPnotificationThrottle = 1000;
            private uint _throttleAsset = 100000;
            private uint _throttleCloud = 10000;
            private uint _throttleLand = 80000;
            private uint _throttleResend = 100000;
            private uint _throttleTask = 200000;
            private uint _throttleTexture = 100000;
            private uint _throttleTotal = 600000;
            private uint _throttleWind = 10000;
            private bool _TOSAccepted;
            private bool _useExpect100Continue;
            private bool _useNaggle;
            private string _vigenereSecret = string.Empty;

            public string FirstName
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _firstName;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _firstName = value;
                    }
                }
            }

            public string LastName
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _lastName;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _lastName = value;
                    }
                }
            }

            public string Password
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _password;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _password = value;
                    }
                }
            }

            public string LoginURL
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _loginURL;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _loginURL = value;
                    }
                }
            }

            public string InstantMessageLogDirectory
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _instantMessageLogDirectory;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _instantMessageLogDirectory = value;
                    }
                }
            }

            public bool InstantMessageLogEnabled
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _instantMessageLogEnabled;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _instantMessageLogEnabled = value;
                    }
                }
            }

            public string LocalMessageLogDirectory
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _localMessageLogDirectory;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _localMessageLogDirectory = value;
                    }
                }
            }

            public bool LocalMessageLogEnabled
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _localMessageLogEnabled;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _localMessageLogEnabled = value;
                    }
                }
            }

            public string RegionMessageLogDirectory
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _regionMessageLogDirectory;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _regionMessageLogDirectory = value;
                    }
                }
            }

            public bool RegionMessageLogEnabled
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _regionMessageLogEnabled;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _regionMessageLogEnabled = value;
                    }
                }
            }

            public bool EnableHTTPServer
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enableHTTPServer;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enableHTTPServer = value;
                    }
                }
            }

            public bool EnableTCPNotificationsServer
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enableTCPNotificationsServer;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enableTCPNotificationsServer = value;
                    }
                }
            }

            public bool EnableAIML
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enableAIML;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enableAIML = value;
                    }
                }
            }

            public bool EnableRLV
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enableRLV;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enableRLV = value;
                    }
                }
            }

            public string HTTPServerPrefix
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerPrefix;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerPrefix = value;
                    }
                }
            }

            public uint TCPNotificationsServerPort
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _TCPNotificationsServerPort;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _TCPNotificationsServerPort = value;
                    }
                }
            }

            public string TCPNotificationsServerAddress
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _TCPNotificationsServerAddress;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _TCPNotificationsServerAddress = value;
                    }
                }
            }

            public uint HTTPServerTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerTimeout = value;
                    }
                }
            }

            public uint HTTPServerDrainTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerDrainTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerDrainTimeout = value;
                    }
                }
            }

            public uint HTTPServerBodyTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerBodyTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerBodyTimeout = value;
                    }
                }
            }

            public uint HTTPServerHeaderTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerHeaderTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerHeaderTimeout = value;
                    }
                }
            }

            public uint HTTPServerIdleTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerIdleTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerIdleTimeout = value;
                    }
                }
            }

            public uint HTTPServerQueueTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerQueueTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerQueueTimeout = value;
                    }
                }
            }

            public HTTPCompressionMethod HTTPServerCompression
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerCompression;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerCompression = value;
                    }
                }
            }

            public uint ThrottleTotal
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleTotal;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleTotal = value;
                    }
                }
            }

            public uint ThrottleLand
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleLand;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleLand = value;
                    }
                }
            }

            public uint ThrottleTask
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleTask;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleTask = value;
                    }
                }
            }

            public uint ThrottleTexture
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleTexture;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleTexture = value;
                    }
                }
            }

            public uint ThrottleWind
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleWind;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleWind = value;
                    }
                }
            }

            public uint ThrottleResend
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleResend;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleResend = value;
                    }
                }
            }

            public uint ThrottleAsset
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleAsset;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleAsset = value;
                    }
                }
            }

            public uint ThrottleCloud
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _throttleCloud;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _throttleCloud = value;
                    }
                }
            }

            public bool HTTPServerKeepAlive
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _HTTPServerKeepAlive;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _HTTPServerKeepAlive = value;
                    }
                }
            }

            public uint CallbackTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _callbackTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _callbackTimeout = value;
                    }
                }
            }

            public uint CallbackThrottle
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _callbackThrottle;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _callbackThrottle = value;
                    }
                }
            }

            public uint CallbackQueueLength
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _callbackQueueLength;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _callbackQueueLength = value;
                    }
                }
            }

            public uint NotificationTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _notificationTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _notificationTimeout = value;
                    }
                }
            }

            public uint NotificationThrottle
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _notificationThrottle;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _notificationThrottle = value;
                    }
                }
            }

            public uint TCPNotificationThrottle
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _TCPnotificationThrottle;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _TCPnotificationThrottle = value;
                    }
                }
            }

            public uint NotificationQueueLength
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _notificationQueueLength;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _notificationQueueLength = value;
                    }
                }
            }

            public uint TCPNotificationQueueLength
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _TCPnotificationQueueLength;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _TCPnotificationQueueLength = value;
                    }
                }
            }

            public uint ConnectionLimit
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _connectionLimit;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _connectionLimit = value;
                    }
                }
            }

            public uint ConnectionIdleTime
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _connectionIdleTime;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _connectionIdleTime = value;
                    }
                }
            }

            public float Range
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _range;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _range = value;
                    }
                }
            }

            public uint SchedulerExpiration
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _schedulerExpiration;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _schedulerExpiration = value;
                    }
                }
            }

            public uint MaximumNotificationThreads
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _maximumNotificationThreads;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _maximumNotificationThreads = value;
                    }
                }
            }

            public uint MaximumCommandThreads
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _maximumCommandThreads;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _maximumCommandThreads = value;
                    }
                }
            }

            public uint MaximumRLVThreads
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _maximumRLVThreads;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _maximumRLVThreads = value;
                    }
                }
            }

            public uint MaximumInstantMessageThreads
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _maximumInstantMessageThreads;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _maximumInstantMessageThreads = value;
                    }
                }
            }

            public uint MaximumLogThreads
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _maximumLogThreads;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _maximumLogThreads = value;
                    }
                }
            }

            public uint MaximumPOSTThreads
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _maximumPOSTThreads;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _maximumPOSTThreads = value;
                    }
                }
            }

            public bool UseNaggle
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _useNaggle;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _useNaggle = value;
                    }
                }
            }

            public bool UseExpect100Continue
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _useExpect100Continue;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _useExpect100Continue = value;
                    }
                }
            }

            public uint ServicesTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _servicesTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _servicesTimeout = value;
                    }
                }
            }

            public uint DataTimeout
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _dataTimeout;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _dataTimeout = value;
                    }
                }
            }

            public wasAdaptiveAlarm.DECAY_TYPE DataDecayType
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _dataDecayType;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _dataDecayType = value;
                    }
                }
            }

            public uint RebakeDelay
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _rebakeDelay;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _rebakeDelay = value;
                    }
                }
            }

            public uint MembershipSweepInterval
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _membershipSweepInterval;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _membershipSweepInterval = value;
                    }
                }
            }

            public bool TOSAccepted
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _TOSAccepted;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _TOSAccepted = value;
                    }
                }
            }

            public UUID ClientIdentificationTag
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _clientIdentificationTag;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _clientIdentificationTag = value;
                    }
                }
            }

            public string StartLocation
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _startLocation;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _startLocation = value;
                    }
                }
            }

            public string BindIPAddress
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _bindIPAddress;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _bindIPAddress = value;
                    }
                }
            }

            public string NetworkCardMAC
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _networkCardMAC;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _networkCardMAC = value;
                    }
                }
            }

            public string DriveIdentifierHash
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _driveIdentifierHash;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _driveIdentifierHash = value;
                    }
                }
            }

            public string ClientLogFile
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _clientLogFile;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _clientLogFile = value;
                    }
                }
            }

            public bool ClientLogEnabled
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _clientLogEnabled;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _clientLogEnabled = value;
                    }
                }
            }

            public bool AutoActivateGroup
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _autoActivateGroup;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _autoActivateGroup = value;
                    }
                }
            }

            public uint ActivateDelay
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _activateDelay;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _activateDelay = value;
                    }
                }
            }

            public uint GroupCreateFee
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _groupCreateFee;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _groupCreateFee = value;
                    }
                }
            }

            public int ExitCodeExpected
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _exitCodeExpected;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _exitCodeExpected = value;
                    }
                }
            }

            public int ExitCodeAbnormal
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _exitCodeAbnormal;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _exitCodeAbnormal = value;
                    }
                }
            }

            public HashSet<Group> Groups
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _groups;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _groups = value;
                    }
                }
            }

            public HashSet<Master> Masters
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _masters;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _masters = value;
                    }
                }
            }

            public List<Filter> InputFilters
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _inputFilters;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _inputFilters = value;
                    }
                }
            }

            public List<Filter> OutputFilters
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _outputFilters;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _outputFilters = value;
                    }
                }
            }

            public byte[] AESKey
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _AESKey;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _AESKey = value;
                    }
                }
            }

            public byte[] AESIV
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _AESIV;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _AESIV = value;
                    }
                }
            }

            public string VIGENERESecret
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _vigenereSecret;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _vigenereSecret = value;
                    }
                }
            }

            public ENIGMA ENIGMA
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _enigma;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _enigma = value;
                    }
                }
            }

            public uint LogoutGrace
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _logoutGrace;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _logoutGrace = value;
                    }
                }
            }

            public uint SchedulesResolution
            {
                get
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        return _schedulesResolution;
                    }
                }
                set
                {
                    lock (ClientInstanceConfigurationLock)
                    {
                        _schedulesResolution = value;
                    }
                }
            }

            public string Read(string file)
            {
                return File.ReadAllText(file, Encoding.UTF8);
            }

            public void Write(string file, string data)
            {
                File.WriteAllText(file, data, Encoding.UTF8);
            }

            public void Write(string file, XmlDocument document)
            {
                using (TextWriter writer = new StreamWriter(file, false, Encoding.UTF8))
                {
                    document.Save(writer);
                    writer.Flush();
                }
            }

            public void Save(string file, ref CorradeConfiguration configuration)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof (CorradeConfiguration));
                        serializer.Serialize(writer, configuration);
                        writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION),
                        ex.Message);
                }
            }

            public void Load(string file, ref CorradeConfiguration configuration)
            {
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.READING_CORRADE_CONFIGURATION));
                try
                {
                    using (StreamReader stream = new StreamReader(file, Encoding.UTF8))
                    {
                        XmlSerializer serializer =
                            new XmlSerializer(typeof (CorradeConfiguration));
                        CorradeConfiguration loadedConfiguration = (CorradeConfiguration) serializer.Deserialize(stream);
                        configuration = loadedConfiguration;
                    }
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.UNABLE_TO_LOAD_CORRADE_CONFIGURATION),
                        ex.Message);
                    return;
                }
                UpdateDynamicConfiguration(configuration);
                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.READ_CORRADE_CONFIGURATION));
            }

            public void UpdateDynamicConfiguration(CorradeConfiguration configuration)
            {
                // Enable the group scheduling thread if permissions were granted to groups.
                switch (configuration.Groups.AsParallel()
                    .Any(o => !(o.PermissionMask & (uint) Permissions.Schedule).Equals(0) && !o.Schedules.Equals(0)))
                {
                    case true:
                        // Don't start if the expiration thread is already started.
                        if (GroupSchedulesThread != null) return;
                        // Start sphere and beam effect expiration thread
                        runGroupSchedulesThread = true;
                        GroupSchedulesThread = new Thread(() =>
                        {
                            do
                            {
                                // Check schedules with a one second resolution.
                                Thread.Sleep((int) corradeConfiguration.SchedulesResolution);
                                HashSet<GroupSchedule> groupSchedules;
                                lock (GroupSchedulesLock)
                                {
                                    groupSchedules =
                                        new HashSet<GroupSchedule>(GroupSchedules.AsParallel()
                                            .Where(
                                                o =>
                                                    DateTime.Compare(DateTime.Now.ToUniversalTime(),
                                                        o.At) >= 0));
                                }
                                if (groupSchedules.Any())
                                {
                                    Parallel.ForEach(groupSchedules,
                                        o =>
                                        {
                                            // Spawn the command.
                                            CorradeThreadPool[CorradeThreadType.COMMAND].Spawn(
                                                () => HandleCorradeCommand(o.Message, o.Sender, o.Identifier, o.Group),
                                                corradeConfiguration.MaximumCommandThreads, o.Group.UUID,
                                                corradeConfiguration.SchedulerExpiration);
                                            lock (GroupSchedulesLock)
                                            {
                                                GroupSchedules.Remove(o);
                                            }
                                        });
                                    SaveGroupSchedulesState.Invoke();
                                }
                            } while (runGroupSchedulesThread);
                        })
                        {IsBackground = true};
                        GroupSchedulesThread.Start();
                        break;
                    default:
                        runGroupSchedulesThread = false;
                        try
                        {
                            if (GroupSchedulesThread != null)
                            {
                                if (
                                    (GroupSchedulesThread.ThreadState.Equals(ThreadState.Running) ||
                                     GroupSchedulesThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                                {
                                    if (!GroupSchedulesThread.Join(1000))
                                    {
                                        GroupSchedulesThread.Abort();
                                        GroupSchedulesThread.Join();
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            /* We are going down and we do not care. */
                        }
                        finally
                        {
                            GroupSchedulesThread = null;
                        }
                        break;
                }
                // Enable AIML in case it was enabled in the configuration file.
                try
                {
                    switch (configuration.EnableAIML)
                    {
                        case true:
                            switch (!AIMLBotBrainCompiled)
                            {
                                case true:
                                    new Thread(
                                        () =>
                                        {
                                            lock (AIMLBotLock)
                                            {
                                                LoadChatBotFiles.Invoke();
                                                AIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                                            }
                                        })
                                    {IsBackground = true}.Start();
                                    break;
                                default:
                                    AIMLBotConfigurationWatcher.EnableRaisingEvents = true;
                                    AIMLBot.isAcceptingUserInput = true;
                                    break;
                            }
                            break;
                        default:
                            AIMLBotConfigurationWatcher.EnableRaisingEvents = false;
                            AIMLBot.isAcceptingUserInput = false;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Feedback(wasGetDescriptionFromEnumValue(ConsoleError.ERROR_SETTING_UP_AIML_CONFIGURATION_WATCHER),
                        ex.Message);
                }

                // Dynamically disable or enable notifications.
                Parallel.ForEach(wasGetEnumDescriptions<Notifications>().AsParallel().Select(
                    wasGetEnumValueFromDescription<Notifications>), o =>
                    {
                        bool enabled = configuration.Groups.AsParallel().Any(
                            p =>
                                !(p.NotificationMask & (uint) o).Equals(0));
                        switch (o)
                        {
                            case Notifications.GroupMembership:
                                switch (enabled)
                                {
                                    case true:
                                        // Start the group membership thread.
                                        StartGroupMembershipSweepThread.Invoke();
                                        break;
                                    default:
                                        // Stop the group sweep thread.
                                        StopGroupMembershipSweepThread.Invoke();
                                        break;
                                }
                                break;
                            case Notifications.Friendship:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Friends.FriendshipOffered += HandleFriendshipOffered;
                                        Client.Friends.FriendshipResponse += HandleFriendShipResponse;
                                        Client.Friends.FriendOnline += HandleFriendOnlineStatus;
                                        Client.Friends.FriendOffline += HandleFriendOnlineStatus;
                                        Client.Friends.FriendRightsUpdate += HandleFriendRightsUpdate;
                                        break;
                                    default:
                                        Client.Friends.FriendshipOffered -= HandleFriendshipOffered;
                                        Client.Friends.FriendshipResponse -= HandleFriendShipResponse;
                                        Client.Friends.FriendOnline -= HandleFriendOnlineStatus;
                                        Client.Friends.FriendOffline -= HandleFriendOnlineStatus;
                                        Client.Friends.FriendRightsUpdate -= HandleFriendRightsUpdate;
                                        break;
                                }
                                break;
                            case Notifications.ScriptPermission:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.ScriptQuestion += HandleScriptQuestion;
                                        break;
                                    default:
                                        Client.Self.ScriptQuestion -= HandleScriptQuestion;
                                        break;
                                }
                                break;
                            case Notifications.AlertMessage:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.AlertMessage += HandleAlertMessage;
                                        break;
                                    default:
                                        Client.Self.AlertMessage -= HandleAlertMessage;
                                        break;
                                }
                                break;
                            case Notifications.Balance:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.MoneyBalance += HandleMoneyBalance;
                                        break;
                                    default:
                                        Client.Self.MoneyBalance -= HandleMoneyBalance;
                                        break;
                                }
                                break;
                            case Notifications.Economy:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.MoneyBalanceReply += HandleMoneyBalance;
                                        break;
                                    default:
                                        Client.Self.MoneyBalanceReply -= HandleMoneyBalance;
                                        break;
                                }
                                break;
                            case Notifications.ScriptDialog:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.ScriptDialog += HandleScriptDialog;
                                        break;
                                    default:
                                        Client.Self.ScriptDialog -= HandleScriptDialog;
                                        break;
                                }
                                break;
                            case Notifications.TerseUpdates:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Objects.TerseObjectUpdate += HandleTerseObjectUpdate;
                                        break;
                                    default:
                                        Client.Objects.TerseObjectUpdate -= HandleTerseObjectUpdate;
                                        break;
                                }
                                break;
                            case Notifications.ViewerEffect:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Avatars.ViewerEffect += HandleViewerEffect;
                                        Client.Avatars.ViewerEffectPointAt += HandleViewerEffect;
                                        Client.Avatars.ViewerEffectLookAt += HandleViewerEffect;
                                        break;
                                    default:
                                        Client.Avatars.ViewerEffect -= HandleViewerEffect;
                                        Client.Avatars.ViewerEffectPointAt -= HandleViewerEffect;
                                        Client.Avatars.ViewerEffectLookAt -= HandleViewerEffect;
                                        break;
                                }
                                break;
                            case Notifications.MeanCollision:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.MeanCollision += HandleMeanCollision;
                                        break;
                                    default:
                                        Client.Self.MeanCollision -= HandleMeanCollision;
                                        break;
                                }
                                break;
                            case Notifications.RegionCrossed:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.RegionCrossed += HandleRegionCrossed;
                                        Client.Network.SimChanged += HandleSimChanged;
                                        break;
                                    default:
                                        Client.Self.RegionCrossed -= HandleRegionCrossed;
                                        Client.Network.SimChanged -= HandleSimChanged;
                                        break;
                                }
                                break;
                            case Notifications.LoadURL:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.LoadURL += HandleLoadURL;
                                        break;
                                    default:
                                        Client.Self.LoadURL -= HandleLoadURL;
                                        break;
                                }
                                break;
                            case Notifications.ScriptControl:
                                switch (enabled)
                                {
                                    case true:
                                        Client.Self.ScriptControlChange += HandleScriptControlChange;
                                        break;
                                    default:
                                        Client.Self.ScriptControlChange -= HandleScriptControlChange;
                                        break;
                                }
                                break;
                        }
                    });

                // Depending on whether groups have bound to the viewer effects notification,
                // start or stop the viwer effect expiration thread.
                switch (
                    configuration.Groups.AsParallel()
                        .Any(o => !(o.NotificationMask & (uint) Notifications.ViewerEffect).Equals(0)))
                {
                    case true:
                        // Don't start if the expiration thread is already started.
                        if (EffectsExpirationThread != null) return;
                        // Start sphere and beam effect expiration thread
                        runEffectsExpirationThread = true;
                        EffectsExpirationThread = new Thread(() =>
                        {
                            do
                            {
                                Thread.Sleep(1000);
                                lock (SphereEffectsLock)
                                {
                                    SphereEffects.RemoveWhere(o => DateTime.Compare(DateTime.Now, o.Termination) > 0);
                                }
                                lock (BeamEffectsLock)
                                {
                                    BeamEffects.RemoveWhere(o => DateTime.Compare(DateTime.Now, o.Termination) > 0);
                                }
                            } while (runEffectsExpirationThread);
                        })
                        {IsBackground = true};
                        EffectsExpirationThread.Start();
                        break;
                    default:
                        runEffectsExpirationThread = false;
                        try
                        {
                            if (EffectsExpirationThread != null)
                            {
                                if (
                                    (EffectsExpirationThread.ThreadState.Equals(ThreadState.Running) ||
                                     EffectsExpirationThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                                {
                                    if (!EffectsExpirationThread.Join(1000))
                                    {
                                        EffectsExpirationThread.Abort();
                                        EffectsExpirationThread.Join();
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            /* We are going down and we do not care. */
                        }
                        finally
                        {
                            EffectsExpirationThread = null;
                        }
                        break;
                }

                // Depending on whether any group has bound either the avatar radar notification,
                // or the primitive radar notification, install or uinstall the listeners.
                switch (
                    configuration.Groups.AsParallel().Any(
                        o =>
                            !(o.NotificationMask & (uint) Notifications.RadarAvatars).Equals(0) ||
                            !(o.NotificationMask & (uint) Notifications.RadarPrimitives).Equals(0)))
                {
                    case true:
                        Client.Network.SimChanged += HandleRadarObjects;
                        Client.Objects.AvatarUpdate += HandleAvatarUpdate;
                        Client.Objects.ObjectUpdate += HandleObjectUpdate;
                        Client.Objects.KillObject += HandleKillObject;
                        break;
                    default:
                        Client.Network.SimChanged -= HandleRadarObjects;
                        Client.Objects.AvatarUpdate -= HandleAvatarUpdate;
                        Client.Objects.ObjectUpdate -= HandleObjectUpdate;
                        Client.Objects.KillObject -= HandleKillObject;
                        break;
                }

                // Enable the TCP notifications server in case it was enabled in the configuration.
                switch (configuration.EnableTCPNotificationsServer)
                {
                    case true:
                        // Don't start if the TCP notifications server is already started.
                        if (TCPNotificationsThread != null) return;
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.STARTING_TCP_NOTIFICATIONS_SERVER));
                        runTCPNotificationsServer = true;
                        // Start the TCP notifications server.
                        TCPNotificationsThread = new Thread(() =>
                        {
                            TCPListener =
                                new TcpListener(
                                    new IPEndPoint(IPAddress.Parse(configuration.TCPNotificationsServerAddress),
                                        (int) configuration.TCPNotificationsServerPort));
                            TCPListener.Start();

                            do
                            {
                                TcpClient TCPClient = TCPListener.AcceptTcpClient();

                                new Thread(() =>
                                {
                                    IPEndPoint remoteEndPoint = null;
                                    Group commandGroup = new Group();
                                    try
                                    {
                                        remoteEndPoint = TCPClient.Client.RemoteEndPoint as IPEndPoint;
                                        using (NetworkStream networkStream = TCPClient.GetStream())
                                        {
                                            using (
                                                StreamReader streamReader = new StreamReader(networkStream,
                                                    Encoding.UTF8))
                                            {
                                                string receiveLine = streamReader.ReadLine();

                                                using (
                                                    StreamWriter streamWriter = new StreamWriter(networkStream,
                                                        Encoding.UTF8))
                                                {
                                                    commandGroup = GetCorradeGroupFromMessage(receiveLine);
                                                    switch (!commandGroup.Equals(default(Group)) &&
                                                            Authenticate(commandGroup.Name,
                                                                wasInput(
                                                                    wasKeyValueGet(
                                                                        wasOutput(
                                                                            wasGetDescriptionFromEnumValue(
                                                                                ScriptKeys.PASSWORD)),
                                                                        receiveLine))))
                                                    {
                                                        case false:
                                                            streamWriter.WriteLine(
                                                                wasKeyValueEncode(new Dictionary<string, string>
                                                                {
                                                                    {
                                                                        wasGetDescriptionFromEnumValue(
                                                                            ScriptKeys.SUCCESS),
                                                                        false.ToString()
                                                                    }
                                                                }));
                                                            streamWriter.Flush();
                                                            TCPClient.Close();
                                                            return;
                                                    }

                                                    string notificationTypes =
                                                        wasInput(
                                                            wasKeyValueGet(
                                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                                                receiveLine));
                                                    Notification notification;
                                                    lock (GroupNotificationsLock)
                                                    {
                                                        notification =
                                                            GroupNotifications.AsParallel().FirstOrDefault(
                                                                o =>
                                                                    o.GroupName.Equals(commandGroup.Name,
                                                                        StringComparison.OrdinalIgnoreCase));
                                                    }
                                                    // Build any requested data for raw notifications.
                                                    string fields =
                                                        wasInput(
                                                            wasKeyValueGet(
                                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                                                receiveLine));
                                                    HashSet<string> data = new HashSet<string>();
                                                    object LockObject = new object();
                                                    if (!string.IsNullOrEmpty(fields))
                                                    {
                                                        Parallel.ForEach(
                                                            wasCSVToEnumerable(fields)
                                                                .AsParallel()
                                                                .Where(o => !string.IsNullOrEmpty(o)),
                                                            o =>
                                                            {
                                                                lock (LockObject)
                                                                {
                                                                    data.Add(o);
                                                                }
                                                            });
                                                    }
                                                    switch (!notification.Equals(default(Notification)))
                                                    {
                                                        case false:
                                                            notification = new Notification
                                                            {
                                                                GroupName = commandGroup.Name,
                                                                GroupUUID = commandGroup.UUID,
                                                                NotificationURLDestination =
                                                                    new SerializableDictionary
                                                                        <Notifications, HashSet<string>>(),
                                                                NotificationTCPDestination =
                                                                    new Dictionary<Notifications, HashSet<IPEndPoint>>(),
                                                                Data = data
                                                            };
                                                            break;
                                                        case true:
                                                            if (notification.NotificationTCPDestination == null)
                                                            {
                                                                notification.NotificationTCPDestination =
                                                                    new Dictionary<Notifications, HashSet<IPEndPoint>>();
                                                            }
                                                            if (notification.NotificationURLDestination == null)
                                                            {
                                                                notification.NotificationURLDestination =
                                                                    new SerializableDictionary
                                                                        <Notifications, HashSet<string>>();
                                                            }
                                                            break;
                                                    }

                                                    bool succeeded = true;
                                                    Parallel.ForEach(wasCSVToEnumerable(
                                                        notificationTypes)
                                                        .AsParallel()
                                                        .Where(o => !string.IsNullOrEmpty(o)),
                                                        (o, state) =>
                                                        {
                                                            uint notificationValue =
                                                                (uint) wasGetEnumValueFromDescription<Notifications>(o);
                                                            if (
                                                                !GroupHasNotification(commandGroup.Name,
                                                                    notificationValue))
                                                            {
                                                                // one of the notification was not allowed, so abort
                                                                succeeded = false;
                                                                state.Break();
                                                            }
                                                            switch (
                                                                !notification.NotificationTCPDestination.ContainsKey(
                                                                    (Notifications) notificationValue))
                                                            {
                                                                case true:
                                                                    lock (LockObject)
                                                                    {
                                                                        notification.NotificationTCPDestination.Add(
                                                                            (Notifications) notificationValue,
                                                                            new HashSet<IPEndPoint> {remoteEndPoint});
                                                                    }
                                                                    break;
                                                                default:
                                                                    lock (LockObject)
                                                                    {
                                                                        notification.NotificationTCPDestination[
                                                                            (Notifications) notificationValue].Add(
                                                                                remoteEndPoint);
                                                                    }
                                                                    break;
                                                            }
                                                        });

                                                    switch (succeeded)
                                                    {
                                                        case true:
                                                            lock (GroupNotificationsLock)
                                                            {
                                                                // Replace notification.
                                                                GroupNotifications.RemoveWhere(
                                                                    o =>
                                                                        o.GroupName.Equals(commandGroup.Name,
                                                                            StringComparison.OrdinalIgnoreCase));
                                                                GroupNotifications.Add(notification);
                                                            }
                                                            // Save the notifications state.
                                                            SaveNotificationState.Invoke();
                                                            streamWriter.WriteLine(
                                                                wasKeyValueEncode(new Dictionary<string, string>
                                                                {
                                                                    {
                                                                        wasGetDescriptionFromEnumValue(
                                                                            ScriptKeys.SUCCESS),
                                                                        true.ToString()
                                                                    }
                                                                }));
                                                            streamWriter.Flush();
                                                            break;
                                                        default:
                                                            streamWriter.WriteLine(
                                                                wasKeyValueEncode(new Dictionary<string, string>
                                                                {
                                                                    {
                                                                        wasGetDescriptionFromEnumValue(
                                                                            ScriptKeys.SUCCESS),
                                                                        false.ToString()
                                                                    }
                                                                }));
                                                            streamWriter.Flush();
                                                            TCPClient.Close();
                                                            return;
                                                    }
                                                    do
                                                    {
                                                        NotificationTCPQueueElement notificationTCPQueueElement =
                                                            new NotificationTCPQueueElement();
                                                        if (
                                                            NotificationTCPQueue.Dequeue(
                                                                (int) configuration.TCPNotificationThrottle,
                                                                ref notificationTCPQueueElement))
                                                        {
                                                            if (
                                                                !notificationTCPQueueElement.Equals(
                                                                    default(NotificationTCPQueueElement)) &&
                                                                notificationTCPQueueElement.IPEndPoint.Equals(
                                                                    remoteEndPoint))
                                                            {
                                                                streamWriter.WriteLine(
                                                                    wasKeyValueEncode(
                                                                        notificationTCPQueueElement.message));
                                                                streamWriter.Flush();
                                                            }
                                                        }
                                                    } while (runTCPNotificationsServer && TCPClient.Connected);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Feedback(
                                            wasGetDescriptionFromEnumValue(ConsoleError.TCP_NOTIFICATIONS_SERVER_ERROR),
                                            ex.Message);
                                    }
                                    finally
                                    {
                                        if (remoteEndPoint != null && !commandGroup.Equals(default(Group)))
                                        {
                                            lock (GroupNotificationsLock)
                                            {
                                                Notification notification =
                                                    GroupNotifications.FirstOrDefault(
                                                        o =>
                                                            o.GroupName.Equals(commandGroup.Name,
                                                                StringComparison.OrdinalIgnoreCase));
                                                if (!notification.Equals(default(Notification)))
                                                {
                                                    Dictionary<Notifications, HashSet<IPEndPoint>>
                                                        notificationTCPDestination =
                                                            new Dictionary<Notifications, HashSet<IPEndPoint>>();
                                                    Parallel.ForEach(notification.NotificationTCPDestination, o =>
                                                    {
                                                        switch (o.Value.Contains(remoteEndPoint))
                                                        {
                                                            case true:
                                                                HashSet<IPEndPoint> destinations =
                                                                    new HashSet<IPEndPoint>(
                                                                        o.Value.Where(p => !p.Equals(remoteEndPoint)));
                                                                notificationTCPDestination.Add(o.Key, destinations);
                                                                break;
                                                            default:
                                                                notificationTCPDestination.Add(o.Key, o.Value);
                                                                break;
                                                        }
                                                    });

                                                    GroupNotifications.Remove(notification);
                                                    GroupNotifications.Add(new Notification
                                                    {
                                                        GroupName = notification.GroupName,
                                                        GroupUUID = notification.GroupUUID,
                                                        NotificationURLDestination =
                                                            notification.NotificationURLDestination,
                                                        NotificationTCPDestination = notificationTCPDestination,
                                                        Afterburn = notification.Afterburn,
                                                        Data = notification.Data
                                                    });
                                                }
                                            }
                                        }
                                    }
                                })
                                {IsBackground = true}.Start();
                            } while (runTCPNotificationsServer);
                        })
                        {IsBackground = true};
                        TCPNotificationsThread.Start();
                        break;
                    default:
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.STOPPING_TCP_NOTIFICATIONS_SERVER));
                        runTCPNotificationsServer = false;
                        try
                        {
                            if (TCPNotificationsThread != null)
                            {
                                TCPListener.Stop();
                                if (
                                    (TCPNotificationsThread.ThreadState.Equals(ThreadState.Running) ||
                                     TCPNotificationsThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                                {
                                    if (!TCPNotificationsThread.Join(1000))
                                    {
                                        TCPNotificationsThread.Abort();
                                        TCPNotificationsThread.Join();
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            /* We are going down and we do not care. */
                        }
                        finally
                        {
                            TCPNotificationsThread = null;
                        }
                        break;
                }

                // Enable the HTTP server in case it is supported and it was enabled in the configuration.
                switch (HttpListener.IsSupported)
                {
                    case true:
                        switch (configuration.EnableHTTPServer)
                        {
                            case true:
                                // Don't start if the HTTP server is already started.
                                if (HTTPListenerThread != null) return;
                                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.STARTING_HTTP_SERVER));
                                runHTTPServer = true;
                                HTTPListenerThread = new Thread(() =>
                                {
                                    try
                                    {
                                        using (HTTPListener = new HttpListener())
                                        {
                                            HTTPListener.Prefixes.Add(configuration.HTTPServerPrefix);
                                            // TimeoutManager is not supported on mono (what is mono good for anyway, practically speaking?).
                                            switch (Environment.OSVersion.Platform)
                                            {
                                                case PlatformID.Win32NT:
                                                    // We have to set this through reflection to prevent mono from bombing.
                                                    PropertyInfo pi =
                                                        HTTPListener.GetType()
                                                            .GetProperty("TimeoutManager",
                                                                BindingFlags.Public | BindingFlags.Instance);
                                                    object timeoutManager = pi?.GetValue(HTTPListener, null);
                                                    // Check if we have TimeoutManager.
                                                    if (timeoutManager == null) break;
                                                    // Now, set the properties through reflection.
                                                    pi = timeoutManager.GetType().GetProperty("DrainEntityBody");
                                                    pi?.SetValue(timeoutManager,
                                                        TimeSpan.FromMilliseconds(configuration.HTTPServerDrainTimeout),
                                                        null);
                                                    pi = timeoutManager.GetType().GetProperty("EntityBody");
                                                    pi?.SetValue(timeoutManager,
                                                        TimeSpan.FromMilliseconds(configuration.HTTPServerBodyTimeout),
                                                        null);
                                                    pi = timeoutManager.GetType().GetProperty("HeaderWait");
                                                    pi?.SetValue(timeoutManager,
                                                        TimeSpan.FromMilliseconds(configuration.HTTPServerHeaderTimeout),
                                                        null);
                                                    pi = timeoutManager.GetType().GetProperty("IdleConnection");
                                                    pi?.SetValue(timeoutManager,
                                                        TimeSpan.FromMilliseconds(configuration.HTTPServerIdleTimeout),
                                                        null);
                                                    pi = timeoutManager.GetType().GetProperty("RequestQueue");
                                                    pi?.SetValue(timeoutManager,
                                                        TimeSpan.FromMilliseconds(configuration.HTTPServerQueueTimeout),
                                                        null);
                                                    break;
                                            }
                                            HTTPListener.Start();
                                            while (runHTTPServer && HTTPListener.IsListening)
                                            {
                                                (HTTPListener.BeginGetContext(ProcessHTTPRequest,
                                                    HTTPListener)).AsyncWaitHandle.WaitOne(
                                                        (int) configuration.HTTPServerTimeout,
                                                        false);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.HTTP_SERVER_ERROR),
                                            ex.Message);
                                    }
                                })
                                {IsBackground = true};
                                HTTPListenerThread.Start();
                                break;
                            default:
                                Feedback(wasGetDescriptionFromEnumValue(ConsoleError.STOPPING_HTTP_SERVER));
                                runHTTPServer = false;
                                try
                                {
                                    if (HTTPListenerThread != null)
                                    {
                                        HTTPListener.Stop();
                                        if (
                                            (HTTPListenerThread.ThreadState.Equals(ThreadState.Running) ||
                                             HTTPListenerThread.ThreadState.Equals(ThreadState.WaitSleepJoin)))
                                        {
                                            if (!HTTPListenerThread.Join(1000))
                                            {
                                                HTTPListenerThread.Abort();
                                                HTTPListenerThread.Join();
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    /* We are going down and we do not care. */
                                }
                                finally
                                {
                                    HTTPListenerThread = null;
                                }
                                break;
                        }
                        break;
                    default:
                        Feedback(wasGetDescriptionFromEnumValue(ConsoleError.HTTP_SERVER_ERROR),
                            wasGetDescriptionFromEnumValue(ConsoleError.HTTP_SERVER_NOT_SUPPORTED));
                        break;
                }

                // Apply settings to the instance.
                Client.Self.Movement.Camera.Far = configuration.Range;
                Client.Settings.LOGIN_TIMEOUT = (int) configuration.ServicesTimeout;
                Client.Settings.LOGOUT_TIMEOUT = (int) configuration.ServicesTimeout;
                Client.Settings.SIMULATOR_TIMEOUT = (int) configuration.ServicesTimeout;
                Client.Settings.CAPS_TIMEOUT = (int) configuration.ServicesTimeout;
                Client.Settings.MAP_REQUEST_TIMEOUT = (int) configuration.ServicesTimeout;
                Client.Settings.TRANSFER_TIMEOUT = (int) configuration.ServicesTimeout;
                Client.Settings.TELEPORT_TIMEOUT = (int) configuration.ServicesTimeout;
                Settings.MAX_HTTP_CONNECTIONS = (int) configuration.ConnectionLimit;

                // Network Settings
                ServicePointManager.DefaultConnectionLimit = (int) corradeConfiguration.ConnectionLimit;
                ServicePointManager.UseNagleAlgorithm = corradeConfiguration.UseNaggle;
                ServicePointManager.Expect100Continue = corradeConfiguration.UseExpect100Continue;
                ServicePointManager.MaxServicePointIdleTime = (int) corradeConfiguration.ConnectionIdleTime;

                // Throttles.
                Client.Throttle.Total = corradeConfiguration.ThrottleTotal;
                Client.Throttle.Land = corradeConfiguration.ThrottleLand;
                Client.Throttle.Task = corradeConfiguration.ThrottleTask;
                Client.Throttle.Texture = corradeConfiguration.ThrottleTexture;
                Client.Throttle.Wind = corradeConfiguration.ThrottleWind;
                Client.Throttle.Resend = corradeConfiguration.ThrottleResend;
                Client.Throttle.Asset = corradeConfiguration.ThrottleAsset;
                Client.Throttle.Cloud = corradeConfiguration.ThrottleCloud;

                // Client Identification Tag.
                Client.Settings.CLIENT_IDENTIFICATION_TAG = corradeConfiguration.ClientIdentificationTag;
            }
        }

        /// <summary>
        ///     Structure containing error messages printed on console for the owner.
        /// </summary>
        private enum ConsoleError
        {
            [Description("none")] NONE = 0,
            [Description("access denied")] ACCESS_DENIED,

            [Description(
                "the Terms of Service (TOS) for the grid you are connecting to have not been accepted, please check your configuration file"
                )] TOS_NOT_ACCEPTED,
            [Description("teleport failed")] TELEPORT_FAILED,
            [Description("teleport succeeded")] TELEPORT_SUCCEEDED,
            [Description("accepted friendship")] ACCEPTED_FRIENDSHIP,
            [Description("login failed")] LOGIN_FAILED,
            [Description("login succeeded")] LOGIN_SUCCEEDED,
            [Description("failed to set appearance")] APPEARANCE_SET_FAILED,
            [Description("appearance set")] APPEARANCE_SET_SUCCEEDED,
            [Description("all simulators disconnected")] ALL_SIMULATORS_DISCONNECTED,
            [Description("simulator connected")] SIMULATOR_CONNECTED,
            [Description("event queue started")] EVENT_QUEUE_STARTED,
            [Description("disconnected")] DISCONNECTED,
            [Description("logging out")] LOGGING_OUT,
            [Description("logging in")] LOGGING_IN,
            [Description("agent not found")] AGENT_NOT_FOUND,
            [Description("reading Corrade configuration")] READING_CORRADE_CONFIGURATION,
            [Description("read Corrade configuration")] READ_CORRADE_CONFIGURATION,
            [Description("configuration file modified")] CONFIGURATION_FILE_MODIFIED,
            [Description("HTTP server error")] HTTP_SERVER_ERROR,
            [Description("HTTP server not supported")] HTTP_SERVER_NOT_SUPPORTED,
            [Description("starting HTTP server")] STARTING_HTTP_SERVER,
            [Description("stopping HTTP server")] STOPPING_HTTP_SERVER,
            [Description("HTTP server processing aborted")] HTTP_SERVER_PROCESSING_ABORTED,
            [Description("timeout logging out")] TIMEOUT_LOGGING_OUT,
            [Description("callback error")] CALLBACK_ERROR,
            [Description("notification error")] NOTIFICATION_ERROR,
            [Description("inventory cache items loaded")] INVENTORY_CACHE_ITEMS_LOADED,
            [Description("inventory cache items saved")] INVENTORY_CACHE_ITEMS_SAVED,
            [Description("unable to load Corrade cache")] UNABLE_TO_LOAD_CORRADE_CACHE,
            [Description("unable to save Corrade cache")] UNABLE_TO_SAVE_CORRADE_CACHE,
            [Description("failed to manifest RLV behaviour")] FAILED_TO_MANIFEST_RLV_BEHAVIOUR,
            [Description("behaviour not implemented")] BEHAVIOUR_NOT_IMPLEMENTED,
            [Description("workers exceeded")] WORKERS_EXCEEDED,
            [Description("AIML bot configuration modified")] AIML_CONFIGURATION_MODIFIED,
            [Description("read AIML bot configuration")] READ_AIML_BOT_CONFIGURATION,
            [Description("reading AIML bot configuration")] READING_AIML_BOT_CONFIGURATION,
            [Description("wrote AIML bot configuration")] WROTE_AIML_BOT_CONFIGURATION,
            [Description("writing AIML bot configuration")] WRITING_AIML_BOT_CONFIGURATION,
            [Description("error loading AIML bot files")] ERROR_LOADING_AIML_BOT_FILES,
            [Description("error saving AIML bot files")] ERROR_SAVING_AIML_BOT_FILES,
            [Description("could not write to client log file")] COULD_NOT_WRITE_TO_CLIENT_LOG_FILE,
            [Description("could not write to group chat log file")] COULD_NOT_WRITE_TO_GROUP_CHAT_LOG_FILE,
            [Description("could not write to instant message log file")] COULD_NOT_WRITE_TO_INSTANT_MESSAGE_LOG_FILE,
            [Description("could not write to local message log file")] COULD_NOT_WRITE_TO_LOCAL_MESSAGE_LOG_FILE,
            [Description("could not write to region message log file")] COULD_NOT_WRITE_TO_REGION_MESSAGE_LOG_FILE,
            [Description("unknown IP address")] UNKNOWN_IP_ADDRESS,
            [Description("unable to save Corrade notifications state")] UNABLE_TO_SAVE_CORRADE_NOTIFICATIONS_STATE,
            [Description("unable to load Corrade notifications state")] UNABLE_TO_LOAD_CORRADE_NOTIFICATIONS_STATE,
            [Description("unknwon notification type")] UNKNOWN_NOTIFICATION_TYPE,
            [Description("teleport throttled")] TELEPORT_THROTTLED,
            [Description("uncaught exception for thread")] UNCAUGHT_EXCEPTION_FOR_THREAD,
            [Description("error setting up configuration watcher")] ERROR_SETTING_UP_CONFIGURATION_WATCHER,
            [Description("error setting up AIML configuration watcher")] ERROR_SETTING_UP_AIML_CONFIGURATION_WATCHER,
            [Description("callback throttled")] CALLBACK_THROTTLED,
            [Description("notification throttled")] NOTIFICATION_THROTTLED,
            [Description("error updating inventory")] ERROR_UPDATING_INVENTORY,
            [Description("unable to load group members state")] UNABLE_TO_LOAD_GROUP_MEMBERS_STATE,
            [Description("unable to save group members state")] UNABLE_TO_SAVE_GROUP_MEMBERS_STATE,
            [Description("error making POST request")] ERROR_MAKING_POST_REQUEST,
            [Description("notifications file modified")] NOTIFICATIONS_FILE_MODIFIED,
            [Description("unable to load Corrade configuration")] UNABLE_TO_LOAD_CORRADE_CONFIGURATION,
            [Description("unable to save Corrade configuration")] UNABLE_TO_SAVE_CORRADE_CONFIGURATION,
            [Description("unable to load Corrade group schedules state")] UNABLE_TO_LOAD_CORRADE_GROUP_SCHEDULES_STATE,
            [Description("unable to save Corrade group schedules state")] UNABLE_TO_SAVE_CORRADE_GROUP_SCHEDULES_STATE,
            [Description("group schedules file modified")] GROUP_SCHEDULES_FILE_MODIFIED,
            [Description("error setting up notifications watcher")] ERROR_SETTING_UP_NOTIFICATIONS_WATCHER,
            [Description("error setting up schedules watcher")] ERROR_SETTING_UP_SCHEDULES_WATCHER,
            [Description("unable to load Corrade movement state")] UNABLE_TO_LOAD_CORRADE_MOVEMENT_STATE,
            [Description("unable to save Corrade movement state")] UNABLE_TO_SAVE_CORRADE_MOVEMENT_STATE,
            [Description("TCP notifications server error")] TCP_NOTIFICATIONS_SERVER_ERROR,
            [Description("stopping TCP notifications server")] STOPPING_TCP_NOTIFICATIONS_SERVER,
            [Description("starting TCP notifications server")] STARTING_TCP_NOTIFICATIONS_SERVER,
            [Description("TCP notification throttled")] TCP_NOTIFICATION_THROTTLED
        }

        /// <summary>
        ///     Corrade's internal thread structure.
        /// </summary>
        public struct CorradeThread
        {
            /// <summary>
            ///     Holds all the live threads.
            /// </summary>
            private static readonly HashSet<Thread> WorkSet = new HashSet<Thread>();

            private static readonly object WorkSetLock = new object();

            /// <summary>
            ///     Semaphore for sequential execution of threads.
            /// </summary>
            private static readonly ManualResetEvent ThreadCompletedEvent = new ManualResetEvent(true);

            /// <summary>
            ///     Holds a map of groups to execution time in milliseconds.
            /// </summary>
            private static readonly Dictionary<UUID, GroupExecution> GroupExecutionTime =
                new Dictionary<UUID, GroupExecution>();

            private static readonly object GroupExecutionTimeLock = new object();
            private static readonly Stopwatch ThreadExecutuionStopwatch = new Stopwatch();
            private static CorradeThreadType corradeThreadType;

            /// <summary>
            ///     Constructor for a Corrade thread.
            /// </summary>
            /// <param name="corradeThreadType">the type of Corrade thread</param>
            public CorradeThread(CorradeThreadType corradeThreadType)
            {
                CorradeThread.corradeThreadType = corradeThreadType;
            }

            /// <summary>
            ///     This is a sequential scheduler that benefits from not blocking Corrade
            ///     and guarrantees that any Corrade thread spawned this way will only execute
            ///     until the previous thread spawned this way has completed.
            /// </summary>
            /// <param name="s">the code to execute as a ThreadStart delegate</param>
            /// <param name="m">the maximum amount of threads</param>
            public void SpawnSequential(ThreadStart s, uint m)
            {
                lock (WorkSetLock)
                {
                    WorkSet.RemoveWhere(o => !o.IsAlive);
                    if (WorkSet.Count > m)
                    {
                        return;
                    }
                }
                Thread t = new Thread(() =>
                {
                    // Wait for previous sequential thread to complete.
                    ThreadCompletedEvent.WaitOne(Timeout.Infinite);
                    ThreadCompletedEvent.Reset();
                    // protect inner thread
                    try
                    {
                        s();
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            wasGetDescriptionFromEnumValue(
                                ConsoleError.UNCAUGHT_EXCEPTION_FOR_THREAD),
                            wasGetDescriptionFromEnumValue(corradeThreadType), ex.Message);
                    }
                    // Thread has completed.
                    ThreadCompletedEvent.Set();
                })
                {IsBackground = true};
                lock (WorkSetLock)
                {
                    WorkSet.Add(t);
                }
                t.Start();
            }

            /// <summary>
            ///     This is an ad-hoc scheduler where threads will be executed in a
            ///     first-come first-served fashion.
            /// </summary>
            /// <param name="s">the code to execute as a ThreadStart delegate</param>
            /// <param name="m">the maximum amount of threads</param>
            public void Spawn(ThreadStart s, uint m)
            {
                lock (WorkSetLock)
                {
                    WorkSet.RemoveWhere(o => !o.IsAlive);
                    if (WorkSet.Count > m)
                    {
                        return;
                    }
                }
                Thread t = new Thread(() =>
                {
                    // protect inner thread
                    try
                    {
                        s();
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            wasGetDescriptionFromEnumValue(
                                ConsoleError.UNCAUGHT_EXCEPTION_FOR_THREAD),
                            wasGetDescriptionFromEnumValue(corradeThreadType), ex.Message);
                    }
                })
                {IsBackground = true};
                lock (WorkSetLock)
                {
                    WorkSet.Add(t);
                }
                t.Start();
            }

            /// <summary>
            ///     This is a fairness-oriented group/time-based scheduler that monitors
            ///     the execution time of threads for each configured group and favors
            ///     threads for the configured groups that have the smallest accumulated
            ///     execution time.
            /// </summary>
            /// <param name="s">the code to execute as a ThreadStart delegate</param>
            /// <param name="m">the maximum amount of threads</param>
            /// <param name="groupUUID">the UUID of the group</param>
            /// <param name="expiration">the time in milliseconds after which measurements are expunged</param>
            public void Spawn(ThreadStart s, uint m, UUID groupUUID, uint expiration)
            {
                // Don't accept to schedule bogus groups.
                if (groupUUID.Equals(UUID.Zero))
                    return;
                lock (WorkSetLock)
                {
                    WorkSet.RemoveWhere(o => !o.IsAlive);
                    if (WorkSet.Count > m)
                    {
                        return;
                    }
                }
                Thread t = new Thread(() =>
                {
                    // protect inner thread
                    try
                    {
                        // First remove any groups that have expired.
                        lock (GroupExecutionTimeLock)
                        {
                            List<UUID> RemoveGroups = new List<UUID>();
                            object LockObject = new object();
                            Parallel.ForEach(GroupExecutionTime, o =>
                            {
                                if ((DateTime.Now - o.Value.TimeStamp).Milliseconds > expiration)
                                {
                                    lock (LockObject)
                                    {
                                        RemoveGroups.Add(o.Key);
                                    }
                                }
                            });
                            Parallel.ForEach(RemoveGroups, o =>
                            {
                                lock (LockObject)
                                {
                                    GroupExecutionTime.Remove(o);
                                }
                            });
                        }
                        int sleepTime = 0;
                        List<KeyValuePair<UUID, GroupExecution>> sortedTimeGroups;
                        lock (GroupExecutionTimeLock)
                        {
                            sortedTimeGroups = GroupExecutionTime.ToList();
                        }
                        // In case only one group is involved, then do not schedule the group.
                        if (sortedTimeGroups.Count > 1 && sortedTimeGroups.Any(o => o.Key.Equals(groupUUID)))
                        {
                            sortedTimeGroups.Sort((o, p) => o.Value.ExecutionTime.CompareTo(p.Value.ExecutionTime));
                            int draw = CorradeRandom.Next(sortedTimeGroups.Sum(o => o.Value.ExecutionTime));
                            int accu = 0;
                            foreach (KeyValuePair<UUID, GroupExecution> group in sortedTimeGroups)
                            {
                                accu += group.Value.ExecutionTime;
                                if (accu < draw) continue;
                                sleepTime = group.Value.ExecutionTime;
                                break;
                            }
                        }
                        Thread.Sleep(sleepTime);
                        ThreadExecutuionStopwatch.Restart();
                        s();
                        ThreadExecutuionStopwatch.Stop();
                        lock (GroupExecutionTimeLock)
                        {
                            // add or change the mean execution time for a group
                            switch (GroupExecutionTime.ContainsKey(groupUUID))
                            {
                                case true:
                                    GroupExecutionTime[groupUUID] = new GroupExecution
                                    {
                                        ExecutionTime = (GroupExecutionTime[groupUUID].ExecutionTime +
                                                         (int) ThreadExecutuionStopwatch.ElapsedMilliseconds)/
                                                        2,
                                        TimeStamp = DateTime.Now
                                    };
                                    break;
                                default:
                                    GroupExecutionTime.Add(groupUUID, new GroupExecution
                                    {
                                        ExecutionTime = (int) ThreadExecutuionStopwatch.ElapsedMilliseconds,
                                        TimeStamp = DateTime.Now
                                    });
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Feedback(
                            wasGetDescriptionFromEnumValue(
                                ConsoleError.UNCAUGHT_EXCEPTION_FOR_THREAD),
                            wasGetDescriptionFromEnumValue(corradeThreadType), ex.Message);
                    }
                })
                {IsBackground = true};
                lock (WorkSetLock)
                {
                    WorkSet.Add(t);
                }
                t.Start();
            }

            private struct GroupExecution
            {
                public int ExecutionTime;
                public DateTime TimeStamp;
            }
        }

        /// <summary>
        ///     An inventory item.
        /// </summary>
        private struct DirItem
        {
            [Description("item")] public UUID Item;
            [Description("name")] public string Name;
            [Description("permissions")] public string Permissions;
            [Description("type")] public DirItemType Type;

            public static DirItem FromInventoryBase(InventoryBase inventoryBase)
            {
                DirItem item = new DirItem
                {
                    Name = inventoryBase.Name,
                    Item = inventoryBase.UUID,
                    Permissions = CORRADE_CONSTANTS.PERMISSIONS.NONE
                };

                if (inventoryBase is InventoryFolder)
                {
                    item.Type = DirItemType.FOLDER;
                    return item;
                }

                if (!(inventoryBase is InventoryItem)) return item;

                InventoryItem inventoryItem = inventoryBase as InventoryItem;
                item.Permissions = wasPermissionsToString(inventoryItem.Permissions);

                if (inventoryItem is InventoryWearable)
                {
                    item.Type = (DirItemType) typeof (DirItemType).GetFields(BindingFlags.Public |
                                                                             BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                string.Equals(o.Name,
                                    Enum.GetName(typeof (WearableType),
                                        (inventoryItem as InventoryWearable).WearableType),
                                    StringComparison.OrdinalIgnoreCase)).GetValue(null);
                    return item;
                }

                if (inventoryItem is InventoryTexture)
                {
                    item.Type = DirItemType.TEXTURE;
                    return item;
                }

                if (inventoryItem is InventorySound)
                {
                    item.Type = DirItemType.SOUND;
                    return item;
                }

                if (inventoryItem is InventoryCallingCard)
                {
                    item.Type = DirItemType.CALLINGCARD;
                    return item;
                }

                if (inventoryItem is InventoryLandmark)
                {
                    item.Type = DirItemType.LANDMARK;
                    return item;
                }

                if (inventoryItem is InventoryObject)
                {
                    item.Type = DirItemType.OBJECT;
                    return item;
                }

                if (inventoryItem is InventoryNotecard)
                {
                    item.Type = DirItemType.NOTECARD;
                    return item;
                }

                if (inventoryItem is InventoryCategory)
                {
                    item.Type = DirItemType.CATEGORY;
                    return item;
                }

                if (inventoryItem is InventoryLSL)
                {
                    item.Type = DirItemType.LSL;
                    return item;
                }

                if (inventoryItem is InventorySnapshot)
                {
                    item.Type = DirItemType.SNAPSHOT;
                    return item;
                }

                if (inventoryItem is InventoryAttachment)
                {
                    item.Type = DirItemType.ATTACHMENT;
                    return item;
                }

                if (inventoryItem is InventoryAnimation)
                {
                    item.Type = DirItemType.ANIMATION;
                    return item;
                }

                if (inventoryItem is InventoryGesture)
                {
                    item.Type = DirItemType.GESTURE;
                    return item;
                }

                item.Type = DirItemType.NONE;
                return item;
            }
        }

        /// <summary>
        ///     Holds item types with the wearable inventory item type expanded to wearable types.
        /// </summary>
        private enum DirItemType : uint
        {
            [Description("none")] NONE = 0,
            [Description("texture")] TEXTURE,
            [Description("sound")] SOUND,
            [Description("callingcard")] CALLINGCARD,
            [Description("landmark")] LANDMARK,
            [Description("object")] OBJECT,
            [Description("notecard")] NOTECARD,
            [Description("category")] CATEGORY,
            [Description("LSL")] LSL,
            [Description("snapshot")] SNAPSHOT,
            [Description("attachment")] ATTACHMENT,
            [Description("animation")] ANIMATION,
            [Description("gesture")] GESTURE,
            [Description("folder")] FOLDER,
            [Description("shape")] SHAPE,
            [Description("skin")] SKIN,
            [Description("hair")] HAIR,
            [Description("eyes")] EYES,
            [Description("shirt")] SHIRT,
            [Description("pants")] PANTS,
            [Description("shoes")] SHOES,
            [Description("socks")] SOCKS,
            [Description("jacket")] JACKET,
            [Description("gloves")] GLOVES,
            [Description("undershirt")] UNDERSHIRT,
            [Description("underpants")] UNDERPANTS,
            [Description("skirt")] SKIRT,
            [Description("tattoo")] TATTOO,
            [Description("alpha")] ALPHA,
            [Description("physics")] PHYSICS
        }

        /// <summary>
        ///     Directions in 3D cartesian.
        /// </summary>
        private enum Direction : uint
        {
            [Description("none")] NONE = 0,
            [Description("back")] BACK,
            [Description("forward")] FORWARD,
            [Description("left")] LEFT,
            [Description("right")] RIGHT,
            [Description("up")] UP,
            [Description("down")] DOWN
        }

        /// <summary>
        ///     ENIGMA machine settings.
        /// </summary>
        public struct ENIGMA
        {
            public char[] plugs;
            public char reflector;
            public char[] rotors;
        }

        /// <summary>
        ///     Possible entities.
        /// </summary>
        private enum Entity : uint
        {
            [Description("none")] NONE = 0,
            [Description("avatar")] AVATAR,
            [Description("local")] LOCAL,
            [Description("group")] GROUP,
            [Description("estate")] ESTATE,
            [Description("region")] REGION,
            [Description("object")] OBJECT,
            [Description("parcel")] PARCEL,
            [Description("range")] RANGE,
            [Description("syntax")] SYNTAX,
            [Description("permission")] PERMISSION,
            [Description("description")] DESCRIPTION
        }

        /// <summary>
        ///     Group structure.
        /// </summary>
        [Serializable]
        public struct Group
        {
            public string ChatLog;
            public bool ChatLogEnabled;
            public string DatabaseFile;
            public string Name;
            public HashSet<Notifications> Notifications;
            public string Password;
            public HashSet<Permissions> Permissions;
            public uint Schedules;
            public UUID UUID;
            public uint Workers;

            public uint NotificationMask
            {
                get
                {
                    return Notifications != null && Notifications.Any()
                        ? Notifications.Cast<uint>()
                            .Aggregate((p, q) => p |= q)
                        : 0;
                }
            }

            public uint PermissionMask
            {
                get
                {
                    return Permissions != null && Permissions.Any()
                        ? Permissions.Cast<uint>()
                            .Aggregate((p, q) => p |= q)
                        : 0;
                }
            }
        }

        /// <summary>
        ///     A structure for group invites.
        /// </summary>
        private struct GroupInvite
        {
            [Description("agent")] public Agent Agent;
            [Description("fee")] public int Fee;
            [Description("group")] public string Group;
            [Description("session")] public UUID Session;
        }

        public struct CorradeCommandParameters
        {
            [Description("group")] public Group Group;
            [Description("identifier")] public string Identifier;
            [Description("message")] public string Message;
            [Description("sender")] public string Sender;
        }

        /// <summary>
        ///     A structure for group scheduled commands.
        /// </summary>
        [Serializable]
        public struct GroupSchedule
        {
            [Description("at")] public DateTime At;
            [Description("group")] public Group Group;
            [Description("identifier")] public string Identifier;
            [Description("message")] public string Message;
            [Description("sender")] public string Sender;
        }

        /// <summary>
        ///     A structure for the agent movement.
        /// </summary>
        [Serializable]
        public struct AgentMovement
        {
            public bool AlwaysRun;
            public bool AutoResetControls;
            public bool Away;
            public Quaternion BodyRotation;
            public AgentFlags Flags;
            public bool Fly;
            public Quaternion HeadRotation;
            public bool Mouselook;
            public bool SitOnGround;
            public bool StandUp;
            public AgentState State;
        }

        /// <summary>
        ///     An event for the group membership notification.
        /// </summary>
        private class GroupMembershipEventArgs : EventArgs
        {
            public Action Action;
            public string AgentName;
            public UUID AgentUUID;
            public string GroupName;
            public UUID GroupUUID;
        }

        /// <summary>
        ///     An event for a group message.
        /// </summary>
        private class GroupMessageEventArgs : EventArgs
        {
            public UUID AgentUUID;
            public string FirstName;
            public string GroupName;
            public UUID GroupUUID;
            public string LastName;
            public string Message;
        }

        /// <summary>
        ///     Linden constants.
        /// </summary>
        private struct LINDEN_CONSTANTS
        {
            public struct ALERTS
            {
                public const string NO_ROOM_TO_SIT_HERE = @"No room to sit here, try another spot.";

                public const string UNABLE_TO_SET_HOME =
                    @"You can only set your 'Home Location' on your land or at a mainland Infohub.";

                public const string HOME_SET = @"Home position set.";
            }

            public struct ASSETS
            {
                public struct NOTECARD
                {
                    public const string NEWLINE = "\n";
                    public const uint MAXIMUM_BODY_LENTH = 65536;
                }
            }

            public struct AVATARS
            {
                public const uint SET_DISPLAY_NAME_SUCCESS = 200;
                public const string LASTNAME_PLACEHOLDER = @"Resident";
                public const uint MAXIMUM_DISPLAY_NAME_CHARACTERS = 31;
                public const uint MINIMUM_DISPLAY_NAME_CHARACTERS = 1;
                public const uint MAXIMUM_NUMBER_OF_ATTACHMENTS = 38;

                public struct PROFILE
                {
                    public const uint SECOND_LIFE_TEXT_SIZE = 510;
                    public const uint FIRST_LIFE_TEXT_SIZE = 253;
                }

                public struct PICKS
                {
                    public const uint MAXIMUM_PICKS = 10;
                    public const uint MAXIMUM_PICK_DESCRIPTION_SIZE = 1022;
                }

                public struct CLASSIFIEDS
                {
                    public const uint MAXIMUM_CLASSIFIEDS = 100;
                }
            }

            public struct PRIMITIVES
            {
                public const uint MAXIMUM_NAME_SIZE = 63;
                public const uint MAXIMUM_DESCRIPTION_SIZE = 127;
                public const double MAXIMUM_REZ_HEIGHT = 4096.0;
                public const double MINIMUM_SIZE_X = 0.01;
                public const double MINIMUM_SIZE_Y = 0.01;
                public const double MINIMUM_SIZE_Z = 0.01;
                public const double MAXIMUM_SIZE_X = 64.0;
                public const double MAXIMUM_SIZE_Y = 64.0;
                public const double MAXIMUM_SIZE_Z = 64.0;
            }

            public struct OBJECTS
            {
                public const uint MAXIMUM_PRIMITIVE_COUNT = 256;
            }

            public struct DIRECTORY
            {
                public struct EVENT
                {
                    public const uint SEARCH_RESULTS_COUNT = 200;
                }

                public struct GROUP
                {
                    public const uint SEARCH_RESULTS_COUNT = 100;
                }

                public struct LAND
                {
                    public const uint SEARCH_RESULTS_COUNT = 100;
                }

                public struct PEOPLE
                {
                    public const uint SEARCH_RESULTS_COUNT = 100;
                }
            }

            public struct ESTATE
            {
                public const uint REGION_RESTART_DELAY = 120;
                public const uint MAXIMUM_BAN_LIST_LENGTH = 500;
                public const uint MAXIMUM_GROUP_LIST_LENGTH = 63;
                public const uint MAXIMUM_USER_LIST_LENGTH = 500;
                public const uint MAXIMUM_MANAGER_LIST_LENGTH = 10;

                public struct MESSAGES
                {
                    public const string REGION_RESTART_MESSAGE = @"restart";
                }
            }

            public struct PARCELS
            {
                public const double MAXIMUM_AUTO_RETURN_TIME = 999999;
                public const uint MINIMUM_AUTO_RETURN_TIME = 0;
                public const uint MAXIMUM_NAME_LENGTH = 63;
                public const uint MAXIMUM_DESCRIPTION_LENGTH = 255;
            }

            public struct GRID
            {
                public const string SECOND_LIFE = @"Second Life";
                public const string TIME_ZONE = @"Pacific Standard Time";
            }

            public struct CHAT
            {
                public const uint MAXIMUM_MESSAGE_LENGTH = 1024;
            }

            public struct GROUPS
            {
                public const uint MAXIMUM_NUMBER_OF_ROLES = 10;
                public const string EVERYONE_ROLE_NAME = @"Everyone";
                public const uint MAXIMUM_GROUP_NAME_LENGTH = 35;
                public const uint MAXIMUM_GROUP_TITLE_LENGTH = 20;
            }

            public struct NOTICES
            {
                public const uint MAXIMUM_NOTICE_MESSAGE_LENGTH = 512;
            }

            public struct LSL
            {
                public const string CSV_DELIMITER = @", ";
                public const float SENSOR_RANGE = 96;
                public const string DATE_TIME_STAMP = @"yyy-MM-ddTHH:mm:ss.ffffffZ";
            }

            public struct REGION
            {
                public const float TELEPORT_MINIMUM_DISTANCE = 1;
                public const float DEFAULT_AGENT_LIMIT = 40;
                public const float DEFAULT_OBJECT_BONUS = 1;
                public const bool DEFAULT_FIXED_SUN = false;
                public const float DEFAULT_TERRAIN_LOWER_LIMIT = -4;
                public const float DEFAULT_TERRAIN_RAISE_LIMIT = 4;
                public const bool DEFAULT_USE_ESTATE_SUN = true;
                public const float DEFAULT_WATER_HEIGHT = 20;
                public const float SUNRISE = 6;
            }

            public struct VIEWER
            {
                public const float MAXIMUM_DRAW_DISTANCE = 4096;
            }

            public struct TELEPORTS
            {
                public struct THROTTLE
                {
                    public const uint MAX_TELEPORTS = 10;
                    public const uint GRACE_SECONDS = 15;
                }
            }
        }

        /// <summary>
        ///     A structure to track LookAt effects.
        /// </summary>
        private struct LookAtEffect
        {
            [Description("effect")] public UUID Effect;
            [Description("offset")] public Vector3d Offset;
            [Description("source")] public UUID Source;
            [Description("target")] public UUID Target;
            [Description("type")] public LookAtType Type;
        }

        /// <summary>
        ///     Masters structure.
        /// </summary>
        public struct Master
        {
            public string FirstName;
            public string LastName;
        }

        /// <summary>
        ///     A Corrade notification.
        /// </summary>
        [Serializable]
        public struct Notification
        {
            public SerializableDictionary<string, string> Afterburn;
            public HashSet<string> Data;
            public string GroupName;
            public UUID GroupUUID;

            /// <summary>
            ///     Holds TCP notification destinations.
            /// </summary>
            /// <remarks>These are state dependant so they do not have to be serialized.</remarks>
            [XmlIgnore] public Dictionary<Notifications, HashSet<IPEndPoint>> NotificationTCPDestination;

            public SerializableDictionary<Notifications, HashSet<string>> NotificationURLDestination;

            public uint NotificationMask
            {
                get
                {
                    return (NotificationURLDestination != null && NotificationURLDestination.Any()
                        ? NotificationURLDestination.Keys.Cast<uint>().Aggregate((p, q) => p |= q)
                        : 0) | (NotificationTCPDestination != null && NotificationTCPDestination.Any()
                            ? NotificationTCPDestination.Keys.Cast<uint>().Aggregate((p, q) => p |= q)
                            : 0);
                }
            }
        }

        /// <summary>
        ///     An element from the notification queue waiting to be dispatched.
        /// </summary>
        private struct NotificationQueueElement
        {
            public Dictionary<string, string> message;
            public string URL;
        }

        private struct NotificationTCPQueueElement
        {
            public IPEndPoint IPEndPoint;
            public Dictionary<string, string> message;
        }

        /// <summary>
        ///     A structure to track PointAt effects.
        /// </summary>
        private struct PointAtEffect
        {
            [Description("effect")] public UUID Effect;
            [Description("offset")] public Vector3d Offset;
            [Description("source")] public UUID Source;
            [Description("target")] public UUID Target;
            [Description("type")] public PointAtType Type;
        }

        /// <summary>
        ///     Keys returned by Corrade.
        /// </summary>
        private enum ResultKeys : uint
        {
            [Description("none")] NONE = 0,
            [Description("data")] DATA,
            [Description("success")] SUCCESS,
            [Description("error")] ERROR,
            [Description("status")] STATUS,
            [Description("time")] TIME
        }

        /// <summary>
        ///     A structure for script dialogs.
        /// </summary>
        private struct ScriptDialog
        {
            public Agent Agent;
            [Description("button")] public List<string> Button;
            [Description("channel")] public int Channel;
            [Description("item")] public UUID Item;
            [Description("message")] public string Message;
            [Description("name")] public string Name;
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
        ///     An exception thrown on script errors.
        /// </summary>
        [Serializable]
        public class ScriptException : Exception
        {
            public ScriptException(ScriptError error)
                : base(wasGetDescriptionFromEnumValue(error))
            {
                Status = wasGetAttributeFromEnumValue<StatusAttribute>(error).Status;
            }

            protected ScriptException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public uint Status { get; }
        }

        /// <summary>
        ///     An exception thrown on processing commands via the HTTP server.
        /// </summary>
        [Serializable]
        public class HTTPCommandException : Exception
        {
            public HTTPCommandException()
            {
            }

            public HTTPCommandException(string message)
                : base(message)
            {
            }

            protected HTTPCommandException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }

        /// <summary>
        ///     Keys reconigzed by Corrade.
        /// </summary>
        private enum ScriptKeys : uint
        {
            [Description("none")] NONE = 0,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=terraform>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR2>>&<height=<FLOAT>>&<width=<FLOAT>>&<amount=<FLOAT>>&<brush=<TerraformBrushSize>>&<action=<TerraformAction>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("terraform")] [Description("terraform")] TERRAFORM,

            [Description("height")] HEIGHT,
            [Description("width")] WIDTH,
            [Description("brush")] BRUSH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getestatecovenant>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getestatecovenant")] [Description("getestatecovenant")] GETESTATECOVENANT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=estateteleportusershome>&<group=<UUID|STRING>>&<password=<STRING>>&[avatars=<UUID|STRING[,UUID|STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("estateteleportusershome")] [Description("estateteleportusershome")] ESTATETELEPORTUSERSHOME,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setregionterrainvariables>&<group=<UUID|STRING>>&<password=<STRING>>&[waterheight=<FLOAT>]&[terrainraiselimit=<FLOAT>]&[terrainlowerlimit=<FLOAT>]&[usestatesun=<BOOL>]&[fixedsun=<BOOL>]&[sunposition=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setregionterrainvariables")] [Description("setregionterrainvariables")] SETREGIONTERRAINVARIABLES,

            [Description("useestatesun")] USEESTATESUN,
            [Description("terrainraiselimit")] TERRAINRAISELIMIT,
            [Description("terrainlowerlimit")] TERRAINLOWERLIMIT,
            [Description("sunposition")] SUNPOSITION,
            [Description("fixedsun")] FIXEDSUN,
            [Description("waterheight")] WATERHEIGHT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getregionterrainheights>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getregionterrainheights")] [Description("getregionterrainheights")] GETREGIONTERRAINHEIGHTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setregionterrainheights>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<FLOAT[,FLOAT...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setregionterrainheights")] [Description("setregionterrainheights")] SETREGIONTERRAINHEIGHTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getregionterraintextures>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getregionterraintextures")] [Description("getregionterraintextures")] GETREGIONTERRAINTEXTURES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setregionterraintextures>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<UUID|STRING[,UUID|STRING...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setregionterraintextures")] [Description("setregionterraintextures")] SETREGIONTERRAINTEXTURES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setregioninfo>&<group=<UUID|STRING>>&<password=<STRING>>&[terraform=<BOOL>]&[fly=<BOOL>]&[damage=<BOOL>]&[resell=<BOOL>]&[push=<BOOL>]&[parcel=<BOOL>]&[limit=<FLOAT>]&[bonus=<FLOAT>]&[mature=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setregioninfo")] [Description("setregioninfo")] SETREGIONINFO,

            [Description("bonus")] BONUS,
            [Description("damage")] DAMAGE,
            [Description("limit")] LIMIT,
            [Description("mature")] MATURE,
            [Description("parcel")] PARCEL,
            [Description("push")] PUSH,
            [Description("resell")] RESELL,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setcameradata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement.AgentCamera[,AgentManager.AgentMovement.AgentCamera...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("setcameradata")] [Description("setcameradata")] SETCAMERADATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getcameradata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement.AgentCamera[,AgentManager.AgentMovement.AgentCamera...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getcameradata")] [Description("getcameradata")] GETCAMERADATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setmovementdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement[,AgentManager.AgentMovement...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("setmovementdata")] [Description("setmovementdata")] SETMOVEMENTDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getmovementdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager.AgentMovement[,AgentManager.AgentMovement...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getmovementdata")] [Description("getmovementdata")] GETMOVEMENTDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=at>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<add|get|remove|list>>&action=add:<time=<Timestamp>>&action=add:<data=<STRING>>&action=get,remove:<index=<INTEGER>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Schedule)] [CorradeCommand("at")] [Description("at")] AT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=flyto>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR3>>&[duration=<INTGEGER>]&[affinity=<INTEGER>]&[fly=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("flyto")] [Description("flyto")] FLYTO,

            [Description("vicinity")] VICINITY,
            [Description("affinity")] AFFINITY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=batchmute>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<mute|unmute>>&[mutes=<STRING|UUID[,STRING|UUID...]>]&action=mute:[type=MuteType]&action=mute:[flags=MuteFlags]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("batchmute")] [Description("batchmute")] BATCHMUTE,

            [Description("mutes")] MUTES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setconfigurationdata>&<group=<UUID|STRING>>&<password=<STRING>>&[data=<CorradeConfiguration,[CorradeConfiguration...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.System)] [CorradeCommand("setconfigurationdata")] [Description("setconfigurationdata")] SETCONFIGURATIONDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getconfigurationdata>&<group=<UUID|STRING>>&<password=<STRING>>&[data=<CorradeConfiguration,[CorradeConfiguration...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.System)] [CorradeCommand("getconfigurationdata")] [Description("getconfigurationdata")] GETCONFIGURATIONDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=ban>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<ban|unban|list>>&action=ban,unban:[avatars=<UUID|STRING[,UUID|STRING...]>]&action=ban:[eject=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("ban")] [Description("ban")] BAN,

            [IsCorradeCommand(true)] [CommandInputSyntax("<command=ping>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.None)] [CorradeCommand("ping")] [Description("ping")] PING,
            [Description("pong")] PONG,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=batcheject>&<group=<UUID|STRING>>&<password=<STRING>>&[avatars=<UUID|STRING[,UUID|STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("batcheject")] [Description("batcheject")] BATCHEJECT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=batchinvite>&<group=<UUID|STRING>>&<password=<STRING>>&[role=<UUID[,STRING...]>]&[avatars=<UUID|STRING[,UUID|STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("batchinvite")] [Description("batchinvite")] BATCHINVITE,

            [Description("avatars")] AVATARS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectmediadata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<face=<INTEGER>>&[data=<MediaEntry[,MediaEntry...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectmediadata")] [Description("setobjectmediadata")] SETOBJECTMEDIADATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getobjectmediadata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<MediaEntry[,MediaEntry...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getobjectmediadata")] [Description("getobjectmediadata")] GETOBJECTMEDIADATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivematerial>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[material=<Material>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivematerial")] [Description("setprimitivematerial")] SETPRIMITIVEMATERIAL,

            [Description("material")] MATERIAL,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivelightdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<LightData[,LightData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivelightdata")] [Description("setprimitivelightdata")] SETPRIMITIVELIGHTDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivelightdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<LightData [,LightData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivelightdata")] [Description("getprimitivelightdata")] GETPRIMITIVELIGHTDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitiveflexibledata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<FlexibleData[,FlexibleData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitiveflexibledata")] [Description("setprimitiveflexibledata")] SETPRIMITIVEFLEXIBLEDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitiveflexibledata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<FlexibleData[,FlexibleData ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitiveflexibledata")] [Description("getprimitiveflexibledata")] GETPRIMITIVEFLEXIBLEDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=creategrass>&<group=<UUID|STRING>>&<password=<STRING>>>&[region=<STRING>]&<position=<VECTOR3>>&[rotation=<Quaternion>]&<type=<Grass>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("creategrass")] [Description("creategrass")] CREATEGRASS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getstatus>&<group=<UUID|STRING>>&<password=<STRING>>&<status=<INTEGER>>&<entity=<description>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.None)] [CorradeCommand("getstatus")] [Description("getstatus")] GETSTATUS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivebodytypes>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivebodytypes")] [Description("getprimitivebodytypes")] GETPRIMITIVEBODYTYPES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivephysicsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<PhysicsProperties[,PhysicsProperties ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivephysicsdata")] [Description("getprimitivephysicsdata")] GETPRIMITIVEPHYSICSDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivepropertiesdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<ObjectProperties[,ObjectProperties ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivepropertiesdata")] [Description("getprimitivepropertiesdata")] GETPRIMITIVEPROPERTIESDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitiveflags>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<SINGLE>]&[temporary=<BOOL>]&[shadows=<BOOL>]&[restitution=<SINGLE>]&[phantom=<BOOL>]&[gravity=<SINGLE>]&[friction=<SINGLE>]&[density=<SINGLE>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitiveflags")] [Description("setprimitiveflags")] SETPRIMITIVEFLAGS,

            [Description("temporary")] TEMPORARY,
            [Description("shadows")] SHADOWS,
            [Description("restitution")] RESTITUTION,
            [Description("phantom")] PHANTOM,
            [Description("gravity")] GRAVITY,
            [Description("friction")] FRICTION,
            [Description("density")] DENSITY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=grab>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&<item=<UUID|STRING>>&[range=<FLOAT>]&<texture=<VECTOR3>&<surface=<VECTOR3>>&<normal=<VECTOR3>>&<binormal=<VECTOR3>>&<face=<INTEGER>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("grab")] [Description("grab")] GRAB,

            [Description("texture")] TEXTURE,
            [Description("surface")] SURFACE,
            [Description("normal")] NORMAL,
            [Description("binormal")] BINORMAL,
            [Description("face")] FACE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=createtree>&<group=<UUID|STRING>>&<password=<STRING>>>&[region=<STRING>]&<position=<VECTOR3>>&[rotation=<Quaternion>]&<type=<Tree>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("createtree")] [Description("createtree")] CREATETREE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivetexturedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[index=<INTEGER>]&[data=<TextureEntryFace [,TextureEntryFace ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivetexturedata")] [Description("setprimitivetexturedata")] SETPRIMITIVETEXTUREDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivetexturedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<TextureEntry[,TextureEntry ...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivetexturedata")] [Description("getprimitivetexturedata")] GETPRIMITIVETEXTUREDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivesculptdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<SculptData[,SculptData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivesculptdata")] [Description("setprimitivesculptdata")] SETPRIMITIVESCULPTDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivesculptdata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<SculptData[,SculptData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivesculptdata")] [Description("getprimitivesculptdata")] GETPRIMITIVESCULPTDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitiveshapedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[type=<CorradePrimitiveShape>]&[data=<ConstructionData[,ConstructionData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitiveshapedata")] [Description("setprimitiveshapedata")] SETPRIMITIVESHAPEDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitiveshapedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[data=<ConstructionData[,ConstructionData...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitiveshapedata")] [Description("getprimitiveshapedata")] GETPRIMITIVESHAPEDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=createprimitive>&<group=<UUID|STRING>>&<password=<STRING>>>&[region=<STRING>]&<position=<VECTOR3>>&[rotation=<Quaternion>]&[type=<CorradePrimitiveShape>]&[data=<ConstructionData>]&[flags=<PrimFlags>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("createprimitive")] [Description("createprimitive")] CREATEPRIMITIVE,

            [Description("flags")] FLAGS,
            [Description("take")] TAKE,
            [Description("pass")] PASS,
            [Description("controls")] CONTROLS,
            [Description("afterburn")] AFTERBURN,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivepayprices>&<group=<UUID|STRING>>&<password=<STRING>>>&item=<STRING|UUID>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivepayprices")] [Description("getprimitivepayprices")] GETPRIMITIVEPAYPRICES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=primitivebuy>&<group=<UUID|STRING>>&<password=<STRING>>>&item=<STRING|UUID>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact | (uint) Permissions.Economy)] [CorradeCommand("primitivebuy")] [Description("primitivebuy")] PRIMITIVEBUY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=changeprimitivelink>&<group=<UUID|STRING>>&<password=<STRING>>>&<action=<link|delink>>&action=link:<item=<STRING|UUID,STRING|UUID[,STRING|UUID...>>&action=delink:<item=<STRING|UUID[,STRING|UUID...>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("changeprimitivelink")] [Description("changeprimitivelink")] CHANGEPRIMITIVELINK,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getgroupmemberdata>&<group=<UUID|STRING>>&<password=<STRING>>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<AvatarGroup[,AvatarGroup...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getgroupmemberdata")] [Description("getgroupmemberdata")] GETGROUPMEMBERDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getcommand>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<entity=<syntax|permission>>&entity=syntax:<type=<input>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.None)] [CorradeCommand("getcommand")] [Description("getcommand")] GETCOMMAND,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=listcommands>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.None)] [CorradeCommand("listcommands")] [Description("listcommands")] LISTCOMMANDS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getconnectedregions>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getconnectedregions")] [Description("getconnectedregions")] GETCONNECTEDREGIONS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getnetworkdata>&<group=<UUID|STRING>>&<password=<STRING>>&[data=<NetworkManager[,NetworkManager...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getnetworkdata")] [Description("getnetworkdata")] GETNETWORKDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=typing>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("typing")] [Description("typing")] TYPING,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=busy>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("busy")] [Description("busy")] BUSY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=away>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("away")] [Description("away")] AWAY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getobjectpermissions>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getobjectpermissions")] [Description("getobjectpermissions")] GETOBJECTPERMISSIONS,
            [Description("scale")] SCALE,
            [Description("uniform")] UNIFORM,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectscale>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<scale=<FLOAT>>&[uniform=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectscale")] [Description("setobjectscale")] SETOBJECTSCALE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivescale>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<scale=<FLOAT>>&[uniform=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivescale")] [Description("setprimitivescale")] SETPRIMITIVESCALE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitiverotation>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<rotation=<QUATERNION>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitiverotation")] [Description("setprimitiverotation")] SETPRIMITIVEROTATION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitiveposition>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&<position=<VECTOR3>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitiveposition")] [Description("setprimitiveposition")] SETPRIMITIVEPOSITION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=exportdae>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&[format=<ImageFormat>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("exportdae")] [Description("exportdae")] EXPORTDAE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=exportxml>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[range=<FLOAT>]&[format=<ImageFormat>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("exportxml")] [Description("exportxml")] EXPORTXML,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivesdata>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<range|parcel|region|avatar>>&entity=range:[range=<FLOAT>]&entity=parcel:[position=<VECTOR2>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[data=<Primitive[,Primitive...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivesdata")] [Description("getprimitivesdata")] GETPRIMITIVESDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getavatarsdata>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<range|parcel|region|avatar>>&entity=range:[range=<FLOAT>]&entity=parcel:[position=<VECTOR2>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[data=<Avatar[,Avatar...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getavatarsdata")] [Description("getavatarsdata")] GETAVATARSDATA,
            [Description("format")] FORMAT,
            [Description("volume")] VOLUME,
            [Description("audible")] AUDIBLE,
            [Description("path")] PATH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=inventory>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<ls|cwd|cd|mkdir|chmod|rm|cp|mv|ln>>&action=ls|mkdir|chmod:[path=<STRING>]&action=cd,action=rm:<path=<STRING>>&action=mkdir:<name=<STRING>>&action=chmod:<permissions=<STRING>>&action=cp|mv|ln:<source=<STRING>>&action=cp|mv|ln:<target=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("inventory")] [Description("inventory")] INVENTORY,
            [Description("offset")] OFFSET,
            [Description("alpha")] ALPHA,
            [Description("color")] COLOR,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=deleteviewereffect>&<group=<UUID|STRING>>&<password=<STRING>>&<effect=<Look|Point>>&<id=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("deleteviewereffect")] [Description("deleteviewereffect")] DELETEVIEWEREFFECT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getviewereffects>&<group=<UUID|STRING>>&<password=<STRING>>&<effect=<Look|Point|Sphere|Beam>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getviewereffects")] [Description("getviewereffects")] GETVIEWEREFFECTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setviewereffect>&<group=<UUID|STRING>>&<password=<STRING>>&<effect=<Look|Point|Sphere|Beam>>&effect=Look:<item=<UUID|STRING>&<range=<FLOAT>>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&effect=Look:<offset=<VECTOR3>>&effect=Look:<type=LookAt>&effect=Point:<item=<UUID|STRING>&<range=<FLOAT>>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&effect=Point:<offset=<VECTOR3>>&effect=Point:<type=PointAt>&effect=Beam:<item=<UUID|STRING>&<range=<FLOAT>>>|<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&effect=Beam:<color=<VECTOR3>>&effect=Beam:<alpha=<FLOAT>>&effect=Beam:<duration=<FLOAT>>&effect=Beam:<offset=<VECTOR3>>&effect=Sphere:<color=<VECTOR3>>&effect=Sphere:<alpha=<FLOAT>>&effect=Sphere:<duration=<FLOAT>>&effect=Sphere:<offset=<VECTOR3>>&[id=<UUID>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setviewereffect")] [Description("setviewereffect")] SETVIEWEREFFECT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=ai>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<process|enable|disable|rebuild>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Talk)] [CorradeCommand("ai")] [Description("ai")] AI,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=gettitles>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("gettitles")] [Description("gettitles")] GETTITLES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=tag>&<group=<UUID|STRING>>&<password=<STRING>>&action=<set|get>&action=set:<title=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("tag")] [Description("tag")] TAG,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=filter>&<group=<UUID|STRING>>&<password=<STRING>>&action=<set|get>&action=get:<type=<input|output>>&action=set:<input=<STRING>>&action=set:<output=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Filter)] [CorradeCommand("filter")] [Description("filter")] FILTER,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=run>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable|get>>&[callback=<STRING>]"
                )
                                     ] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("run")] [Description("run")] RUN,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=relax>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("relax")] [Description("relax")] RELAX,
            [Description("sift")] SIFT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=rlv>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<enable|disable>>&[callback=<STRING>]")
                                     ] [CommandPermissionMask((uint) Permissions.System)] [CorradeCommand("rlv")] [Description("rlv")] RLV,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getinventorypath>&<group=<UUID|STRING>>&<password=<STRING>>&<pattern=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("getinventorypath")] [Description("getinventorypath")] GETINVENTORYPATH,
            [Description("committed")] COMMITTED,
            [Description("credit")] CREDIT,
            [Description("success")] SUCCESS,
            [Description("transaction")] TRANSACTION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getscriptdialogs>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getscriptdialogs")] [Description("getscriptdialogs")] GETSCRIPTDIALOGS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getscriptpermissionrequests>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getscriptpermissionrequests")] [Description("getscriptpermissionrequests")] GETSCRIPTPERMISSIONREQUESTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getteleportlures>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("getteleportlures")] [Description("getteleportlures")] GETTELEPORTLURES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=replytogroupinvite>&<group=<UUID|STRING>>&<password=<STRING>>&[action=<accept|decline>]&<session=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group | (uint) Permissions.Economy)] [CorradeCommand("replytogroupinvite")] [Description("replytogroupinvite")] REPLYTOGROUPINVITE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getgroupinvites>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getgroupinvites")] [Description("getgroupinvites")] GETGROUPINVITES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getmemberroles>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getmemberroles")] [Description("getmemberroles")] GETMEMBERROLES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=execute>&<group=<UUID|STRING>>&<password=<STRING>>&<file=<STRING>>&[parameter=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Execute)] [CorradeCommand("execute")] [Description("execute")] EXECUTE,
            [Description("parameter")] PARAMETER,
            [Description("file")] FILE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=cache>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<purge|load|save>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.System)] [CorradeCommand("cache")] [Description("cache")] CACHE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getgridregiondata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<GridRegion[,GridRegion...]>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getgridregiondata")] [Description("getgridregiondata")] GETGRIDREGIONDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getregionparcelsboundingbox>&<group=<UUID|STRING>>&<password=<STRING>>&[region=<STRING>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getregionparcelsboundingbox")] [Description("getregionparcelsboundingbox")] GETREGIONPARCELSBOUNDINGBOX,
            [Description("pattern")] PATTERN,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=searchinventory>&<group=<UUID|STRING>>&<password=<STRING>>&<pattern=<STRING>>&[type=<AssetType>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("searchinventory")] [Description("searchinventory")] SEARCHINVENTORY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getterrainheight>&<group=<UUID|STRING>>&<password=<STRING>>&[southwest=<VECTOR>]&[northwest=<VECTOR>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getterrainheight")] [Description("getterrainheight")] GETTERRAINHEIGHT,
            [Description("northeast")] NORTHEAST,
            [Description("southwest")] SOUTHWEST,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=upload>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<type=<Texture|Sound|Animation|Clothing|Bodypart|Landmark|Gesture|Notecard|LSLText>>&type=Clothing:[wear=<WearableType>]&type=Bodypart:[wear=<WearableType>]&<data=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory | (uint) Permissions.Economy)] [CorradeCommand("upload")] [Description("upload")] UPLOAD,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=download>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<type=<Texture|Sound|Animation|Clothing|Bodypart|Landmark|Gesture|Notecard|LSLText>>&type=Texture:[format=<ImageFormat>]&[path=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact | (uint) Permissions.System)] [CorradeCommand("download")] [Description("download")] DOWNLOAD,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setparceldata>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR>]&[data=<Parcel[,Parcel...]>]&[region=<STRING>]&[callback=<STRING>]"
                )
                                     ] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setparceldata")] [Description("setparceldata")] SETPARCELDATA,
            [Description("new")] NEW,
            [Description("old")] OLD,
            [Description("aggressor")] AGGRESSOR,
            [Description("magnitude")] MAGNITUDE,
            [Description("time")] TIME,
            [Description("victim")] VICTIM,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=playgesture>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("playgesture")] [Description("playgesture")] PLAYGESTURE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=jump>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("jump")] [Description("jump")] JUMP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=crouch>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("crouch")] [Description("crouch")] CROUCH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=turnto>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR3>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("turnto")] [Description("turnto")] TURNTO,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=nudge>&<group=<UUID|STRING>>&<password=<STRING>>&<direction=<left|right|up|down|back|forward>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("nudge")] [Description("nudge")] NUDGE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=createnotecard>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[text=<STRING>]&[description=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("createnotecard")] [Description("createnotecard")] CREATENOTECARD,
            [Description("direction")] DIRECTION,
            [Description("agent")] AGENT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=replytoinventoryoffer>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<accept|decline>>&<session=<UUID>>&[folder=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("replytoinventoryoffer")] [Description("replytoinventoryoffer")] REPLYTOINVENTORYOFFER,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getinventoryoffers>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("getinventoryoffers")] [Description("getinventoryoffers")] GETINVENTORYOFFERS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=updateprimitiveinventory>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<add|remove|take>>&action=add:<entity=<UUID|STRING>>&action=remove:<entity=<UUID|STRING>>&action=take:<entity=<UUID|STRING>>&action=take:<folder=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("updateprimitiveinventory")] [Description("updateprimitiveinventory")] UPDATEPRIMITIVEINVENTORY,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=version>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.None)] [CorradeCommand("version")] [Description("version")] VERSION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=playsound>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[gain=<FLOAT>]&[position=<VECTOR3>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("playsound")] [Description("playsound")] PLAYSOUND,
            [Description("gain")] GAIN,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getrolemembers>&<group=<UUID|STRING>>&<password=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getrolemembers")] [Description("getrolemembers")] GETROLEMEMBERS,
            [Description("status")] STATUS,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getmembers>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getmembers")] [Description("getmembers")] GETMEMBERS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=replytoteleportlure>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<session=<UUID>>&<action=<accept|decline>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("replytoteleportlure")] [Description("replytoteleportlure")] REPLYTOTELEPORTLURE,
            [Description("session")] SESSION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=replytoscriptpermissionrequest>&<group=<UUID|STRING>>&<password=<STRING>>&<task=<UUID>>&<item=<UUID>>&<permissions=<ScriptPermission>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("replytoscriptpermissionrequest")] [Description("replytoscriptpermissionrequest")] REPLYTOSCRIPTPERMISSIONREQUEST,
            [Description("task")] TASK,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getparcellist>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getparcellist")] [Description("getparcellist")] GETPARCELLIST,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=parcelrelease>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("parcelrelease")] [Description("parcelrelease")] PARCELRELEASE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=parcelbuy>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[forgroup=<BOOL>]&[removecontribution=<BOOL>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land | (uint) Permissions.Economy)] [CorradeCommand("parcelbuy")] [Description("parcelbuy")] PARCELBUY,
            [Description("removecontribution")] REMOVECONTRIBUTION,
            [Description("forgroup")] FORGROUP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=parceldeed>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("parceldeed")] [Description("parceldeed")] PARCELDEED,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=parcelreclaim>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("parcelreclaim")] [Description("parcelreclaim")] PARCELRECLAIM,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=unwear>&<group=<UUID|STRING>>&<password=<STRING>>&<wearables=<STRING[,UUID...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("unwear")] [Description("unwear")] UNWEAR,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=wear>&<group=<UUID|STRING>>&<password=<STRING>>&<wearables=<STRING[,UUID...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("wear")] [Description("wear")] WEAR,
            [Description("wearables")] WEARABLES,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getwearables>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getwearables")] [Description("getwearables")] GETWEARABLES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=changeappearance>&<group=<UUID|STRING>>&<password=<STRING>>&<folder=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("changeappearance")] [Description("changeappearance")] CHANGEAPPEARANCE,
            [Description("folder")] FOLDER,
            [Description("replace")] REPLACE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectrotation>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<rotation=<QUARTERNION>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectrotation")] [Description("setobjectrotation")] SETOBJECTROTATION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivedescription>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<description=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivedescription")] [Description("setprimitivedescription")] SETPRIMITIVEDESCRIPTION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprimitivename>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setprimitivename")] [Description("setprimitivename")] SETPRIMITIVENAME,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectposition>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<position=<VECTOR3>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectposition")] [Description("setobjectposition")] SETOBJECTPOSITION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectsaleinfo>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<price=<INTEGER>>&<type=<SaleType>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectsaleinfo")] [Description("setobjectsaleinfo")] SETOBJECTSALEINFO,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectgroup>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectgroup")] [Description("setobjectgroup")] SETOBJECTGROUP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=objectdeed>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("objectdeed")] [Description("objectdeed")] OBJECTDEED,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setobjectpermissions>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<permissions=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setobjectpermissions")] [Description("setobjectpermissions")] SETOBJECTPERMISSIONS,
            [Description("permissions")] PERMISSIONS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getavatarpositions>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<region|parcel>>&entity=parcel:<position=<VECTOR2>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getavatarpositions")] [Description("getavatarpositions")] GETAVATARPOSITIONS,
            [Description("delay")] DELAY,
            [Description("asset")] ASSET,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setregiondebug>&<group=<UUID|STRING>>&<password=<STRING>>&<scripts=<BOOL>>&<collisions=<BOOL>>&<physics=<BOOL>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setregiondebug")] [Description("setregiondebug")] SETREGIONDEBUG,
            [Description("scripts")] SCRIPTS,
            [Description("collisions")] COLLISIONS,
            [Description("physics")] PHYSICS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getmapavatarpositions>&<group=<UUID|STRING>>&<password=<STRING>>&<region=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getmapavatarpositions")] [Description("getmapavatarpositions")] GETMAPAVATARPOSITIONS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=mapfriend>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("mapfriend")] [Description("mapfriend")] MAPFRIEND,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=replytofriendshiprequest>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<action=<accept|decline>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("replytofriendshiprequest")] [Description("replytofriendshiprequest")] REPLYTOFRIENDSHIPREQUEST,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getfriendshiprequests>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("getfriendshiprequests")] [Description("getfriendshiprequests")] GETFRIENDSHIPREQUESTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=grantfriendrights>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<rights=<FriendRights>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("grantfriendrights")] [Description("grantfriendrights")] GRANTFRIENDRIGHTS,
            [Description("rights")] RIGHTS,

            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getfriendslist>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("getfriendslist")] [Description("getfriendslist")] GETFRIENDSLIST,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=terminatefriendship>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("terminatefriendship")] [Description("terminatefriendship")] TERMINATEFRIENDSHIP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=offerfriendship>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("offerfriendship")] [Description("offerfriendship")] OFFERFRIENDSHIP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getfrienddata>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<FriendInfo[,FriendInfo...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Friendship)] [CorradeCommand("getfrienddata")] [Description("getfrienddata")] GETFRIENDDATA,
            [Description("days")] DAYS,
            [Description("interval")] INTERVAL,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getgroupaccountsummarydata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<GroupAccountSummary[,GroupAccountSummary...]>>&<days=<INTEGER>>&<interval=<INTEGER>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getgroupaccountsummarydata")] [Description("getgroupaccountsummarydata")] GETGROUPACCOUNTSUMMARYDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getselfdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AgentManager[,AgentManager...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getselfdata")] [Description("getselfdata")] GETSELFDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=deleteclassified>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("deleteclassified")] [Description("deleteclassified")] DELETECLASSIFIED,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=addclassified>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&<price=<INTEGER>>&<type=<Any|Shopping|LandRental|PropertyRental|SpecialAttraction|NewProducts|Employment|Wanted|Service|Personal>>&[item=<UUID|STRING>]&[description=<STRING>]&[renew=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming | (uint) Permissions.Economy)] [CorradeCommand("addclassified")] [Description("addclassified")] ADDCLASSIFIED,
            [Description("price")] PRICE,
            [Description("renew")] RENEW,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=logout>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.System)] [CorradeCommand("logout")] [Description("logout")] LOGOUT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=displayname>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<get|set>>&action=set:<name=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("displayname")] [Description("displayname")] DISPLAYNAME,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=returnprimitives>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<entity=<parcel|estate>>&<type=<Owner|Group|Other|Sell|ReturnScripted|ReturnOnOthersLand|ReturnScriptedAndOnOthers>>&type=Owner|Group|Other|Sell:[position=<VECTOR2>]&type=ReturnScripted|ReturnOnOthersLand|ReturnScriptedAndOnOthers:[all=<BOOL>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("returnprimitives")] [Description("returnprimitives")] RETURNPRIMITIVES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getgroupdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Group[,Group...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getgroupdata")] [Description("getgroupdata")] GETGROUPDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getavatardata>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<Avatar[,Avatar...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getavatardata")] [Description("getavatardata")] GETAVATARDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitiveinventory>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitiveinventory")] [Description("getprimitiveinventory")] GETPRIMITIVEINVENTORY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getinventorydata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<data=<InventoryItem[,InventoryItem...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("getinventorydata")] [Description("getinventorydata")] GETINVENTORYDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitiveinventorydata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<data=<InventoryItem[,InventoryItem...]>>&<entity=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitiveinventorydata")] [Description("getprimitiveinventorydata")] GETPRIMITIVEINVENTORYDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getscriptrunning>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<entity=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getscriptrunning")] [Description("getscriptrunning")] GETSCRIPTRUNNING,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setscriptrunning>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<entity=<STRING|UUID>>&<action=<start|stop>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("setscriptrunning")] [Description("setscriptrunning")] SETSCRIPTRUNNING,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=derez>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[folder=<STRING|UUID>]&[type=<DeRezDestination>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("derez")] [Description("derez")] DEREZ,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getparceldata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Parcel[,Parcel...]>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getparceldata")] [Description("getparceldata")] GETPARCELDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=rez>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR2>>&<item=<UUID|STRING>&[rotation=<QUARTERNION>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("rez")] [Description("rez")] REZ,
            [Description("rotation")] ROTATION,
            [Description("index")] INDEX,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=replytoscriptdialog>&<group=<UUID|STRING>>&<password=<STRING>>&<channel=<INTEGER>>&<index=<INTEGER>&<button=<STRING>>&<item=<UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("replytoscriptdialog")] [Description("replytoscriptdialog")] REPLYTOSCRIPTDIALOG,
            [Description("owner")] OWNER,
            [Description("button")] BUTTON,

            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getanimations>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")
                                     ] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getanimations")] [Description("getanimations")] GETANIMATIONS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=animation>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&<action=<start|stop>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("animation")] [Description("animation")] ANIMATION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setestatelist>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<ban|group|manager|user>>&<action=<add|remove>>&type=ban|manager|user,action=add|remove:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&type=group,action=add|remove:<target=<STRING|UUID>>&[all=<BOOL>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("setestatelist")] [Description("setestatelist")] SETESTATELIST,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getestatelist>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<ban|group|manager|user>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getestatelist")] [Description("getestatelist")] GETESTATELIST,
            [Description("all")] ALL,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getregiontop>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<scripts|colliders>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getregiontop")] [Description("getregiontop")] GETREGIONTOP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=restartregion>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<scripts|colliders>>&[delay=<INTEGER>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("restartregion")] [Description("restartregion")] RESTARTREGION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=directorysearch>&<group=<UUID|STRING>>&<password=<STRING>>&<type=<classified|event|group|land|people|places>>&type=classified:<data=<Classified[,Classified...]>>&type=classified:<name=<STRING>>&type=event:<data=<EventsSearchData[,EventSearchData...]>>&type=event:<name=<STRING>>&type=group:<data=<GroupSearchData[,GroupSearchData...]>>&type=land:<data=<DirectoryParcel[,DirectoryParcel...]>>&type=people:<data=<AgentSearchData[,AgentSearchData...]>>&type=places:<data=<DirectoryParcel[,DirectoryParcel...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Directory)] [CorradeCommand("directorysearch")] [Description("directorysearch")] DIRECTORYSEARCH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprofiledata>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<data=<AvatarProperties[,AvatarProperties...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprofiledata")] [Description("getprofiledata")] GETPROFILEDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getparticlesystem>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getparticlesystem")] [Description("getparticlesystem")] GETPARTICLESYSTEM,
            [Description("data")] DATA,
            [Description("range")] RANGE,
            [Description("balance")] BALANCE,
            [Description("key")] KEY,
            [Description("value")] VALUE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=database>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<get|set|delete>>&action=get|delete:<key=<STRING>>&action=set:<key=<STRING>>&action=set:<value=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Database)] [CorradeCommand("database")] [Description("database")] DATABASE,
            [Description("text")] TEXT,
            [Description("quorum")] QUORUM,
            [Description("majority")] MAJORITY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=startproposal>&<group=<UUID|STRING>>&<password=<STRING>>&<duration=<INTEGER>>&<majority=<FLOAT>>&<quorum=<INTEGER>>&<text=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("startproposal")] [Description("startproposal")] STARTPROPOSAL,
            [Description("duration")] DURATION,
            [Description("action")] ACTION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=deletefromrole>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("deletefromrole")] [Description("deletefromrole")] DELETEFROMROLE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=addtorole>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("addtorole")] [Description("addtorole")] ADDTOROLE,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=leave>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("leave")] [Description("leave")] LEAVE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=updategroupdata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<[Charter<,STRING>][,ListInProfile<,BOOL>][,MembershipFee<,INTEGER>][,OpenEnrollment<,BOOL>][,ShowInList<,BOOL>]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("updategroupdata")] [Description("updategroupdata")] UPDATEGROUPDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=eject>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("eject")] [Description("eject")] EJECT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=invite>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[role=<UUID[,STRING...]>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("invite")] [Description("invite")] INVITE,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=join>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group | (uint) Permissions.Economy)] [CorradeCommand("join")] [Description("join")] JOIN,
            [Description("callback")] CALLBACK,
            [Description("group")] GROUP,
            [Description("password")] PASSWORD,
            [Description("firstname")] FIRSTNAME,
            [Description("lastname")] LASTNAME,
            [Description("command")] COMMAND,
            [Description("role")] ROLE,
            [Description("title")] TITLE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=tell>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<local|group|avatar|estate|region>>&entity=local:<type=<Normal|Whisper|Shout>>&entity=local,type=Normal|Whisper|Shout:[channel=<INTEGER>]&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Talk)] [CorradeCommand("tell")] [Description("tell")] TELL,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=notice>&<group=<UUID|STRING>>&<password=<STRING>>&<message=<STRING>>&[subject=<STRING>]&[item=<UUID|STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("notice")] [Description("notice")] NOTICE,
            [Description("message")] MESSAGE,
            [Description("subject")] SUBJECT,
            [Description("item")] ITEM,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=pay>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<avatar|object|group>>&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&entity=object:<target=<UUID>>&[reason=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Economy)] [CorradeCommand("pay")] [Description("pay")] PAY,
            [Description("amount")] AMOUNT,
            [Description("target")] TARGET,
            [Description("reason")] REASON,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getbalance>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Economy)] [CorradeCommand("getbalance")] [Description("getbalance")] GETBALANCE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=teleport>&<group=<UUID|STRING>>&<password=<STRING>>&<region=<STRING>>&[position=<VECTOR3>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("teleport")] [Description("teleport")] TELEPORT,
            [Description("region")] REGION,
            [Description("position")] POSITION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getregiondata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Simulator[,Simulator...]>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getregiondata")] [Description("getregiondata")] GETREGIONDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=sit>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("sit")] [Description("sit")] SIT,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=stand>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("stand")] [Description("stand")] STAND,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=parceleject>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[ban=<BOOL>]&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("parceleject")] [Description("parceleject")] PARCELEJECT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=creategroup>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<Group[,Group...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group | (uint) Permissions.Economy)] [CorradeCommand("creategroup")] [Description("creategroup")] CREATEGROUP,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=parcelfreeze>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[freeze=<BOOL>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("parcelfreeze")] [Description("parcelfreeze")] PARCELFREEZE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=createrole>&<group=<UUID|STRING>>&<password=<STRING>>&<role=<STRING>>&[powers=<GroupPowers[,GroupPowers...]>]&[title=<STRING>]&[description=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("createrole")] [Description("createrole")] CREATEROLE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=deleterole>&<group=<UUID|STRING>>&<password=<STRING>>&<role=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("deleterole")] [Description("deleterole")] DELETEROLE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getrolesmembers>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getrolesmembers")] [Description("getrolesmembers")] GETROLESMEMBERS,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getroles>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getroles")] [Description("getroles")] GETROLES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getrolepowers>&<group=<UUID|STRING>>&<password=<STRING>>&<role=<UUID|STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("getrolepowers")] [Description("getrolepowers")] GETROLEPOWERS,
            [Description("powers")] POWERS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=lure>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("lure")] [Description("lure")] LURE,
            [Description("URL")] URL,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=sethome>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("sethome")] [Description("sethome")] SETHOME,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=gohome>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("gohome")] [Description("gohome")] GOHOME,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=setprofiledata>&<group=<UUID|STRING>>&<password=<STRING>>&<data=<AvatarProperties[,AvatarProperties...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("setprofiledata")] [Description("setprofiledata")] SETPROFILEDATA,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=give>&<group=<UUID|STRING>>&<password=<STRING>>&<entity=<avatar|object>>&entity=avatar:<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&entity=avatar:<item=<UUID|STRING>&entity=object:<item=<UUID|STRING>&entity=object:[range=<FLOAT>]&entity=object:<target=<UUID|STRING>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("give")] [Description("give")] GIVE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=deleteitem>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<STRING|UUID>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("deleteitem")] [Description("deleteitem")] DELETEITEM,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=emptytrash>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Inventory)] [CorradeCommand("emptytrash")] [Description("emptytrash")] EMPTYTRASH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=fly>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<start|stop>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("fly")] [Description("fly")] FLY,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=addpick>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[description=<STRING>]&[item=<STRING|UUID>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("addpick")] [Description("addpick")] ADDPICK,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=deletepick>&<group=<UUID|STRING>>&<password=<STRING>>&<name=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("deltepick")] [Description("deltepick")] DELETEPICK,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=touch>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("touch")] [Description("touch")] TOUCH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=moderate>&<group=<UUID|STRING>>&<password=<STRING>>&<agent=<UUID>|firstname=<STRING>&lastname=<STRING>>&<type=<voice|text>>&<silence=<BOOL>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Group)] [CorradeCommand("moderate")] [Description("moderate")] MODERATE,
            [Description("type")] TYPE,
            [Description("silence")] SILENCE,
            [Description("freeze")] FREEZE,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=rebake>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("rebake")] [Description("rebake")] REBAKE,

            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getattachments>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("getattachments")] [Description("getattachments")] GETATTACHMENTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=attach>&<group=<UUID|STRING>>&<password=<STRING>>&<attachments=<AttachmentPoint<,<UUID|STRING>>[,AttachmentPoint<,<UUID|STRING>>...]>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("attach")] [Description("attach")] ATTACH,
            [Description("attachments")] ATTACHMENTS,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=detach>&<group=<UUID|STRING>>&<password=<STRING>>&<attachments=<STRING[,UUID...]>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("detach")] [Description("detach")] DETACH,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitiveowners>&<group=<UUID|STRING>>&<password=<STRING>>&[position=<VECTOR2>]&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("getprimitiveowners")] [Description("getprimitiveowners")] GETPRIMITIVEOWNERS,
            [Description("entity")] ENTITY,
            [Description("channel")] CHANNEL,
            [Description("name")] NAME,
            [Description("description")] DESCRIPTION,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=getprimitivedata>&<group=<UUID|STRING>>&<password=<STRING>>&<item=<UUID|STRING>>&[range=<FLOAT>]&<data=<Primitive[,Primitive...]>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Interact)] [CorradeCommand("getprimitivedata")] [Description("getprimitivedata")] GETPRIMITIVEDATA,
            [IsCorradeCommand(true)] [CommandInputSyntax("<command=activate>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Grooming)] [CorradeCommand("activate")] [Description("activate")] ACTIVATE,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=autopilot>&<group=<UUID|STRING>>&<password=<STRING>>&<position=<VECTOR2>>&<action=<start|stop>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Movement)] [CorradeCommand("autopilot")] [Description("autopilot")] AUTOPILOT,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=mute>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<mute|unmute>>&action=mute:<name=<STRING>&target=<UUID>>&action=unmute:<name=<STRING>|target=<UUID>>&action=mute:[type=MuteType]&action=mute:[flags=MuteFlags]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Mute)] [CorradeCommand("mute")] [Description("mute")] MUTE,

            [IsCorradeCommand(true)] [CommandInputSyntax("<command=getmutes>&<group=<UUID|STRING>>&<password=<STRING>>&[callback=<STRING>]")] [CommandPermissionMask((uint) Permissions.Mute)] [CorradeCommand("getmutes")] [Description("getmutes")] GETMUTES,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=notify>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<add|set|remove|list|clear|purge>>&action=add|set|remove|clear:<type=<STRING[,STRING...]>>&action=add|set|remove:<URL=<STRING>>&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Notifications)] [CorradeCommand("notify")] [Description("notify")] NOTIFY,
            [Description("source")] SOURCE,
            [Description("effect")] EFFECT,
            [Description("id")] ID,

            [IsCorradeCommand(true)] [CommandInputSyntax(
                "<command=terrain>&<group=<UUID|STRING>>&<password=<STRING>>&<action=<set|get>>&action=set:<data=<STRING>>&[region=<STRING>]&[callback=<STRING>]"
                )] [CommandPermissionMask((uint) Permissions.Land)] [CorradeCommand("terrain")] [Description("terrain")] TERRAIN,
            [Description("output")] OUTPUT,
            [Description("input")] INPUT
        }

        /// <summary>
        ///     The permission mask of a command.
        /// </summary>
        private class CommandPermissionMaskAttribute : Attribute
        {
            public CommandPermissionMaskAttribute(uint permissionMask)
            {
                PermissionMask = permissionMask;
            }

            public uint PermissionMask { get; }
        }

        /// <summary>
        ///     Whether this is a command or not.
        /// </summary>
        private class IsCorradeCommandAttribute : Attribute
        {
            public IsCorradeCommandAttribute(bool isCorradeCommand)
            {
                IsCorradeCorradeCommand = isCorradeCommand;
            }

            public bool IsCorradeCorradeCommand { get; }
        }

        private class IsRLVBehaviourAttribute : Attribute
        {
            public IsRLVBehaviourAttribute(bool isRLVBehaviour)
            {
                IsRLVBehaviour = isRLVBehaviour;
            }

            public bool IsRLVBehaviour { get; }
        }

        private class CorradeCommandAttribute : Attribute
        {
            public CorradeCommandAttribute(string command)
            {
                FieldInfo fi =
                    typeof (CorradeCommands).GetFields(BindingFlags.Static | BindingFlags.Public)
                        .AsParallel()
                        .Where(o => o.FieldType == typeof (Action<CorradeCommandParameters, Dictionary<string, string>>))
                        .SingleOrDefault(o => o.Name.Equals(command));
                CorradeCommand = (Action<CorradeCommandParameters, Dictionary<string, string>>) fi?.GetValue(null);
            }

            public Action<CorradeCommandParameters, Dictionary<string, string>> CorradeCommand { get; }
        }

        private class RLVBehaviourAttribute : Attribute
        {
            public RLVBehaviourAttribute(string behaviour)
            {
                FieldInfo fi =
                    typeof (RLVBehaviours).GetFields(BindingFlags.Static | BindingFlags.Public)
                        .AsParallel()
                        .Where(o => o.FieldType == typeof (Action<string, RLVRule, UUID>))
                        .SingleOrDefault(o => o.Name.Equals(behaviour));
                RLVBehaviour = (Action<string, RLVRule, UUID>) fi?.GetValue(null);
            }

            public Action<string, RLVRule, UUID> RLVBehaviour { get; }
        }

        /// <summary>
        ///     The syntax for a command.
        /// </summary>
        private class CommandInputSyntaxAttribute : Attribute
        {
            public CommandInputSyntaxAttribute(string syntax)
            {
                Syntax = syntax;
            }

            public string Syntax { get; }
        }

        /// <summary>
        ///     A structure for script permission requests.
        /// </summary>
        private struct ScriptPermissionRequest
        {
            public Agent Agent;
            [Description("item")] public UUID Item;
            [Description("name")] public string Name;
            [Description("permission")] public ScriptPermission Permission;
            [Description("region")] public string Region;
            [Description("task")] public UUID Task;
        }

        /// <summary>
        ///     A serializable dictionary implementation.
        /// </summary>
        /// <typeparam name="TKey">the key</typeparam>
        /// <typeparam name="TVal">the value</typeparam>
        /// <remarks>Copyright (c) Dacris Software Inc. MIT license</remarks>
        [Serializable]
        public sealed class SerializableDictionary<TKey, TVal> : Dictionary<TKey, TVal>, IXmlSerializable, ISerializable
        {
            #region Constants

            private const string DictionaryNodeName = "Dictionary";
            private const string ItemNodeName = "Item";
            private const string KeyNodeName = "Key";
            private const string ValueNodeName = "Value";

            #endregion

            #region Constructors

            public SerializableDictionary()
            {
            }

            public SerializableDictionary(IDictionary<TKey, TVal> dictionary)
                : base(dictionary)
            {
            }

            public SerializableDictionary(IEqualityComparer<TKey> comparer)
                : base(comparer)
            {
            }

            public SerializableDictionary(int capacity)
                : base(capacity)
            {
            }

            public SerializableDictionary(IDictionary<TKey, TVal> dictionary, IEqualityComparer<TKey> comparer)
                : base(dictionary, comparer)
            {
            }

            public SerializableDictionary(int capacity, IEqualityComparer<TKey> comparer)
                : base(capacity, comparer)
            {
            }

            #endregion

            #region ISerializable Members

            private SerializableDictionary(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                int itemCount = info.GetInt32("ItemCount");
                for (int i = 0; i < itemCount; i++)
                {
                    KeyValuePair<TKey, TVal> kvp =
                        (KeyValuePair<TKey, TVal>)
                            info.GetValue(string.Format("Item{0}", i), typeof (KeyValuePair<TKey, TVal>));
                    Add(kvp.Key, kvp.Value);
                }
            }

            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("ItemCount", Count);
                int itemIdx = 0;
                foreach (KeyValuePair<TKey, TVal> kvp in this)
                {
                    info.AddValue(string.Format("Item{0}", itemIdx), kvp, typeof (KeyValuePair<TKey, TVal>));
                    itemIdx++;
                }
                base.GetObjectData(info, context);
            }

            #endregion

            #region IXmlSerializable Members

            void IXmlSerializable.WriteXml(XmlWriter writer)
            {
                //writer.WriteStartElement(DictionaryNodeName);
                foreach (KeyValuePair<TKey, TVal> kvp in this)
                {
                    writer.WriteStartElement(ItemNodeName);
                    writer.WriteStartElement(KeyNodeName);
                    KeySerializer.Serialize(writer, kvp.Key);
                    writer.WriteEndElement();
                    writer.WriteStartElement(ValueNodeName);
                    ValueSerializer.Serialize(writer, kvp.Value);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                //writer.WriteEndElement();
            }

            void IXmlSerializable.ReadXml(XmlReader reader)
            {
                if (reader.IsEmptyElement)
                {
                    return;
                }

                // Move past container
                if (!reader.Read())
                {
                    throw new XmlException("Error in Deserialization of Dictionary");
                }

                //reader.ReadStartElement(DictionaryNodeName);
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    reader.ReadStartElement(ItemNodeName);
                    reader.ReadStartElement(KeyNodeName);
                    TKey key = (TKey) KeySerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    reader.ReadStartElement(ValueNodeName);
                    TVal value = (TVal) ValueSerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    reader.ReadEndElement();
                    Add(key, value);
                    reader.MoveToContent();
                }
                //reader.ReadEndElement();

                reader.ReadEndElement(); // Read End Element to close Read of containing node
            }

            XmlSchema IXmlSerializable.GetSchema()
            {
                return null;
            }

            #endregion

            #region Private Properties

            private XmlSerializer ValueSerializer
            {
                get { return valueSerializer ?? (valueSerializer = new XmlSerializer(typeof (TVal))); }
            }

            private XmlSerializer KeySerializer
            {
                get { return keySerializer ?? (keySerializer = new XmlSerializer(typeof (TKey))); }
            }

            #endregion

            #region Private Members

            private XmlSerializer keySerializer;
            private XmlSerializer valueSerializer;

            #endregion
        }

        /// <summary>
        ///     A structure to track Sphere effects.
        /// </summary>
        private struct SphereEffect
        {
            [Description("alpha")] public float Alpha;
            [Description("color")] public Vector3 Color;
            [Description("duration")] public float Duration;
            [Description("effect")] public UUID Effect;
            [Description("offset")] public Vector3d Offset;
            [Description("termination")] public DateTime Termination;
        }

        /// <summary>
        ///     A structure for teleport lures.
        /// </summary>
        private struct TeleportLure
        {
            public Agent Agent;
            public UUID Session;
        }

        /// <summary>
        ///     Various types.
        /// </summary>
        private enum Type : uint
        {
            [Description("none")] NONE = 0,
            [Description("text")] TEXT,
            [Description("voice")] VOICE,
            [Description("scripts")] SCRIPTS,
            [Description("colliders")] COLLIDERS,
            [Description("ban")] BAN,
            [Description("group")] GROUP,
            [Description("user")] USER,
            [Description("manager")] MANAGER,
            [Description("classified")] CLASSIFIED,
            [Description("event")] EVENT,
            [Description("land")] LAND,
            [Description("people")] PEOPLE,
            [Description("place")] PLACE,
            [Description("input")] INPUT,
            [Description("output")] OUTPUT
        }

        /// <summary>
        ///     Possible viewer effects.
        /// </summary>
        private enum ViewerEffectType : uint
        {
            [Description("none")] NONE = 0,
            [Description("look")] LOOK,
            [Description("point")] POINT,
            [Description("sphere")] SPHERE,
            [Description("beam")] BEAM
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Given a number of allowed events per seconds, this class allows you
        ///     to determine via the IsSafe property whether it is safe to trigger
        ///     another lined-up event. This is mostly used to check that throttles
        ///     are being respected.
        /// </summary>
        public class wasTimedThrottle : IDisposable
        {
            private readonly uint EventsAllowed;
            private readonly object LockObject = new object();
            private uint _Events;
            private System.Timers.Timer timer;

            public wasTimedThrottle(uint events, uint seconds)
            {
                EventsAllowed = events;
                if (timer == null)
                {
                    timer = new System.Timers.Timer(seconds);
                    timer.Elapsed += (o, p) =>
                    {
                        lock (LockObject)
                        {
                            _Events = 0;
                        }
                    };
                    timer.Start();
                }
            }

            public bool IsSafe
            {
                get
                {
                    lock (LockObject)
                    {
                        return ++_Events <= EventsAllowed;
                    }
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool dispose)
            {
                if (timer != null)
                {
                    timer.Dispose();
                    timer = null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     An alarm class similar to the UNIX alarm with the added benefit
        ///     of a decaying timer that tracks the time between rescheduling.
        /// </summary>
        /// <remarks>
        ///     (C) Wizardry and Steamworks 2013 - License: GNU GPLv3
        /// </remarks>
        public class wasAdaptiveAlarm : IDisposable
        {
            [Flags]
            public enum DECAY_TYPE
            {
                [XmlEnum(Name = "none")] [Description("none")] NONE = 0,
                [XmlEnum(Name = "arithmetic")] [Description("arithmetic")] ARITHMETIC = 1,
                [XmlEnum(Name = "geometric")] [Description("geometric")] GEOMETRIC = 2,
                [XmlEnum(Name = "harmonic")] [Description("harmonic")] HARMONIC = 4,
                [XmlEnum(Name = "weighted")] [Description("weighted")] WEIGHTED = 5
            }

            private readonly DECAY_TYPE decay = DECAY_TYPE.NONE;
            private readonly Stopwatch elapsed = new Stopwatch();
            private readonly object LockObject = new object();
            private readonly HashSet<double> times = new HashSet<double>();
            private System.Timers.Timer alarm;

            /// <summary>
            ///     The default constructor using no decay.
            /// </summary>
            public wasAdaptiveAlarm()
            {
                Signal = new ManualResetEvent(false);
            }

            /// <summary>
            ///     The constructor for the wasAdaptiveAlarm class taking as parameter a decay type.
            /// </summary>
            /// <param name="decay">the type of decay: arithmetic, geometric, harmonic, heronian or quadratic</param>
            public wasAdaptiveAlarm(DECAY_TYPE decay)
            {
                Signal = new ManualResetEvent(false);
                this.decay = decay;
            }

            public ManualResetEvent Signal { get; set; }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public void Alarm(double deadline)
            {
                lock (LockObject)
                {
                    switch (alarm == null)
                    {
                        case true:
                            alarm = new System.Timers.Timer(deadline);
                            alarm.Elapsed += (o, p) =>
                            {
                                lock (LockObject)
                                {
                                    Signal.Set();
                                    elapsed.Stop();
                                    times.Clear();
                                    alarm = null;
                                }
                            };
                            elapsed.Start();
                            alarm.Start();
                            return;
                        case false:
                            elapsed.Stop();
                            times.Add(elapsed.ElapsedMilliseconds);
                            switch (decay)
                            {
                                case DECAY_TYPE.ARITHMETIC:
                                    alarm.Interval = (deadline + times.Aggregate((a, b) => b + a))/(1f + times.Count);
                                    break;
                                case DECAY_TYPE.GEOMETRIC:
                                    alarm.Interval = Math.Pow(deadline*times.Aggregate((a, b) => b*a),
                                        1f/(1f + times.Count));
                                    break;
                                case DECAY_TYPE.HARMONIC:
                                    alarm.Interval = (1f + times.Count)/
                                                     (1f/deadline + times.Aggregate((a, b) => 1f/b + 1f/a));
                                    break;
                                case DECAY_TYPE.WEIGHTED:
                                    HashSet<double> d = new HashSet<double>(times) {deadline};
                                    double total = d.Aggregate((a, b) => b + a);
                                    alarm.Interval = d.Aggregate((a, b) => Math.Pow(a, 2)/total + Math.Pow(b, 2)/total);
                                    break;
                                default:
                                    alarm.Interval = deadline;
                                    break;
                            }
                            elapsed.Reset();
                            elapsed.Start();
                            break;
                    }
                }
            }

            protected virtual void Dispose(bool dispose)
            {
                if (alarm != null)
                {
                    alarm.Dispose();
                    alarm = null;
                }
            }
        }

        #region KEY-VALUE DATA

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns the value of a key from a key-value data string.
        /// </summary>
        /// <param name="key">the key of the value</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>true if the key was found in data</returns>
        private static string wasKeyValueGet(string key, string data)
        {
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = o.Last()
                })
                .Where(o => o.k.Equals(key))
                .Select(o => o.v)
                .FirstOrDefault();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns a key-value data string with a key set to a given value.
        /// </summary>
        /// <param name="key">the key of the value</param>
        /// <param name="value">the value to set the key to</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>
        ///     a key-value data string or the empty string if either key or
        ///     value are empty
        /// </returns>
        private static string wasKeyValueSet(string key, string value, string data)
        {
            HashSet<string> output = new HashSet<string>(data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = !o.First().Equals(key) ? o.Last() : value
                }).Select(o => string.Join("=", o.k, o.v)));
            string append = string.Join("=", key, value);
            if (!output.Contains(append))
            {
                output.Add(append);
            }
            return string.Join("&", output.ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Deletes a key-value pair from a string referenced by a key.
        /// </summary>
        /// <param name="key">the key to search for</param>
        /// <param name="data">the key-value data segment</param>
        /// <returns>a key-value pair string</returns>
        private static string wasKeyValueDelete(string key, string data)
        {
            return string.Join("&", data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = o.Last()
                })
                .Where(o => !o.k.Equals(key))
                .Select(o => string.Join("=", o.k, o.v))
                .ToArray());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decodes key-value pair data to a dictionary.
        /// </summary>
        /// <param name="data">the key-value pair data</param>
        /// <returns>a dictionary containing the keys and values</returns>
        private static Dictionary<string, string> wasKeyValueDecode(string data)
        {
            return data.Split('&')
                .AsParallel()
                .Select(o => o.Split('=').ToList())
                .Where(o => o.Count.Equals(2))
                .Select(o => new
                {
                    k = o.First(),
                    v = o.Last()
                })
                .GroupBy(o => o.k)
                .ToDictionary(o => o.Key, p => p.First().v);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Serialises a dictionary to key-value data.
        /// </summary>
        /// <param name="data">a dictionary</param>
        /// <returns>a key-value data encoded string</returns>
        private static string wasKeyValueEncode(Dictionary<string, string> data)
        {
            return string.Join("&", data.AsParallel().Select(o => string.Join("=", o.Key, o.Value)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>Escapes a dictionary's keys and values for sending as POST data.</summary>
        /// <param name="data">A dictionary containing keys and values to be escaped</param>
        private static Dictionary<string, string> wasKeyValueEscape(Dictionary<string, string> data)
        {
            return data.AsParallel().ToDictionary(o => wasOutput(o.Key), p => wasOutput(p.Value));
        }

        #endregion

        #region CRYPTOGRAPHY

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets an array element at a given modulo index.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="index">a positive or negative index of the element</param>
        /// <param name="data">the array</param>
        /// <return>an array element</return>
        public static T wasGetElementAt<T>(T[] data, int index)
        {
            switch (index < 0)
            {
                case true:
                    return data[((index%data.Length) + data.Length)%data.Length];
                default:
                    return data[index%data.Length];
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets a sub-array from an array.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="data">the array</param>
        /// <param name="start">the start index</param>
        /// <param name="stop">the stop index (-1 denotes the end)</param>
        /// <returns>the array slice between start and stop</returns>
        public static T[] wasGetSubArray<T>(T[] data, int start, int stop)
        {
            if (stop.Equals(-1))
                stop = data.Length - 1;
            T[] result = new T[stop - start + 1];
            Array.Copy(data, start, result, 0, stop - start + 1);
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Delete a sub-array and return the result.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="data">the array</param>
        /// <param name="start">the start index</param>
        /// <param name="stop">the stop index (-1 denotes the end)</param>
        /// <returns>the array without elements between start and stop</returns>
        public static T[] wasDeleteSubArray<T>(T[] data, int start, int stop)
        {
            if (stop.Equals(-1))
                stop = data.Length - 1;
            T[] result = new T[data.Length - (stop - start) - 1];
            Array.Copy(data, 0, result, 0, start);
            Array.Copy(data, stop + 1, result, start, data.Length - stop - 1);
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Concatenate multiple arrays.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="arrays">multiple arrays</param>
        /// <returns>a flat array with all arrays concatenated</returns>
        public static T[] wasConcatenateArrays<T>(params T[][] arrays)
        {
            int resultLength = 0;
            foreach (T[] o in arrays)
            {
                resultLength += o.Length;
            }
            T[] result = new T[resultLength];
            int offset = 0;
            for (int x = 0; x < arrays.Length; x++)
            {
                arrays[x].CopyTo(result, offset);
                offset += arrays[x].Length;
            }
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Permutes an array in reverse a given number of times.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="input">the array</param>
        /// <param name="times">the number of times to permute</param>
        /// <returns>the array with the elements permuted</returns>
        private static T[] wasReversePermuteArrayElements<T>(T[] input, int times)
        {
            if (times.Equals(0)) return input;
            T[] slice = new T[input.Length];
            Array.Copy(input, 1, slice, 0, input.Length - 1);
            Array.Copy(input, 0, slice, input.Length - 1, 1);
            return wasReversePermuteArrayElements(slice, --times);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Permutes an array forward a given number of times.
        /// </summary>
        /// <typeparam name="T">the array type</typeparam>
        /// <param name="input">the array</param>
        /// <param name="times">the number of times to permute</param>
        /// <returns>the array with the elements permuted</returns>
        private static T[] wasForwardPermuteArrayElements<T>(T[] input, int times)
        {
            if (times.Equals(0)) return input;
            T[] slice = new T[input.Length];
            Array.Copy(input, input.Length - 1, slice, 0, 1);
            Array.Copy(input, 0, slice, 1, input.Length - 1);
            return wasForwardPermuteArrayElements(slice, --times);
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Encrypt or decrypt a message given a set of rotors, plugs and a reflector.
        /// </summary>
        /// <param name="message">the message to encyrpt or decrypt</param>
        /// <param name="rotors">any combination of: 1, 2, 3, 4, 5, 6, 7, 8, b, g</param>
        /// <param name="plugs">the letter representing the start character for the rotor</param>
        /// <param name="reflector">any one of: B, b, C, c</param>
        /// <returns>either a decrypted or encrypted string</returns>
        private static string wasEnigma(string message, char[] rotors, char[] plugs, char reflector)
        {
            Dictionary<char, char[]> def_rotors = new Dictionary<char, char[]>
            {
                {
                    '1', new[]
                    {
                        'e', 'k', 'm', 'f', 'l',
                        'g', 'd', 'q', 'v', 'z',
                        'n', 't', 'o', 'w', 'y',
                        'h', 'x', 'u', 's', 'p',
                        'a', 'i', 'b', 'r', 'c',
                        'j'
                    }
                },
                {
                    '2', new[]
                    {
                        'a', 'j', 'd', 'k', 's',
                        'i', 'r', 'u', 'x', 'b',
                        'l', 'h', 'w', 't', 'm',
                        'c', 'q', 'g', 'z', 'n',
                        'p', 'y', 'f', 'v', 'o',
                        'e'
                    }
                },
                {
                    '3', new[]
                    {
                        'b', 'd', 'f', 'h', 'j',
                        'l', 'c', 'p', 'r', 't',
                        'x', 'v', 'z', 'n', 'y',
                        'e', 'i', 'w', 'g', 'a',
                        'k', 'm', 'u', 's', 'q',
                        'o'
                    }
                },
                {
                    '4', new[]
                    {
                        'e', 's', 'o', 'v', 'p',
                        'z', 'j', 'a', 'y', 'q',
                        'u', 'i', 'r', 'h', 'x',
                        'l', 'n', 'f', 't', 'g',
                        'k', 'd', 'c', 'm', 'w',
                        'b'
                    }
                },
                {
                    '5', new[]
                    {
                        'v', 'z', 'b', 'r', 'g',
                        'i', 't', 'y', 'u', 'p',
                        's', 'd', 'n', 'h', 'l',
                        'x', 'a', 'w', 'm', 'j',
                        'q', 'o', 'f', 'e', 'c',
                        'k'
                    }
                },
                {
                    '6', new[]
                    {
                        'j', 'p', 'g', 'v', 'o',
                        'u', 'm', 'f', 'y', 'q',
                        'b', 'e', 'n', 'h', 'z',
                        'r', 'd', 'k', 'a', 's',
                        'x', 'l', 'i', 'c', 't',
                        'w'
                    }
                },
                {
                    '7', new[]
                    {
                        'n', 'z', 'j', 'h', 'g',
                        'r', 'c', 'x', 'm', 'y',
                        's', 'w', 'b', 'o', 'u',
                        'f', 'a', 'i', 'v', 'l',
                        'p', 'e', 'k', 'q', 'd',
                        't'
                    }
                },
                {
                    '8', new[]
                    {
                        'f', 'k', 'q', 'h', 't',
                        'l', 'x', 'o', 'c', 'b',
                        'j', 's', 'p', 'd', 'z',
                        'r', 'a', 'm', 'e', 'w',
                        'n', 'i', 'u', 'y', 'g',
                        'v'
                    }
                },
                {
                    'b', new[]
                    {
                        'l', 'e', 'y', 'j', 'v',
                        'c', 'n', 'i', 'x', 'w',
                        'p', 'b', 'q', 'm', 'd',
                        'r', 't', 'a', 'k', 'z',
                        'g', 'f', 'u', 'h', 'o',
                        's'
                    }
                },
                {
                    'g', new[]
                    {
                        'f', 's', 'o', 'k', 'a',
                        'n', 'u', 'e', 'r', 'h',
                        'm', 'b', 't', 'i', 'y',
                        'c', 'w', 'l', 'q', 'p',
                        'z', 'x', 'v', 'g', 'j',
                        'd'
                    }
                }
            };

            Dictionary<char, char[]> def_reflectors = new Dictionary<char, char[]>
            {
                {
                    'B', new[]
                    {
                        'a', 'y', 'b', 'r', 'c', 'u', 'd', 'h',
                        'e', 'q', 'f', 's', 'g', 'l', 'i', 'p',
                        'j', 'x', 'k', 'n', 'm', 'o', 't', 'z',
                        'v', 'w'
                    }
                },
                {
                    'b', new[]
                    {
                        'a', 'e', 'b', 'n', 'c', 'k', 'd', 'q',
                        'f', 'u', 'g', 'y', 'h', 'w', 'i', 'j',
                        'l', 'o', 'm', 'p', 'r', 'x', 's', 'z',
                        't', 'v'
                    }
                },
                {
                    'C', new[]
                    {
                        'a', 'f', 'b', 'v', 'c', 'p', 'd', 'j',
                        'e', 'i', 'g', 'o', 'h', 'y', 'k', 'r',
                        'l', 'z', 'm', 'x', 'n', 'w', 't', 'q',
                        's', 'u'
                    }
                },
                {
                    'c', new[]
                    {
                        'a', 'r', 'b', 'd', 'c', 'o', 'e', 'j',
                        'f', 'n', 'g', 't', 'h', 'k', 'i', 'v',
                        'l', 'm', 'p', 'w', 'q', 'z', 's', 'x',
                        'u', 'y'
                    }
                }
            };

            // Setup rotors from plugs.
            foreach (char rotor in rotors)
            {
                char plug = plugs[Array.IndexOf(rotors, rotor)];
                int i = Array.IndexOf(def_rotors[rotor], plug);
                if (i.Equals(0)) continue;
                def_rotors[rotor] = wasConcatenateArrays(new[] {plug},
                    wasGetSubArray(wasDeleteSubArray(def_rotors[rotor], i, i), i, -1),
                    wasGetSubArray(wasDeleteSubArray(def_rotors[rotor], i + 1, -1), 0, i - 1));
            }

            StringBuilder result = new StringBuilder();
            foreach (char c in message)
            {
                if (!char.IsLetter(c))
                {
                    result.Append(c);
                    continue;
                }

                // Normalize to lower.
                char l = char.ToLower(c);

                Action<char[]> rotate = o =>
                {
                    int i = o.Length - 1;
                    do
                    {
                        def_rotors[o[0]] = wasForwardPermuteArrayElements(def_rotors[o[0]], 1);
                        if (i.Equals(0))
                        {
                            rotors = wasReversePermuteArrayElements(o, 1);
                            continue;
                        }
                        l = wasGetElementAt(def_rotors[o[1]], Array.IndexOf(def_rotors[o[0]], l) - 1);
                        o = wasReversePermuteArrayElements(o, 1);
                    } while (--i > -1);
                };

                // Forward pass through the Enigma's rotors.
                rotate.Invoke(rotors);

                // Reflect
                int x = Array.IndexOf(def_reflectors[reflector], l);
                l = (x + 1)%2 == 0 ? def_reflectors[reflector][x - 1] : def_reflectors[reflector][x + 1];

                // Reverse the order of the rotors.
                Array.Reverse(rotors);

                // Reverse pass through the Enigma's rotors.
                rotate.Invoke(rotors);

                if (char.IsUpper(c))
                {
                    l = char.ToUpper(l);
                }
                result.Append(l);
            }

            return result.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Expand the VIGENRE key to the length of the input.
        /// </summary>
        /// <param name="input">the input to expand to</param>
        /// <param name="enc_key">the key to expand</param>
        /// <returns>the expanded key</returns>
        private static string wasVigenereExpandKey(string input, string enc_key)
        {
            string exp_key = string.Empty;
            int i = 0, j = 0;
            do
            {
                char p = input[i];
                if (!char.IsLetter(p))
                {
                    exp_key += p;
                    ++i;
                    continue;
                }
                int m = j%enc_key.Length;
                exp_key += enc_key[m];
                ++j;
                ++i;
            } while (i < input.Length);
            return exp_key;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Encrypt using VIGENERE.
        /// </summary>
        /// <param name="input">the input to encrypt</param>
        /// <param name="enc_key">the key to encrypt with</param>
        /// <returns>the encrypted input</returns>
        private static string wasEncryptVIGENERE(string input, string enc_key)
        {
            char[] a =
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
            };

            enc_key = wasVigenereExpandKey(input, enc_key);
            string result = string.Empty;
            int i = 0;
            do
            {
                char p = input[i];
                if (!char.IsLetter(p))
                {
                    result += p;
                    ++i;
                    continue;
                }
                char q =
                    wasReversePermuteArrayElements(a, Array.IndexOf(a, enc_key[i]))[
                        Array.IndexOf(a, char.ToLowerInvariant(p))];
                if (char.IsUpper(p))
                {
                    q = char.ToUpperInvariant(q);
                }
                result += q;
                ++i;
            } while (i < input.Length);
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2014 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Decrypt using VIGENERE.
        /// </summary>
        /// <param name="input">the input to decrypt</param>
        /// <param name="enc_key">the key to decrypt with</param>
        /// <returns>the decrypted input</returns>
        private static string wasDecryptVIGENERE(string input, string enc_key)
        {
            char[] a =
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
            };

            enc_key = wasVigenereExpandKey(input, enc_key);
            string result = string.Empty;
            int i = 0;
            do
            {
                char p = input[i];
                if (!char.IsLetter(p))
                {
                    result += p;
                    ++i;
                    continue;
                }
                char q =
                    a[
                        Array.IndexOf(wasReversePermuteArrayElements(a, Array.IndexOf(a, enc_key[i])),
                            char.ToLowerInvariant(p))];
                if (char.IsUpper(p))
                {
                    q = char.ToUpperInvariant(q);
                }
                result += q;
                ++i;
            } while (i < input.Length);
            return result;
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     An implementation of the ATBASH cypher for latin alphabets.
        /// </summary>
        /// <param name="data">the data to encrypt or decrypt</param>
        /// <returns>the encrypted or decrypted data</returns>
        private static string wasATBASH(string data)
        {
            char[] a =
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
            };

            char[] input = data.ToArray();

            Parallel.ForEach(Enumerable.Range(0, data.Length), i =>
            {
                char e = input[i];
                if (!char.IsLetter(e)) return;
                int x = 25 - Array.BinarySearch(a, char.ToLowerInvariant(e));
                if (!char.IsUpper(e))
                {
                    input[i] = a[x];
                    return;
                }
                input[i] = char.ToUpperInvariant(a[x]);
            });

            return new string(input);
        }

        /// <summary>
        ///     Encrypts a string given a key and initialization vector.
        /// </summary>
        /// <param name="data">the string to encrypt</param>
        /// <param name="Key">the key</param>
        /// <param name="IV">the initialization bector</param>
        /// <returns>Base64 encoded encrypted data</returns>
        private static string wasAESEncrypt(string data, byte[] Key, byte[] IV)
        {
            byte[] encryptedData;
            using (RijndaelManaged rijdanelManaged = new RijndaelManaged())
            {
                //  FIPS-197 / CBC
                rijdanelManaged.BlockSize = 128;
                rijdanelManaged.Mode = CipherMode.CBC;

                rijdanelManaged.Key = Key;
                rijdanelManaged.IV = IV;

                ICryptoTransform encryptor = rijdanelManaged.CreateEncryptor(rijdanelManaged.Key, rijdanelManaged.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write)
                        )
                    {
                        using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(data);
                            streamWriter.Flush();
                        }
                        encryptedData = memoryStream.ToArray();
                    }
                }
            }
            return Convert.ToBase64String(encryptedData);
        }

        /// <summary>
        ///     Decrypts a Base64 encoded string using AES with a given key and initialization vector.
        /// </summary>
        /// <param name="data">a Base64 encoded string of the data to decrypt</param>
        /// <param name="Key">the key</param>
        /// <param name="IV">the initialization vector</param>
        /// <returns>the decrypted data</returns>
        private static string wasAESDecrypt(string data, byte[] Key, byte[] IV)
        {
            string plaintext;
            using (RijndaelManaged rijdanelManaged = new RijndaelManaged())
            {
                //  FIPS-197 / CBC
                rijdanelManaged.BlockSize = 128;
                rijdanelManaged.Mode = CipherMode.CBC;

                rijdanelManaged.Key = Key;
                rijdanelManaged.IV = IV;

                ICryptoTransform decryptor = rijdanelManaged.CreateDecryptor(rijdanelManaged.Key, rijdanelManaged.IV);

                using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(data)))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(cryptoStream))
                        {
                            plaintext = streamReader.ReadToEnd();
                        }
                    }
                }
            }
            return plaintext;
        }

        #endregion

        #region NAME AND UUID RESOLVERS

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Tries to build an UUID out of the data string.
        /// </summary>
        /// <param name="data">a string</param>
        /// <returns>an UUID or the supplied string in case data could not be resolved</returns>
        private static object StringOrUUID(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            UUID @UUID;
            if (!UUID.TryParse(data, out @UUID))
            {
                return data;
            }
            return @UUID;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates the current balance by requesting it from the grid.
        /// </summary>
        /// <param name="millisecondsTimeout">timeout for the request in milliseconds</param>
        /// <returns>true if the balance could be retrieved</returns>
        private static bool UpdateBalance(uint millisecondsTimeout)
        {
            ManualResetEvent MoneyBalanceEvent = new ManualResetEvent(false);
            EventHandler<MoneyBalanceReplyEventArgs> MoneyBalanceEventHandler =
                (sender, args) => MoneyBalanceEvent.Set();
            lock (ClientInstanceSelfLock)
            {
                Client.Self.MoneyBalanceReply += MoneyBalanceEventHandler;
                Client.Self.RequestBalance();
                if (!MoneyBalanceEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
                    return false;
                }
                Client.Self.MoneyBalanceReply -= MoneyBalanceEventHandler;
            }
            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a group name to an UUID by using the directory search.
        /// </summary>
        /// <param name="groupName">the name of the group to resolve</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="groupUUID">an object in which to store the UUID of the group</param>
        /// <returns>true if the group name could be resolved to an UUID</returns>
        private static bool directGroupNameToUUID(string groupName, uint millisecondsTimeout, uint dataTimeout,
            ref UUID groupUUID)
        {
            UUID localGroupUUID = UUID.Zero;
            wasAdaptiveAlarm DirGroupsReceivedAlarm = new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
            EventHandler<DirGroupsReplyEventArgs> DirGroupsReplyDelegate = (sender, args) =>
            {
                DirGroupsReceivedAlarm.Alarm(dataTimeout);
                DirectoryManager.GroupSearchData groupSearchData =
                    args.MatchedGroups.AsParallel()
                        .FirstOrDefault(o => o.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                switch (!groupSearchData.Equals(default(DirectoryManager.GroupSearchData)))
                {
                    case true:
                        localGroupUUID = groupSearchData.GroupID;
                        DirGroupsReceivedAlarm.Signal.Set();
                        break;
                }
            };
            Client.Directory.DirGroupsReply += DirGroupsReplyDelegate;
            Client.Directory.StartGroupSearch(groupName, 0);
            if (!DirGroupsReceivedAlarm.Signal.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Directory.DirGroupsReply -= DirGroupsReplyDelegate;
                return false;
            }
            Client.Directory.DirGroupsReply -= DirGroupsReplyDelegate;
            if (localGroupUUID.Equals(UUID.Zero)) return false;
            groupUUID = localGroupUUID;
            return true;
        }

        /// <summary>
        ///     A wrapper for resolving group names to UUIDs by using Corrade's internal cache.
        /// </summary>
        /// <param name="groupName">the name of the group to resolve</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="groupUUID">an object in which to store the UUID of the group</param>
        /// <returns>true if the group name could be resolved to an UUID</returns>
        private static bool GroupNameToUUID(string groupName, uint millisecondsTimeout, uint dataTimeout,
            ref UUID groupUUID)
        {
            Cache.Groups @group = Cache.GroupCache.AsParallel().FirstOrDefault(o => o.Name.Equals(groupName));

            if (!@group.Equals(default(Cache.Groups)))
            {
                groupUUID = @group.UUID;
                return true;
            }

            bool succeeded;
            lock (ClientInstanceDirectoryLock)
            {
                succeeded = directGroupNameToUUID(groupName, millisecondsTimeout, dataTimeout, ref groupUUID);
            }
            if (succeeded)
            {
                Cache.GroupCache.Add(new Cache.Groups
                {
                    Name = groupName,
                    UUID = groupUUID
                });
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a group name to an UUID by using the directory search.
        /// </summary>
        /// <param name="groupName">a string to store the name to</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groupUUID">the UUID of the group to resolve</param>
        /// <returns>true if the group UUID could be resolved to an name</returns>
        private static bool directGroupUUIDToName(UUID groupUUID, uint millisecondsTimeout,
            ref string groupName)
        {
            string localGroupName = groupName;
            ManualResetEvent GroupProfileReceivedEvent = new ManualResetEvent(false);
            EventHandler<GroupProfileEventArgs> GroupProfileDelegate = (o, s) =>
            {
                localGroupName = s.Group.Name;
                GroupProfileReceivedEvent.Set();
            };
            Client.Groups.GroupProfile += GroupProfileDelegate;
            Client.Groups.RequestGroupProfile(groupUUID);
            if (!GroupProfileReceivedEvent.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Groups.GroupProfile -= GroupProfileDelegate;
                return false;
            }
            Client.Groups.GroupProfile -= GroupProfileDelegate;
            groupName = localGroupName;
            return true;
        }

        /// <summary>
        ///     A wrapper for resolving group names to UUIDs by using Corrade's internal cache.
        /// </summary>
        /// <param name="groupName">a string to store the name to</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="groupUUID">the UUID of the group to resolve</param>
        /// <returns>true if the group UUID could be resolved to an name</returns>
        private static bool GroupUUIDToName(UUID groupUUID, uint millisecondsTimeout,
            ref string groupName)
        {
            Cache.Groups @group = Cache.GroupCache.AsParallel().FirstOrDefault(o => o.UUID.Equals(groupUUID));

            if (!@group.Equals(default(Cache.Groups)))
            {
                groupName = @group.Name;
                return true;
            }

            bool succeeded;
            lock (ClientInstanceGroupsLock)
            {
                succeeded = directGroupUUIDToName(groupUUID, millisecondsTimeout, ref groupName);
            }
            if (succeeded)
            {
                Cache.GroupCache.Add(new Cache.Groups
                {
                    Name = groupName,
                    UUID = groupUUID
                });
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves an agent name to an agent UUID by searching the directory
        ///     services.
        /// </summary>
        /// <param name="agentFirstName">the first name of the agent</param>
        /// <param name="agentLastName">the last name of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="agentUUID">an object to store the agent UUID</param>
        /// <returns>true if the agent name could be resolved to an UUID</returns>
        private static bool directAgentNameToUUID(string agentFirstName, string agentLastName, uint millisecondsTimeout,
            uint dataTimeout,
            ref UUID agentUUID)
        {
            UUID localAgentUUID = UUID.Zero;
            wasAdaptiveAlarm DirPeopleReceivedAlarm = new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
            EventHandler<DirPeopleReplyEventArgs> DirPeopleReplyDelegate = (sender, args) =>
            {
                DirPeopleReceivedAlarm.Alarm(dataTimeout);
                DirectoryManager.AgentSearchData agentSearchData =
                    args.MatchedPeople.AsParallel().FirstOrDefault(
                        o =>
                            o.FirstName.Equals(agentFirstName, StringComparison.OrdinalIgnoreCase) &&
                            o.LastName.Equals(agentLastName, StringComparison.OrdinalIgnoreCase));
                switch (!agentSearchData.Equals(default(DirectoryManager.AgentSearchData)))
                {
                    case true:
                        localAgentUUID = agentSearchData.AgentID;
                        DirPeopleReceivedAlarm.Signal.Set();
                        break;
                }
            };
            Client.Directory.DirPeopleReply += DirPeopleReplyDelegate;
            Client.Directory.StartPeopleSearch(
                string.Format(Utils.EnUsCulture, "{0} {1}", agentFirstName, agentLastName), 0);
            if (!DirPeopleReceivedAlarm.Signal.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Directory.DirPeopleReply -= DirPeopleReplyDelegate;
                return false;
            }
            Client.Directory.DirPeopleReply -= DirPeopleReplyDelegate;
            if (localAgentUUID.Equals(UUID.Zero)) return false;
            agentUUID = localAgentUUID;
            return true;
        }

        /// <summary>
        ///     A wrapper for looking up an agent name using Corrade's internal cache.
        /// </summary>
        /// <param name="agentFirstName">the first name of the agent</param>
        /// <param name="agentLastName">the last name of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="agentUUID">an object to store the agent UUID</param>
        /// <returns>true if the agent name could be resolved to an UUID</returns>
        private static bool AgentNameToUUID(string agentFirstName, string agentLastName, uint millisecondsTimeout,
            uint dataTimeout,
            ref UUID agentUUID)
        {
            Cache.Agents agent = Cache.GetAgent(agentFirstName, agentLastName);
            if (!agent.Equals(default(Cache.Agents)))
            {
                agentUUID = agent.UUID;
                return true;
            }
            bool succeeded;
            lock (ClientInstanceDirectoryLock)
            {
                succeeded = directAgentNameToUUID(agentFirstName, agentLastName, millisecondsTimeout, dataTimeout,
                    ref agentUUID);
            }
            if (succeeded)
            {
                Cache.AddAgent(agentFirstName, agentLastName, agentUUID);
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves an agent UUID to an agent name.
        /// </summary>
        /// <param name="agentUUID">the UUID of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="agentName">an object to store the name of the agent in</param>
        /// <returns>true if the UUID could be resolved to a name</returns>
        private static bool directAgentUUIDToName(UUID agentUUID, uint millisecondsTimeout,
            ref string agentName)
        {
            if (agentUUID.Equals(UUID.Zero))
                return false;
            string localAgentName = string.Empty;
            ManualResetEvent UUIDNameReplyEvent = new ManualResetEvent(false);
            EventHandler<UUIDNameReplyEventArgs> UUIDNameReplyDelegate = (sender, args) =>
            {
                KeyValuePair<UUID, string> UUIDNameReply =
                    args.Names.AsParallel().FirstOrDefault(o => o.Key.Equals(agentUUID));
                if (!UUIDNameReply.Equals(default(KeyValuePair<UUID, string>)))
                    localAgentName = UUIDNameReply.Value;
                UUIDNameReplyEvent.Set();
            };
            Client.Avatars.UUIDNameReply += UUIDNameReplyDelegate;
            Client.Avatars.RequestAvatarName(agentUUID);
            if (!UUIDNameReplyEvent.WaitOne((int) millisecondsTimeout, false))
            {
                Client.Avatars.UUIDNameReply -= UUIDNameReplyDelegate;
                return false;
            }
            Client.Avatars.UUIDNameReply -= UUIDNameReplyDelegate;
            if (string.IsNullOrEmpty(localAgentName)) return false;
            agentName = localAgentName;
            return true;
        }

        /// <summary>
        ///     A wrapper for agent to UUID lookups using Corrade's internal cache.
        /// </summary>
        /// <param name="agentUUID">the UUID of the agent</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="agentName">an object to store the name of the agent in</param>
        /// <returns>true if the UUID could be resolved to a name</returns>
        private static bool AgentUUIDToName(UUID agentUUID, uint millisecondsTimeout,
            ref string agentName)
        {
            Cache.Agents agent = Cache.GetAgent(agentUUID);
            if (!agent.Equals(default(Cache.Agents)))
            {
                agentName = string.Join(" ", agent.FirstName, agent.LastName);
                return true;
            }
            bool succeeded;
            lock (ClientInstanceAvatarsLock)
            {
                succeeded = directAgentUUIDToName(agentUUID, millisecondsTimeout, ref agentName);
            }
            if (succeeded)
            {
                List<string> name = new List<string>(GetAvatarNames(agentName));
                Cache.AddAgent(name.First(), name.Last(), agentUUID);
            }
            return succeeded;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// ///
        /// <summary>
        ///     Resolves a role name to a role UUID.
        /// </summary>
        /// <param name="roleName">the name of the role to be resolved to an UUID</param>
        /// <param name="groupUUID">the UUID of the group to query for the role UUID</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="roleUUID">an UUID object to store the role UUID in</param>
        /// <returns>true if the role could be found</returns>
        private static bool RoleNameToUUID(string roleName, UUID groupUUID, uint millisecondsTimeout,
            ref UUID roleUUID)
        {
            switch (!roleName.Equals(LINDEN_CONSTANTS.GROUPS.EVERYONE_ROLE_NAME, StringComparison.Ordinal))
            {
                case false:
                    roleUUID = UUID.Zero;
                    return true;
            }
            ManualResetEvent GroupRoleDataReceivedAlarm = new ManualResetEvent(false);
            Dictionary<UUID, GroupRole> groupRoles = null;
            EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReplyDelegate = (sender, args) =>
            {
                groupRoles = args.Roles;
                GroupRoleDataReceivedAlarm.Set();
            };
            lock (ClientInstanceGroupsLock)
            {
                Client.Groups.GroupRoleDataReply += GroupRoleDataReplyDelegate;
                Client.Groups.RequestGroupRoles(groupUUID);
                if (!GroupRoleDataReceivedAlarm.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
                    return false;
                }
                Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
            }
            switch (groupRoles != null)
            {
                case true:
                    KeyValuePair<UUID, GroupRole> role = groupRoles.AsParallel()
                        .FirstOrDefault(o => o.Value.Name.Equals(roleName, StringComparison.Ordinal));
                    if (!role.Equals(default(KeyValuePair<UUID, GroupRole>)))
                        roleUUID = role.Key;
                    return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2013 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Resolves a role name to a role UUID.
        /// </summary>
        /// <param name="RoleUUID">the UUID of the role to be resolved to a name</param>
        /// <param name="GroupUUID">the UUID of the group to query for the role name</param>
        /// <param name="millisecondsTimeout">timeout for the search in milliseconds</param>
        /// <param name="dataTimeout">timeout for receiving answers from services</param>
        /// <param name="roleName">a string object to store the role name in</param>
        /// <returns>true if the role could be resolved</returns>
        private static bool RoleUUIDToName(UUID RoleUUID, UUID GroupUUID, uint millisecondsTimeout, uint dataTimeout,
            ref string roleName)
        {
            switch (!RoleUUID.Equals(UUID.Zero))
            {
                case false:
                    roleName = LINDEN_CONSTANTS.GROUPS.EVERYONE_ROLE_NAME;
                    return true;
            }
            ManualResetEvent GroupRoleDataReceivedEvent = new ManualResetEvent(false);
            Dictionary<UUID, GroupRole> groupRoles = null;
            EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReplyDelegate = (sender, args) =>
            {
                groupRoles = args.Roles;
                GroupRoleDataReceivedEvent.Set();
            };
            lock (ClientInstanceGroupsLock)
            {
                Client.Groups.GroupRoleDataReply += GroupRoleDataReplyDelegate;
                Client.Groups.RequestGroupRoles(GroupUUID);
                if (!GroupRoleDataReceivedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
                    return false;
                }
                Client.Groups.GroupRoleDataReply -= GroupRoleDataReplyDelegate;
            }
            switch (groupRoles != null)
            {
                case true:
                    KeyValuePair<UUID, GroupRole> role =
                        groupRoles.AsParallel().FirstOrDefault(o => o.Key.Equals(RoleUUID));
                    if (!role.Equals(default(KeyValuePair<UUID, GroupRole>)))
                        roleName = role.Value.Name;
                    return true;
            }
            return false;
        }

        #endregion

        #region RLV STRUCTURES

        /// <summary>
        ///     Holds all the active RLV rules.
        /// </summary>
        private static readonly HashSet<RLVRule> RLVRules = new HashSet<RLVRule>();

        /// <summary>
        ///     Locks down RLV for linear concurrent access.
        /// </summary>
        private static readonly object RLVRulesLock = new object();

        /// <summary>
        ///     RLV Wearables.
        /// </summary>
        private static readonly List<RLVWearable> RLVWearables = new List<RLVWearable>
        {
            new RLVWearable {Name = @"gloves", WearableType = WearableType.Gloves},
            new RLVWearable {Name = @"jacket", WearableType = WearableType.Jacket},
            new RLVWearable {Name = @"pants", WearableType = WearableType.Pants},
            new RLVWearable {Name = @"shirt", WearableType = WearableType.Shirt},
            new RLVWearable {Name = @"shoes", WearableType = WearableType.Shoes},
            new RLVWearable {Name = @"skirt", WearableType = WearableType.Skirt},
            new RLVWearable {Name = @"socks", WearableType = WearableType.Socks},
            new RLVWearable {Name = @"underpants", WearableType = WearableType.Underpants},
            new RLVWearable {Name = @"undershirt", WearableType = WearableType.Undershirt},
            new RLVWearable {Name = @"skin", WearableType = WearableType.Skin},
            new RLVWearable {Name = @"eyes", WearableType = WearableType.Eyes},
            new RLVWearable {Name = @"hair", WearableType = WearableType.Hair},
            new RLVWearable {Name = @"shape", WearableType = WearableType.Shape},
            new RLVWearable {Name = @"alpha", WearableType = WearableType.Alpha},
            new RLVWearable {Name = @"tattoo", WearableType = WearableType.Tattoo},
            new RLVWearable {Name = @"physics", WearableType = WearableType.Physics}
        };

        /// <summary>
        ///     RLV Attachments.
        /// </summary>
        private static readonly List<RLVAttachment> RLVAttachments = new List<RLVAttachment>
        {
            new RLVAttachment {Name = @"none", AttachmentPoint = AttachmentPoint.Default},
            new RLVAttachment {Name = @"chest", AttachmentPoint = AttachmentPoint.Chest},
            new RLVAttachment {Name = @"skull", AttachmentPoint = AttachmentPoint.Skull},
            new RLVAttachment {Name = @"left shoulder", AttachmentPoint = AttachmentPoint.LeftShoulder},
            new RLVAttachment {Name = @"right shoulder", AttachmentPoint = AttachmentPoint.RightShoulder},
            new RLVAttachment {Name = @"left hand", AttachmentPoint = AttachmentPoint.LeftHand},
            new RLVAttachment {Name = @"right hand", AttachmentPoint = AttachmentPoint.RightHand},
            new RLVAttachment {Name = @"left foot", AttachmentPoint = AttachmentPoint.LeftFoot},
            new RLVAttachment {Name = @"right foot", AttachmentPoint = AttachmentPoint.RightFoot},
            new RLVAttachment {Name = @"spine", AttachmentPoint = AttachmentPoint.Spine},
            new RLVAttachment {Name = @"pelvis", AttachmentPoint = AttachmentPoint.Pelvis},
            new RLVAttachment {Name = @"mouth", AttachmentPoint = AttachmentPoint.Mouth},
            new RLVAttachment {Name = @"chin", AttachmentPoint = AttachmentPoint.Chin},
            new RLVAttachment {Name = @"left ear", AttachmentPoint = AttachmentPoint.LeftEar},
            new RLVAttachment {Name = @"right ear", AttachmentPoint = AttachmentPoint.RightEar},
            new RLVAttachment {Name = @"left eyeball", AttachmentPoint = AttachmentPoint.LeftEyeball},
            new RLVAttachment {Name = @"right eyeball", AttachmentPoint = AttachmentPoint.RightEyeball},
            new RLVAttachment {Name = @"nose", AttachmentPoint = AttachmentPoint.Nose},
            new RLVAttachment {Name = @"r upper arm", AttachmentPoint = AttachmentPoint.RightUpperArm},
            new RLVAttachment {Name = @"r forearm", AttachmentPoint = AttachmentPoint.RightForearm},
            new RLVAttachment {Name = @"l upper arm", AttachmentPoint = AttachmentPoint.LeftUpperArm},
            new RLVAttachment {Name = @"l forearm", AttachmentPoint = AttachmentPoint.LeftForearm},
            new RLVAttachment {Name = @"right hip", AttachmentPoint = AttachmentPoint.RightHip},
            new RLVAttachment {Name = @"r upper leg", AttachmentPoint = AttachmentPoint.RightUpperLeg},
            new RLVAttachment {Name = @"r lower leg", AttachmentPoint = AttachmentPoint.RightLowerLeg},
            new RLVAttachment {Name = @"left hip", AttachmentPoint = AttachmentPoint.LeftHip},
            new RLVAttachment {Name = @"l upper leg", AttachmentPoint = AttachmentPoint.LeftUpperLeg},
            new RLVAttachment {Name = @"l lower leg", AttachmentPoint = AttachmentPoint.LeftLowerLeg},
            new RLVAttachment {Name = @"stomach", AttachmentPoint = AttachmentPoint.Stomach},
            new RLVAttachment {Name = @"left pec", AttachmentPoint = AttachmentPoint.LeftPec},
            new RLVAttachment {Name = @"right pec", AttachmentPoint = AttachmentPoint.RightPec},
            new RLVAttachment {Name = @"center 2", AttachmentPoint = AttachmentPoint.HUDCenter2},
            new RLVAttachment {Name = @"top right", AttachmentPoint = AttachmentPoint.HUDTopRight},
            new RLVAttachment {Name = @"top", AttachmentPoint = AttachmentPoint.HUDTop},
            new RLVAttachment {Name = @"top left", AttachmentPoint = AttachmentPoint.HUDTopLeft},
            new RLVAttachment {Name = @"center", AttachmentPoint = AttachmentPoint.HUDCenter},
            new RLVAttachment {Name = @"bottom left", AttachmentPoint = AttachmentPoint.HUDBottomLeft},
            new RLVAttachment {Name = @"bottom", AttachmentPoint = AttachmentPoint.HUDBottom},
            new RLVAttachment {Name = @"bottom right", AttachmentPoint = AttachmentPoint.HUDBottomRight},
            new RLVAttachment {Name = @"neck", AttachmentPoint = AttachmentPoint.Neck},
            new RLVAttachment {Name = @"root", AttachmentPoint = AttachmentPoint.Root}
        };

        /// <summary>
        ///     RLV attachment structure.
        /// </summary>
        private struct RLVAttachment
        {
            public AttachmentPoint AttachmentPoint;
            public string Name;
        }

        /// <summary>
        ///     Enumeration for supported RLV commands.
        /// </summary>
        private enum RLVBehaviour : uint
        {
            [Description("none")] NONE = 0,
            [IsRLVBehaviour(true)] [RLVBehaviour("version")] [Description("version")] VERSION,
            [IsRLVBehaviour(true)] [RLVBehaviour("versionnew")] [Description("versionnew")] VERSIONNEW,
            [IsRLVBehaviour(true)] [RLVBehaviour("versionnum")] [Description("versionnum")] VERSIONNUM,
            [IsRLVBehaviour(true)] [RLVBehaviour("getgroup")] [Description("getgroup")] GETGROUP,
            [IsRLVBehaviour(true)] [RLVBehaviour("setgroup")] [Description("setgroup")] SETGROUP,
            [IsRLVBehaviour(true)] [RLVBehaviour("getsitid")] [Description("getsitid")] GETSITID,
            [IsRLVBehaviour(true)] [RLVBehaviour("getstatusall")] [Description("getstatusall")] GETSTATUSALL,
            [IsRLVBehaviour(true)] [RLVBehaviour("getstatus")] [Description("getstatus")] GETSTATUS,
            [IsRLVBehaviour(true)] [RLVBehaviour("sit")] [Description("sit")] SIT,
            [IsRLVBehaviour(true)] [RLVBehaviour("unsit")] [Description("unsit")] UNSIT,
            [IsRLVBehaviour(true)] [RLVBehaviour("setrot")] [Description("setrot")] SETROT,
            [IsRLVBehaviour(true)] [RLVBehaviour("tpto")] [Description("tpto")] TPTO,
            [IsRLVBehaviour(true)] [RLVBehaviour("getoutfit")] [Description("getoutfit")] GETOUTFIT,
            [IsRLVBehaviour(true)] [RLVBehaviour("getattach")] [Description("getattach")] GETATTACH,
            [IsRLVBehaviour(true)] [RLVBehaviour("remattach")] [Description("remattach")] REMATTACH,
            [IsRLVBehaviour(true)] [RLVBehaviour("detach")] [Description("detach")] DETACH,
            [IsRLVBehaviour(true)] [RLVBehaviour("detachme")] [Description("detachme")] DETACHME,
            [IsRLVBehaviour(true)] [RLVBehaviour("remoutfit")] [Description("remoutfit")] REMOUTFIT,
            [IsRLVBehaviour(true)] [RLVBehaviour("attach")] [Description("attach")] ATTACH,
            [IsRLVBehaviour(true)] [RLVBehaviour("attachoverorreplace")] [Description("attachoverorreplace")] ATTACHOVERORREPLACE,
            [IsRLVBehaviour(true)] [RLVBehaviour("attachover")] [Description("attachover")] ATTACHOVER,
            [IsRLVBehaviour(true)] [RLVBehaviour("getinv")] [Description("getinv")] GETINV,
            [IsRLVBehaviour(true)] [RLVBehaviour("getinvworn")] [Description("getinvworn")] GETINVWORN,
            [IsRLVBehaviour(true)] [RLVBehaviour("getpath")] [Description("getpath")] GETPATH,
            [IsRLVBehaviour(true)] [RLVBehaviour("getpathnew")] [Description("getpathnew")] GETPATHNEW,
            [IsRLVBehaviour(true)] [RLVBehaviour("findfolder")] [Description("findfolder")] FINDFOLDER,
            [IsRLVBehaviour(true)] [RLVBehaviour("clear")] [Description("clear")] CLEAR,
            [Description("accepttp")] ACCEPTTP,
            [Description("acceptpermission")] ACCEPTPERMISSION
        }

        public struct RLVRule
        {
            public string Behaviour;
            public UUID ObjectUUID;
            public string Option;
            public string Param;
        }

        /// <summary>
        ///     RLV wearable structure.
        /// </summary>
        private struct RLVWearable
        {
            public string Name;
            public WearableType WearableType;
        }

        /// <summary>
        ///     Structure for RLV constants.
        /// </summary>
        private struct RLV_CONSTANTS
        {
            public const string COMMAND_OPERATOR = @"@";
            public const string VIEWER = @"RestrainedLife viewer";
            public const string SHORT_VERSION = @"1.23";
            public const string LONG_VERSION = @"1230100";
            public const string FORCE = @"force";
            public const string FALSE_MARKER = @"0";
            public const string TRUE_MARKER = @"1";
            public const string CSV_DELIMITER = @",";
            public const string DOT_MARKER = @".";
            public const string TILDE_MARKER = @"~";
            public const string PROPORTION_SEPARATOR = @"|";
            public const string SHARED_FOLDER_NAME = @"#RLV";
            public const string AND_OPERATOR = @"&&";
            public const string PATH_SEPARATOR = @"/";
            public const string Y = @"y";
            public const string ADD = @"add";
            public const string N = @"n";
            public const string REM = @"rem";
            public const string STATUS_SEPARATOR = @";";

            /// <summary>
            ///     Regex used to match RLV commands.
            /// </summary>
            public static readonly Regex RLVRegEx = new Regex(@"(?<behaviour>[^:=]+)(:(?<option>[^=]*))?=(?<param>\w+)",
                RegexOptions.Compiled);
        }

        #endregion
    }

    public class NativeMethods
    {
        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        /// <summary>
        ///     Import console handler for windows.
        /// </summary>
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool SetConsoleCtrlHandler(Corrade.EventHandler handler,
            [MarshalAs(UnmanagedType.U1)] bool add);
    }
}