// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class Monitor : Device
    {
        // Reference:
        // http://www.cepece.info/amstrad/docs/crtcnew.html
        // http://www.cepece.info/amstrad/docs/screen.html
        // http://www.cepece.info/amstrad/docs/scraddr.html
        // http://www.cepece.info/amstrad/docs/graphics.html
        // http://www.grimware.org/doku.php/documentations/devices/crtc
        // http://www.irespa.eu/daan/lib/howtoCPCemu.htm
        // http://www.cpctech.org.uk/docs/screen.html

        private TextureDisplay m_texture_display;
        private CRTC m_crtc;
        private Memory m_memory;
        private GateArray m_gate_array;

        private uint[] m_buffer;
        private int m_buffer_pos;
        
        private uint[] m_pen_colours;
        private int m_screen_mode;

        private bool m_do_ga_mode_update;
        private uint m_border_colour;

        private bool m_skip_next_frame;
        private bool m_skip_this_frame;


        public const int MAX_SCREEN_WIDTH = 640;
        public const int MAX_SCREEN_HEIGHT = 200;

        private const int MAX_SCREEN_ROWS = 60;
        private const uint BORDER_COLOUR = 0;       //< No border rasters for now. XNA Backbuffer clear colour is the border.

        private const int NUM_SCREEN_MODES = 4;
        private const int NUM_SCREENBYTE_VALS = 256;
        private const int NUM_PIXELS_PER_SCREENBYTE = 8; //< 8 real CPC pixels for 'mode 0'. 4 for 'mode 1', and 2 for 'mode 0'.

        private readonly static int[][] PEN_LOOKUP;

        static Monitor()
        {
            // Instead of running a costly switch statement, and bitshifting code (at least seemingly on Xbox 360 CF/JIT)... I'm
            // generating a lookup table of all possible 256 byte values, to their pixel layout counterparts.
            
            // Reference for this lookup table code is:
            // http://www.cepece.info/amstrad/docs/graphics.html

            PEN_LOOKUP = new int[NUM_SCREEN_MODES][];
            for ( int mode = 0; mode < NUM_SCREEN_MODES; mode++ )
            {
                PEN_LOOKUP[mode] = new int[NUM_SCREENBYTE_VALS * NUM_PIXELS_PER_SCREENBYTE];
                int[] buf = PEN_LOOKUP[mode];
                int buffer_pos = 0;

                for ( int screen_byte = 0; screen_byte < NUM_SCREENBYTE_VALS; screen_byte++ )
                {
                    switch (mode)
                    {
                        case 1:	//< Mode 1: 2 bpp - 4 colours ... 4 CPC pixels, 8 XNA ones.
                            {
                                // Pixel 0 is bits 3 and 7
                                int pixel_colour = (((screen_byte & 0x80) >> 7) | ((screen_byte & 0x08) >> 2));

                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;

                                // Pixel 1 is bits 2 and 6
                                pixel_colour = (((screen_byte & 0x40) >> 6) | ((screen_byte & 0x04) >> 1));

                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;

                                // Pixel 2 is bits 1 and 5
                                pixel_colour = (((screen_byte & 0x20) >> 5) | (screen_byte & 0x02));

                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;

                                // Pixel 3 is bits 0 and 4
                                pixel_colour = (((screen_byte & 0x10) >> 4) | ((screen_byte & 0x01) << 1));

                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;
                            }
                            break;

                        case 0:	//< Mode 0: 4 bpp - 16 colours
                        case 3:	//< Mode 3: 4 bpp - 4 colours (untested) ... 2 CPC pixels, 8 XNA ones.
                            {
                                // Pixel 0 is bits 1, 5, 3 and 7
                                int pixel_colour = (screen_byte & 0xAA);

                                pixel_colour = (
                                    ((pixel_colour & 0x80) >> 7) |
                                    ((pixel_colour & 0x08) >> 2) |
                                    ((pixel_colour & 0x20) >> 3) |
                                    ((pixel_colour & 0x02) << 2));

                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;

                                // Pixel 1 is bits 0, 4, 2 and 6
                                pixel_colour = (screen_byte & 0x55);

                                pixel_colour = (
                                    ((pixel_colour & 0x40) >> 6) |
                                    ((pixel_colour & 0x04) >> 1) |
                                    ((pixel_colour & 0x10) >> 2) |
                                    ((pixel_colour & 0x01) << 3));

                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;
                                buf[buffer_pos++] = pixel_colour;

                            }
                            break;

                        case 2: //< Mode 2: 1 bpp - Monochrome ... 8 CPC pixels, 8 XNA ones.
                            {
                                // Pixels are in order: Bit 7, 6, 5, 4, 3, 2, 1, then 0

                                for (int i = 7; i >= 0; i--)
                                {
                                    bool pixel_on = ((screen_byte & (1 << i)) != 0);
                                    buf[buffer_pos++] = pixel_on ? 1 : 0;
                                }
                            }
                            break;
                    }
                }
            }

        }

        public Monitor( TextureDisplay texture_display, CRTC crtc, Memory memory, GateArray gate_array )
        {
            m_texture_display = texture_display;
            m_crtc = crtc;
            m_memory = memory;
            m_gate_array = gate_array;

            m_pen_colours = new uint[GateArray.NUM_PEN_SETTINGS];

            // Implicitly hookup the CRTC to the monitor.
            m_crtc.AddHSyncCallback( OnHSync );
            m_crtc.AddVSyncCallback( OnVSync );

            Reset();
        }

        public void Reset()
        {
            m_buffer = null;
            m_buffer_pos = 0;

            for (int i = 0; i < GateArray.NUM_PEN_SETTINGS; i++)
            {
                m_pen_colours[0] = 0;
            }
            m_screen_mode = 0;

            m_do_ga_mode_update = false;
            m_border_colour = 0;

            m_skip_next_frame = false;
            m_skip_this_frame = false;
        }
                
        public uint BorderColour
        {
            get { return m_border_colour; }
        }

        public void SkipNextFrame()
        {
            // This will still do correct emulation of the next frame. It just won't be written out to the destination texture.
            // It saves a little CPU time on Xbox 360. I was using this a lot when I was consistently dropping under 50fps.
            m_skip_next_frame = true;
        }

        public void OnInterruptAcknowledge()
        {
            // Flag to grab a new screenmode when we start a new CRTC row
            m_do_ga_mode_update = true;
        }

        private void UpdateModeFromGateArray()
        {
            m_screen_mode = m_gate_array.ScreenMode;
        }

        private void UpdatePensFromGateArray()
        {
            for (int i = 0; i < GateArray.NUM_PEN_SETTINGS; i++)
            {
                m_pen_colours[i] = m_gate_array.GetPenColour(i);
            }
        }

        unsafe public void OnHSync()
        {
            // This code effectively renders out a single scanline to the 'TextureDisplay' buffer.
            if ( m_skip_this_frame )
            {
                return;
            }

            int cur_row = m_crtc.CurrentVerticalRow;
            int cur_scanline_in_row = m_crtc.CurrentVerticalScanlineInRow;
 
            // Update GA stuff on first scanline in a row
            if (cur_scanline_in_row == 0)
            {
                UpdatePensFromGateArray();

                // Mode is updated when flagged by an interrupt
                if ( m_do_ga_mode_update )
                {
                    m_do_ga_mode_update = false;
                    UpdateModeFromGateArray();
                }      
            }             

            int screen_width_bytes = m_crtc.GetRegister(CRTC.Register.HorizontalDisplayed) * 2;
            int screen_height_rows = m_crtc.GetRegister(CRTC.Register.VerticalDisplayed);
            int crtc_screen_address = m_crtc.GetRegister(CRTC.Register.DisplayStartAddressHigh) << 8;
            crtc_screen_address |= m_crtc.GetRegister(CRTC.Register.DisplayStartAddressLow);
            
            // Start of a brand new screen?
            if (( cur_row == 0 ) && ( cur_scanline_in_row == 0 ))
            {
                if ( m_skip_next_frame )
                {
                    m_skip_this_frame = true;
                    m_skip_next_frame = false;
                    return;
                }
                UpdatePensFromGateArray();
                UpdateModeFromGateArray();
                m_buffer_pos = 0;
                m_buffer = m_texture_display.GetNewFrameBuffer();

                int raster_height = m_crtc.GetRegister( CRTC.Register.MaximumRasterAddress ) + 1;
                int height_padding_pixels = ( MAX_SCREEN_HEIGHT - ( screen_height_rows * raster_height ) ) / 2;
                if ( height_padding_pixels < 0 )
                {
                    height_padding_pixels = 0;
                }

                // Clear top border of frame so it's zero alpha, to show up the border.
                for ( int i=0;i<height_padding_pixels * MAX_SCREEN_WIDTH;i++ )
                {
                    m_buffer[m_buffer_pos++] = BORDER_COLOUR;
                }
            }

            // Done with drawing the screen?
            if ((cur_row >= screen_height_rows) || (m_buffer_pos >= TextureDisplay.BUFFER_SIZE))
            {
                return;
            }

            // Figure out horizontal border padding required.
            int width_padding_pixels = (MAX_SCREEN_WIDTH - (screen_width_bytes * 8)) / 2;
            if (width_padding_pixels < 0)
            {
                width_padding_pixels = 0;
            }

            // Calculate base addresses.
            int base_start_address = ((crtc_screen_address << 2) & (0xF000));
            int base_address_offset = (crtc_screen_address * 2) & 0x7FF;

            // Calculate left-hand border size
            int scanline_pixels_left_over = MAX_SCREEN_WIDTH - width_padding_pixels;
            int pixels_to_write = ( screen_width_bytes * NUM_PIXELS_PER_SCREENBYTE );
            if (pixels_to_write > scanline_pixels_left_over)
            {
                // Adjust screen width if it's too big
                screen_width_bytes = scanline_pixels_left_over / NUM_PIXELS_PER_SCREENBYTE;
            }

            // Deduct the screen width, this is the right-hand border size
            Debug.Assert((8 * screen_width_bytes) <= scanline_pixels_left_over);
            scanline_pixels_left_over -= (8 * screen_width_bytes);

            // Find address based off the y co-ord, which won't change for this scanline. Find address for the x, which we'll increment.
            int address_x = base_address_offset + ( ( cur_row * screen_width_bytes ) & 0x7ff );
            int address_y = base_start_address + ( 2048 * cur_scanline_in_row );

            int[] lookup = PEN_LOOKUP[m_screen_mode];
            byte[] ram = m_memory.RAM;
            fixed (uint* p_buffer = m_buffer)
            {
                fixed (byte* p_ram = ram)
                {
                    fixed (int* p_lookup = lookup)
                    {
                        fixed ( uint* p_pen_colours = m_pen_colours )
                        {
                            // Draw zero alpha bit of scanline for left-hand border.
                            for ( int i = 0; i < width_padding_pixels; i++ )
                            {
                                p_buffer[m_buffer_pos++] = BORDER_COLOUR;
                            }

                            // Draw memory-mapped part of the scanline
                            for ( int x_byte = 0; x_byte < screen_width_bytes; x_byte++ )
                            {
                                int screen_byte = p_ram[address_y + address_x];
                                int lookup_index = ( screen_byte * NUM_PIXELS_PER_SCREENBYTE );

                                // Use lookup tables to figure out the eight pixels to write.
                                // First lookup (p_lookup) sees how the screen byte maps into eight pixels, in CPC pen colours
                                // Second lookup (p_pen_colours) looks up the full uint ABGR value to write to the XNA texture
                                // NOTE: Hand-unrolling this loop jumped me from ~43fps on Xbox to ~51, with the frame limiting turned off.
                                // NOTE: Yes, this is with the jit-er running. Not connected to a debugger. "Bad Compact Framework, bad."
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];
                                p_buffer[m_buffer_pos++] = p_pen_colours[p_lookup[lookup_index++]];

                                // Done eight pixels, go to next byte address.
                                address_x++;
                                address_x &= 0x7ff;
                            }
                        }
                    }
                }
                
                // Draw zero alpha bit of scanline for right-hand border.
                while (scanline_pixels_left_over > 0)
                {
                    p_buffer[m_buffer_pos++] = BORDER_COLOUR;
                    scanline_pixels_left_over--;
                }
            }
        }

        public void OnVSync()
        {
            // Fill in rest of border, and inform tetxure buffer stuff that we're done with this frame.
            if ( m_buffer != null )
            {
                // No correctly rastered border, unfortunately. 
                // We'll just use a solid colour, and take the gatearray colour at the vsync point.
                m_border_colour = m_gate_array.BorderColour;

                // Clear rest of frame so it's zero alpha, to show up the border.
                while ( m_buffer_pos < TextureDisplay.BUFFER_SIZE )
                {
                    m_buffer[m_buffer_pos++] = BORDER_COLOUR;
                }

                m_texture_display.OnBufferComplete();
                m_buffer = null;
            }

            m_skip_this_frame = false;
        }


    }
}
