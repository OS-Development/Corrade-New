///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using wasSharp;

namespace wasOpenMetaverse
{
    public static class OSD
    {
        /// <summary>
        ///     Map non-specification compliant nodes to the correct values.
        /// </summary>
        private static readonly Dictionary<string, string> OSDSanitizeMap = new Dictionary<string, string>
        {
            {"float", "real"}
        };

        /// <summary>
        ///     Converts an XML string to an OSD object whilst sanitizing the XML-LLSD
        /// </summary>
        /// <param name="data">the XML string to covnert</param>
        /// <returns>the OSD object</returns>
        public static OpenMetaverse.StructuredData.OSD XMLToOSD(string data)
        {
            XDocument doc;
            using (var reader = new StringReader(data))
            {
                doc = XDocument.Load(reader);
            }

            OSDSanitizeMap.AsParallel().ForAll(o => { XML.RenameNodes(doc.Root, o.Key, o.Value); });

            using (var memoryStream = new MemoryStream())
            {
                using (TextWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8))
                {
                    doc.Save(textWriter);
                }
                data = Utils.BytesToString(memoryStream.ToArray());
            }

            return OSDParser.DeserializeLLSDXml(data);
        }

        /// <summary>
        ///     Get all the OSD descendants of an OSD map.
        /// </summary>
        /// <param name="map">the OSD map to get descendants from</param>
        /// <returns>plain OSD objects</returns>
        public static IEnumerable<OpenMetaverse.StructuredData.OSD> OSDMapDescendants(OSDMap map)
        {
            foreach (KeyValuePair<string, OpenMetaverse.StructuredData.OSD> kvp in map)
            {
                yield return kvp.Key;
                switch (kvp.Value.Type)
                {
                    case OSDType.Map:
                        foreach (var osd in OSDMapDescendants(kvp.Value as OSDMap))
                        {
                            yield return osd;
                        }
                        break;
                    default:
                        yield return kvp.Value;
                        break;
                }
            }
        }

        /// <summary>
        ///     Get the value of key from an OSD map.
        /// </summary>
        /// <param name="key">the key name to get</param>
        /// <param name="map">the OSD map to search</param>
        /// <returns>an enumerable of keys by values</returns>
        public static IEnumerable<string> OSDMapGet(string key, OSDMap map)
        {
            foreach (KeyValuePair<string, OpenMetaverse.StructuredData.OSD> kvp in map)
            {
                if (kvp.Key.Equals(key))
                {
                    yield return key;
                    switch (kvp.Value.Type)
                    {
                        case OSDType.Map:
                            foreach (var osd in OSDMapDescendants(kvp.Value as OSDMap))
                            {
                                yield return osd.ToString();
                            }
                            break;
                        default:
                            yield return kvp.Value;
                            break;
                    }
                }

                if (kvp.Value is OSDMap)
                {
                    foreach (var data in OSDMapGet(key, kvp.Value as OSDMap))
                    {
                        yield return data;
                    }
                }
            }
        }
    }
}