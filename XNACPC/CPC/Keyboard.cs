// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Input; 

namespace XNACPC.CPC
{
    class Keyboard : Device
    {
        // Reference:
        // http://www.cpcwiki.eu/index.php/Programming:Keyboard_scanning

        private int m_current_row;
        private List<Joystick> m_joysticks;

        private const int KEYS_PER_ROW = 8;
        private const int MAX_JOYSTICKS_MAPPED = 4;
        
        private static readonly Keys[,] KEYBOARD_MAP = new Keys[,]
        { 
            { 
                Keys.Up,
                Keys.Right,
                Keys.Down,
                Keys.F9,
                Keys.F6,
                Keys.F3,
                Keys.RightControl,  //< Enter key, this is the little enter key on the CPC (I think?)
                Keys.None           //< Num pad '.' ?
            },
            {
                Keys.Left,
                Keys.Insert,        //< Copy key on CPC
                Keys.F7,
                Keys.F8,
                Keys.F5,
                Keys.F1,
                Keys.F2,
                Keys.F3
            },
            { 
                Keys.Delete,        //< Clear key on CPC
                Keys.OemOpenBrackets, 
                Keys.Enter,         //< The big enter/return key on the CPC
                Keys.OemCloseBrackets,
                Keys.F4,
                Keys.LeftShift,     //< One and only shift key on CPC
                Keys.OemBackslash,
                Keys.LeftControl,   //< One and only ctrl key on CPC
            },
            { 
                Keys.OemPlus,       //< Odd Up-Arrow, British Pound key
                Keys.OemMinus, 
                Keys.OemPipe,       //< @ and Pipe key on CPC
                Keys.P,
                Keys.OemSemicolon,
                Keys.OemQuotes,     //< : key on CPC
                Keys.OemQuestion,   //< Forwardslash on CPC
                Keys.OemPeriod
            },
            {
                Keys.D0,
                Keys.D9,
                Keys.O,
                Keys.I,
                Keys.L,
                Keys.K,
                Keys.M,
                Keys.OemComma,
            },
            {
                Keys.D8,
                Keys.D7,
                Keys.U,
                Keys.Y,
                Keys.H,
                Keys.J,
                Keys.N,
                Keys.Space
            },
            {
                Keys.D6,
                Keys.D5,
                Keys.R,
                Keys.T,
                Keys.G,
                Keys.F,
                Keys.B,
                Keys.V
            },
            {
                Keys.D4,
                Keys.D3,
                Keys.E,
                Keys.W,
                Keys.S,
                Keys.D,
                Keys.C,
                Keys.X
            },
            {
                Keys.D1,
                Keys.D2,
                Keys.Escape,
                Keys.Q,
                Keys.Tab,
                Keys.A,
                Keys.CapsLock,
                Keys.Z
            },
            {
                Keys.NumPad8,   //< Joystick Up
                Keys.NumPad2,   //< Joystick Down
                Keys.NumPad4,   //< Joystick Left
                Keys.NumPad6,   //< Joystick Right
                Keys.NumPad5,   //< Joystick Fire
                Keys.NumPad1,
                Keys.None,
                Keys.Back       //< Backspace
            }
        };

        public Keyboard()
        {
            m_joysticks = new List<Joystick>( MAX_JOYSTICKS_MAPPED );

            Reset();
        }

        public void Reset()
        {
            m_current_row = 0;
        }

        public void AssignJoystick( Joystick joystick )
        {
            m_joysticks.Add( joystick );
        }

        public void RemoveJoystick( Joystick joystick )
        {
            m_joysticks.Remove( joystick );
        }

        public void SetCurrentRow( int value )
        {
            m_current_row = value;
        }

        public int ReadCurrentRow()
        {
            if ( MenuInputComponent.MenuStillDebouncing() )
            {
                // Ignore keypresses if we just came from the menu system
                return 0xFF;
            }

            if ( m_current_row > KEYBOARD_MAP.GetUpperBound(0) )
            {
                return 0xFF;
            }

            // TODO: Read once per frame
            KeyboardState key_states = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            int return_value = 0xFF;

            for ( int i=0;i<KEYS_PER_ROW;i++ )
            {
                Keys key = KEYBOARD_MAP[ m_current_row, i ];

                if ( key_states.IsKeyDown( key ) )
                {
                    return_value &= ~( 1 << i );
                }

                // Horrible special case so that both shift keys work... The keyboard map logic is nice, so I'd hate to add
                // a third dimension to support multiple XNA-keys per CPC-key.
                if ( key == Keys.LeftShift )
                {
                    if ( key_states.IsKeyDown( Keys.RightShift ) )
                    {
                        return_value &= ~( 1 << i );
                    }
                }
            }

            foreach( Joystick joystick in m_joysticks )
            {
                joystick.ReadInput( ref return_value, m_current_row );
            }

            return return_value;
        }


    }
}
