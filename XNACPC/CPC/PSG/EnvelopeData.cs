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

    public struct EnvelopeData
    {
        // Reference:
        // http://www.cpcwiki.eu/index.php/PSG
        // http://www.cpctech.org.uk/docs/ay38912/psgspec.htm

        public int m_output_volume;
        private int m_period;
        private int m_counter;
        private int m_shape_position;
        private int[] m_current_shape;
        private bool m_should_hold;

        static readonly int[][] SHAPES;
        const int NUM_SHAPES = 16;
        const int NUM_VOLUME_LEVELS = 16;

        static EnvelopeData()
        {
            // Envelope shape data is from here:
            // http://www.cpctech.org.uk/docs/ay38912/psgspec.htm

            SHAPES = new int[NUM_SHAPES][];

            for ( int i = 0x00; i <= 0x03; i++ )
            {
                SHAPES[i] = new int[NUM_VOLUME_LEVELS] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            }
            for ( int i = 0x04; i <= 0x07; i++ )
            {
                SHAPES[i] = new int[NUM_VOLUME_LEVELS + 1] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0 };
            }
            for ( int i = 0x08; i <= 0x09; i++ )
            {
                SHAPES[i] = new int[NUM_VOLUME_LEVELS] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            }
            SHAPES[0x0a] = new int[NUM_VOLUME_LEVELS + NUM_VOLUME_LEVELS] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            SHAPES[0x0b] = new int[NUM_VOLUME_LEVELS + 1] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 15 };

            for ( int i = 0x0c; i <= 0x0d; i++ )
            {
                SHAPES[i] = new int[NUM_VOLUME_LEVELS] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            }
            SHAPES[0x0e] = new int[NUM_VOLUME_LEVELS + NUM_VOLUME_LEVELS] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            SHAPES[0x0f] = new int[NUM_VOLUME_LEVELS + 1] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0 };
        }

        public void Reset()
        {
            m_output_volume = 0;
            m_period = 1;
            m_counter = 0;
            m_shape_position = 0;
            m_current_shape = SHAPES[0];
            m_should_hold = true;
        }

        public void SetPeriod( int high_byte, int low_byte )
        {
            m_period = ( high_byte << 8 ) + low_byte;
        }

        public void SetShape( int shape )
        {
            Debug.Assert( shape < 16 );
            Debug.Assert( shape >= 0 );
            m_current_shape = SHAPES[shape];
            m_shape_position = 0;

            m_should_hold = ( ( shape & 0x01 ) != 0 );

            // First eight shapes always hold
            if ( shape < 8 )
            {
                m_should_hold = true;
            }

            // Should it do this? Or wait for the first update? Or maybe the first period to be met?
            m_output_volume = m_current_shape[0];
        }

        public void Update()
        {
            m_counter++;
            if ( m_counter >= m_period )
            {
                // Met period, so move shape position along one, to the next volume level.
                m_counter = 0;
                m_output_volume = m_current_shape[m_shape_position];

                m_shape_position++;
                if ( m_shape_position == m_current_shape.Length )
                {
                    if ( m_should_hold )
                    {
                        // Hold, just back one, so that effectively the last index is repeated over and over.
                        m_shape_position--;
                    }
                    else
                    {
                        // Not holding, repeating the whole thing again
                        m_shape_position = 0;
                    }
                }
            }
        }
    }
}
