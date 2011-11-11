// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

#define ENABLE_FRAME_LIMIT
#if PROFILE
#define ENABLE_TEST_FPS
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace XNACPC
{
	/// <summary>
	/// This is the main type for your game
	/// </summary>
	public class MainGame : Microsoft.Xna.Framework.Game
	{
		private const float SCREEN_WIDTH_STRETCH = 1.1f;	//< Slight stretch for widescreen, looks better IMHO
        private const int CPC_PIXEL_HEIGHT = 3; 			//< 200 CPC pixels high, 600 XNA pixels high.
        static readonly long TARGET_FRAME_TIME;
		
		GraphicsDeviceManager m_graphics;
		ListMenuComponent m_pause_menu;
		ListMenuComponent m_snapshot_menu;
		SpriteBatch m_sprite_batch;
		Rectangle m_screen_rect;
		Rectangle m_border_rect;
		Effect m_crt_effect_screen;
		Effect m_crt_effect_border;
		Texture2D m_white_texture;

#if ENABLE_TEST_FPS
        FPSDisplayComponent m_fps_display;
#endif // ENABLE_TEST_FPS

        Stopwatch m_timer;
		long m_time_over;
		bool m_skipped_last_frame;
        bool m_allow_frameskipping;

		CPC.Emulator m_emulator;
		TextureDisplay m_texture_display;
		Audio m_audio;
		string m_startup_load_sna;
		        
        List<String> m_snapshot_files;
		
		bool m_paused;
        bool m_use_crt_shader;
        bool m_throttle_speed;


		private enum EPauseMenuOptions
		{
			Back = -1,
			Resume = 0,
			LoadSnapshot,
			Reset,
            ToggleCRTShader,
            ThrottleSpeed,
			Quit
		}

		static MainGame()
		{
			// Frequency is over one second. So this calculates the time for one frame, in 'Stopwatch' timer ticks.
            TARGET_FRAME_TIME = (Stopwatch.Frequency / CPC.Emulator.TARGET_FPS);
		}

		public MainGame( string[] args )
		{
			m_timer = new Stopwatch();

			m_graphics = new GraphicsDeviceManager( this );
			Content.RootDirectory = "Content";

            m_snapshot_files = new List<String>(64);
            m_use_crt_shader = true;
            m_paused = false;

            IsFixedTimeStep = false;
			m_graphics.SynchronizeWithVerticalRetrace = false;

            m_graphics.PreferredBackBufferWidth = 1280;
			m_graphics.PreferredBackBufferHeight = 720;
			
            if ( args.Length > 0 )
			{
				m_startup_load_sna = args[0];
			}

            m_allow_frameskipping = true;
#if XBOX
#if !DEBUG
            // Allow frameskipping if debugger is attached. Otherwise JITing should make it unnecessary.
            // Only can do this now that Xbox runs at >50fps! Frameskipping was required when it was running at ~40.
            if ( System.Diagnostics.Debugger.IsAttached == false )
            {
                m_allow_frameskipping = false;
            }
#endif // #if !DEBUG
#endif // #if XBOX

#if PROFILE
            m_allow_frameskipping = false;
            m_throttle_speed = false;
#else // #if PROFILE
            m_throttle_speed = true;
#endif // #if PROFILE

			m_time_over = 0;
			m_skipped_last_frame = false;
		}
        
		private BinaryFile LoadBinaryFile( string filename )
		{
			return Content.Load<BinaryFile>( filename );
		}

		protected override void Initialize()
		{
			// 600 XNA pixels displays 200 CPC pixels. Nice fit. Leaves 120 pixels (from 720 high), for the CPC borders.
			// The width is derived from that, so it'll work on any target aspect ratio.
			// I also stretch the width a little, so it looks nicer on a widescreen set.
            int screen_height = TextureDisplay.MAX_SCREEN_HEIGHT * CPC_PIXEL_HEIGHT;
			int screen_width = ( ( screen_height * 4 ) / 3 );
			if ( m_graphics.GraphicsDevice.Viewport.AspectRatio > 1.4f )
			{
				screen_width = (int)( screen_width * SCREEN_WIDTH_STRETCH );
			}

			m_screen_rect = new Rectangle(
				( m_graphics.GraphicsDevice.Viewport.Width - screen_width ) / 2,
				( m_graphics.GraphicsDevice.Viewport.Height - screen_height ) / 2,
				screen_width,
				screen_height );

			m_border_rect = new Rectangle(
				0,
				0,
				m_graphics.GraphicsDevice.Viewport.Width,
				m_graphics.GraphicsDevice.Viewport.Height );
			
			int menu_width = ( screen_width / 4 ) * 3;
			Rectangle menu_extents = new Rectangle(
				( GraphicsDevice.Viewport.Width - menu_width ) / 2,
				0,
				menu_width,
				GraphicsDevice.Viewport.Height );

			m_pause_menu = new ListMenuComponent( this, menu_extents );
			Components.Add( m_pause_menu );

			m_snapshot_menu = new ListMenuComponent( this, menu_extents );
			Components.Add( m_snapshot_menu );

			Components.Add( new MenuInputComponent( this ) );

#if ENABLE_TEST_FPS
            m_fps_display = new FPSDisplayComponent(this);
            Components.Add(m_fps_display);
#endif // ENABLE_TEST_FPS

#if WINDOWS
            m_pause_menu.ShowMessage("Press F1 to pause and load snapshot files.\nNumpad with num lock on is the Joystick.", 5000);
#else // WINDOWS
            m_pause_menu.ShowMessage("Press Start to pause and load snapshot files.", 5000);
#endif // WINDOWS

            base.Initialize();
		}
		
		private void GetSnapshotFileList()
		{
			List<string> contentFiles = Content.Load<List<string>>( "manifest" );

			foreach ( String path in contentFiles )
			{
				int last_slash = path.IndexOf( '\\' );
				if ( last_slash > 0 )
				{
					string directory = path.Substring( 0, last_slash );
					string pathWithoutSnaDir = path.Substring( last_slash + 1 );

					if ( directory == "sna" )
                    {
                        string[] sna_and_size = pathWithoutSnaDir.Split(',');
                        int size = Convert.ToInt32(sna_and_size[1]);
                        if ((size > (128 * 1024)) && (CPC.Memory.TOTAL_RAM_NUM_BANKS == 4))
                        {
                            // This is a 128k snapshot, but this Amstrad being emulated is only 64k. Ignore it.
                        }
                        else
                        {
                            m_snapshot_files.Add(sna_and_size[0]);
                        }
					}
				}
			}

			m_snapshot_files.Sort();
		}

		protected override void LoadContent()
		{
			m_sprite_batch = new SpriteBatch( GraphicsDevice );
			m_white_texture = Content.Load<Texture2D>( "White" );
            
            m_crt_effect_border = Content.Load<Effect>("crt");
            m_crt_effect_border.Parameters["Viewport"].SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
            m_crt_effect_border.Parameters["TextureHeight"].SetValue(GraphicsDevice.Viewport.Height / CPC_PIXEL_HEIGHT);
            m_crt_effect_border.Parameters["ScreenHeight"].SetValue(GraphicsDevice.Viewport.Height);

            m_crt_effect_screen = m_crt_effect_border.Clone();
			m_crt_effect_screen.Parameters["Viewport"].SetValue( new Vector2( GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height ) );
			m_crt_effect_screen.Parameters["TextureHeight"].SetValue( TextureDisplay.MAX_SCREEN_HEIGHT );
            m_crt_effect_screen.Parameters["ScreenHeight"].SetValue(m_screen_rect.Height);

			m_texture_display = new TextureDisplay( GraphicsDevice );
			m_audio = new Audio();
			m_emulator = new CPC.Emulator( m_audio, m_texture_display );

			for ( PlayerIndex index = PlayerIndex.One; index <= PlayerIndex.Four; index++ )
			{
				// For now all gamepads respond as joystick 0
				m_emulator.Keyboard.AssignJoystick( new CPC.Joystick( 0, index ) );
			}

			// Use CPC464 ROM only for now
			BinaryFile rom_file = LoadBinaryFile( "rom\\CPC464" );
			m_emulator.Memory.LoadROM( CPC.ROM.LOWER_ROM_INDEX, rom_file.m_data, 0 );
			m_emulator.Memory.LoadROM( CPC.ROM.BASIC_ROM_INDEX, rom_file.m_data, CPC.ROM.ROM_SIZE );

			// List snapshots that can be played
			GetSnapshotFileList();
			
			List<string> pause_menu_options = new List<string>();
			pause_menu_options.Add( "Unpause" );		//Resume
			pause_menu_options.Add( "Load Snapshot" );	//LoadSnapshot
            pause_menu_options.Add( "Reset CPC" );		//Reset
            pause_menu_options.Add("Toggle CRT Shader");	//ToggleCRTShader
            pause_menu_options.Add("Throttle Speed");	//ThrottleSpeed
			pause_menu_options.Add( "Quit" );			//Quit

            m_pause_menu.SetupMenu("XNACPC - Gavin Pugh 2011", pause_menu_options, PauseCallback);
            m_pause_menu.SetupMenuToggle((int)EPauseMenuOptions.ThrottleSpeed, m_throttle_speed);
            m_pause_menu.SetupMenuToggle((int)EPauseMenuOptions.ToggleCRTShader, m_use_crt_shader);

			m_snapshot_menu.SetupMenu( "Choose a snapshot", m_snapshot_files, SnapshotCallback );

			if ( m_startup_load_sna != null )
			{
				// Check the sna list. Do a case-insensitive compare to check it's a valid .sna
				foreach ( string snaFile in m_snapshot_files )
				{
					if ( String.Compare( snaFile, m_startup_load_sna, StringComparison.OrdinalIgnoreCase ) == 0 )
					{
						// Got it! Now just load it in.
						LoadSnapshotFile( m_startup_load_sna );
						break;
					}
				}
			}

			GC.Collect();

			m_timer.Reset();
			m_timer.Start();

			base.LoadContent();
		}

		protected override void UnloadContent()
		{

		}

		public void ResetCPC()
		{
			m_emulator.Reset();
            m_texture_display.Reset();
            m_audio.Reset();
		}

		public void Unpause()
		{
			// Remove the menus and unpause
			m_paused = false;
			m_pause_menu.Close();
			m_snapshot_menu.Close();
			MenuInputComponent.Disable();

			// Try and clean up any allocs
			GC.Collect();
		}

		private void LoadSnapshotFile( string path )
		{
			BinaryFile sna_file = null;
			try
			{
				sna_file = LoadBinaryFile( "sna\\" + path );
			}
			finally
			{
				CPC.SNAData sna_data = new CPC.SNAData( sna_file.m_data );

                ResetCPC();
				sna_data.LoadSnapshot( m_emulator );
			}
		}

		private void SnapshotCallback( int selection )
		{
			if ( selection >= 0 )
			{
				string snapshot_filename = m_snapshot_files[selection];
				LoadSnapshotFile( snapshot_filename );
				Unpause();
			}
			else
			{
				m_snapshot_menu.Close();
				m_pause_menu.ShowMenu();
			}
		}

		private void PauseCallback( int selection )
		{
			switch ( (EPauseMenuOptions)selection )
			{
                case EPauseMenuOptions.LoadSnapshot:
                    {
                        m_pause_menu.Close();
                        m_snapshot_menu.ShowMenu();
                    }
                    break;

                case EPauseMenuOptions.Reset:
                    {
                        ResetCPC();
                        Unpause();
                    }
                    break;

                case EPauseMenuOptions.Back:
                case EPauseMenuOptions.Resume:
                    {
                        Unpause();
                    }
                    break;

                case EPauseMenuOptions.ToggleCRTShader:
                    {
                        m_use_crt_shader = !m_use_crt_shader;
                    }
                    break;

                case EPauseMenuOptions.ThrottleSpeed:
                    {
                        m_throttle_speed = !m_throttle_speed;
                    }
                    break;

                case EPauseMenuOptions.Quit:
                    {
                        this.Exit();
                    }
                    break;
			}
		}

		private void SkipDrawingNextFrame()
		{
            if ( m_allow_frameskipping == false )
            {
                return;
            }
            if (m_skipped_last_frame == false)
            {
                m_emulator.SkipDrawingNextFrame();
#if ENABLE_TEST_FPS
                m_fps_display.SkippingFrame();
#endif // ENABLE_TEST_FPS
                m_skipped_last_frame = true;
            }
            else
            {
                m_skipped_last_frame = false;
            }
		}

        private void FramerateLimiter()
        {
            if (m_throttle_speed == false)
            {
                return;
            }

			// See how bad the last frame was
			long time_over = m_timer.ElapsedTicks - TARGET_FRAME_TIME;
			if ( time_over > 0 )
			{
				// Went over time.
				m_time_over += time_over;
                if (m_time_over > TARGET_FRAME_TIME)
                {
                    SkipDrawingNextFrame();
					m_time_over = TARGET_FRAME_TIME;
				}
            }
			else if ( m_time_over > 0 )
			{
				// Within a frame!
				// BUT... Already in debt though, deduct from the debt (time_over is negative).
				m_time_over += time_over;
				if ( m_time_over < 0 )
				{
					// Nice, it earned back the debt! Ignore any credit.
					// If we were in debt, we shouldn't be lazy and wait again right now. 
					// Will do so next frame, if it's good too.
					m_time_over = 0;
				}
				else
				{
                    // Skip next frame, if we didn't skip the last. Since we're still in debt.
                    SkipDrawingNextFrame();
				}
			}
			else
			{
				// Within a frame, and our credit is good
				if ( m_time_over == 0 )
				{
					while ( m_timer.ElapsedTicks < TARGET_FRAME_TIME )
					{
#if WINDOWS
						// Only sleep with Windows. Using Sleep() here on Xbox usually ends up way off the target.
						System.Threading.Thread.Sleep( 0 );
#endif // WINDOWS
					}
				}
			}
			Debug.Assert( m_time_over >= 0 );
			
			m_timer.Reset();
			m_timer.Start();
        }

		protected override void Update( GameTime gameTime )
        {
#if ENABLE_FRAME_LIMIT && !PROFILE
            FramerateLimiter();
#endif // ENABLE_FRAME_LIMIT && !PROFILE
            
            if (!m_paused)
            {
                if (MenuInputComponent.MenuStillDebouncing() == false)
                {
                    if ((GamePad.GetState(PlayerIndex.One).Buttons.Start == ButtonState.Pressed) ||
                        (Keyboard.GetState().IsKeyDown(Keys.F1)))
                    {
                        m_paused = true;

                        MenuInputComponent.Enable();
                        m_pause_menu.ShowMenu();
                    }
                }

				m_emulator.Update();
            }

			base.Update( gameTime );            
		}

		private Color ConvertCPCColourToXNAColour( uint cpcColour )
		{
			Color colour = new Color();
            colour.R = (byte)(cpcColour & 0xFF);
            colour.G = (byte)((cpcColour & 0xFF00) >> 8);
            colour.B = (byte)((cpcColour & 0xFF0000) >> 16);
            colour.A = 255;

            return colour;
		}

		protected override void Draw( GameTime gameTime )
		{
			m_texture_display.SetData();

            // Grab border colour
            uint border_colour_value = m_emulator.BorderColour;
			Color border_colour = ConvertCPCColourToXNAColour( border_colour_value );
		
			// Clear backbuffer
			GraphicsDevice.Clear( Color.White );

            // Choose effects to use, user may have disabled the CRT shader
            Effect border_effect = m_use_crt_shader ? m_crt_effect_border : null;
			Effect screen_effect = m_use_crt_shader ? m_crt_effect_screen : null;
						
			// Draw big backbuffer-covering sprite, for the CPC border. Was using the clear colour, but I need the CRT effect to be applied.
			m_sprite_batch.Begin( 0, BlendState.Opaque, null, null, null, border_effect );
            m_sprite_batch.Draw( m_white_texture, m_border_rect, border_colour );
			m_sprite_batch.End();

			// Draw CPC screen texture onto the backbuffer
			m_sprite_batch.Begin( 0, BlendState.Opaque, null, null, null, screen_effect );
			m_sprite_batch.Draw( m_texture_display.Texture, m_screen_rect, Color.White );
			m_sprite_batch.End();

			// This prevents an exception in SetData(), in TextureDisplay.Draw().
			// See: https://connect.microsoft.com/site226/feedback/details/318195/unable-to-get-set-data-on-a-texture-in-the-update-method
			GraphicsDevice.Textures[0] = null;

			base.Draw( gameTime );
		}
	}
}
