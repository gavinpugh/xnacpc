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

    public struct ChannelData
    {
        // Reference:
        // http://www.cpcwiki.eu/index.php/PSG

        private const int NUM_VOLUME_LEVELS = 16;
        public const int MAX_FINAL_SAMPLE_VALUE = 32767;

        private static readonly int[] AY_VOLUMES_A_C = new int[NUM_VOLUME_LEVELS];
        private static readonly int[] AY_VOLUMES_B = new int[NUM_VOLUME_LEVELS];

        public bool m_tone_enabled;
        public bool m_noise_enabled;
        public bool m_use_envelope_volume;

        public int m_volume;
        private int m_tone_period;
        private int m_tone_output;    //< Tone square wave output level
        private int m_tone_counter;   //< Tone square wave counter, counts up to 'm_tone_period'

        public int m_mix;             //< Output mix for this sample. There's multiple CPC updates per XNA sample.

        private int[] m_volume_lookup;

        static ChannelData()
        {
            // From this: http://www.cpcwiki.eu/index.php/PSG
            // amplitude = max / sqrt(2)^(15-nn) . Where nn is 0-15.
            for (int i = 0; i < NUM_VOLUME_LEVELS; i++)
            {
                int range = MAX_FINAL_SAMPLE_VALUE;
                int vol_val = (int)((double)range / Math.Pow(Math.Sqrt(2.0), NUM_VOLUME_LEVELS - 1 - i));

                Debug.Assert(vol_val >= 0);
                Debug.Assert(vol_val <= MAX_FINAL_SAMPLE_VALUE);

                // A and C use 2/3rds the full volume. B (which comes out both speakers), uses 1/3rd.
                // Also need to divide by the sample period, since we'll add the mix values together over that period.
                AY_VOLUMES_A_C[i] = ( ( vol_val * 2 ) / 3) / PSG.PSG_SAMPLE_PERIOD;
                AY_VOLUMES_B[i] = ( ( vol_val * 1 ) / 3 ) / PSG.PSG_SAMPLE_PERIOD;
            }

            int max = AY_VOLUMES_A_C[NUM_VOLUME_LEVELS - 1] + AY_VOLUMES_B[NUM_VOLUME_LEVELS - 1];
            Debug.Assert(max >= 0);
            Debug.Assert(max <= MAX_FINAL_SAMPLE_VALUE);
        }

        public void Reset(bool middle_channel)
        {
            m_tone_enabled = false;
            m_noise_enabled = false;
            m_use_envelope_volume = false;

            m_volume = 0;
            m_tone_period = 1;
            m_tone_output = 0;
            m_tone_counter = 0;

            m_mix = 0;
            m_volume_lookup = middle_channel ? AY_VOLUMES_B : AY_VOLUMES_A_C;
        }

        public void SetPeriod(int high_byte, int low_byte)
        {
            m_tone_period = (high_byte << 8) + low_byte;

            // A period of zero should be treated as one. Sounds good in "Friday the 13th".
            if (m_tone_period == 0)
            {
                m_tone_period = 1;
            }

            // Normalise counter based on current period. *2 is the entire square wave.
            if (m_tone_counter >= (m_tone_period * 2))
            {
                m_tone_counter %= (m_tone_period * 2);
            }
        }

        public void Update(int envelope_volume, int noise_output)
        {
            Debug.Assert(m_tone_period > 0);
            Debug.Assert(envelope_volume >= 0);
            Debug.Assert(envelope_volume < 16);
            Debug.Assert(m_volume >= 0);
            Debug.Assert(m_volume < 16);

            m_tone_counter++;

            // Square wave period met?
            if (m_tone_counter >= m_tone_period)
            {
                // Reverse signal, start new period
                m_tone_output ^= 0xff;
                m_tone_counter = 0;
            }
            
            // Output is 'high'?
            bool output = false;

            if (m_noise_enabled)
            {
                if (noise_output != 0)
                {
                    output = true;
                }
            }
            if (m_tone_enabled)
            {
                if (m_tone_output != 0)
                {
                    output = true;
                }
            }

            // Cnsider the square wave output for this channel
            if (output)
            {
                m_mix += m_volume_lookup[(m_use_envelope_volume ? envelope_volume : m_volume)];
            }
            else
            {
                m_mix -= m_volume_lookup[(m_use_envelope_volume ? envelope_volume : m_volume)];
            }
        }
    }
}
