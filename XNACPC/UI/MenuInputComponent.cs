// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace XNACPC
{
    class MenuInputComponent : GameComponent
    {
        static long m_debounce = 0;
        static bool m_enabled = false;
        static EMenuInput m_last_input = EMenuInput.None;

        const long DEBOUNCE_TIME = 150;
        const long DEBOUNCE_ENTRY_TIME = 500;

        public enum EMenuInput
        {
            None,

            Start,
            Up,
            Down,
            Select,
            Back
        };

        public MenuInputComponent( Game game )
            : base( game )
        {

        }

        public static void Enable()
        {
            m_enabled = true;
            m_last_input = EMenuInput.None;

            // Debounce going in, so we don't carry over button presses into menus
            m_debounce = DEBOUNCE_ENTRY_TIME;
        }

        public static void Disable()
        {
            m_enabled = false;
            m_last_input = EMenuInput.None;
        }

        public static bool MenuStillDebouncing()
        {
            return ( m_debounce > 0 );
        }

        public static EMenuInput PollInput()
        {
            if ( m_last_input != EMenuInput.None )
            {
                EMenuInput ret_input = m_last_input;
                m_last_input = EMenuInput.None;
                m_debounce = DEBOUNCE_TIME;
                return ret_input;
            }
            return EMenuInput.None;
        }
        
        public override void Update( GameTime gameTime )
        {
            EMenuInput new_input = EMenuInput.None;

            if ( m_enabled )
            {
                GamePadState gamePad = GamePad.GetState( PlayerIndex.One );
                KeyboardState keyboard = Keyboard.GetState();

                if ( ( gamePad.ThumbSticks.Left.Y > 0.2f ) ||
                        ( gamePad.ThumbSticks.Right.Y > 0.2f ) ||
                        ( gamePad.DPad.Up == ButtonState.Pressed ) ||
                        ( keyboard.IsKeyDown( Keys.Up ) ) )
                {
                    new_input = EMenuInput.Up;
                }
                else if ( ( gamePad.ThumbSticks.Left.Y < -0.2f ) ||
                        ( gamePad.ThumbSticks.Right.Y < -0.2f ) ||
                        ( gamePad.DPad.Down == ButtonState.Pressed ) ||
                        ( keyboard.IsKeyDown( Keys.Down ) ) )
                {
                    new_input = EMenuInput.Down;
                }
                else if ( ( gamePad.Buttons.A == ButtonState.Pressed ) ||
                       keyboard.IsKeyDown( Keys.Space ) ||
                       keyboard.IsKeyDown( Keys.Enter ) )
                {
                    new_input = EMenuInput.Select;
                }
                else if ( ( gamePad.Buttons.B == ButtonState.Pressed ) ||
                      ( gamePad.Buttons.Back == ButtonState.Pressed ) ||
                       keyboard.IsKeyDown( Keys.Escape ) ||
                       keyboard.IsKeyDown( Keys.Back ) )
                {
                    new_input = EMenuInput.Back;
                }
                else if ( ( gamePad.Buttons.Start == ButtonState.Pressed ) ||
                       keyboard.IsKeyDown( Keys.F1 ) )
                {
                    new_input = EMenuInput.Start;
                }

                if ( new_input == EMenuInput.None )
                {
                    // Nothing pressed. Reset the debounce so that tapping keys is still nice and fast.
                    m_debounce = 0;
                }
                else if ( m_debounce == 0 )
                {
                    // Register this reading
                    m_last_input = new_input;
                }
            }

            // Decay the debounce value. Ignore this new reading
            if ( m_debounce > 0 )
            {
                m_debounce -= gameTime.ElapsedGameTime.Milliseconds;
                if ( m_debounce < 0 )
                {
                    m_debounce = 0;
                }
            }
        }

    }
}
