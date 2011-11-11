// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace XNACPC
{
    public class ListMenuComponent : Microsoft.Xna.Framework.DrawableGameComponent
    {
        public delegate void MenuCallback( int chosen_index );

        SpriteBatch m_sprite_batch;
        SpriteFont m_sprite_font;
        Texture2D m_white_texture;

        Rectangle m_menu_extents;
        string m_title;

        string m_intro_text;
        int m_intro_text_time;

        struct MenuItem
        {
            public string m_text;
            public bool m_is_toggle;
            public bool m_toggle_on;

            public MenuItem( string text )
            {
                m_text = text;
                m_is_toggle = false;
                m_toggle_on = false;
            }
        }

        MenuItem[] m_menu_choices;
        int m_menu_index;
        bool m_active;

        MenuCallback m_callback = null;

        public ListMenuComponent( Game game, Rectangle menu_extents )
            : base( game )
        {
            m_menu_choices = null;
            m_menu_extents = menu_extents;
            m_menu_index = -1;
            m_intro_text_time = 0;
            m_active = false;
        }

        public void SetupMenu( string title, List<string> items, MenuCallback callback )
        {
            m_title = title;
            m_menu_index = 0;
            m_callback = callback;
            m_active = false;

            m_menu_choices = new MenuItem[items.Count];
            int index = 0;
            foreach( string str in items )
            {
                m_menu_choices[index++] = new MenuItem(str);
            }
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            m_sprite_batch = new SpriteBatch( GraphicsDevice );
            m_sprite_font = Game.Content.Load<SpriteFont>( "DebugFont" );
            m_white_texture = Game.Content.Load<Texture2D>( "White" );

            base.LoadContent();
        }

        public void ShowMessage( string msg, int time )
        {
            m_intro_text_time = time;
            m_intro_text = msg;
        }

        public void SetupMenuToggle( int index, bool current_setting )
        {
            m_menu_choices[index].m_is_toggle = true;
            m_menu_choices[index].m_toggle_on = current_setting;
        }

        public override void Update( GameTime gameTime )
        {
            if ( m_active )
            {
                MenuInputComponent.EMenuInput input = MenuInputComponent.PollInput();

                switch ( input )
                {
                case MenuInputComponent.EMenuInput.Up:
                    {
                        m_menu_index -= 1;
                        if ( m_menu_index < 0 )
                        {
                            m_menu_index = m_menu_choices.Length - 1;
                        }
                    }
                    break;

                case MenuInputComponent.EMenuInput.Down:
                    {
                        m_menu_index += 1;
                        if (m_menu_index >= m_menu_choices.Length)
                        {
                            m_menu_index = 0;
                        }
                    }
                    break;

                case MenuInputComponent.EMenuInput.Select:
                    {
                        if ( m_menu_choices[m_menu_index].m_is_toggle )
                        {
                            m_menu_choices[m_menu_index].m_toggle_on = !m_menu_choices[m_menu_index].m_toggle_on;
                        }

                        m_callback( m_menu_index );
                    }
                    break;

                case MenuInputComponent.EMenuInput.Back:
                case MenuInputComponent.EMenuInput.Start:
                    {
                        m_callback( -1 );
                    }
                    break;
                }
            }

            if (m_intro_text_time > 0)
            {
                m_intro_text_time -= gameTime.ElapsedGameTime.Milliseconds;
            }

            base.Update( gameTime );
        }

        public override void Draw( GameTime gameTime )
        {
            if ( m_active )
            {
                StringBuilder str_cat = new StringBuilder(128, 128);

                m_sprite_batch.Begin();

                m_sprite_batch.Draw( m_white_texture, m_menu_extents, Color.Black );

                m_sprite_batch.DrawString( m_sprite_font,
                    m_title,
                    new Vector2( m_menu_extents.Left + 20, m_menu_extents.Top + 20 ),
                    Color.Red,
                    0.0f,
                    new Vector2( 0.0f, 0.0f ),
                    1.0f,
                    SpriteEffects.None,
                    1.0f );

                m_sprite_batch.DrawString( m_sprite_font,
                    "<paused>",
                    new Vector2( m_menu_extents.Right - 100, m_menu_extents.Top + 20 ),
                    Color.Red,
                    0.0f,
                    new Vector2( 0.0f, 0.0f ),
                    1.0f,
                    SpriteEffects.None,
                    1.0f );

                Vector2 textPos = new Vector2( m_menu_extents.Center.X, m_menu_extents.Center.Y );
                Vector2 textPosSep = new Vector2( 0.0f, 20.0f );

                textPos -= ( textPosSep * m_menu_index );

                for ( int i = 0;i< m_menu_choices.Length; i++ )
                {
                    Color clr = Color.Blue;
                    float scale = 1.0f;

                    if ( i == m_menu_index )
                    {
                        clr = Color.Red;
                        scale = 1.25f;
                    }

                    str_cat.Length = 0;
                    str_cat.Append(m_menu_choices[i].m_text);
                    if (m_menu_choices[i].m_is_toggle)
                    {
                        if ( m_menu_choices[i].m_toggle_on )
                        {
                            str_cat.Append(": On");
                        }
                        else
                        {
                            str_cat.Append(": Off");
                        }
                    }
                    Vector2 str_size = m_sprite_font.MeasureString(str_cat);
                    str_size /= 2.0f;
                    str_size *= scale;

                    m_sprite_batch.DrawString( m_sprite_font,
                        str_cat,
                        textPos - str_size,
                        clr,
                        0.0f,
                        new Vector2( 0.0f, 0.0f ),
                        scale,
                        SpriteEffects.None,
                        1.0f );

                    textPos += textPosSep;
                }

                m_sprite_batch.End();
            }

            if (m_intro_text_time > 0)
            {
                m_sprite_batch.Begin();

                m_sprite_batch.DrawString(m_sprite_font,
                    m_intro_text,
                    new Vector2(20.0f, 20.0f),
                    Color.Black,
                    0.0f,
                    new Vector2(0.0f, 0.0f),
                    1.0f,
                    SpriteEffects.None,
                    1.0f);

                m_sprite_batch.DrawString(m_sprite_font,
                    m_intro_text,
                    new Vector2(22.0f, 22.0f),
                    Color.White,
                    0.0f,
                    new Vector2(0.0f, 0.0f),
                    1.0f,
                    SpriteEffects.None,
                    1.0f);

                m_sprite_batch.End();
            }

            base.Draw( gameTime );
        }

        public void ShowMenu()
        {
            m_active = true;
        }

        public void Close()
        {
            m_active = false;
        }
    }
}
