///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using wasSharp;

namespace wasOpenMetaverse
{
    public static class Graphics
    {
        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Function that scales a supplied texture size to a size compliant with the Linden texture specifications.
        /// </summary>
        /// <param name="width">the original width of the image</param>
        /// <param name="height">the original height of the image</param>
        /// <returns>a texture size object with the scaled width and height</returns>
        public static TextureSize GetScaleTextureSize(int width, int height)
        {
            // if the size is too large, scale it down.
            if (width > Constants.TEXTURES.MAX_PIXEL_WIDTH || height > Constants.TEXTURES.MAX_PIXEL_HEIGHT)
            {
                var ratio = Math.Min(Constants.TEXTURES.MAX_PIXEL_WIDTH/width,
                    Constants.TEXTURES.MAX_PIXEL_HEIGHT/height);
                width = width*ratio;
                height = height*ratio;
            }

            return new TextureSize
            {
                Width = width.IsPowerOfTwo() ? width : width.PreviousPowerOfTwo(),
                Height = height.IsPowerOfTwo() ? height : height.PreviousPowerOfTwo()
            };
        }

        public struct TextureSize
        {
            public int Width;
            public int Height;
        }
    }
}