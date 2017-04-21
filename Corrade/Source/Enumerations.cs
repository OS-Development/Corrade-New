///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using wasSharp;

namespace Corrade
{
    public class Enumerations
    {
        /// <summary>
        ///     Possible actions.
        /// </summary>
        public enum Action : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [Reflection.NameAttribute("get")]
            GET,

            [Reflection.NameAttribute("set")]
            SET,

            [Reflection.NameAttribute("add")]
            ADD,

            [Reflection.NameAttribute("remove")]
            REMOVE,

            [Reflection.NameAttribute("start")]
            START,

            [Reflection.NameAttribute("stop")]
            STOP,

            [Reflection.NameAttribute("mute")]
            MUTE,

            [Reflection.NameAttribute("unmute")]
            UNMUTE,

            [Reflection.NameAttribute("restart")]
            RESTART,

            [Reflection.NameAttribute("cancel")]
            CANCEL,

            [Reflection.NameAttribute("accept")]
            ACCEPT,

            [Reflection.NameAttribute("decline")]
            DECLINE,

            [Reflection.NameAttribute("online")]
            ONLINE,

            [Reflection.NameAttribute("offline")]
            OFFLINE,

            [Reflection.NameAttribute("request")]
            REQUEST,

            [Reflection.NameAttribute("response")]
            RESPONSE,

            [Reflection.NameAttribute("delete")]
            DELETE,

            [Reflection.NameAttribute("take")]
            TAKE,

            [Reflection.NameAttribute("read")]
            READ,

            [Reflection.NameAttribute("wrtie")]
            WRITE,

            [Reflection.NameAttribute("purge")]
            PURGE,

            [Reflection.NameAttribute("crossed")]
            CROSSED,

            [Reflection.NameAttribute("changed")]
            CHANGED,

            [Reflection.NameAttribute("reply")]
            REPLY,

            [Reflection.NameAttribute("offer")]
            OFFER,

            [Reflection.NameAttribute("generic")]
            GENERIC,

            [Reflection.NameAttribute("point")]
            POINT,

            [Reflection.NameAttribute("look")]
            LOOK,

            [Reflection.NameAttribute("update")]
            UPDATE,

            [Reflection.NameAttribute("received")]
            RECEIVED,

            [Reflection.NameAttribute("joined")]
            JOINED,

            [Reflection.NameAttribute("parted")]
            PARTED,

            [Reflection.NameAttribute("save")]
            SAVE,

            [Reflection.NameAttribute("load")]
            LOAD,

            [Reflection.NameAttribute("enable")]
            ENABLE,

            [Reflection.NameAttribute("disable")]
            DISABLE,

            [Reflection.NameAttribute("process")]
            PROCESS,

            [Reflection.NameAttribute("rebuild")]
            REBUILD,

            [Reflection.NameAttribute("clear")]
            CLEAR,

            [Reflection.NameAttribute("ls")]
            LS,

            [Reflection.NameAttribute("cwd")]
            CWD,

            [Reflection.NameAttribute("cd")]
            CD,

            [Reflection.NameAttribute("mkdir")]
            MKDIR,

            [Reflection.NameAttribute("chmod")]
            CHMOD,

            [Reflection.NameAttribute("rm")]
            RM,

            [Reflection.NameAttribute("ln")]
            LN,

            [Reflection.NameAttribute("mv")]
            MV,

            [Reflection.NameAttribute("cp")]
            CP,

            [Reflection.NameAttribute("appear")]
            APPEAR,

            [Reflection.NameAttribute("vanish")]
            VANISH,

            [Reflection.NameAttribute("list")]
            LIST,

            [Reflection.NameAttribute("link")]
            LINK,

            [Reflection.NameAttribute("delink")]
            DELINK,

            [Reflection.NameAttribute("ban")]
            BAN,

            [Reflection.NameAttribute("unban")]
            UNBAN,

            [Reflection.NameAttribute("send")]
            SEND,

            [Reflection.NameAttribute("search")]
            SEARCH,

            [Reflection.NameAttribute("attach")]
            ATTACH,

            [Reflection.NameAttribute("detach")]
            DETACH,

            [Reflection.NameAttribute("wear")]
            WEAR,

            [Reflection.NameAttribute("unwear")]
            UNWEAR,

            [Reflection.NameAttribute("post")]
            POST,

            [Reflection.NameAttribute("tweet")]
            TWEET,

            [Reflection.NameAttribute("detect")]
            DETECT,

            [Reflection.NameAttribute("ignore")]
            IGNORE,

            [Reflection.NameAttribute("revoke")]
            REVOKE,

            [Reflection.NameAttribute("reject")]
            REJECT,

            [Reflection.NameAttribute("propose")]
            PROPOSE,

            [Reflection.NameAttribute("append")]
            APPEND,

            [Reflection.NameAttribute("create")]
            CREATE,

            [Reflection.NameAttribute("detail")]
            DETAIL,

            [Reflection.NameAttribute("import")]
            IMPORT,

            [Reflection.NameAttribute("export")]
            EXPORT,

            [Reflection.NameAttribute("train")]
            TRAIN,

            [Reflection.NameAttribute("classify")]
            CLASSIFY,

            [Reflection.NameAttribute("merge")]
            MERGE,

            [Reflection.NameAttribute("untrain")]
            UNTRAIN,

            [Reflection.NameAttribute("rename")]
            RENAME,

            [Reflection.NameAttribute("schedule")]
            SCHEDULE
        }

        /// <summary>
        ///     Structure containing error messages printed on console for the owner.
        /// </summary>
        public enum ConsoleMessage
        {
            [Reflection.DescriptionAttribute("none")]
            NONE = 0,

            [Reflection.DescriptionAttribute("access denied")]
            ACCESS_DENIED,

            [Reflection.DescriptionAttribute(
                "the Terms of Service (TOS) for the grid you are connecting to have not been accepted, please check your configuration file"
                )]
            TOS_NOT_ACCEPTED,

            [Reflection.DescriptionAttribute("teleport failed")]
            TELEPORT_FAILED,

            [Reflection.DescriptionAttribute("teleport succeeded")]
            TELEPORT_SUCCEEDED,

            [Reflection.DescriptionAttribute("accepted friendship")]
            ACCEPTED_FRIENDSHIP,

            [Reflection.DescriptionAttribute("login failed")]
            LOGIN_FAILED,

            [Reflection.DescriptionAttribute("login succeeded")]
            LOGIN_SUCCEEDED,

            [Reflection.DescriptionAttribute("failed to set appearance")]
            APPEARANCE_SET_FAILED,

            [Reflection.DescriptionAttribute("appearance set")]
            APPEARANCE_SET_SUCCEEDED,

            [Reflection.DescriptionAttribute("all simulators disconnected")]
            ALL_SIMULATORS_DISCONNECTED,

            [Reflection.DescriptionAttribute("simulator connected")]
            SIMULATOR_CONNECTED,

            [Reflection.DescriptionAttribute("event queue started")]
            EVENT_QUEUE_STARTED,

            [Reflection.DescriptionAttribute("disconnected")]
            DISCONNECTED,

            [Reflection.DescriptionAttribute("logging out")]
            LOGGING_OUT,

            [Reflection.DescriptionAttribute("logging in")]
            LOGGING_IN,

            [Reflection.DescriptionAttribute("agent not found")]
            AGENT_NOT_FOUND,

            [Reflection.DescriptionAttribute("reading Corrade configuration")]
            READING_CORRADE_CONFIGURATION,

            [Reflection.DescriptionAttribute("read Corrade configuration")]
            READ_CORRADE_CONFIGURATION,

            [Reflection.DescriptionAttribute("configuration file modified")]
            CONFIGURATION_FILE_MODIFIED,

            [Reflection.DescriptionAttribute("HTTP server error")]
            HTTP_SERVER_ERROR,

            [Reflection.DescriptionAttribute("HTTP server not supported")]
            HTTP_SERVER_NOT_SUPPORTED,

            [Reflection.DescriptionAttribute("starting HTTP server")]
            STARTING_HTTP_SERVER,

            [Reflection.DescriptionAttribute("stopping HTTP server")]
            STOPPING_HTTP_SERVER,

            [Reflection.DescriptionAttribute("HTTP server processing aborted")]
            HTTP_SERVER_PROCESSING_ABORTED,

            [Reflection.DescriptionAttribute("timeout logging out")]
            TIMEOUT_LOGGING_OUT,

            [Reflection.DescriptionAttribute("callback error")]
            CALLBACK_ERROR,

            [Reflection.DescriptionAttribute("notification error")]
            NOTIFICATION_ERROR,

            [Reflection.DescriptionAttribute("inventory cache items loaded")]
            INVENTORY_CACHE_ITEMS_LOADED,

            [Reflection.DescriptionAttribute("inventory cache items saved")]
            INVENTORY_CACHE_ITEMS_SAVED,

            [Reflection.DescriptionAttribute("unable to load Corrade cache")]
            UNABLE_TO_LOAD_CORRADE_CACHE,

            [Reflection.DescriptionAttribute("unable to save Corrade cache")]
            UNABLE_TO_SAVE_CORRADE_CACHE,

            [Reflection.DescriptionAttribute("failed to manifest RLV behaviour")]
            FAILED_TO_MANIFEST_RLV_BEHAVIOUR,

            [Reflection.DescriptionAttribute("behaviour not implemented")]
            BEHAVIOUR_NOT_IMPLEMENTED,

            [Reflection.DescriptionAttribute("workers exceeded")]
            WORKERS_EXCEEDED,

            [Reflection.DescriptionAttribute("SIML bot configuration modified")]
            SIML_CONFIGURATION_MODIFIED,

            [Reflection.DescriptionAttribute("read SIML bot configuration")]
            READ_SIML_BOT_CONFIGURATION,

            [Reflection.DescriptionAttribute("reading SIML bot configuration")]
            READING_SIML_BOT_CONFIGURATION,

            [Reflection.DescriptionAttribute("wrote SIML bot configuration")]
            WROTE_SIML_BOT_CONFIGURATION,

            [Reflection.DescriptionAttribute("writing SIML bot configuration")]
            WRITING_SIML_BOT_CONFIGURATION,

            [Reflection.DescriptionAttribute("error loading SIML bot files")]
            ERROR_LOADING_SIML_BOT_FILES,

            [Reflection.DescriptionAttribute("error saving SIML bot files")]
            ERROR_SAVING_SIML_BOT_FILES,

            [Reflection.DescriptionAttribute("could not write to client log file")]
            COULD_NOT_WRITE_TO_CLIENT_LOG_FILE,

            [Reflection.DescriptionAttribute("could not write to group chat log file")]
            COULD_NOT_WRITE_TO_GROUP_CHAT_LOG_FILE,

            [Reflection.DescriptionAttribute("could not write to instant message log file")]
            COULD_NOT_WRITE_TO_INSTANT_MESSAGE_LOG_FILE,

            [Reflection.DescriptionAttribute("could not write to local message log file")]
            COULD_NOT_WRITE_TO_LOCAL_MESSAGE_LOG_FILE,

            [Reflection.DescriptionAttribute("could not read from local message log file")]
            COULD_NOT_READ_FROM_LOCAL_MESSAGE_LOG_FILE,

            [Reflection.DescriptionAttribute("could not write to region message log file")]
            COULD_NOT_WRITE_TO_REGION_MESSAGE_LOG_FILE,

