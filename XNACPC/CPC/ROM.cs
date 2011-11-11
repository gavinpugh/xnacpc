// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework.Content;

namespace XNACPC.CPC
{
    unsafe struct ROMBuffer
    {
        public fixed byte _buffer[ROM.ROM_SIZE];
    }

    // NOTE: You may notice that the 'Memory' class doesn't use unsafe code. In my profiling the ROM change to unsafe code was a slight
    // NOTE: improvement. When I did the same to the 'Memory' class, I had pretty nasty crashes which brought down the debugger with it too.
    // NOTE: Perhaps it's possible to get that working with an unsafe buffer too. Something to consider.

    class ROM
    {
        private ROMBuffer m_data;
        private int m_size;
        public const int ROM_SIZE = ( 16 * 1024 ); //< CPC ROMs are all 16k
        public const int LOWER_ROM_INDEX = 255;
        public const int BASIC_ROM_INDEX = 0;

        unsafe public ROM( byte[] rom_file, int offset, int size )
        {
            m_data = new ROMBuffer();

            Debug.Assert( size <= ROM_SIZE );
            m_size = size;
            fixed (byte* p_data = m_data._buffer)
            {
                for (int i = 0; i < ROM_SIZE; i++)
                {
                    p_data[i] = rom_file[offset + i];
                }
            }
        }

        unsafe public byte Read( int location )
        {
            Debug.Assert( location >= 0 );
            Debug.Assert( location < ROM_SIZE );
            
            fixed( byte* p_data = m_data._buffer )
            {
                return p_data[location];
            }
        }
    }
}
