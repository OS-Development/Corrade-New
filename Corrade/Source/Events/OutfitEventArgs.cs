///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using OpenMetaverse;

namespace Corrade.Events
{
    /// <summary>
    ///     An event for chaning outfits.
    /// </summary>
    public class OutfitEventArgs : EventArgs
    {
        public Enumerations.Action Action;
        public UUID Asset;
        public UUID Creator;
        public string Description;
        public AssetType Entity;
        public InventoryType Inventory;
        public UUID Item;
        public string Name;
        public string Permissions;
        public bool Replace;
        public string Slot;
    }
}
