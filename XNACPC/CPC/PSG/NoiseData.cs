// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    // NOTE: This is a struct, to force use of direct calls instead of vcalls.
    // NOTE: I'm getting desperate to get this thing running within 50fps on the Xbox 360
    // NOTE: So it's unfortunately more 'C'-like, than C#

    public struct NoiseData
    {
        public int m_output;
        public int m_period;
        private int m_counter;
        public int m_rng;

        public void Reset()
        {
            m_output = 0xff;
            m_rng = 1;
            m_counter = 0;
            m_period = 0;
        }

        public void Update()
        {
            m_counter++;
            if ( m_counter >= m_period )
            {
                // From http://mamedev.org/source/src/emu/sound/ay8910.c.html

                /* Is noise output going to change? */
                if ( ( ( m_rng + 1 ) & 2 ) != 0 ) /* (bit0^bit1)? */
                {
                    m_output ^= 0xFF;
                }

                /* The Random Number Generator of the 8910 is a 17-bit shift */
                /* register. The input to the shift register is bit0 XOR bit3 */
                /* (bit0 is the output). This was verified on AY-3-8910 and YM2149 chips. */

                /* The following is a fast way to compute bit17 = bit0^bit3. */
                /* Instead of doing all the logic operations, we only check */
                /* bit0, relying on the fact that after three shifts of the */
                /* register, what now is bit3 will become bit0, and will */
                /* invert, if necessary, bit14, which previously was bit17. */
                if ( ( m_rng & 1 ) != 0 )
                {
                    m_rng ^= 0x24000; /* This version is called the "Galois configuration". */
                }

                m_rng >>= 1;
                m_counter = 0;
            }
        }
    }
}
