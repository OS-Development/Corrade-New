///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wasSharp
{
    public class Cryptography
    {
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
        public static string wasEnigma(string message, char[] rotors, char[] plugs, char reflector)
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
                def_rotors[rotor] = Arrays.wasConcatenateArrays(new[] {plug},
                    Arrays.wasGetSubArray(Arrays.wasDeleteSubArray(def_rotors[rotor], i, i), i, -1),
                    Arrays.wasGetSubArray(Arrays.wasDeleteSubArray(def_rotors[rotor], i + 1, -1), 0, i - 1));
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
                        def_rotors[o[0]] = Arrays.wasForwardPermuteArrayElements(def_rotors[o[0]], 1);
                        if (i.Equals(0))
                        {
                            rotors = Arrays.wasReversePermuteArrayElements(o, 1);
                            continue;
                        }
                        l = Arrays.wasGetElementAt(def_rotors[o[1]], Array.IndexOf(def_rotors[o[0]], l) - 1);
                        o = Arrays.wasReversePermuteArrayElements(o, 1);
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
        public static string wasVigenereExpandKey(string input, string enc_key)
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
        public static string wasEncryptVIGENERE(string input, string enc_key)
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
                    Arrays.wasReversePermuteArrayElements(a, Array.IndexOf(a, enc_key[i]))[
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
        public static string wasDecryptVIGENERE(string input, string enc_key)
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
                        Array.IndexOf(Arrays.wasReversePermuteArrayElements(a, Array.IndexOf(a, enc_key[i])),
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
        public static string wasATBASH(string data)
        {
            char[] a =
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
            };

            char[] input = data.ToCharArray();

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
    }
}