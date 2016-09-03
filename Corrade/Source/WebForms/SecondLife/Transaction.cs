///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Xml.Serialization;
using OpenMetaverse;

namespace Corrade.WebForms.SecondLife
{
    /// <summary>
    ///     Second Life website transaction.
    /// </summary>
    [XmlRoot("transaction")]
    public class Transaction
    {
        [XmlElement("id")]
        public UUID ID { get; set; }

        [XmlElement("type")]
        public string Type { get; set; }

        [XmlElement("description")]
        public string Description { get; set; }

        [XmlElement("region")]
        public string Region { get; set; }

        [XmlElement("deposit")]
        public uint Deposit { get; set; }

        [XmlIgnore]
        public DateTime Time { get; set; }

        [XmlElement("time")]
        public string Timestamp
        {
            get { return Time.ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP); }
            set { Time = DateTime.Parse(value); }
        }

        [XmlElement("resident")]
        public string Resident { get; set; }

        [XmlElement("end_balance")]
        public uint EndBalance { get; set; }
    }
}