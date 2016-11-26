///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Xml;
using OpenMetaverse;

namespace wasOpenMetaverse
{
    /// <summary>
    ///     Linden constants.
    /// </summary>
    public static class Constants
    {
        public struct ALERTS
        {
            public const string NO_ROOM_TO_SIT_HERE = @"No room to sit here, try another spot.";

            public const string UNABLE_TO_SET_HOME =
                @"You can only set your 'Home Location' on your land or at a mainland Infohub.";

            public const string HOME_SET = @"Home position set.";
        }

        public struct INVENTORY
        {
            public const int MAXIMUM_FOLDER_TRANSFER_ITEM_COUNT = 42;
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
            public static Vector3 DEFAULT_NEW_PRIMITIVE_SCALE = new Vector3(0.5f, 0.5f, 0.5f);
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
            public const uint MAXIMUM_GROUP_BANS = 500;
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

        public struct EVENTS
        {
            public const uint MAXIMUM_EVENT_DESCRIPTION_LENGTH = 1024;
        }

        public struct PARTNERSHIP
        {
            public const uint MAXIMUM_PROPOSAL_MESSAGE_LENGTH = 254;
        }

        public struct TEXTURES
        {
            public static readonly UUID TEXTURE_BLANK = new UUID(@"5748decc-f629-461c-9a36-a35a221fe21f");
            public static readonly UUID TEXTURE_PLYWOOD = new UUID(@"89556747-24cb-43ed-920b-47caed15465f");
            public static readonly UUID TEXTURE_TRANSPARENT = new UUID(@"8dcd4a48-2d37-4909-9f78-f7a9eb4ef903");
            public static readonly UUID TEXTURE_MEDIA = new UUID(@"8b5fec65-8d8d-9dc5-cda8-8fdf2716e361");
            // Default texture in Second Life is the plywood texture.
            public static readonly UUID TEXTURE_DEFAULT = TEXTURE_BLANK;
            public static readonly UUID DEFAULT_SCULPT = new UUID(@"be293869-d0d9-0a69-5989-ad27f1946fd4");
            public static readonly int MAX_PIXEL_WIDTH = 1024;
            public static readonly int MAX_PIXEL_HEIGHT = 1024;
        }

        public struct OAR
        {
            public static readonly string ARCHIVE_FILE_NAME = @"archive.xml";

            public static readonly string ARCHIVE_FILE_CONTENT =
                "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<archive major_version=\"0\" minor_version=\"1\" />";

            public static readonly string OBJECT_FILE_PREFIX = @"Primitive_";
            public static readonly Formatting OBJECT_FILE_XML_FORMATTING = Formatting.Indented;
            public static readonly int OBJECT_FILE_XML_INDENTATION = 4;
            public static readonly char OBJECT_FILE_XML_INDENT_CHAR = ' ';
        }
    }
}