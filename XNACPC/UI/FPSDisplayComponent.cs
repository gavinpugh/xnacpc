// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace XNACPC
{
    class FPSDisplayComponent : Microsoft.Xna.Framework.DrawableGameComponent
    {
        class Counter
        {
            public const int NUM_READINGS = 60;
            public Int64 m_reading_total;
            public int m_index = 0;

            public float m_last_fps = 0.0f;

            public void Add( GameTime gameTime )
            {
                m_reading_total += gameTime.ElapsedGameTime.Ticks;
                m_index++;
                
                if ( m_index >= NUM_READINGS )
                {
                    m_index = 0;
                    m_reading_total /= NUM_READINGS;
                    m_last_fps = (10000000.0f / m_reading_total);
                    m_reading_total = 0;
                }
            }
            
            public float GetFPS()
            {
                return m_last_fps;
            }

            public bool ShouldRefresh()
            {
                return (m_index == 0);
            }
        }

        StringBuilder m_fps_display = new StringBuilder(32);

        SpriteBatch m_sprite_batch;
        SpriteFont m_sprite_font;

        Counter m_update_counter = new Counter();
        Counter m_draw_counter = new Counter();

        int m_skipped_frames;
        
        public FPSDisplayComponent( Game game )
            : base( game )
        {
            m_skipped_frames = 0;
        }

        public void SkippingFrame()
        {
            m_skipped_frames++;
        }

        protected override void LoadContent()
        {
            m_sprite_batch = new SpriteBatch(GraphicsDevice);
            m_sprite_font = Game.Content.Load<SpriteFont>("DebugFont");
        }

        public override void Update(GameTime gameTime)
        {
            m_update_counter.Add(gameTime);

            base.Update(gameTime);
        }

        public override void Draw( GameTime gameTime )
        {
            m_draw_counter.Add(gameTime);

            if (m_draw_counter.ShouldRefresh())
            {
                m_fps_display.Length = 0;
                m_fps_display.ConcatFormat("Draw:{0:0.00}\n", m_draw_counter.GetFPS());
                m_fps_display.ConcatFormat("Update:{0:0.00}\n", m_update_counter.GetFPS());
                m_fps_display.ConcatFormat("Skipped:{0}\n", m_skipped_frames);
                m_skipped_frames = 0;
            }
            
            m_sprite_batch.Begin();

            m_sprite_batch.DrawString(m_sprite_font,
                m_fps_display,
                new Vector2(1100.0f, 20.0f),
                Color.Black,
                0.0f,
                new Vector2(0.0f, 0.0f),
                1.0f,
                SpriteEffects.None,
                1.0f);

            m_sprite_batch.DrawString(m_sprite_font,
                m_fps_display,
                new Vector2(1102.0f, 22.0f),
                Color.White,
                0.0f,
                new Vector2(0.0f, 0.0f),
                1.0f,
                SpriteEffects.None,
                1.0f);
            
            m_sprite_batch.End();

            base.Draw(gameTime);
        }
    }
}
