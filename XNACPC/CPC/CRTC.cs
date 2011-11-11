// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class CRTC : Device
    {
        // Reference:
        // http://www.cepece.info/amstrad/docs/crtcnew.html
        // http://www.grimware.org/doku.php/documentations/devices/crtc
        // http://www.cpcwiki.eu/imgs/c/c0/Hd6845.hitachi.pdf
        // http://www.cpcwiki.eu/index.php/CRTC
        // http://www.6502.org/users/andre/hwinfo/crtc/diffs.html
                
        public const int NUM_REGISTERS = 18;

        public enum Register
        {
            HorizontalTotal = 0,
            HorizontalDisplayed,
            HorizontalSyncPosition,
            HorizAndVertSyncWidths,
            VerticalTotal,
            VerticalTotalAdjust,
            VerticalDisplayed,
            VerticalSyncPosition,
            InterlaceAndSkew,
            MaximumRasterAddress,
            CursorStartRaster,
            CursorEndRaster,
            DisplayStartAddressHigh,
            DisplayStartAddressLow,
            CursorAddressHigh,
            CursorAddressLow,
            LightPenAddressHigh,
            LightPenAddressLow
        }

        private enum Function
        {
            SelectRegister = 0,
            WriteSelectedRegister,
            Unused,
            ReadFromSelectedRegister
        }

        private static readonly int[] REGISTER_DEFAULTS = new int[NUM_REGISTERS] {
            0x3f, // HorizontalTotal
            0x28, // HorizontalDisplayed
            0x34, // HorizontalSyncPosition
            0x34, // HorizAndVertSyncWidths
            0x14, // VerticalTotal
            0x08, // VerticalTotalAdjust
            0x10, // VerticalDisplayed
            0x13, // VerticalSyncPosition
            0x00, // InterlaceAndSkew
            0x0b, // MaximumRasterAddress
            0x49, // CursorStartRaster
            0x0a, // CursorEndRaster
            0x00, // DisplayStartAddressHigh
            0x00, // DisplayStartAddressLow
            0x00, // CursorAddressHigh
            0x00, // CursorAddressLow
            0x00, // LightPenAddressHigh
            0x00, // LightPenAddressLow
        };

        private static readonly int[] REGISTER_MASKS = new int[NUM_REGISTERS] {
            0xff, // HorizontalTotal
            0xff, // HorizontalDisplayed
            0xff, // HorizontalSyncPosition
            0xff, // HorizAndVertSyncWidths
            0x7f, // VerticalTotal
            0x1f, // VerticalTotalAdjust
            0x7f, // VerticalDisplayed
            0x7f, // VerticalSyncPosition
            0x03, // InterlaceAndSkew
            0x1f, // MaximumRasterAddress
            0x1f, // CursorStartRaster
            0x1f, // CursorEndRaster
            0x3f, // DisplayStartAddressHigh
            0xff, // DisplayStartAddressLow
            0x3f, // CursorAddressHigh
            0xff, // CursorAddressLow
            0x3f, // LightPenAddressHigh
            0xff, // LightPenAddressLow
        };

        public delegate void SyncCallback();
        
        private SyncCallback m_hsync_callbacks;
        private SyncCallback m_vsync_callbacks;
            
        int[] m_registers;
        Register m_register_selected;

        int m_hori_sync_width;
        int m_vert_sync_width;

        int m_current_column;
        int m_current_line_in_row;
        int m_current_row;
        
        bool m_in_hsync;
        bool m_in_vsync;

        int m_hsync_counter;
        int m_vsync_counter;
        
        public CRTC( GateArray gatearray )
        {
            // Implicitly hook up the gatearray to ourselves
            AddHSyncCallback( gatearray.OnHSync );
            AddVSyncCallback( gatearray.OnVSync );

            m_registers = new int[NUM_REGISTERS];
            
            Reset();
        }

        public void Reset()
        {
            for ( int i = 0; i < NUM_REGISTERS; i++ )
            {
                // This also will set 'm_hori_sync_width' and 'm_vert_sync_width'
                SetRegister( (Register)i, REGISTER_DEFAULTS[i] );
            }
            m_register_selected = 0;

            m_current_column = 0;
            m_current_line_in_row = 0;
            m_current_row = 0;
            
            m_in_hsync = false;
            m_in_vsync = false;

            m_hsync_counter = 0;
            m_vsync_counter = 0;
        }
        
        public void AddHSyncCallback( SyncCallback hsync_callback )
        {
            m_hsync_callbacks += hsync_callback;
        }

        public void AddVSyncCallback( SyncCallback vsync_callback )
        {
            m_vsync_callbacks += vsync_callback;
        }

        public void SetTiming( int horiz_char, int vert_char, int scanline, int hsync_count, int vsync_count, bool hsync_on, bool vsync_on )
        {
            // Used by SNA-snapshot code
            m_current_column = horiz_char;
            Debug.Assert( m_current_column <= GetRegister( Register.HorizontalTotal ) );
            m_current_row = vert_char;
            Debug.Assert( m_current_row <= GetRegister( Register.VerticalTotal ) );
            m_current_line_in_row = scanline;
            Debug.Assert( m_current_line_in_row <= GetRegister( Register.MaximumRasterAddress ) );

            if ( hsync_on )
            {
                // Sync counters count up when given to us. So have to convert to my 'count-down' form.
                Debug.Assert( hsync_count <= m_hori_sync_width );
                m_hsync_counter = m_hori_sync_width - hsync_count;
                m_in_hsync = true;
            }
            else
            {
                m_hsync_counter = 0;
                m_in_hsync = false;
            }

            if ( vsync_on )
            {
                // Sync counters count up when given to us. So have to convert to my 'count-down' form.
                Debug.Assert( vsync_count <= m_vert_sync_width );
                m_vsync_counter = m_vert_sync_width - vsync_count;
                m_in_vsync = true;
            }
            else
            {
                m_vsync_counter = 0;
                m_in_vsync = false;
            }
        }

        public void OnIOWrite( int function, int value )
        {
            switch ( (Function)function )
            {
                case Function.SelectRegister:
                    {
                        // Select internal 6845 register
                        // Address Register is 5-bits
                        int reg = ( value & 0x1f );
                        SelectRegister( (Register)reg );
                    }
                    break;

                case Function.WriteSelectedRegister: 
                    {
                        // Write to selected internal 6845 register
                        WriteSelectedRegister( value );
                    }
                    break;
            }
        }

        public int OnIORead( int function )
        {
            switch ( (Function)function )
            {
                case Function.ReadFromSelectedRegister: 
                    {
                        // Read from selected internal 6845 register
                        return ReadSelectedRegister();
                    }
            }

            return 0xFF;
        }

        public void SelectRegister( Register register )
        {
            m_register_selected = register;
        }

        public void WriteSelectedRegister( int value )
        {
            Debug.Assert( value >= 0 );
            Debug.Assert( value < 256 );
            SetRegister( m_register_selected, value );
        }

        public int ReadSelectedRegister()
        {
            // From: http://www.cpcwiki.eu/index.php/CRTC
            // "On type 0 and 1, if a Write Only register is read from, "0" is returned."
            if ( m_register_selected < Register.DisplayStartAddressHigh )
            {
                return 0;
            }

            return m_registers[(int)m_register_selected];
        }

        public int GetRegister( Register register )
        {
            return m_registers[(int)register];
        }

        public void SetRegister( Register register, int value )
        {
            Debug.Assert( value >= 0 );
            Debug.Assert( value < 256 );
            m_registers[(int)register] = ( value & REGISTER_MASKS[(int)register] );

            if ( register == Register.HorizAndVertSyncWidths )
            {
                m_hori_sync_width = ( ( GetRegister( Register.HorizAndVertSyncWidths ) >> 0 ) & 0x0f );
                m_vert_sync_width = ( ( GetRegister( Register.HorizAndVertSyncWidths ) >> 4 ) & 0x0f );
                if ( m_hori_sync_width == 0 )
                {
                    m_hori_sync_width = 16; // Zero for this means '16'
                }
                if ( m_vert_sync_width == 0 )
                {
                    m_vert_sync_width = 16; // Zero for this means '16'
                }
            }
        }
        
        public void OnClock()
        {
            // Great diagram of what this function deals with, here:
            // http://www.grimware.org/doku.php/documentations/devices/crtc#crtc
            
            // Deal with hsync
            if ( m_hsync_counter > 0 )
            {
                m_hsync_counter--;
                if ( m_hsync_counter == 0 )
                {
                    m_in_hsync = false;
                }
            }

            // Clock means we've moved one character horizontally.
            m_current_column++;

            // We done with this scanline yet?
            if ( m_current_column == ( GetRegister( Register.HorizontalTotal ) + 1 ) )
            { 
                // Yup, go back to zero for the next scanline
                m_current_column = 0;
                
                // Decrement for any vsync that could be occuring
                if (m_vsync_counter > 0 )
                {
                    m_vsync_counter--;
                    if ( m_vsync_counter == 0 )
                    {
                        m_in_vsync = false;
                    }
                }

                // New line
                m_current_line_in_row++;

                // Done with this row?
                if ( m_current_line_in_row == ( GetRegister( Register.MaximumRasterAddress ) + 1 ) )
                { 
                    // We're at line #0 of the new row then.
                    m_current_line_in_row = 0;
                    m_current_row++;

                    // Off the bottom of the whole screen?
                    if ( m_current_row == ( GetRegister( Register.VerticalTotal ) + 1 ) )
                    { 
                        // Wraparound. We're right back at the top again!
                        m_current_row = 0;
                    }
                }

                if ( ( m_in_vsync == false ) && ( m_current_row == GetRegister( Register.VerticalSyncPosition ) ) )
                {
                    // Start of a new vsync
                    m_in_vsync = true;
                    m_vsync_counter = m_vert_sync_width;
                    m_vsync_callbacks();
                }

                // TODO : VerticalTotalAdjust
                // TODO : No games I've tested make use of it yet.
            }
            else if ( ( m_in_hsync == false ) && ( m_current_column == GetRegister( Register.HorizontalSyncPosition ) ) )
            {
                m_in_hsync = true;
                m_hsync_counter = m_hori_sync_width;
                m_hsync_callbacks();
            }
        }
        
        public bool IsInVSync
        {
            get { return m_in_vsync; }
        }

        public bool IsInHSync
        {
            get { return m_in_hsync; }
        }

        public int CurrentVerticalRow
        {
            get { return m_current_row; }
        }

        public int CurrentVerticalScanlineInRow
        {
            get { return m_current_line_in_row; }
        }

    }
}
