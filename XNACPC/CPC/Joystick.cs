// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace XNACPC.CPC
{
    class Joystick : Device
    {
        // Reference:
        // http://www.cpcwiki.eu/index.php/Programming:Keyboard_scanning

        private int m_joystick_index;
        private PlayerIndex m_xna_gamepad_index;
        
        public const int MAX_JOYSTICKS = 2;
        private const int NUM_JOYSTICK_KEYS = 6;
        private const int XNA_BUTTONS_PER_KEY = 2;

        private static readonly Buttons[,] JOYSTICK_KEY_BIT_MAPPING = new Buttons[NUM_JOYSTICK_KEYS, XNA_BUTTONS_PER_KEY] {
            { Buttons.DPadUp, Buttons.LeftThumbstickUp },
            { Buttons.DPadDown, Buttons.LeftThumbstickDown },
            { Buttons.DPadLeft, Buttons.LeftThumbstickLeft },
            { Buttons.DPadRight, Buttons.LeftThumbstickRight },
            { Buttons.A, Buttons.A },   //< Fire1
            { Buttons.B, Buttons.B }    //< Fire2
        };

        private static readonly int[] JOYSTICK_KEY_ROWS = new int[] {
            0x09,
            0x06
        };

        public Joystick( int joystick_index, PlayerIndex xna_gamepad_index )
        {
            m_joystick_index = joystick_index;
            m_xna_gamepad_index = xna_gamepad_index;
        }

        public void Reset()
        {
            // No state data to reset. Everything is dealt with upon each ReadInput().
        }

        public void ReadInput( ref int keyboard_read_data, int keyboard_row )
        {
            if ( MenuInputComponent.MenuStillDebouncing() )
            {
                // Ignore keypresses if we just came from the menu system
                return;
            }

            // Matches the row this joystick is mapped to?
            if ( keyboard_row == JOYSTICK_KEY_ROWS[m_joystick_index] )
            {
                // Grab XNA pad state
                // TODO: Read once per frame
                GamePadState pad_state = GamePad.GetState( m_xna_gamepad_index );

                for ( int i = 0; i < NUM_JOYSTICK_KEYS; i++ )
                {
                    // Check each button combo
                    for ( int j = 0; j < XNA_BUTTONS_PER_KEY; j++ )
                    {
                        // If pressed, unset this bit
                        if ( pad_state.IsButtonDown( JOYSTICK_KEY_BIT_MAPPING[i, j] ) )
                        {
                            keyboard_read_data &= ( ~( 1 << i ) ); // Indice here is also the bit to unset in the keyboard matrix
                            break;
                        }
                    }
                }
            }

#if XBOX
            // HACK: Hack to map enter and space onto Xbox controlpads
            const int ENTER_KEY_ROW = 0x02;
            const int ENTER_KEY_BIT = 0x02;
            const int SPACE_KEY_ROW = 0x05;
            const int SPACE_KEY_BIT = 0x07;

            if ( keyboard_row == ENTER_KEY_ROW )
            {
                if ( GamePad.GetState( m_xna_gamepad_index ).IsButtonDown( Buttons.Y ) )
                {
                    keyboard_read_data &= ( ~( 1 << ENTER_KEY_BIT ) );
                }
            }
            if ( keyboard_row == SPACE_KEY_ROW )
            {
                if ( GamePad.GetState( m_xna_gamepad_index ).IsButtonDown( Buttons.X ) )
                {
                    keyboard_read_data &= ( ~( 1 << SPACE_KEY_BIT ) );
                }
            }
#endif // XBOX
        }
    }
}
