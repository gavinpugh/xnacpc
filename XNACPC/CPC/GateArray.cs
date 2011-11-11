// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class GateArray : Device
    {
        // Reference:
        // http://www.cepece.info/amstrad/docs/garray.html
        // http://www.cepece.info/amstrad/docs/ints.html
        // http://www.grimware.org/doku.php/documentations/devices/gatearray

        private enum Function
        {
            SelectPen = 0,
            SelectColour,
            ScreenModeAndROM,
            RAMSelect
        }

        public const int BORDER_COLOUR_VALUE = 16;
        public const int NUM_PEN_SETTINGS = 17;

        private const int SCANLINE_COUNTER_MAX = 52;
        private const int VSYNC_DELAY_AMOUNT = 2;
        private const int NUM_HARDWARE_COLOURS = 32;
        private const int NUM_SCREEN_MODES = 4;

        private static readonly uint[] HARDWARE_COLOURS = new uint[NUM_HARDWARE_COLOURS] {
            
            // From SOFT 158, Appendix V: Inks and Colours
            // http://www.cepece.info/amstrad/docs/manual/s158ap05.pdf
            // http://www.cepece.info/amstrad/docs/garray.html

            // These are in BGR format, little endian

            0xFF808080,	// White
            0xFF808080,	// * Dupe (White)
            0xFF80FF00,	// Sea green
            0xFF80FFFF,	// Pastel yellow
            0xFF800000,	// Blue
            0xFF8000FF,	// Purple
            0xFF808000,	// Cyan
            0xFF8080FF,	// Pink
            0xFF8000FF,	// * Dupe (Purple)
            0xFF80FFFF,	// * Dupe (Pastel yellow)
            0xFF00FFFF,	// Bright yellow
            0xFFFFFFFF,	// Bright white
            0xFF0000FF,	// Bright red
            0xFFFF00FF,	// Bright magenta
            0xFF0080FF,	// Orange
            0xFFFF80FF,	// Pastel magenta
            0xFF800000,	// * Dupe (Blue)
            0xFF80FF00,	// * Dupe (Sea Green)
            0xFF00FF00,	// Bright green
            0xFFFFFF00,	// Bright cyan
            0xFF000000,	// Black
            0xFFFF0000,	// Bright blue
            0xFF008000,	// Green
            0xFFFF8000,	// Sky blue
            0xFF800080,	// Magenta
            0xFF80FF80,	// Pastel green
            0xFF00FF80,	// Lime
            0xFFFFFF80,	// Pastel cyan
            0xFF000080,	// Red
            0xFFFF0080,	// Mauve
            0xFF008080,	// Yellow
            0xFFFF8080,	// Pastel blue

            // TODO: Try colours from here:
            // http://www.grimware.org/doku.php/documentations/devices/gatearray
        };


        private int[] m_pen_colours;
        private int m_current_pen;
        
        private int m_screen_mode;
        
        private int m_interrupt_scanline_counter;
        private int m_vsync_delay_counter;

        private Memory m_memory;
        private CPU.Z80 m_cpu;

        public GateArray( Memory memory, CPU.Z80 cpu )
        {
            m_memory = memory;
            m_cpu = cpu;

            m_pen_colours = new int[NUM_PEN_SETTINGS];

            Reset();
        }

        public void Reset()
        {
            m_current_pen = 0;

            m_screen_mode = 1;
            for ( int i = 0; i < NUM_PEN_SETTINGS; i++ )
            {
                m_pen_colours[i] = 0;
            }

            m_interrupt_scanline_counter = 0;
            m_vsync_delay_counter = 0;
        }

        public int ScreenMode
        {
            get { return m_screen_mode; }
        }

        public uint BorderColour
        {
            get { return HARDWARE_COLOURS[m_pen_colours[BORDER_COLOUR_VALUE]]; }
        }
        
        private Function GetFunction( int value )
        {
            // The top two bits map to the enumeration
            int func_num = ( value & 0xC0 ) >> 6;
            Debug.Assert( func_num < 4 );
            return (Function)( func_num );
        }

        public void SetCurrentPen( int pen )
        {
            Debug.Assert( pen >= 0 );
            Debug.Assert( pen < NUM_PEN_SETTINGS );
            m_current_pen = pen;
        }

        public void SetPenColour( int pen_index, int colour )
        {
            Debug.Assert( colour >= 0 );
            Debug.Assert( colour < NUM_HARDWARE_COLOURS );
            m_pen_colours[pen_index] = colour;
        }

        public uint GetPenColour( int pen_index )
        {
            Debug.Assert( pen_index >= 0 );
            Debug.Assert( pen_index < NUM_PEN_SETTINGS );
            return HARDWARE_COLOURS[m_pen_colours[pen_index]];
        }

        public void SetScreenMode( int mode )
        {
            Debug.Assert( mode >= 0 );
            Debug.Assert( mode < NUM_SCREEN_MODES );
            m_screen_mode = mode;
        }

        public void SetCounters( int scanline_count, int vsync_delay )
        {
            // Used by SNA-snapshot code
            m_interrupt_scanline_counter = scanline_count;
            Debug.Assert( m_interrupt_scanline_counter < SCANLINE_COUNTER_MAX );
            m_vsync_delay_counter = vsync_delay;
            Debug.Assert( m_vsync_delay_counter <= VSYNC_DELAY_AMOUNT );
        }

        public void ScreenModeAndROMSelect( int io_write_setting )
        {
            // Screen mode
            SetScreenMode( io_write_setting & 0x03 );

            // ROM enable/disable
            if ( ( io_write_setting & 0x08 ) != 0 )
            {
                m_memory.SetUpperROMState( false );
            }
            else
            {
                m_memory.SetUpperROMState( true );
            }
            if ( ( io_write_setting & 0x04 ) != 0 )
            {
                m_memory.SetLowerROMState( false );
            }
            else
            {
                m_memory.SetLowerROMState( true );
            }

            // Delay interrupts bit
            if ( ( io_write_setting & 0x10 ) != 0 )
            {
                m_cpu.SetInterruptRequest(false);
                m_interrupt_scanline_counter = 0;
            }
        }
        
        public void OnIOWrite( int value )
        {
            Function func = GetFunction( value );

            switch ( func )
            {
                case Function.SelectPen:
                    {
                        if (( value & 0x10 ) != 0 )
                        {
                            // Bit four is set, do border colour
                            SetCurrentPen( BORDER_COLOUR_VALUE );
                        }
                        else
                        {
                            SetCurrentPen( value & 0x0F );
                        }
                    }
                    break;

                case Function.SelectColour:
                    {
                        SetPenColour( m_current_pen, value & 0x1F );
                    }
                    break;

                case Function.ScreenModeAndROM:
                    {
                        ScreenModeAndROMSelect( value );
                    }
                    break;

                case Function.RAMSelect:
                    {
                        m_memory.RAMBankSelect(value);
                    }
                    break;
            }

        }

        public void OnInterruptAcknowledge()
        {
            // Unset bit 5
            m_interrupt_scanline_counter &= 0x01F;
        }

        public void OnHSync()
        {
            // update GA scan line counter
            m_interrupt_scanline_counter++;

            if ( m_vsync_delay_counter == 0 )
            {
                if ( m_interrupt_scanline_counter == SCANLINE_COUNTER_MAX )
                {
                    // trigger interrupt?
                    m_cpu.SetInterruptRequest( true );
                    m_interrupt_scanline_counter = 0; // clear counter
                }
            }
            else
            { 
                // delaying on VSYNC?
                m_vsync_delay_counter--;
                if ( m_vsync_delay_counter == 0 ) 
                {
                    if ( m_interrupt_scanline_counter >= 32 ) 
                    { 
                        // counter above save margin?
                        // queue interrupt
                        m_cpu.SetInterruptRequest( true );
                    }
                    m_interrupt_scanline_counter = 0; // clear counter
                }
            }

        }

        public void OnVSync()
        {
            // Delay interrupts because of the VSync
            m_vsync_delay_counter = VSYNC_DELAY_AMOUNT;
        }

    }
}
