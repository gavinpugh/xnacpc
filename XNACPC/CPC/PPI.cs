// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class PPI : Device
    {
        // Reference:
        // http://www.cpctech.org.uk/docs/manual/s158ap12.pdf
        // http://www.cpcwiki.eu/index.php/8255

        private enum Manufacturer
        {
            Isp = 0,
            Triumph,
            Saisho,
            Solavox,
            Awa,
            Schneider,
            Orion,
            Amstrad
        }

        private enum Function
        {
            PortA = 0,
            PortB,
            PortC,
            Control
        }

        private struct PortState
        {
            public bool m_input_mode;       // false == output mode
            public int m_operation_mode;    // Should be zero on CPC

            public int m_data;              // Data in port

            public void Reset()
            {
                m_input_mode = true;
                m_operation_mode = 0;
                m_data = 0xFF;
            }
        }

        PortState m_port_a;
        PortState m_port_b;
        PortState m_port_c_lower;
        PortState m_port_c_upper;

        PSG m_psg;
        CRTC m_crtc;
        GateArray m_gatearray;

        public PPI( CRTC crtc, GateArray gatearray, PSG psg )
        {
            m_crtc = crtc;
            m_psg = psg;
            m_gatearray = gatearray;

            m_port_a = new PortState();
            m_port_b = new PortState();
            m_port_c_lower = new PortState();
            m_port_c_upper = new PortState();

            Reset();
        }

        public void Reset()
        {
            m_port_a.Reset();
            m_port_b.Reset();
            m_port_c_lower.Reset();
            m_port_c_upper.Reset();
        }

        private void WritePortA( int value )
        {
            m_port_a.m_data = value;

            if ( m_port_a.m_input_mode == false )
            {
                // Work with the PSG
                m_psg.WriteValue(value);
            }
        }

        private int ReadPortA()
        {
            if ( m_port_a.m_input_mode )
            {
                return m_psg.GetValue( m_port_a.m_data );
            }
            else
            {
                return m_port_a.m_data;
            }
        }

        private void WritePortB( int value )
        {
            m_port_b.m_data = value;
        }

        private int ReadPortB()
        {
            if ( m_port_b.m_input_mode )
            {
                // OS Settings

                int cassette_read = 0; // TODO
                int printer_ready = 1; // TODO
                int expansion_port = 0; // TODO
                int refresh_50hz = 1;
                int manufacturer = (int)Manufacturer.Amstrad; // TODO: Allow this to change?
                int vsync = m_crtc.IsInVSync ? 1 : 0;

                Debug.Assert( cassette_read < 2 );
                Debug.Assert( printer_ready < 2 );
                Debug.Assert( expansion_port < 2 );
                Debug.Assert( refresh_50hz < 2 );
                Debug.Assert( manufacturer < 8 );
                Debug.Assert( vsync < 2 );

                return (
                    ( cassette_read << 7 ) |
                    ( printer_ready << 6 ) |
                    ( expansion_port << 5 ) |
                    ( refresh_50hz << 4 ) |
                    ( manufacturer << 1 ) |
                    ( vsync )
                    );
            }
            else
            {
                return m_port_b.m_data;
            }
        }

        private int ReadPortC()
        {
            int ret_val = m_port_c_upper.m_data;

            if ( m_port_c_upper.m_input_mode )
            {
                ret_val |= 0xf0;
            }
            if ( m_port_c_lower.m_input_mode )
            {
                ret_val |= 0x0f;
            }

            return ret_val;
        }

        public void WritePortC( int value )
        {
            m_port_c_lower.m_data = value;
            m_port_c_upper.m_data = value;

            if ( m_port_c_lower.m_input_mode == false )
            {
                // Lower C is set to output

                // Tell keyboard which row to scan
                m_psg.Keyboard.SetCurrentRow( m_port_c_lower.m_data & 0x0F );
            }

            if ( m_port_c_upper.m_input_mode == false )
            {
                // Upper C is set to output

                // Work with the PSG
                m_psg.SetFunction(value);
                m_psg.WriteValue(m_port_a.m_data);
            }
        }

        public void SetPortA( int value )
        {
            m_port_a.m_data = value;
        }

        public void SetPortB( int value )
        {
            m_port_b.m_data = value;
        }

        public void SetPortC( int value )
        {
            m_port_c_upper.m_data = value;
            m_port_c_lower.m_data = value;
        }

        private void WriteControl( int value )
        {
            if (( value & 0x80 ) != 0 )
            {
                // PPI Control with Bit7=1

                // Bits 5 and 6 - Port A / Upper C operation mode
                int op_mode_a = ( ( value & 0x60 ) >> 5 );
                Debug.Assert( op_mode_a == 0 );
                m_port_a.m_operation_mode = op_mode_a;
                m_port_c_upper.m_operation_mode = op_mode_a;

                // Bit 4 - Port A direction
                m_port_a.m_input_mode = (( value & 0x10 ) != 0 );

                // Bit 3 - Port C Upper direction
                m_port_c_upper.m_input_mode = (( value & 0x08 ) != 0 );;

                // Bit 2 - Port B / Lower C operation mode
                int op_mode_b = ( ( ( value & 0x04 ) != 0 ) ? 1 : 0 );
                Debug.Assert( op_mode_b == 0 );
                m_port_b.m_operation_mode = op_mode_b;
                m_port_c_lower.m_operation_mode = op_mode_b;

                // Bit 1 - Port B direction
                m_port_b.m_input_mode = (( value & 0x02 ) != 0 );

                // Bit 0 - Port C Lower direction
                m_port_c_lower.m_input_mode = (( value & 0x01 ) != 0 );

                // From: http://www.cpcwiki.eu/index.php/8255
                // "CAUTION: Writing to PIO Control Register (with Bit7 set), automatically resets PIO Ports A,B,C to 00h each!"
                m_port_a.m_data = 0;
                m_port_b.m_data = 0;
                m_port_c_lower.m_data = 0;
                m_port_c_upper.m_data = 0;
            }
            else
            {
                // isolate bit to set
                int bit = (value >> 1) & 7;

                // set bit?
                if ((value & 1) != 0 )
                {
                    // set requested bit
                    m_port_c_lower.m_data |= (1 << bit);
                    m_port_c_upper.m_data |= (1 << bit);
                }
                else
                {
                    // reset requested bit
                    m_port_c_lower.m_data &= ~(1 << bit);
                    m_port_c_upper.m_data &= ~(1 << bit);
                }

                // output lower half?
                if (m_port_c_lower.m_input_mode == false)
                {
                    // Should update keyboard
                    m_psg.Keyboard.SetCurrentRow( m_port_c_lower.m_data & 0x0F );
                }

                // output upper half?
                if (m_port_c_upper.m_input_mode == false)
                {
                    // Work with the PSG
                    m_psg.SetFunction(value);
                    m_psg.WriteValue(m_port_a.m_data);
                }
            }
        }

        public void IOWrite( int function, int value )
        {
            switch ( (Function)function )
            {
                case Function.PortA: // Port A
                    WritePortA( value );
                    break;

                case Function.PortB: // Port B
                    WritePortB( value );
                    break;

                case Function.PortC: // Port C
                    WritePortC( value );
                    break;

                case Function.Control: // Control
                    WriteControl( value );
                    break;
            }
        }

        public int IORead( int function )
        {
            switch ( (Function)function )
            {
                case Function.PortA: // Port A
                    return ReadPortA();

                case Function.PortB: // Port B
                    return ReadPortB();

                case Function.PortC: // Port C
                    return ReadPortC();

                // Control function is write-only
            }

            return 0xFF;
        }



    }
}