            [Reflection.DescriptionAttribute("unknown IP address")]
            UNKNOWN_IP_ADDRESS,

            [Reflection.DescriptionAttribute("unable to save Corrade notifications state")]
            UNABLE_TO_SAVE_CORRADE_NOTIFICATIONS_STATE,

            [Reflection.DescriptionAttribute("unable to load Corrade notifications state")]
            UNABLE_TO_LOAD_CORRADE_NOTIFICATIONS_STATE,

            [Reflection.DescriptionAttribute("unknwon notification type")]
            UNKNOWN_NOTIFICATION_TYPE,

            [Reflection.DescriptionAttribute("teleport throttled")]
            TELEPORT_THROTTLED,

            [Reflection.DescriptionAttribute("uncaught exception for thread")]
            UNCAUGHT_EXCEPTION_FOR_THREAD,

            [Reflection.DescriptionAttribute("error setting up configuration watcher")]
            ERROR_SETTING_UP_CONFIGURATION_WATCHER,

            [Reflection.DescriptionAttribute("error setting up SIML configuration watcher")]
            ERROR_SETTING_UP_SIML_CONFIGURATION_WATCHER,

            [Reflection.DescriptionAttribute("callback throttled")]
            CALLBACK_THROTTLED,

            [Reflection.DescriptionAttribute("notification throttled")]
            NOTIFICATION_THROTTLED,

            [Reflection.DescriptionAttribute("error updating inventory")]
            ERROR_UPDATING_INVENTORY,

            [Reflection.DescriptionAttribute("unable to load group members state")]
            UNABLE_TO_LOAD_GROUP_MEMBERS_STATE,

            [Reflection.DescriptionAttribute("unable to save group members state")]
            UNABLE_TO_SAVE_GROUP_MEMBERS_STATE,

            [Reflection.DescriptionAttribute("error making POST request")]
            ERROR_MAKING_POST_REQUEST,

            [Reflection.DescriptionAttribute("notifications file modified")]
            NOTIFICATIONS_FILE_MODIFIED,

            [Reflection.DescriptionAttribute("unable to load Corrade configuration")]
            UNABLE_TO_LOAD_CORRADE_CONFIGURATION,

            [Reflection.DescriptionAttribute("unable to save Corrade configuration")]
            UNABLE_TO_SAVE_CORRADE_CONFIGURATION,

            [Reflection.DescriptionAttribute("unable to load Corrade group schedules state")]
            UNABLE_TO_LOAD_CORRADE_GROUP_SCHEDULES_STATE,

            [Reflection.DescriptionAttribute("unable to save Corrade group schedules state")]
            UNABLE_TO_SAVE_CORRADE_GROUP_SCHEDULES_STATE,

            [Reflection.DescriptionAttribute("group schedules file modified")]
            GROUP_SCHEDULES_FILE_MODIFIED,

            [Reflection.DescriptionAttribute("error setting up notifications watcher")]
            ERROR_SETTING_UP_NOTIFICATIONS_WATCHER,

            [Reflection.DescriptionAttribute("error setting up schedules watcher")]
            ERROR_SETTING_UP_SCHEDULES_WATCHER,

            [Reflection.DescriptionAttribute("unable to load Corrade movement state")]
            UNABLE_TO_LOAD_CORRADE_MOVEMENT_STATE,

            [Reflection.DescriptionAttribute("unable to save Corrade movement state")]
            UNABLE_TO_SAVE_CORRADE_MOVEMENT_STATE,

            [Reflection.DescriptionAttribute("TCP notifications server error")]
            TCP_NOTIFICATIONS_SERVER_ERROR,

            [Reflection.DescriptionAttribute("stopping TCP notifications server")]
            STOPPING_TCP_NOTIFICATIONS_SERVER,

            [Reflection.DescriptionAttribute("starting TCP notifications server")]
            STARTING_TCP_NOTIFICATIONS_SERVER,

            [Reflection.DescriptionAttribute("TCP notification throttled")]
            TCP_NOTIFICATION_THROTTLED,

            [Reflection.DescriptionAttribute("unknown group")]
            UNKNOWN_GROUP,

            [Reflection.DescriptionAttribute("group feeds file modified")]
            GROUP_FEEDS_FILE_MODIFIED,

            [Reflection.DescriptionAttribute("unable to save Corrade feeds state")]
            UNABLE_TO_SAVE_CORRADE_FEEDS_STATE,

            [Reflection.DescriptionAttribute("unable to load Corrade feeds state")]
            UNABLE_TO_LOAD_CORRADE_FEEDS_STATE,

            [Reflection.DescriptionAttribute("error setting up feeds watcher")]
            ERROR_SETTING_UP_FEEDS_WATCHER,

            [Reflection.DescriptionAttribute("error loading feed")]
            ERROR_LOADING_FEED,

            [Reflection.DescriptionAttribute("error saving SIML bot learning file")]
            ERROR_SAVING_SIML_BOT_LEARNING_FILE,

            [Reflection.DescriptionAttribute("error saving SIML bot memorizing file")]
            ERROR_SAVING_SIML_BOT_MEMORIZING_FILE,

            [Reflection.DescriptionAttribute("error loading language detection")]
            ERROR_LOADING_LANGUAGE_DETECTION,

            [Reflection.DescriptionAttribute("updating Corrade configuration")]
            UPDATING_CORRADE_CONFIGURATION,

            [Reflection.DescriptionAttribute("Corrade configuration updated")]
            CORRADE_CONFIGURATION_UPDATED,

            [Reflection.DescriptionAttribute("Connecting to login server")]
            CONNECTING_TO_LOGIN_SERVER,

            [Reflection.DescriptionAttribute("Redirecting")]
            REDIRECTING,

            [Reflection.DescriptionAttribute("Connecting to simulator")]
            CONNECTING_TO_SIMULATOR,

            [Reflection.DescriptionAttribute("Reading response")]
            READING_RESPONSE,

            [Reflection.DescriptionAttribute("unable to load group cookies state")]
            UNABLE_TO_LOAD_GROUP_COOKIES_STATE,

            [Reflection.DescriptionAttribute("unable to save group cookies state")]
            UNABLE_TO_SAVE_GROUP_COOKIES_STATE,

            [Reflection.DescriptionAttribute("could not write to conference message log file")]
            COULD_NOT_WRITE_TO_CONFERENCE_MESSAGE_LOG_FILE,

            [Reflection.DescriptionAttribute("unable to load conference state")]
            UNABLE_TO_LOAD_CONFERENCE_STATE,

            [Reflection.DescriptionAttribute("unable to save conference state")]
            UNABLE_TO_SAVE_CONFERENCE_STATE,

            [Reflection.DescriptionAttribute("unable to restore conference")]
            UNABLE_TO_RESTORE_CONFERENCE,

            [Reflection.DescriptionAttribute("unable to store peer cache entity")]
            UNABLE_TO_STORE_PEER_CACHE_ENTITY,

            [Reflection.DescriptionAttribute("unable to distribute resource")]
            UNABLE_TO_DISTRIBUTE_RESOURCE,

            [Reflection.DescriptionAttribute("peer synchronization successful")]
            PEER_SYNCHRONIZATION_SUCCESSFUL,

            [Reflection.DescriptionAttribute("peer attempting synchronization")]
            PEER_ATTEMPTING_SYNCHRONIZATION,

            [Reflection.DescriptionAttribute("unable to read distributed resource")]
            UNABLE_TO_READ_DISTRIBUTED_RESOURCE,

            [Reflection.DescriptionAttribute("configuration file mismatch")]
            CONFIGURATION_FILE_VERSION_MISMATCH,

            [Reflection.DescriptionAttribute("unable to save group soft ban state")]
            UNABLE_TO_SAVE_GROUP_SOFT_BAN_STATE,

            [Reflection.DescriptionAttribute("unable to load group soft ban state")]
            UNABLE_TO_LOAD_GROUP_SOFT_BAN_STATE,

            [Reflection.DescriptionAttribute("group soft bans file modified")]
            GROUP_SOFT_BANS_FILE_MODIFIED,

            [Reflection.DescriptionAttribute("error setting up soft bans watcher")]
            ERROR_SETTING_UP_SOFT_BANS_WATCHER,

            [Reflection.DescriptionAttribute("unable to apply soft ban")]
            UNABLE_TO_APPLY_SOFT_BAN,

            [Reflection.DescriptionAttribute("unable to lift hard soft ban")]
            UNABLE_TO_LIFT_HARD_SOFT_BAN,

            [Reflection.DescriptionAttribute("could not find notification file")]
            COULD_NOT_FIND_NOTIFICATION_FILE,

            [Reflection.DescriptionAttribute("unable to deserialize notification data")]
            UNABLE_TO_DESERIALIZE_NOTIFICATION_DATA,

            [Reflection.DescriptionAttribute("parameters for requested event not found")]
            PARAMETERS_FOR_REQUESTED_EVENT_NOT_FOUND,

            [Reflection.DescriptionAttribute("timeout preloading downloading preload sound")]
            TIMEOUT_DOWNLOADING_PRELOAD_SOUND,

            [Reflection.DescriptionAttribute("unable to install service")]
            UNABLE_TO_INSTALL_SERVICE,

            [Reflection.DescriptionAttribute("unable to uninstall service")]
            UNABLE_TO_UNINSTALL_SERVICE,

            [Reflection.DescriptionAttribute("unable to save group Bayes data")]
            UNABLE_TO_SAVE_GROUP_BAYES_DATA,

            [Reflection.DescriptionAttribute("unable to load group Bayes data")]
            UNABLE_TO_LOAD_GROUP_BAYES_DATA,

            [Reflection.DescriptionAttribute("start locations exhausted")]
            START_LOCATIONS_EXHAUSTED,

            [Reflection.DescriptionAttribute("cycling simulators")]
            CYCLING_SIMULATORS,

            [Reflection.DescriptionAttribute("no start locations found")]
            NO_START_LOCATIONS_FOUND,

            [Reflection.DescriptionAttribute("simulator disconnected")]
            SIMULATOR_DISCONNECTED,

            [Reflection.DescriptionAttribute("starting Nucleus server")]
            STARTING_NUCLEUS_SERVER,

            [Reflection.DescriptionAttribute("stopping Nucleus server")]
            STOPPING_NUCLEUS_SERVER,

            [Reflection.DescriptionAttribute("Nucleus server error")]
            NUCLEUS_SERVER_ERROR,

            [Reflection.DescriptionAttribute("Nucleus processing aborted")]
            NUCLEUS_PROCESSING_ABORTED,

            [Reflection.DescriptionAttribute("HTTP server command error")]
            HTTP_SERVER_COMMAND_ERROR,

            [Reflection.DescriptionAttribute("HTTP server command error")]
            HTTP_SERVER_SYNCHRONIZATION_ERROR,

            [Reflection.DescriptionAttribute("Nucleus compile failed")]
            NUCLEUS_COMPILE_FAILED,

            [Reflection.DescriptionAttribute("unable to write Openmetaverse log")]
            UNABLE_TO_WRITE_TO_OPENMETAVERSE_LOG,

            [Reflection.DescriptionAttribute("unable to store last execution state")]
            UNABLE_TO_STORE_LAST_EXECUTION_STATE,

            [Reflection.DescriptionAttribute("unable to retrieve last execution state")]
            UNABLE_TO_RETRIEVE_LAST_EXECUTION_STATE,

