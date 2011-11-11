// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class PSG : Device
    {
        // Reference:
        // http://www.cpcwiki.eu/index.php/PSG
        // http://www.cpcwiki.eu/index.php/How_to_access_the_PSG_via_PPI
        // http://www.cpctech.org.uk/docs/psg.html
        // http://www.cpctech.org.uk/docs/psgnotes.htm
        // http://www.cpctech.org.uk/docs/ay38912/psgspec.htm

        private enum Function
        {
            Inactive = 0,
            ReadRegister,
            WriteRegister,
            SelectRegister
        }

        public enum Register : int
        {
            ChannelAToneFreqLow8Bit = 0,
            ChannelAToneFreqHigh4Bit,
            ChannelBToneFreqLow8Bit,
            ChannelBToneFreqHigh4Bit,
            ChannelCToneFreqLow8Bit,
            ChannelCToneFreqHigh4Bit,
            NoiseFrequency,
            MixerControl,
            ChannelAVolume,
            ChannelBVolume,
            ChannelCVolume,
            VolumeEnvFreqLow,
            VolumeEnvFreqHigh,
            VolumeEnvShape,
            ExternalDataPortA,  //< Keyboard
            ExternalDataPortB   //< Unused
        }

        private const int BUFFER_SIZE = (Audio.BYTES_PER_SAMPLE * Audio.SAMPLE_RATE);   //< Enough for one second of audio

        public const int NUM_REGISTERS = 16;
        private const int NUM_CHANNELS = 3;
        private const int ENVELOPE_CLOCK_UPDATE_MASK = 0xf;
        private const int TONE_NOISE_CLOCK_UPDATE_MASK = 0x7;

        public const int AUDIO_SAMPLING_RATE = (TONE_NOISE_CLOCK_UPDATE_MASK + 1);

        private const int MIN_FINAL_SAMPLE_VALUE = short.MinValue;
        private const int MAX_FINAL_SAMPLE_VALUE = short.MaxValue;

        // CPC audio runs at ~125khz. XNA audio doesn't go that high. I run it at 1/3rd of that. ~41.6khz 
        private const int PSG_SAMPLE_HZ = Emulator.CLOCK_UPDATE_HZ / AUDIO_SAMPLING_RATE;   //< Sample at every tone update
        public const int PSG_SAMPLE_PERIOD = PSG_SAMPLE_HZ / Audio.SAMPLE_RATE;             //< Calc number of CPC samples per XNA Audio sample

        // Ensure the sampling rate numbers are evenly divisible
        const byte STATIC_ASSERT_CHECK = ((Audio.SAMPLE_RATE * PSG_SAMPLE_PERIOD) == PSG_SAMPLE_HZ) ? 0 : -1;

        private static readonly int[] PSG_MASKS = new int[NUM_REGISTERS] {
            0xFF, // ChannelAToneFreqLow8Bit
            0x0F, // ChannelAToneFreqHigh4Bit
            0xFF, // ChannelBToneFreqLow8Bit
            0x0F, // ChannelBToneFreqHigh4Bit
            0xFF, // ChannelCToneFreqLow8Bit 
            0x0F, // ChannelCToneFreqHigh4Bit
            0x1F, // NoiseFrequency
            0xFF, // MixerControl
            0x1F, // ChannelAVolume
            0x1F, // ChannelBVolume
            0x1F, // ChannelCVolume
            0xFF, // VolumeEnvFreqLow
            0xFF, // VolumeEnvFreqHigh
            0x0F, // VolumeEnvShape
            0xFF, // ExternalDataPortA
            0xFF  // ExternalDataPortB
        };

        private Function m_function;
        private Register m_selected_register;
        private int[] m_register_values;

        private Keyboard m_keyboard;
        private Audio m_audio;

        private int m_psg_clock_count;
        private bool m_port_a_input;
        private bool m_port_b_input;

        private ChannelData m_channel_a;
        private ChannelData m_channel_b;
        private ChannelData m_channel_c;
        private NoiseData m_noise;
        private EnvelopeData m_envelope;

        private int m_sampling_counter;

        private byte[] m_output_buffer;
        private int m_output_buffer_pos;


        public PSG(Audio audio, Keyboard keyboard)
        {
            m_audio = audio;
            m_keyboard = keyboard;

            m_register_values = new int[NUM_REGISTERS];
            m_output_buffer = new byte[BUFFER_SIZE];

            Reset();
        }

        public void Reset()
        {
            m_selected_register = Register.ChannelAToneFreqLow8Bit;
            m_port_a_input = true;
            m_port_b_input = true;

            for (int i = 0; i < NUM_REGISTERS; i++)
            {
                m_register_values[i] = 0;
            }

            m_function = Function.Inactive;

            m_channel_a.Reset(false);
            m_channel_b.Reset(true);
            m_channel_c.Reset(false);
            m_noise.Reset();
            m_envelope.Reset();
            m_output_buffer_pos = 0;
            m_sampling_counter = 0;
        }

        public Keyboard Keyboard
        {
            get { return m_keyboard; }
        }


        public void WriteRegister(Register register, int value)
        {
            // Use correct value masking. A real CPC ignores certain bits.
            value &= PSG_MASKS[(int)register];
            m_register_values[(int)register] = value;

            switch (register)
            {
                case Register.MixerControl:
                    {
                        m_channel_a.m_tone_enabled = ((value & 0x01) == 0);
                        m_channel_b.m_tone_enabled = ((value & 0x02) == 0);
                        m_channel_c.m_tone_enabled = ((value & 0x04) == 0);

                        m_channel_a.m_noise_enabled = ((value & 0x08) == 0);
                        m_channel_b.m_noise_enabled = ((value & 0x10) == 0);
                        m_channel_c.m_noise_enabled = ((value & 0x20) == 0);

                        m_port_a_input = ((value & 0x40) == 0);
                        m_port_b_input = ((value & 0x80) == 0);
                    }
                    break;

                case Register.ChannelAVolume:
                    {
                        m_channel_a.m_volume = (value & 0x0f);
                        m_channel_a.m_use_envelope_volume = ((value & 0x10) != 0);
                    }
                    break;

                case Register.ChannelBVolume:
                    {
                        m_channel_b.m_volume = (value & 0x0f);
                        m_channel_b.m_use_envelope_volume = ((value & 0x10) != 0);
                    }
                    break;

                case Register.ChannelCVolume:
                    {
                        m_channel_c.m_volume = (value & 0x0f);
                        m_channel_c.m_use_envelope_volume = ((value & 0x10) != 0);
                    }
                    break;

                case Register.ChannelAToneFreqLow8Bit:
                case Register.ChannelAToneFreqHigh4Bit:
                    {
                        m_channel_a.SetPeriod(
                            m_register_values[(int)Register.ChannelAToneFreqHigh4Bit],
                            m_register_values[(int)Register.ChannelAToneFreqLow8Bit]);
                    }
                    break;

                case Register.ChannelBToneFreqLow8Bit:
                case Register.ChannelBToneFreqHigh4Bit:
                    {
                        m_channel_b.SetPeriod(
                            m_register_values[(int)Register.ChannelBToneFreqHigh4Bit],
                            m_register_values[(int)Register.ChannelBToneFreqLow8Bit]);
                    }
                    break;

                case Register.ChannelCToneFreqLow8Bit:
                case Register.ChannelCToneFreqHigh4Bit:
                    {
                        m_channel_c.SetPeriod(
                            m_register_values[(int)Register.ChannelCToneFreqHigh4Bit],
                            m_register_values[(int)Register.ChannelCToneFreqLow8Bit]);
                    }
                    break;

                case Register.NoiseFrequency:
                    {
                        m_noise.m_period = value;
                    }
                    break;

                case Register.VolumeEnvFreqLow:
                case Register.VolumeEnvFreqHigh:
                    {
                        m_envelope.SetPeriod(
                            m_register_values[(int)Register.VolumeEnvFreqHigh],
                            m_register_values[(int)Register.VolumeEnvFreqLow]);
                    }
                    break;

                case Register.VolumeEnvShape:
                    {
                        m_envelope.SetShape(value);
                    }
                    break;

                default:
                    break;
            }
        }

        unsafe public void OnClock()
        {
            // Called every clock: 1MHz.
            m_psg_clock_count++;

            // Tone update is every 8 clocks. This is the effective frequency of the CPC audio. 125khz.
            if ((m_psg_clock_count & TONE_NOISE_CLOCK_UPDATE_MASK) == 0)
            {
                // Envelope update is every 16 clocks
                if ((m_psg_clock_count & ENVELOPE_CLOCK_UPDATE_MASK) == 0)
                {
                    m_envelope.Update();
                }

                m_noise.Update();

                // Channels also do the mixing/output, so have to come last.
                m_channel_a.Update(m_envelope.m_output_volume, m_noise.m_output);
                m_channel_b.Update(m_envelope.m_output_volume, m_noise.m_output);
                m_channel_c.Update(m_envelope.m_output_volume, m_noise.m_output);

                // Multiple CPC samples, per XNA audio sample.
                m_sampling_counter++;
                if (m_sampling_counter >= PSG_SAMPLE_PERIOD)
                {
                    // Ready to write out an XNA audio sample...
                    m_sampling_counter = 0;
                    
                    // Check we're all within range
                    Debug.Assert( m_channel_a.m_mix >= MIN_FINAL_SAMPLE_VALUE );
                    Debug.Assert( m_channel_a.m_mix <= MAX_FINAL_SAMPLE_VALUE );
                    Debug.Assert( m_channel_b.m_mix >= MIN_FINAL_SAMPLE_VALUE );
                    Debug.Assert( m_channel_b.m_mix <= MAX_FINAL_SAMPLE_VALUE );
                    Debug.Assert( m_channel_c.m_mix >= MIN_FINAL_SAMPLE_VALUE );
                    Debug.Assert( m_channel_c.m_mix <= MAX_FINAL_SAMPLE_VALUE );

                    // Left speaker should use Channel C + B, Right should use A + B.
                    int left_sample = m_channel_c.m_mix + m_channel_b.m_mix;
                    int right_sample = m_channel_a.m_mix + m_channel_b.m_mix;

                    // Clear mix for next sampling set
                    m_channel_a.m_mix = 0;
                    m_channel_b.m_mix = 0;
                    m_channel_c.m_mix = 0;

                    // Check cumulative numbers are within range too
                    Debug.Assert(left_sample >= MIN_FINAL_SAMPLE_VALUE);
                    Debug.Assert(left_sample <= MAX_FINAL_SAMPLE_VALUE);
                    Debug.Assert(right_sample >= MIN_FINAL_SAMPLE_VALUE);
                    Debug.Assert(right_sample <= MAX_FINAL_SAMPLE_VALUE);
                    
                    // Cast to unsigned, for packing in the buffer.
                    ushort final_left = (ushort)left_sample;
                    ushort final_right = (ushort)right_sample;

                    Debug.Assert(( m_output_buffer_pos + Audio.BYTES_PER_SAMPLE ) < BUFFER_SIZE);

                    // Could use BitConverter.IsLittleEndian... But I'm preferring #if as I don't want to leave optimization down to chance.
                    // If shifts are too slow on 360, try using a lookup table instead?
                    fixed (byte* p_buffer = m_output_buffer)
                    {
#if WINDOWS
                        p_buffer[m_output_buffer_pos++] = (byte)(final_left & 0xFF);
                        p_buffer[m_output_buffer_pos++] = (byte)((final_left >> 8) & 0xFF);
                        p_buffer[m_output_buffer_pos++] = (byte)(final_right & 0xFF);
                        p_buffer[m_output_buffer_pos++] = (byte)((final_right >> 8) & 0xFF);
#else // WINDOWS
                        p_buffer[m_output_buffer_pos++] = (byte)((final_left >> 8) & 0xFF);
                        p_buffer[m_output_buffer_pos++] = (byte)(final_left & 0xFF);
                        p_buffer[m_output_buffer_pos++] = (byte)((final_right >> 8) & 0xFF);
                        p_buffer[m_output_buffer_pos++] = (byte)(final_right & 0xFF);
#endif //
                    }
                }
            }
        }

        public void OnFrame()
        {
            // Called every frame. Doesn't necessarily do work. Only if we've enough data in the buffer.
            if (m_audio.TrySubmitBuffer(m_output_buffer, m_output_buffer_pos))
            {
                m_output_buffer_pos = 0;
            }
        }

        public void SelectRegister(Register register)
        {
            m_selected_register = register;
        }

        public void SetFunction(int value)
        {
            // Top two bits are the function. Between 0 and 3.
            int bits = ((value & 0xC0) >> 6);
            Debug.Assert(bits >= 0 && bits < 4);

            m_function = (Function)bits;
        }

        public void WriteValue(int value)
        {
            switch (m_function)
            {
                case Function.WriteRegister:
                    {
                        WriteRegister(m_selected_register, value);
                    }
                    break;

                case Function.SelectRegister:
                    {
                        // Lower nybble is the register
                        int bits = (value & 0x0F);
                        SelectRegister((Register)bits);
                    }
                    break;

                default:
                    break;
            }
        }

        public int GetValue(int ppi_channel_a)
        {
            if (m_function == Function.ReadRegister)
            {
                return ReadSelectedRegister();
            }
            return ppi_channel_a;
        }

        private int ReadSelectedRegister()
        {
            switch (m_selected_register)
            {
                case Register.ExternalDataPortB:
                    {
                        if (m_port_b_input)
                        {
                            return m_register_values[(int)m_selected_register];
                        }
                    }
                    break;

                case Register.ExternalDataPortA:
                    {
                        if (m_port_a_input)
                        {
                            // read keyboard matrix node status
                            return m_keyboard.ReadCurrentRow();
                        }
                        else
                        {
                            // return last value w/ logic AND of input
                            return m_keyboard.ReadCurrentRow() & m_register_values[(int)m_selected_register];
                        }
                    }
            }

            return m_register_values[(int)m_selected_register];
        }

    }
}
