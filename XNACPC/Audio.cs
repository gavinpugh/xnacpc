// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;


namespace XNACPC
{
	class Audio
    {
        private const int CPC_SAMPLES_PER_XNA_SAMPLE = 3;
        public const int SAMPLE_RATE = (CPC.Emulator.CLOCK_UPDATE_HZ / CPC.PSG.AUDIO_SAMPLING_RATE / CPC_SAMPLES_PER_XNA_SAMPLE); //< 41600 Hz. Divides into the CPC clock cleanly.
        public const int BYTES_PER_SAMPLE = 4;

#if XBOX
        // Buffering up a bit more seems to help Xbox
        private const int FRAMES_TO_BUFFER = 8;
#else // XBOX
        private const int FRAMES_TO_BUFFER = 4;
#endif // XBOX

        private const int SUBMIT_SIZE = (SAMPLE_RATE * BYTES_PER_SAMPLE * 4) / CPC.Emulator.TARGET_FPS;
        private const int MAX_OVERLAPPED_SUBMITS = 4;                           //< Overlapped submits should only ever get this high if the game is running without frame limiting                 

		private DynamicSoundEffectInstance m_sfx;
		
		public Audio()
		{
			m_sfx = new DynamicSoundEffectInstance( SAMPLE_RATE, AudioChannels.Stereo );
			m_sfx.Play();
		}

		public void Reset()
		{
			m_sfx.Stop();
			m_sfx.Play();
		}
				 		
		public bool TrySubmitBuffer( byte[] buffer, int buffer_size )
		{
			if ( buffer_size >= SUBMIT_SIZE )
			{
                if (m_sfx.PendingBufferCount > MAX_OVERLAPPED_SUBMITS)
				{
					// This happens if the game runs too fast compared to all the math assumptions made.
					// Effectively it's if the game update goes faster than 'Emulator.TARGET_FPS'. It will Stop current samples playing, so
					// that any new ones can be pushed in.
					// Without this, the DynamicSoundEffectInstance can reach it's sample limit. Which strangely just kills the game and
					// debugger without any sort of exception, assert or error. Even on PC.
					m_sfx.Stop();
				}
				m_sfx.SubmitBuffer( buffer, 0, buffer_size );
				m_sfx.Play();

				return true;
			}

			return false;
		}
				
	}
}