            [Reflection.DescriptionAttribute("scripted agent status")]
            SCRIPTED_AGENT_STATUS,

            [Reflection.DescriptionAttribute("registered as scripted agent")]
            REGISTERED_AS_SCRIPTED_AGENT,

            [Reflection.DescriptionAttribute("unregistered as scripted agent")]
            UNREGISTERED_AS_SCRIPTED_AGENT,

            [Reflection.DescriptionAttribute("unregistered to retrieve last scripted agent status state")]
            UNABLE_TO_RETRIEVE_LAST_SCRIPTED_AGENT_STATUS_STATE,

            [Reflection.DescriptionAttribute("unable to store last scripted agent status state")]
            UNABLE_TO_STORE_LAST_SCRIPTED_AGENT_STATUS_STATE
        }

        /// <summary>
        ///     Directions in 3D cartesian.
        /// </summary>
        public enum Direction : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [Reflection.NameAttribute("back")]
            BACK,

            [Reflection.NameAttribute("forward")]
            FORWARD,

            [Reflection.NameAttribute("left")]
            LEFT,

            [Reflection.NameAttribute("right")]
            RIGHT,

            [Reflection.NameAttribute("up")]
            UP,

            [Reflection.NameAttribute("down")]
            DOWN
        }

        /// <summary>
        ///     Holds item types with the wearable inventory item type expanded to wearable types.
        /// </summary>
        public enum DirItemType : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [Reflection.NameAttribute("texture")]
            TEXTURE,

            [Reflection.NameAttribute("sound")]
            SOUND,

            [Reflection.NameAttribute("callingcard")]
            CALLINGCARD,

            [Reflection.NameAttribute("landmark")]
            LANDMARK,

            [Reflection.NameAttribute("object")]
            OBJECT,

            [Reflection.NameAttribute("notecard")]
            NOTECARD,

            [Reflection.NameAttribute("category")]
            CATEGORY,

            [Reflection.NameAttribute("LSL")]
            LSL,

            [Reflection.NameAttribute("snapshot")]
            SNAPSHOT,

            [Reflection.NameAttribute("attachment")]
            ATTACHMENT,

            [Reflection.NameAttribute("animation")]
            ANIMATION,

            [Reflection.NameAttribute("gesture")]
            GESTURE,

            [Reflection.NameAttribute("folder")]
            FOLDER,

            [Reflection.NameAttribute("shape")]
            SHAPE,

            [Reflection.NameAttribute("skin")]
            SKIN,

            [Reflection.NameAttribute("hair")]
            HAIR,

            [Reflection.NameAttribute("eyes")]
            EYES,

            [Reflection.NameAttribute("shirt")]
            SHIRT,

            [Reflection.NameAttribute("pants")]
            PANTS,

            [Reflection.NameAttribute("shoes")]
            SHOES,

            [Reflection.NameAttribute("socks")]
            SOCKS,

            [Reflection.NameAttribute("jacket")]
            JACKET,

            [Reflection.NameAttribute("gloves")]
            GLOVES,

            [Reflection.NameAttribute("undershirt")]
            UNDERSHIRT,

            [Reflection.NameAttribute("underpants")]
            UNDERPANTS,

            [Reflection.NameAttribute("skirt")]
            SKIRT,

            [Reflection.NameAttribute("tattoo")]
            TATTOO,

            [Reflection.NameAttribute("alpha")]
            ALPHA,

            [Reflection.NameAttribute("physics")]
            PHYSICS
        }

        /// <summary>
        ///     Possible entities.
        /// </summary>
        public enum Entity : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [Reflection.NameAttribute("avatar")]
            AVATAR,

            [Reflection.NameAttribute("local")]
            LOCAL,

            [Reflection.NameAttribute("group")]
            GROUP,

            [Reflection.NameAttribute("estate")]
            ESTATE,

            [Reflection.NameAttribute("region")]
            REGION,

            [Reflection.NameAttribute("object")]
            OBJECT,

            [Reflection.NameAttribute("parcel")]
            PARCEL,

            [Reflection.NameAttribute("range")]
            RANGE,

            [Reflection.NameAttribute("syntax")]
            SYNTAX,

            [Reflection.NameAttribute("permission")]
            PERMISSION,

            [Reflection.NameAttribute("description")]
            DESCRIPTION,

            [Reflection.NameAttribute("message")]
            MESSAGE,

            [Reflection.NameAttribute("world")]
            WORLD,

            [Reflection.NameAttribute("statistics")]
            STATISTICS,

            [Reflection.NameAttribute("lindex")]
            LINDEX,

            [Reflection.NameAttribute("conference")]
            CONFERENCE,

            [Reflection.NameAttribute("mute")]
            MUTE,

            [Reflection.NameAttribute("global")]
            GLOBAL,

            [Reflection.NameAttribute("landmark")]
            LANDMARK,

            [Reflection.NameAttribute("file")]
            FILE,

            [Reflection.NameAttribute("text")]
            TEXT,

            [Reflection.NameAttribute("URL")]
            URL,

            [Reflection.NameAttribute("authentication")]
            AUTHENTICATION
        }

        /// <summary>
        ///     Structure containing errors returned to scripts.
        /// </summary>
        /// <remarks>
        ///     Status is generated by:
        ///     1.) jot -r 900 0 65535 | uniq | xargs printf "%05d\n" | pbcopy
        ///     2.) paste codes.txt status.txt | awk -F"\n" '{print $1,$2}' | pbcopy
        ///     Removals: 43508 - could not get land users
        ///     Removals: 42240 - no notification provided
        ///     Removals: 56462 - texture not found
        ///     Removals: 57429 - timeout waiting for estate list
        ///     Removals: 14951 - timeout joining group chat
        ///     Removals: 36716 - timeout retrieving item
        ///     Removals: 11869 - timeout getting primitive data
        ///     Removals: 20238 - parcel must be owned
        ///     Removals: 22961 - could not add mute entry
        ///     Removals: 12181 - no database key specified
        ///     Removals: 44994 - no database value specified
        ///     Removals: 19142 - unknown database action
        ///     Removals: 24951 - feature only available in secondlife
        /// </remarks>
        public enum ScriptError : uint
        {
            [Command.StatusAttribute(0)]
            [Reflection.DescriptionAttribute("none")]
            NONE = 0,

            [Command.StatusAttribute(35392)]
            [Reflection.DescriptionAttribute("could not join group")]
            COULD_NOT_JOIN_GROUP,

            [Command.StatusAttribute(20900)]
            [Reflection.DescriptionAttribute("could not leave group")]
            COULD_NOT_LEAVE_GROUP,

            [Command.StatusAttribute(57961)]
            [Reflection.DescriptionAttribute("agent not found")]
            AGENT_NOT_FOUND,

            [Command.StatusAttribute(28002)]
            [Reflection.DescriptionAttribute("group not found")]
            GROUP_NOT_FOUND,

            [Command.StatusAttribute(15345)]
            [Reflection.DescriptionAttribute("already in group")]
            ALREADY_IN_GROUP,

            [Command.StatusAttribute(11502)]
            [Reflection.DescriptionAttribute("not in group")]
            NOT_IN_GROUP,

            [Command.StatusAttribute(32472)]
            [Reflection.DescriptionAttribute("role not found")]
            ROLE_NOT_FOUND,

            [Command.StatusAttribute(08653)]
            [Reflection.DescriptionAttribute("command not found")]
            COMMAND_NOT_FOUND,

            [Command.StatusAttribute(14634)]
            [Reflection.DescriptionAttribute("could not eject agent")]
            COULD_NOT_EJECT_AGENT,

            [Command.StatusAttribute(30473)]
            [Reflection.DescriptionAttribute("no group power for command")]
            NO_GROUP_POWER_FOR_COMMAND,

            [Command.StatusAttribute(27605)]
            [Reflection.DescriptionAttribute("cannot eject owners")]
            CANNOT_EJECT_OWNERS,

            [Command.StatusAttribute(25984)]
            [Reflection.DescriptionAttribute("inventory item not found")]
            INVENTORY_ITEM_NOT_FOUND,

            [Command.StatusAttribute(43982)]
            [Reflection.DescriptionAttribute("invalid amount")]
            INVALID_AMOUNT,

            [Command.StatusAttribute(02169)]
            [Reflection.DescriptionAttribute("insufficient funds")]
            INSUFFICIENT_FUNDS,

            [Command.StatusAttribute(47624)]
            [Reflection.DescriptionAttribute("invalid pay target")]
            INVALID_PAY_TARGET,

            [Command.StatusAttribute(32164)]
            [Reflection.DescriptionAttribute("teleport failed")]
            TELEPORT_FAILED,

            [Command.StatusAttribute(22693)]
            [Reflection.DescriptionAttribute("primitive not found")]
            PRIMITIVE_NOT_FOUND,

            [Command.StatusAttribute(28613)]
            [Reflection.DescriptionAttribute("could not sit")]
            COULD_NOT_SIT,

            [Command.StatusAttribute(48467)]
            [Reflection.DescriptionAttribute("no Corrade permissions")]
            NO_CORRADE_PERMISSIONS,

            [Command.StatusAttribute(54214)]
            [Reflection.DescriptionAttribute("could not create group")]
            COULD_NOT_CREATE_GROUP,

            [Command.StatusAttribute(11287)]
            [Reflection.DescriptionAttribute("could not create role")]
            COULD_NOT_CREATE_ROLE,

            [Command.StatusAttribute(12758)]
            [Reflection.DescriptionAttribute("no role name specified")]
            NO_ROLE_NAME_SPECIFIED,

            [Command.StatusAttribute(34084)]
            [Reflection.DescriptionAttribute("timeout getting group roles members")]
            TIMEOUT_GETING_GROUP_ROLES_MEMBERS,

            [Command.StatusAttribute(11050)]
            [Reflection.DescriptionAttribute("timeout getting group roles")]
            TIMEOUT_GETTING_GROUP_ROLES,

            [Command.StatusAttribute(39016)]
            [Reflection.DescriptionAttribute("timeout getting role powers")]
            TIMEOUT_GETTING_ROLE_POWERS,

            [Command.StatusAttribute(64390)]
            [Reflection.DescriptionAttribute("could not find parcel")]
            COULD_NOT_FIND_PARCEL,

            [Command.StatusAttribute(17019)]
            [Reflection.DescriptionAttribute("unable to set home")]
            UNABLE_TO_SET_HOME,

            [Command.StatusAttribute(31493)]
            [Reflection.DescriptionAttribute("unable to go home")]
            UNABLE_TO_GO_HOME,

            [Command.StatusAttribute(32923)]
            [Reflection.DescriptionAttribute("timeout getting profile")]
            TIMEOUT_GETTING_PROFILE,

            [Command.StatusAttribute(36068)]
            [Reflection.DescriptionAttribute("type can only be voice or text")]
            TYPE_CAN_BE_VOICE_OR_TEXT,

            [Command.StatusAttribute(19862)]
            [Reflection.DescriptionAttribute("agent not in group")]
            AGENT_NOT_IN_GROUP,

            [Command.StatusAttribute(29345)]
            [Reflection.DescriptionAttribute("empty attachments")]
            EMPTY_ATTACHMENTS,

            [Command.StatusAttribute(48899)]
            [Reflection.DescriptionAttribute("empty pick name")]
            EMPTY_PICK_NAME,

            [Command.StatusAttribute(22733)]
            [Reflection.DescriptionAttribute("unable to join group chat")]
            UNABLE_TO_JOIN_GROUP_CHAT,

            [Command.StatusAttribute(59524)]
            [Reflection.DescriptionAttribute("invalid position")]
            INVALID_POSITION,

            [Command.StatusAttribute(02707)]
            [Reflection.DescriptionAttribute("could not find title")]
            COULD_NOT_FIND_TITLE,

            [Command.StatusAttribute(43713)]
            [Reflection.DescriptionAttribute("fly action can only be start or stop")]
            FLY_ACTION_START_OR_STOP,

            [Command.StatusAttribute(64868)]
            [Reflection.DescriptionAttribute("invalid proposal text")]
            INVALID_PROPOSAL_TEXT,

            [Command.StatusAttribute(03098)]
            [Reflection.DescriptionAttribute("invalid proposal quorum")]
            INVALID_PROPOSAL_QUORUM,

            [Command.StatusAttribute(41810)]
            [Reflection.DescriptionAttribute("invalid proposal majority")]
            INVALID_PROPOSAL_MAJORITY,

            [Command.StatusAttribute(07628)]
            [Reflection.DescriptionAttribute("invalid proposal duration")]
            INVALID_PROPOSAL_DURATION,

            [Command.StatusAttribute(64123)]
            [Reflection.DescriptionAttribute("invalid mute target")]
            INVALID_MUTE_TARGET,

            [Command.StatusAttribute(59526)]
            [Reflection.DescriptionAttribute("unknown action")]
            UNKNOWN_ACTION,

            [Command.StatusAttribute(28087)]
            [Reflection.DescriptionAttribute("no database file configured")]
            NO_DATABASE_FILE_CONFIGURED,

            [Command.StatusAttribute(01253)]
            [Reflection.DescriptionAttribute("cannot remove owner role")]
            CANNOT_REMOVE_OWNER_ROLE,

            [Command.StatusAttribute(47808)]
            [Reflection.DescriptionAttribute("cannot remove user from owner role")]
            CANNOT_REMOVE_USER_FROM_OWNER_ROLE,

            [Command.StatusAttribute(47469)]
            [Reflection.DescriptionAttribute("timeout getting picks")]
            TIMEOUT_GETTING_PICKS,

            [Command.StatusAttribute(41256)]
            [Reflection.DescriptionAttribute("maximum number of roles exceeded")]
            MAXIMUM_NUMBER_OF_ROLES_EXCEEDED,

            [Command.StatusAttribute(40908)]
            [Reflection.DescriptionAttribute("cannot delete a group member from the everyone role")]
            CANNOT_DELETE_A_GROUP_MEMBER_FROM_THE_EVERYONE_ROLE,

            [Command.StatusAttribute(00458)]
            [Reflection.DescriptionAttribute("group members are by default in the everyone role")]
            GROUP_MEMBERS_ARE_BY_DEFAULT_IN_THE_EVERYONE_ROLE,

            [Command.StatusAttribute(33413)]
            [Reflection.DescriptionAttribute("cannot delete the everyone role")]
            CANNOT_DELETE_THE_EVERYONE_ROLE,

            [Command.StatusAttribute(65303)]
            [Reflection.DescriptionAttribute("invalid url provided")]
            INVALID_URL_PROVIDED,

            [Command.StatusAttribute(65327)]
            [Reflection.DescriptionAttribute("invalid notification types")]
            INVALID_NOTIFICATION_TYPES,

            [Command.StatusAttribute(49640)]
            [Reflection.DescriptionAttribute("notification not allowed")]
            NOTIFICATION_NOT_ALLOWED,

            [Command.StatusAttribute(44447)]
            [Reflection.DescriptionAttribute("unknown directory search type")]
            UNKNOWN_DIRECTORY_SEARCH_TYPE,

            [Command.StatusAttribute(65101)]
            [Reflection.DescriptionAttribute("no search text provided")]
            NO_SEARCH_TEXT_PROVIDED,

            [Command.StatusAttribute(14337)]
            [Reflection.DescriptionAttribute("unknown restart action")]
            UNKNOWN_RESTART_ACTION,

            [Command.StatusAttribute(28429)]
            [Reflection.DescriptionAttribute("unknown move action")]
            UNKNOWN_MOVE_ACTION,

            [Command.StatusAttribute(20541)]
            [Reflection.DescriptionAttribute("timeout getting top scripts")]
            TIMEOUT_GETTING_TOP_SCRIPTS,

            [Command.StatusAttribute(47172)]
            [Reflection.DescriptionAttribute("timeout getting top colliders")]
            TIMEOUT_GETTING_TOP_COLLIDERS,

            [Command.StatusAttribute(41676)]
            [Reflection.DescriptionAttribute("unknown top type")]
            UNKNOWN_TOP_TYPE,

            [Command.StatusAttribute(25897)]
            [Reflection.DescriptionAttribute("unknown estate list action")]
            UNKNOWN_ESTATE_LIST_ACTION,

            [Command.StatusAttribute(46990)]
            [Reflection.DescriptionAttribute("unknown estate list")]
            UNKNOWN_ESTATE_LIST,

            [Command.StatusAttribute(43156)]
            [Reflection.DescriptionAttribute("no item specified")]
            NO_ITEM_SPECIFIED,

            [Command.StatusAttribute(09348)]
            [Reflection.DescriptionAttribute("unknown animation action")]
            UNKNOWN_ANIMATION_ACTION,

            [Command.StatusAttribute(42216)]
            [Reflection.DescriptionAttribute("no channel specified")]
            NO_CHANNEL_SPECIFIED,

            [Command.StatusAttribute(31049)]
            [Reflection.DescriptionAttribute("no button index specified")]
            NO_BUTTON_INDEX_SPECIFIED,

            [Command.StatusAttribute(38931)]
            [Reflection.DescriptionAttribute("no label or index specified")]
            NO_LABEL_OR_INDEX_SPECIFIED,

            [Command.StatusAttribute(19059)]
            [Reflection.DescriptionAttribute("no land rights")]
            NO_LAND_RIGHTS,

            [Command.StatusAttribute(61113)]
            [Reflection.DescriptionAttribute("unknown entity")]
            UNKNOWN_ENTITY,

            [Command.StatusAttribute(58183)]
            [Reflection.DescriptionAttribute("invalid rotation")]
            INVALID_ROTATION,

            [Command.StatusAttribute(45364)]
            [Reflection.DescriptionAttribute("could not set script state")]
            COULD_NOT_SET_SCRIPT_STATE,

            [Command.StatusAttribute(50218)]
            [Reflection.DescriptionAttribute("item is not a script")]
            ITEM_IS_NOT_A_SCRIPT,

            [Command.StatusAttribute(49722)]
            [Reflection.DescriptionAttribute("failed to get display name")]
            FAILED_TO_GET_DISPLAY_NAME,

            [Command.StatusAttribute(40665)]
            [Reflection.DescriptionAttribute("no name provided")]
            NO_NAME_PROVIDED,

            [Command.StatusAttribute(35198)]
            [Reflection.DescriptionAttribute("could not set display name")]
            COULD_NOT_SET_DISPLAY_NAME,

            [Command.StatusAttribute(63713)]
            [Reflection.DescriptionAttribute("timeout joining group")]
            TIMEOUT_JOINING_GROUP,

            [Command.StatusAttribute(32404)]
            [Reflection.DescriptionAttribute("timeout creating group")]
            TIMEOUT_CREATING_GROUP,

            [Command.StatusAttribute(00616)]
            [Reflection.DescriptionAttribute("timeout ejecting agent")]
            TIMEOUT_EJECTING_AGENT,

            [Command.StatusAttribute(25426)]
            [Reflection.DescriptionAttribute("timeout getting group role members")]
            TIMEOUT_GETTING_GROUP_ROLE_MEMBERS,

            [Command.StatusAttribute(31237)]
            [Reflection.DescriptionAttribute("timeout leaving group")]
            TIMEOUT_LEAVING_GROUP,

            [Command.StatusAttribute(43780)]
            [Reflection.DescriptionAttribute("timeout during teleport")]
            TIMEOUT_DURING_TELEPORT,

            [Command.StatusAttribute(46316)]
            [Reflection.DescriptionAttribute("timeout requesting sit")]
            TIMEOUT_REQUESTING_SIT,

            [Command.StatusAttribute(09111)]
            [Reflection.DescriptionAttribute("timeout getting land users")]
            TIMEOUT_GETTING_LAND_USERS,

            [Command.StatusAttribute(23364)]
            [Reflection.DescriptionAttribute("timeout getting script state")]
            TIMEOUT_GETTING_SCRIPT_STATE,

            [Command.StatusAttribute(26393)]
            [Reflection.DescriptionAttribute("timeout updating mute list")]
            TIMEOUT_UPDATING_MUTE_LIST,

            [Command.StatusAttribute(32362)]
            [Reflection.DescriptionAttribute("timeout getting parcels")]
            TIMEOUT_GETTING_PARCELS,

            [Command.StatusAttribute(46942)]
            [Reflection.DescriptionAttribute("empty classified name")]
            EMPTY_CLASSIFIED_NAME,

            [Command.StatusAttribute(38184)]
            [Reflection.DescriptionAttribute("invalid price")]
            INVALID_PRICE,

            [Command.StatusAttribute(59103)]
            [Reflection.DescriptionAttribute("timeout getting classifieds")]
            TIMEOUT_GETTING_CLASSIFIEDS,

            [Command.StatusAttribute(08241)]
            [Reflection.DescriptionAttribute("could not find classified")]
            COULD_NOT_FIND_CLASSIFIED,

            [Command.StatusAttribute(53947)]
            [Reflection.DescriptionAttribute("invalid days")]
            INVALID_DAYS,

            [Command.StatusAttribute(18490)]
            [Reflection.DescriptionAttribute("invalid interval")]
            INVALID_INTERVAL,

            [Command.StatusAttribute(53829)]
            [Reflection.DescriptionAttribute("timeout getting group account summary")]
            TIMEOUT_GETTING_GROUP_ACCOUNT_SUMMARY,

            [Command.StatusAttribute(30207)]
            [Reflection.DescriptionAttribute("friend not found")]
            FRIEND_NOT_FOUND,

            [Command.StatusAttribute(32366)]
            [Reflection.DescriptionAttribute("the agent already is a friend")]
            AGENT_ALREADY_FRIEND,

            [Command.StatusAttribute(04797)]
            [Reflection.DescriptionAttribute("friendship offer not found")]
            FRIENDSHIP_OFFER_NOT_FOUND,

            [Command.StatusAttribute(65003)]
            [Reflection.DescriptionAttribute("friend does not allow mapping")]
            FRIEND_DOES_NOT_ALLOW_MAPPING,

            [Command.StatusAttribute(10691)]
            [Reflection.DescriptionAttribute("timeout mapping friend")]
            TIMEOUT_MAPPING_FRIEND,

            [Command.StatusAttribute(23309)]
            [Reflection.DescriptionAttribute("friend offline")]
            FRIEND_OFFLINE,

            [Command.StatusAttribute(34964)]
            [Reflection.DescriptionAttribute("timeout getting region")]
            TIMEOUT_GETTING_REGION,

            [Command.StatusAttribute(35447)]
            [Reflection.DescriptionAttribute("region not found")]
            REGION_NOT_FOUND,

            [Command.StatusAttribute(00337)]
            [Reflection.DescriptionAttribute("no map items found")]
            NO_MAP_ITEMS_FOUND,

            [Command.StatusAttribute(53549)]
            [Reflection.DescriptionAttribute("no description provided")]
            NO_DESCRIPTION_PROVIDED,

            [Command.StatusAttribute(43982)]
            [Reflection.DescriptionAttribute("no folder specified")]
            NO_FOLDER_SPECIFIED,

            [Command.StatusAttribute(29512)]
            [Reflection.DescriptionAttribute("empty wearables")]
            EMPTY_WEARABLES,

            [Command.StatusAttribute(35316)]
            [Reflection.DescriptionAttribute("parcel not for sale")]
            PARCEL_NOT_FOR_SALE,

            [Command.StatusAttribute(42051)]
            [Reflection.DescriptionAttribute("unknown access list type")]
            UNKNOWN_ACCESS_LIST_TYPE,

            [Command.StatusAttribute(29438)]
            [Reflection.DescriptionAttribute("no task specified")]
            NO_TASK_SPECIFIED,

            [Command.StatusAttribute(37470)]
            [Reflection.DescriptionAttribute("timeout getting group members")]
            TIMEOUT_GETTING_GROUP_MEMBERS,

            [Command.StatusAttribute(24939)]
            [Reflection.DescriptionAttribute("group not open")]
            GROUP_NOT_OPEN,

            [Command.StatusAttribute(30384)]
            [Reflection.DescriptionAttribute("timeout downloading terrain")]
            TIMEOUT_DOWNLOADING_ASSET,

            [Command.StatusAttribute(57005)]
            [Reflection.DescriptionAttribute("timeout uploading terrain")]
            TIMEOUT_UPLOADING_ASSET,

            [Command.StatusAttribute(16667)]
            [Reflection.DescriptionAttribute("empty terrain data")]
            EMPTY_ASSET_DATA,

            [Command.StatusAttribute(34749)]
            [Reflection.DescriptionAttribute("the specified folder contains no equipable items")]
            NO_EQUIPABLE_ITEMS,

            [Command.StatusAttribute(42249)]
            [Reflection.DescriptionAttribute("inventory offer not found")]
            INVENTORY_OFFER_NOT_FOUND,

            [Command.StatusAttribute(23805)]
            [Reflection.DescriptionAttribute("no session specified")]
            NO_SESSION_SPECIFIED,

            [Command.StatusAttribute(61018)]
            [Reflection.DescriptionAttribute("folder not found")]
            FOLDER_NOT_FOUND,

            [Command.StatusAttribute(37211)]
            [Reflection.DescriptionAttribute("timeout creating item")]
            TIMEOUT_CREATING_ITEM,

            [Command.StatusAttribute(09541)]
            [Reflection.DescriptionAttribute("timeout uploading item")]
            TIMEOUT_UPLOADING_ITEM,

            [Command.StatusAttribute(36684)]
            [Reflection.DescriptionAttribute("unable to upload item")]
            UNABLE_TO_UPLOAD_ITEM,

            [Command.StatusAttribute(05034)]
            [Reflection.DescriptionAttribute("unable to create item")]
            UNABLE_TO_CREATE_ITEM,

            [Command.StatusAttribute(44397)]
            [Reflection.DescriptionAttribute("timeout uploading item data")]
            TIMEOUT_UPLOADING_ITEM_DATA,

            [Command.StatusAttribute(12320)]
            [Reflection.DescriptionAttribute("unable to upload item data")]
            UNABLE_TO_UPLOAD_ITEM_DATA,

            [Command.StatusAttribute(55979)]
            [Reflection.DescriptionAttribute("unknown direction")]
            UNKNOWN_DIRECTION,

            [Command.StatusAttribute(22576)]
            [Reflection.DescriptionAttribute("timeout requesting to set home")]
            TIMEOUT_REQUESTING_TO_SET_HOME,

            [Command.StatusAttribute(07255)]
            [Reflection.DescriptionAttribute("timeout transferring asset")]
            TIMEOUT_TRANSFERRING_ASSET,

            [Command.StatusAttribute(60269)]
            [Reflection.DescriptionAttribute("asset upload failed")]
            ASSET_UPLOAD_FAILED,

            [Command.StatusAttribute(57085)]
            [Reflection.DescriptionAttribute("failed to download asset")]
            FAILED_TO_DOWNLOAD_ASSET,

            [Command.StatusAttribute(60025)]
            [Reflection.DescriptionAttribute("unknown asset type")]
            UNKNOWN_ASSET_TYPE,

            [Command.StatusAttribute(59048)]
            [Reflection.DescriptionAttribute("invalid asset data")]
            INVALID_ASSET_DATA,

            [Command.StatusAttribute(32709)]
            [Reflection.DescriptionAttribute("unknown wearable type")]
            UNKNOWN_WEARABLE_TYPE,

            [Command.StatusAttribute(06097)]
            [Reflection.DescriptionAttribute("unknown inventory type")]
            UNKNOWN_INVENTORY_TYPE,

            [Command.StatusAttribute(64698)]
            [Reflection.DescriptionAttribute("could not compile regular expression")]
            COULD_NOT_COMPILE_REGULAR_EXPRESSION,

            [Command.StatusAttribute(18680)]
            [Reflection.DescriptionAttribute("no pattern provided")]
            NO_PATTERN_PROVIDED,

            [Command.StatusAttribute(11910)]
            [Reflection.DescriptionAttribute("no executable file provided")]
            NO_EXECUTABLE_FILE_PROVIDED,

            [Command.StatusAttribute(31381)]
            [Reflection.DescriptionAttribute("timeout waiting for execution")]
            TIMEOUT_WAITING_FOR_EXECUTION,

            [Command.StatusAttribute(04541)]
            [Reflection.DescriptionAttribute("group invite not found")]
            GROUP_INVITE_NOT_FOUND,

            [Command.StatusAttribute(38125)]
            [Reflection.DescriptionAttribute("unable to obtain money balance")]
            UNABLE_TO_OBTAIN_MONEY_BALANCE,

            [Command.StatusAttribute(20048)]
            [Reflection.DescriptionAttribute("timeout getting avatar data")]
            TIMEOUT_GETTING_AVATAR_DATA,

            [Command.StatusAttribute(13712)]
            [Reflection.DescriptionAttribute("timeout retrieving estate list")]
            TIMEOUT_RETRIEVING_ESTATE_LIST,

            [Command.StatusAttribute(37559)]
            [Reflection.DescriptionAttribute("destination too close")]
            DESTINATION_TOO_CLOSE,

            [Command.StatusAttribute(11229)]
            [Reflection.DescriptionAttribute("timeout getting group titles")]
            TIMEOUT_GETTING_GROUP_TITLES,

            [Command.StatusAttribute(47101)]
            [Reflection.DescriptionAttribute("no message provided")]
            NO_MESSAGE_PROVIDED,

            [Command.StatusAttribute(04075)]
            [Reflection.DescriptionAttribute("could not remove SIML package file")]
            COULD_NOT_REMOVE_SIML_PACKAGE_FILE,

            [Command.StatusAttribute(54456)]
            [Reflection.DescriptionAttribute("unknown effect")]
            UNKNOWN_EFFECT,

            [Command.StatusAttribute(48775)]
            [Reflection.DescriptionAttribute("no effect UUID provided")]
            NO_EFFECT_UUID_PROVIDED,

            [Command.StatusAttribute(38858)]
            [Reflection.DescriptionAttribute("effect not found")]
            EFFECT_NOT_FOUND,

            [Command.StatusAttribute(16572)]
            [Reflection.DescriptionAttribute("invalid viewer effect")]
            INVALID_VIEWER_EFFECT,

            [Command.StatusAttribute(19011)]
            [Reflection.DescriptionAttribute("ambiguous path")]
            AMBIGUOUS_PATH,

            [Command.StatusAttribute(53066)]
            [Reflection.DescriptionAttribute("path not found")]
            PATH_NOT_FOUND,

            [Command.StatusAttribute(13857)]
            [Reflection.DescriptionAttribute("unexpected item in path")]
            UNEXPECTED_ITEM_IN_PATH,

            [Command.StatusAttribute(59282)]
            [Reflection.DescriptionAttribute("no path provided")]
            NO_PATH_PROVIDED,

            [Command.StatusAttribute(26623)]
            [Reflection.DescriptionAttribute("unable to create folder")]
            UNABLE_TO_CREATE_FOLDER,

            [Command.StatusAttribute(28866)]
            [Reflection.DescriptionAttribute("no permissions provided")]
            NO_PERMISSIONS_PROVIDED,

            [Command.StatusAttribute(43615)]
            [Reflection.DescriptionAttribute("setting permissions failed")]
            SETTING_PERMISSIONS_FAILED,

            [Command.StatusAttribute(39391)]
            [Reflection.DescriptionAttribute("expected item as source")]
            EXPECTED_ITEM_AS_SOURCE,

            [Command.StatusAttribute(22655)]
            [Reflection.DescriptionAttribute("expected folder as target")]
            EXPECTED_FOLDER_AS_TARGET,

            [Command.StatusAttribute(63024)]
            [Reflection.DescriptionAttribute("unable to load configuration")]
            UNABLE_TO_LOAD_CONFIGURATION,

            [Command.StatusAttribute(33564)]
            [Reflection.DescriptionAttribute("unable to save configuration")]
            UNABLE_TO_SAVE_CONFIGURATION,

            [Command.StatusAttribute(20900)]
            [Reflection.DescriptionAttribute("invalid xml path")]
            INVALID_XML_PATH,

            [Command.StatusAttribute(03638)]
            [Reflection.DescriptionAttribute("no data provided")]
            NO_DATA_PROVIDED,

            [Command.StatusAttribute(42903)]
            [Reflection.DescriptionAttribute("unknown image format requested")]
            UNKNOWN_IMAGE_FORMAT_REQUESTED,

            [Command.StatusAttribute(02380)]
            [Reflection.DescriptionAttribute("unknown image format provided")]
            UNKNOWN_IMAGE_FORMAT_PROVIDED,

            [Command.StatusAttribute(04994)]
            [Reflection.DescriptionAttribute("unable to decode asset data")]
            UNABLE_TO_DECODE_ASSET_DATA,

            [Command.StatusAttribute(61067)]
            [Reflection.DescriptionAttribute("unable to convert to requested format")]
            UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT,

            [Command.StatusAttribute(08411)]
            [Reflection.DescriptionAttribute("could not start process")]
            COULD_NOT_START_PROCESS,

            [Command.StatusAttribute(22737)]
            [Reflection.DescriptionAttribute("object not found")]
            OBJECT_NOT_FOUND,

            [Command.StatusAttribute(19143)]
            [Reflection.DescriptionAttribute("timeout meshmerizing object")]
            COULD_NOT_MESHMERIZE_OBJECT,

            [Command.StatusAttribute(37841)]
            [Reflection.DescriptionAttribute("could not get primitive properties")]
            COULD_NOT_GET_PRIMITIVE_PROPERTIES,

            [Command.StatusAttribute(54854)]
            [Reflection.DescriptionAttribute("avatar not in range")]
            AVATAR_NOT_IN_RANGE,

            [Command.StatusAttribute(03475)]
            [Reflection.DescriptionAttribute("invalid scale")]
            INVALID_SCALE,

            [Command.StatusAttribute(30129)]
            [Reflection.DescriptionAttribute("could not get current groups")]
            COULD_NOT_GET_CURRENT_GROUPS,

            [Command.StatusAttribute(39613)]
            [Reflection.DescriptionAttribute("maximum number of groups reached")]
            MAXIMUM_NUMBER_OF_GROUPS_REACHED,

            [Command.StatusAttribute(43003)]
            [Reflection.DescriptionAttribute("unknown syntax type")]
            UNKNOWN_SYNTAX_TYPE,

            [Command.StatusAttribute(13053)]
            [Reflection.DescriptionAttribute("too many characters for group name")]
            TOO_MANY_CHARACTERS_FOR_GROUP_NAME,

            [Command.StatusAttribute(19325)]
            [Reflection.DescriptionAttribute("too many characters for group title")]
            TOO_MANY_CHARACTERS_FOR_GROUP_TITLE,

            [Command.StatusAttribute(26178)]
            [Reflection.DescriptionAttribute("too many characters for notice message")]
            TOO_MANY_CHARACTERS_FOR_NOTICE_MESSAGE,

            [Command.StatusAttribute(35277)]
            [Reflection.DescriptionAttribute("notecard message body too large")]
            NOTECARD_MESSAGE_BODY_TOO_LARGE,

            [Command.StatusAttribute(47571)]
            [Reflection.DescriptionAttribute("too many or too few characters for display name")]
            TOO_MANY_OR_TOO_FEW_CHARACTERS_FOR_DISPLAY_NAME,

            [Command.StatusAttribute(30293)]
            [Reflection.DescriptionAttribute("name too large")]
            NAME_TOO_LARGE,

            [Command.StatusAttribute(60515)]
            [Reflection.DescriptionAttribute("position would exceed maximum rez altitude")]
            POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE,

            [Command.StatusAttribute(43683)]
            [Reflection.DescriptionAttribute("description too large")]
            DESCRIPTION_TOO_LARGE,

            [Command.StatusAttribute(54154)]
            [Reflection.DescriptionAttribute("scale would exceed building constraints")]
            SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS,

            [Command.StatusAttribute(29745)]
            [Reflection.DescriptionAttribute("attachments would exceed maximum attachment limit")]
            ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT,

            [Command.StatusAttribute(52299)]
            [Reflection.DescriptionAttribute("too many or too few characters in message")]
            TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE,

            [Command.StatusAttribute(50593)]
            [Reflection.DescriptionAttribute("maximum ban list length reached")]
            MAXIMUM_BAN_LIST_LENGTH_REACHED,

            [Command.StatusAttribute(09935)]
            [Reflection.DescriptionAttribute("maximum group list length reached")]
            MAXIMUM_GROUP_LIST_LENGTH_REACHED,

            [Command.StatusAttribute(42536)]
            [Reflection.DescriptionAttribute("maximum user list length reached")]
            MAXIMUM_USER_LIST_LENGTH_REACHED,

            [Command.StatusAttribute(28625)]
            [Reflection.DescriptionAttribute("maximum manager list length reached")]
            MAXIMUM_MANAGER_LIST_LENGTH_REACHED,

            [Command.StatusAttribute(28126)]
            [Reflection.DescriptionAttribute("auto return time outside limit range")]
            AUTO_RETURN_TIME_OUTSIDE_LIMIT_RANGE,

            [Command.StatusAttribute(56379)]
            [Reflection.DescriptionAttribute("second life text too large")]
            SECOND_LIFE_TEXT_TOO_LARGE,

            [Command.StatusAttribute(09924)]
            [Reflection.DescriptionAttribute("first life text too large")]
            FIRST_LIFE_TEXT_TOO_LARGE,

            [Command.StatusAttribute(50405)]
            [Reflection.DescriptionAttribute("maximum amount of picks reached")]
            MAXIMUM_AMOUNT_OF_PICKS_REACHED,

            [Command.StatusAttribute(17894)]
            [Reflection.DescriptionAttribute("description would exceed maximum size")]
            DESCRIPTION_WOULD_EXCEED_MAXIMUM_SIZE,

            [Command.StatusAttribute(28247)]
            [Reflection.DescriptionAttribute("maximum amount of classifieds reached")]
            MAXIMUM_AMOUNT_OF_CLASSIFIEDS_REACHED,

            [Command.StatusAttribute(38609)]
            [Reflection.DescriptionAttribute("timeout changing links")]
            TIMEOUT_CHANGING_LINKS,

            [Command.StatusAttribute(45074)]
            [Reflection.DescriptionAttribute("link would exceed maximum link limit")]
            LINK_WOULD_EXCEED_MAXIMUM_LINK_LIMIT,

            [Command.StatusAttribute(40773)]
            [Reflection.DescriptionAttribute("invalid number of items specified")]
            INVALID_NUMBER_OF_ITEMS_SPECIFIED,

            [Command.StatusAttribute(52751)]
            [Reflection.DescriptionAttribute("timeout requesting price")]
            TIMEOUT_REQUESTING_PRICE,

            [Command.StatusAttribute(01536)]
            [Reflection.DescriptionAttribute("primitive not for sale")]
            PRIMITIVE_NOT_FOR_SALE,

            [Command.StatusAttribute(36123)]
            [Reflection.DescriptionAttribute("teleport throttled")]
            TELEPORT_THROTTLED,

            [Command.StatusAttribute(06617)]
            [Reflection.DescriptionAttribute("dialog button not found")]
            DIALOG_BUTTON_NOT_FOUND,

            [Command.StatusAttribute(08842)]
            [Reflection.DescriptionAttribute("unknown tree type")]
            UNKNOWN_TREE_TYPE,

            [Command.StatusAttribute(62130)]
            [Reflection.DescriptionAttribute("invalid texture coordinates")]
            INVALID_TEXTURE_COORDINATES,

            [Command.StatusAttribute(10945)]
            [Reflection.DescriptionAttribute("invalid surface coordinates")]
            INVALID_SURFACE_COORDINATES,

            [Command.StatusAttribute(28487)]
            [Reflection.DescriptionAttribute("invalid normal vector")]
            INVALID_NORMAL_VECTOR,

            [Command.StatusAttribute(13296)]
            [Reflection.DescriptionAttribute("invalid binormal vector")]
            INVALID_BINORMAL_VECTOR,

            [Command.StatusAttribute(44554)]
            [Reflection.DescriptionAttribute("primitives not in same region")]
            PRIMITIVES_NOT_IN_SAME_REGION,

            [Command.StatusAttribute(38798)]
            [Reflection.DescriptionAttribute("invalid face specified")]
            INVALID_FACE_SPECIFIED,

            [Command.StatusAttribute(61473)]
            [Reflection.DescriptionAttribute("invalid status supplied")]
            INVALID_STATUS_SUPPLIED,

            [Command.StatusAttribute(13764)]
            [Reflection.DescriptionAttribute("status not found")]
            STATUS_NOT_FOUND,

            [Command.StatusAttribute(30556)]
            [Reflection.DescriptionAttribute("no description for status")]
            NO_DESCRIPTION_FOR_STATUS,

            [Command.StatusAttribute(64368)]
            [Reflection.DescriptionAttribute("unknown grass type")]
            UNKNOWN_GRASS_TYPE,

            [Command.StatusAttribute(53274)]
            [Reflection.DescriptionAttribute("unknown material type")]
            UNKNOWN_MATERIAL_TYPE,

            [Command.StatusAttribute(18463)]
            [Reflection.DescriptionAttribute("could not retrieve object media")]
            COULD_NOT_RETRIEVE_OBJECT_MEDIA,

            [Command.StatusAttribute(02193)]
            [Reflection.DescriptionAttribute("no avatars to ban or unban")]
            NO_AVATARS_TO_BAN_OR_UNBAN,

            [Command.StatusAttribute(45568)]
            [Reflection.DescriptionAttribute("could not retrieve broup ban list")]
            COULD_NOT_RETRIEVE_GROUP_BAN_LIST,

            [Command.StatusAttribute(15719)]
            [Reflection.DescriptionAttribute("timeout retrieving group ban list")]
            TIMEOUT_RETRIEVING_GROUP_BAN_LIST,

            [Command.StatusAttribute(26749)]
            [Reflection.DescriptionAttribute("timeout modifying group ban list")]
            TIMEOUT_MODIFYING_GROUP_BAN_LIST,

            [Command.StatusAttribute(26715)]
            [Reflection.DescriptionAttribute("mute entry not found")]
            MUTE_ENTRY_NOT_FOUND,

            [Command.StatusAttribute(51086)]
            [Reflection.DescriptionAttribute("no name or UUID provided")]
            NO_NAME_OR_UUID_PROVIDED,

            [Command.StatusAttribute(16450)]
            [Reflection.DescriptionAttribute("could not retrieve mute list")]
            COULD_NOT_RETRIEVE_MUTE_LIST,

            [Command.StatusAttribute(39647)]
            [Reflection.DescriptionAttribute("mute entry already exists")]
            MUTE_ENTRY_ALREADY_EXISTS,

            [Command.StatusAttribute(39787)]
            [Reflection.DescriptionAttribute("timeout reaching destination")]
            TIMEOUT_REACHING_DESTINATION,

            [Command.StatusAttribute(10776)]
            [Reflection.DescriptionAttribute("group schedules exceeded")]
            GROUP_SCHEDULES_EXCEEDED,

            [Command.StatusAttribute(36896)]
            [Reflection.DescriptionAttribute("no index provided")]
            NO_INDEX_PROVIDED,

            [Command.StatusAttribute(56094)]
            [Reflection.DescriptionAttribute("no schedule found")]
            NO_SCHEDULE_FOUND,

            [Command.StatusAttribute(41612)]
            [Reflection.DescriptionAttribute("unknown date time stamp")]
            UNKNOWN_DATE_TIME_STAMP,

            [Command.StatusAttribute(07457)]
            [Reflection.DescriptionAttribute("no permissions for item")]
            NO_PERMISSIONS_FOR_ITEM,

            [Command.StatusAttribute(10374)]
            [Reflection.DescriptionAttribute("timeout retrieving estate covenant")]
            TIMEOUT_RETRIEVING_ESTATE_COVENANT,

            [Command.StatusAttribute(56901)]
            [Reflection.DescriptionAttribute("no terraform action specified")]
            NO_TERRAFORM_ACTION_SPECIFIED,

            [Command.StatusAttribute(41211)]
            [Reflection.DescriptionAttribute("no terraform brush specified")]
            NO_TERRAFORM_BRUSH_SPECIFIED,

            [Command.StatusAttribute(63486)]
            [Reflection.DescriptionAttribute("invalid height")]
            INVALID_HEIGHT,

            [Command.StatusAttribute(20547)]
            [Reflection.DescriptionAttribute("invalid width")]
            INVALID_WIDTH,

            [Command.StatusAttribute(28891)]
            [Reflection.DescriptionAttribute("invalid terraform action")]
            INVALID_TERRAFORM_ACTION,

            [Command.StatusAttribute(41190)]
            [Reflection.DescriptionAttribute("invalid terraform brush")]
            INVALID_TERRAFORM_BRUSH,

            [Command.StatusAttribute(58619)]
            [Reflection.DescriptionAttribute("could not terraform")]
            COULD_NOT_TERRAFORM,

            [Command.StatusAttribute(38289)]
            [Reflection.DescriptionAttribute("timeout waiting for display name")]
            TIMEOUT_WAITING_FOR_DISPLAY_NAME,

            [Command.StatusAttribute(51050)]
            [Reflection.DescriptionAttribute("script permission request not found")]
            SCRIPT_PERMISSION_REQUEST_NOT_FOUND,

            [Command.StatusAttribute(60073)]
            [Reflection.DescriptionAttribute("teleport lure not found")]
            TELEPORT_LURE_NOT_FOUND,

            [Command.StatusAttribute(42248)]
            [Reflection.DescriptionAttribute("unable to save Corrade configuration")]
            UNABLE_TO_SAVE_CORRADE_CONFIGURATION,

            [Command.StatusAttribute(26356)]
            [Reflection.DescriptionAttribute("timeout retrieving group notices")]
            TIMEOUT_RETRIEVING_GROUP_NOTICES,

            [Command.StatusAttribute(42798)]
            [Reflection.DescriptionAttribute("timeout retrieving notice")]
            TIMEOUT_RETRIEVING_NOTICE,

            [Command.StatusAttribute(06330)]
            [Reflection.DescriptionAttribute("no notice found")]
            NO_NOTICE_FOUND,

            [Command.StatusAttribute(20303)]
            [Reflection.DescriptionAttribute("notice does not contain attachment")]
            NOTICE_DOES_NOT_CONTAIN_ATTACHMENT,

            [Command.StatusAttribute(10522)]
            [Reflection.DescriptionAttribute("failed to read log file")]
            FAILED_TO_READ_LOG_FILE,

            [Command.StatusAttribute(62646)]
            [Reflection.DescriptionAttribute("effect UUID belongs to different effect")]
            EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT,

            [Command.StatusAttribute(25252)]
            [Reflection.DescriptionAttribute("no SQL string provided")]
            NO_SQL_STRING_PROVIDED,

            [Command.StatusAttribute(45173)]
            [Reflection.DescriptionAttribute("invalid angle provided")]
            INVALID_ANGLE_PROVIDED,

            [Command.StatusAttribute(32453)]
            [Reflection.DescriptionAttribute("could not get parcel info data")]
            COULD_NOT_GET_PARCEL_INFO,

            [Command.StatusAttribute(02188)]
            [Reflection.DescriptionAttribute("could not get parcel info data")]
            NO_TARGET_SPECIFIED,

            [Command.StatusAttribute(47350)]
            [Reflection.DescriptionAttribute("no type provided")]
            NO_TYPE_PROVIDED,

            [Command.StatusAttribute(64450)]
            [Reflection.DescriptionAttribute("unknown sift")]
            UNKNOWN_SIFT,

            [Command.StatusAttribute(28353)]
            [Reflection.DescriptionAttribute("invalid feed provided")]
            INVALID_FEED_PROVIDED,

            [Command.StatusAttribute(34869)]
            [Reflection.DescriptionAttribute("already subscribed to feed")]
            ALREADY_SUBSCRIBED_TO_FEED,

            [Command.StatusAttribute(32157)]
            [Reflection.DescriptionAttribute("no consumer key provided")]
            NO_CONSUMER_KEY_PROVIDED,

            [Command.StatusAttribute(40762)]
            [Reflection.DescriptionAttribute("no consumer secret provided")]
            NO_CONSUMER_SECRET_PROVIDED,

            [Command.StatusAttribute(13399)]
            [Reflection.DescriptionAttribute("no access token provided")]
            NO_ACCESS_TOKEN_PROVIDED,

            [Command.StatusAttribute(55091)]
            [Reflection.DescriptionAttribute("no access token secret provided")]
            NO_ACCESS_TOKEN_SECRET_PROVIDED,

            [Command.StatusAttribute(55051)]
            [Reflection.DescriptionAttribute("message too long")]
            MESSAGE_TOO_LONG,

            [Command.StatusAttribute(18672)]
            [Reflection.DescriptionAttribute("could not post tweet")]
            COULD_NOT_POST_TWEET,

            [Command.StatusAttribute(25119)]
            [Reflection.DescriptionAttribute("unable to retrieve transactions")]
            UNABLE_TO_RETRIEVE_TRANSACTIONS,

            [Command.StatusAttribute(54668)]
            [Reflection.DescriptionAttribute("unable to authenticate")]
            UNABLE_TO_AUTHENTICATE,

            [Command.StatusAttribute(40491)]
            [Reflection.DescriptionAttribute("no transactions found")]
            NO_TRANSACTIONS_FOUND,

            [Command.StatusAttribute(41007)]
            [Reflection.DescriptionAttribute("no secret provided")]
            NO_SECRET_PROVIDED,

            [Command.StatusAttribute(21833)]
            [Reflection.DescriptionAttribute("invalid date")]
            INVALID_DATE,

            [Command.StatusAttribute(33381)]
            [Reflection.DescriptionAttribute("unable to reach events page")]
            UNABLE_TO_REACH_EVENTS_PAGE,

            [Command.StatusAttribute(54450)]
            [Reflection.DescriptionAttribute("unable to agree to ToS")]
            UNABLE_TO_AGREE_TO_TOS,

            [Command.StatusAttribute(01691)]
            [Reflection.DescriptionAttribute("unable to post event")]
            UNABLE_TO_POST_EVENT,

            [Command.StatusAttribute(44059)]
            [Reflection.DescriptionAttribute("unable to get event identifier")]
            UNABLE_TO_GET_EVENT_IDENTIFIER,

            [Command.StatusAttribute(63915)]
            [Reflection.DescriptionAttribute("no time provided")]
            NO_TIME_PROVIDED,

            [Command.StatusAttribute(57196)]
            [Reflection.DescriptionAttribute("no duration provided")]
            NO_DURATION_PROVIDED,

            [Command.StatusAttribute(63597)]
            [Reflection.DescriptionAttribute("no date provided")]
            NO_DATE_PROVIDED,

            [Command.StatusAttribute(25003)]
            [Reflection.DescriptionAttribute("no category provided")]
            NO_CATEGORY_PROVIDED,

            [Command.StatusAttribute(21718)]
            [Reflection.DescriptionAttribute("no location provided")]
            NO_LOCATION_PROVIDED,

            [Command.StatusAttribute(23926)]
            [Reflection.DescriptionAttribute("unable to delete event")]
            UNABLE_TO_DELETE_EVENT,

            [Command.StatusAttribute(08339)]
            [Reflection.DescriptionAttribute("no event identifier provided")]
            NO_EVENT_IDENTIFIER_PROVIDED,

            [Command.StatusAttribute(33994)]
            [Reflection.DescriptionAttribute("unable to retrieve form parameters")]
            UNABLE_TO_RETRIEVE_FORM_PARAMETERS,

            [Command.StatusAttribute(53494)]
            [Reflection.DescriptionAttribute("too many characters for event description")]
            TOO_MANY_CHARACTERS_FOR_EVENT_DESCRIPTION,

            [Command.StatusAttribute(58751)]
            [Reflection.DescriptionAttribute("name may not contain HTML")]
            NAME_MAY_NOT_CONTAIN_HTML,

            [Command.StatusAttribute(54528)]
            [Reflection.DescriptionAttribute("description may not contain HTML")]
            DESCRIPTION_MAY_NOT_CONTAIN_HTML,

            [Command.StatusAttribute(21743)]
            [Reflection.DescriptionAttribute("event posting rejected")]
            EVENT_POSTING_REJECTED,

            [Command.StatusAttribute(43671)]
            [Reflection.DescriptionAttribute("unable to revoke proposal")]
            UNABLE_TO_REVOKE_PROPOSAL,

            [Command.StatusAttribute(50003)]
            [Reflection.DescriptionAttribute("unable to reject proposal")]
            UNABLE_TO_REJECT_PROPOSAL,

            [Command.StatusAttribute(56345)]
            [Reflection.DescriptionAttribute("unable to reach partnership page")]
            UNABLE_TO_REACH_PARTNERSHIP_PAGE,

            [Command.StatusAttribute(00303)]
            [Reflection.DescriptionAttribute("unable to post proposal")]
            UNABLE_TO_POST_PROPOSAL,

            [Command.StatusAttribute(61983)]
            [Reflection.DescriptionAttribute("unable to accept proposal")]
            UNABLE_TO_ACCEPT_PROPOSAL,

            [Command.StatusAttribute(31126)]
            [Reflection.DescriptionAttribute("too many characters for proposal message")]
            TOO_MANY_CHARACTERS_FOR_PROPOSAL_MESSAGE,

            [Command.StatusAttribute(34379)]
            [Reflection.DescriptionAttribute("proposal rejected")]
            PROPOSAL_REJECTED,

            [Command.StatusAttribute(43767)]
            [Reflection.DescriptionAttribute("proposal already sent")]
            PROPOSAL_ALREADY_SENT,

            [Command.StatusAttribute(22119)]
            [Reflection.DescriptionAttribute("no proposal to reject")]
            NO_PROPOSAL_TO_REJECT,

            [Command.StatusAttribute(21106)]
            [Reflection.DescriptionAttribute("message may not contain HTML")]
            MESSAGE_MAY_NOT_CONTAIN_HTML,

            [Command.StatusAttribute(46612)]
            [Reflection.DescriptionAttribute("no partner found")]
            NO_PARTNER_FOUND,

            [Command.StatusAttribute(41257)]
            [Reflection.DescriptionAttribute("unable to post divorce")]
            UNABLE_TO_POST_DIVORCE,

            [Command.StatusAttribute(58870)]
            [Reflection.DescriptionAttribute("unable to divorce")]
            UNABLE_TO_DIVORCE,

            [Command.StatusAttribute(31267)]
            [Reflection.DescriptionAttribute("agent has been banned")]
            AGENT_HAS_BEEN_BANNED,

            [Command.StatusAttribute(16927)]
            [Reflection.DescriptionAttribute("no estate powers for command")]
            NO_ESTATE_POWERS_FOR_COMMAND,

            [Command.StatusAttribute(21160)]
            [Reflection.DescriptionAttribute("unable to write file")]
            UNABLE_TO_WRITE_FILE,

            [Command.StatusAttribute(38278)]
            [Reflection.DescriptionAttribute("unable to read file")]
            UNABLE_TO_READ_FILE,

            [Command.StatusAttribute(19343)]
            [Reflection.DescriptionAttribute("unable to retrive data")]
            UNABLE_TO_RETRIEVE_DATA,

            [Command.StatusAttribute(18737)]
            [Reflection.DescriptionAttribute("unable to process data")]
            UNABLE_TO_PROCESS_DATA,

            [Command.StatusAttribute(33047)]
            [Reflection.DescriptionAttribute("failed rezzing root primitive")]
            FAILED_REZZING_ROOT_PRIMITIVE,

            [Command.StatusAttribute(25329)]
            [Reflection.DescriptionAttribute("failed rezzing child primitive")]
            FAILED_REZZING_CHILD_PRIMITIVE,

            [Command.StatusAttribute(29530)]
            [Reflection.DescriptionAttribute("could not read XML file")]
            COULD_NOT_READ_XML_FILE,

            [Command.StatusAttribute(40901)]
            [Reflection.DescriptionAttribute("SIML not enabled")]
            SIML_NOT_ENABLED,

            [Command.StatusAttribute(14989)]
            [Reflection.DescriptionAttribute("no avatars found")]
            NO_AVATARS_FOUND,

            [Command.StatusAttribute(42351)]
            [Reflection.DescriptionAttribute("timeout starting conference")]
            TIMEOUT_STARTING_CONFERENCE,

            [Command.StatusAttribute(41969)]
            [Reflection.DescriptionAttribute("unable to start conference")]
            UNABLE_TO_START_CONFERENCE,

            [Command.StatusAttribute(43898)]
            [Reflection.DescriptionAttribute("session not found")]
            SESSION_NOT_FOUND,

            [Command.StatusAttribute(22786)]
            [Reflection.DescriptionAttribute("conference member not found")]
            CONFERENCE_MEMBER_NOT_FOUND,

            [Command.StatusAttribute(46804)]
            [Reflection.DescriptionAttribute("could not send message")]
            COULD_NOT_SEND_MESSAGE,

            [Command.StatusAttribute(55110)]
            [Reflection.DescriptionAttribute("unknown mute type")]
            UNKNOWN_MUTE_TYPE,

            [Command.StatusAttribute(13491)]
            [Reflection.DescriptionAttribute("ban would exceed maximum ban list length")]
            BAN_WOULD_EXCEED_MAXIMUM_BAN_LIST_LENGTH,

            [Command.StatusAttribute(32528)]
            [Reflection.DescriptionAttribute("agent is soft banned")]
            AGENT_IS_SOFT_BANNED,

            [Command.StatusAttribute(05762)]
            [Reflection.DescriptionAttribute("primitives already linked")]
            PRIMITIVES_ALREADY_LINKED,

            [Command.StatusAttribute(64420)]
            [Reflection.DescriptionAttribute("primitives are children of object")]
            PRIMITIVES_ARE_CHILDREN_OF_OBJECT,

            [Command.StatusAttribute(10348)]
            [Reflection.DescriptionAttribute("primitives already delinked")]
            PRIMITIVES_ALREADY_DELINKED,

            [Command.StatusAttribute(23932)]
            [Reflection.DescriptionAttribute("no position provided")]
            NO_POSITION_PROVIDED,

            [Command.StatusAttribute(01382)]
            [Reflection.DescriptionAttribute("unknwon sound requested")]
            UNKOWN_SOUND_FORMAT_REQUESTED,

            [Command.StatusAttribute(64423)]
            [Reflection.DescriptionAttribute("timeout getting folder contents")]
            TIMEOUT_GETTING_FOLDER_CONTENTS,

            [Command.StatusAttribute(15964)]
            [Reflection.DescriptionAttribute("transfer would exceed maximum count")]
            TRANSFER_WOULD_EXCEED_MAXIMUM_COUNT,

            [Command.StatusAttribute(36616)]
            [Reflection.DescriptionAttribute("invalid item type")]
            INVALID_ITEM_TYPE,

            [Command.StatusAttribute(38945)]
            [Reflection.DescriptionAttribute("no source specified")]
            NO_SOURCE_SPECIFIED,

            [Command.StatusAttribute(23716)]
            [Reflection.DescriptionAttribute("classified not found")]
            CLASSIFIED_NOT_FOUND,

            [Command.StatusAttribute(49113)]
            [Reflection.DescriptionAttribute("pick not found")]
            PICK_NOT_FOUND,

            [Command.StatusAttribute(22970)]
            [Reflection.DescriptionAttribute("timeout getting profile classified")]
            TIMEOUT_GETTING_PROFILE_CLASSIFIED,

            [Command.StatusAttribute(07168)]
            [Reflection.DescriptionAttribute("timeout getting profile pick")]
            TIMEOUT_GETTING_PROFILE_PICK,

            [Command.StatusAttribute(11979)]
            [Reflection.DescriptionAttribute("could not retrieve pick")]
            COULD_NOT_RETRIEVE_PICK,

            [Command.StatusAttribute(25420)]
            [Reflection.DescriptionAttribute("could not retrieve classified")]
            COULD_NOT_RETRIEVE_CLASSIFIED,

            [Command.StatusAttribute(61317)]
            [Reflection.DescriptionAttribute("invalid workers provided")]
            INVALID_WORKERS_PROVIDED,

            [Command.StatusAttribute(23570)]
            [Reflection.DescriptionAttribute("invalid schedules provided")]
            INVALID_SCHEDULES_PROVIDED,

            [Command.StatusAttribute(09703)]
            [Reflection.DescriptionAttribute("group not configured")]
            GROUP_NOT_CONFIGURED,

            [Command.StatusAttribute(31868)]
            [Reflection.DescriptionAttribute("no database path provided")]
            NO_DATABASE_PATH_PROVIDED,

            [Command.StatusAttribute(13030)]
            [Reflection.DescriptionAttribute("no chatlog path provided")]
            NO_CHATLOG_PATH_PROVIDED,

            [Command.StatusAttribute(42140)]
            [Reflection.DescriptionAttribute("group already configured")]
            GROUP_ALREADY_CONFIGURED,

            [Command.StatusAttribute(15517)]
            [Reflection.DescriptionAttribute("eject needs demote")]
            EJECT_NEEDS_DEMOTE,

            [Command.StatusAttribute(01488)]
            [Reflection.DescriptionAttribute("no dialog specified")]
            NO_DIALOG_SPECIFIED,

            [Command.StatusAttribute(55394)]
            [Reflection.DescriptionAttribute("no matching dialog found")]
            NO_MATCHING_DIALOG_FOUND,

            [Command.StatusAttribute(64179)]
            [Reflection.DescriptionAttribute("unable to serialize primitive")]
            UNABLE_TO_SERIALIZE_PRIMITIVE,

            [Command.StatusAttribute(58212)]
            [Reflection.DescriptionAttribute("platform not supported")]
            PLATFORM_NOT_SUPPORTED,

            [Command.StatusAttribute(16233)]
            [Reflection.DescriptionAttribute("invalid asset")]
            INVALID_ASSET,

            [Command.StatusAttribute(23123)]
            [Reflection.DescriptionAttribute("nucleus server error")]
            NUCLEUS_SERVER_ERROR,

            [Command.StatusAttribute(23114)]
            [Reflection.DescriptionAttribute("timeout waiting for sensor")]
            TIMEOUT_WAITING_FOR_SENSOR,

            [Command.StatusAttribute(39921)]
            [Reflection.DescriptionAttribute("could not set agent access")]
            COULD_NOT_SET_AGENT_ACCESS,

            [Command.StatusAttribute(29947)]
            [Reflection.DescriptionAttribute("unknown agent access")]
            UNKNOWN_AGENT_ACCESSS,

            [Command.StatusAttribute(61492)]
            [Reflection.DescriptionAttribute("unknown language")]
            UNKNOWN_LANGUAGE,

            [Command.StatusAttribute(38624)]
            [Reflection.DescriptionAttribute("invalid secret provided")]
            INVALID_SECRET_PROVIDED,

            [Command.StatusAttribute(58478)]
            [Reflection.DescriptionAttribute("could not get parcel resources")]
            COULD_NOT_GET_PARCEL_RESOURCES,

            [Command.StatusAttribute(62531)]
            [Reflection.DescriptionAttribute("could not get land resources")]
            COULD_NOT_GET_LAND_RESOURCES,

            [Command.StatusAttribute(27910)]
            [Reflection.DescriptionAttribute("timeout getting parcel list")]
            TIMEOUT_GETTING_PARCEL_LIST,

            [Command.StatusAttribute(53059)]
            [Reflection.DescriptionAttribute("could not update parcel list")]
            COULD_NOT_UPDATE_PARCEL_LIST,

            [Command.StatusAttribute(48110)]
            [Reflection.DescriptionAttribute("no history found")]
            NO_HISTORY_FOUND,

            [Command.StatusAttribute(36675)]
            [Reflection.DescriptionAttribute("no server provided")]
            NO_SERVER_PROVIDED,

            [Command.StatusAttribute(02021)]
            [Reflection.DescriptionAttribute("invalid version provided")]
            INVALID_VERSION_PROVIDED,

            [Command.StatusAttribute(54956)]
            [Reflection.DescriptionAttribute("timeout retrieving estate info")]
            TIMEOUT_RETRIEVING_ESTATE_INFO,

            [Command.StatusAttribute(38271)]
            [Reflection.DescriptionAttribute("script compilation failed")]
            SCRIPT_COMPILATION_FAILED
        }

        /// <summary>
        ///     Various types.
        /// </summary>
        public enum Type : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [Reflection.NameAttribute("text")]
            TEXT,

            [Reflection.NameAttribute("voice")]
            VOICE,

            [Reflection.NameAttribute("scripts")]
            SCRIPTS,

            [Reflection.NameAttribute("colliders")]
            COLLIDERS,

            [Reflection.NameAttribute("ban")]
            BAN,

            [Reflection.NameAttribute("group")]
            GROUP,

            [Reflection.NameAttribute("user")]
            USER,

            [Reflection.NameAttribute("manager")]
            MANAGER,

            [Reflection.NameAttribute("classified")]
            CLASSIFIED,

            [Reflection.NameAttribute("event")]
            EVENT,

            [Reflection.NameAttribute("land")]
            LAND,

            [Reflection.NameAttribute("people")]
            PEOPLE,

            [Reflection.NameAttribute("place")]
            PLACE,

            [Reflection.NameAttribute("input")]
            INPUT,

            [Reflection.NameAttribute("output")]
            OUTPUT,

            [Reflection.NameAttribute("slot")]
            SLOT,

            [Reflection.NameAttribute("name")]
            NAME,

            [Reflection.NameAttribute("UUID")]
            UUID,

            [Reflection.NameAttribute("xml")]
            XML,

            [Reflection.NameAttribute("zip")]
            ZIP,

            [Reflection.NameAttribute("path")]
            PATH
        }

        /// <summary>
        ///     Possible viewer effects.
        /// </summary>
        public enum ViewerEffectType : uint
        {
            [Reflection.NameAttribute("none")]
            NONE = 0,

            [Reflection.NameAttribute("look")]
            LOOK,

            [Reflection.NameAttribute("point")]
            POINT,

            [Reflection.NameAttribute("sphere")]
            SPHERE,

            [Reflection.NameAttribute("beam")]
            BEAM
        }

        /// <summary>
        ///     Possible webserver resources.
        /// </summary>
        public enum WebResource : uint
        {
            [Reflection.NameAttribute("cache")]
            CACHE,

            [Reflection.NameAttribute("mute")]
            MUTE,

            [Reflection.NameAttribute("softban")]
            SOFTBAN,

            [Reflection.NameAttribute("user")]
            USER,

            [Reflection.NameAttribute("bayes")]
            BAYES
        }

        /// <summary>
        ///     Possible outcomes of setting or retrieving the agent status.
        /// </summary>
        public enum ScriptedAgentStatusError
        {
            [Reflection.DescriptionAttribute("unable to authenticate")]
            UNABLE_TO_AUTHENTICATE,

            [Reflection.DescriptionAttribute("unable to reach scripted agent status page")]
            UNABLE_TO_REACH_SCRIPTED_AGENT_STATUS_PAGE,

            [Reflection.DescriptionAttribute("could not get scripted agent status")]
            COULD_NOT_GET_SCRIPTED_AGENT_STATUS,

            [Reflection.DescriptionAttribute("could not set scripted agent status")]
            COULD_NOT_SET_SCRIPTED_AGENT_STATUS,

            [Reflection.DescriptionAttribute("logout failed")]
            LOGOUT_FAILED,

            [Reflection.DescriptionAttribute("login failed")]
            LOGIN_FAILED
        }
    }
}
