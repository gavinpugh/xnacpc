// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace XNACPC
{
    class TextureDisplay
    {
        private Texture2D m_texture;

        private uint[] m_buffer_front;
        private uint[] m_buffer_back;
        
        public const int MAX_SCREEN_WIDTH = 640;
        public const int MAX_SCREEN_HEIGHT = 200;
        public const int BUFFER_SIZE = (MAX_SCREEN_WIDTH * MAX_SCREEN_HEIGHT);

        private enum MonitorState
        {
            Idle,
            DrawingToBackBuffer,
            VSync
        };

        private MonitorState m_monitor_state;

        private enum TextureState
        {
            Idle,
            SetDataPending,
            SetDataComplete
        };

        private TextureState m_texture_state;

        public TextureDisplay(GraphicsDevice graphics_device)
        {
            m_buffer_front = new uint[BUFFER_SIZE];
            m_buffer_back = new uint[BUFFER_SIZE];
            
            m_texture = new Texture2D(graphics_device, MAX_SCREEN_WIDTH, MAX_SCREEN_HEIGHT, false, SurfaceFormat.Color);

            Reset();
        }
        
        public void Reset()
        {
            m_monitor_state = MonitorState.Idle;
            m_texture_state = TextureState.Idle;
        }

        public Texture2D Texture
        {
            get { return m_texture; }
        }

        public uint[] GetNewFrameBuffer()
        {
            Debug.Assert(m_monitor_state != MonitorState.DrawingToBackBuffer);
            m_monitor_state = MonitorState.DrawingToBackBuffer;

            return m_buffer_back;
        }

        public void OnBufferComplete()
        {
            Debug.Assert(m_monitor_state == MonitorState.DrawingToBackBuffer);
            m_monitor_state = MonitorState.VSync;
            
            // Swap buffers
            uint[] temp = m_buffer_front;
            m_buffer_front = m_buffer_back;
            m_buffer_back = temp;

            m_texture_state = TextureState.SetDataPending;
        }

        public void SetData()
        {
            if (m_texture_state == TextureState.SetDataPending)
            {                
                m_texture.SetData<uint>(m_buffer_front, 0, BUFFER_SIZE);
                m_texture_state = TextureState.SetDataComplete;
            }
        }
    }
}
