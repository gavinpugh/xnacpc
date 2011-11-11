// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class Emulator
    {
        // Reference:
        // http://cpctech.cpc-live.com/docs/ints.html
        // http://www.irespa.eu/daan/lib/howtoCPCemu.htm

        private const int XNA_TICKS_PER_SECOND = 10000000;
        private const int CPU_SPEED = ( 3993600 );                            //< 4Mhz processor. # of tstates based on a 50Hz CPC
        private const int TSTATES_PER_FRAME = ( CPU_SPEED / TARGET_FPS );
        private const int TSTATES_PER_CLOCK = 4;

        public const int CLOCK_UPDATE_HZ = ( CPU_SPEED / TSTATES_PER_CLOCK );
        public const int TARGET_FPS = 50;

        private Memory m_memory;
        private CPU.Z80 m_processor;

        private PPI m_ppi;
        private CRTC m_crtc;
        private GateArray m_gate_array;
        private PSG m_psg;
        private Monitor m_monitor;
        private Keyboard m_keyboard;
                
        public Emulator( Audio audio, TextureDisplay texture_display )
        {
            m_memory = new Memory();
            
            m_processor = new CPU.Z80(
                m_memory.CPUWrite,
                m_memory.CPURead,
                OnIOWrite,
                OnIORead,
                OnInterruptAcknowledge,
                OnClock);

            m_gate_array = new GateArray(m_memory, m_processor);

            m_keyboard = new Keyboard();

            m_crtc = new CRTC( m_gate_array );

            m_psg = new PSG( audio, m_keyboard );
            m_ppi = new PPI( m_crtc, m_gate_array, m_psg );

            m_monitor = new Monitor( texture_display, m_crtc, m_memory, m_gate_array );

            Reset();
        }

        public void Reset()
        {
            // Reset all components.
            // Since this is via the Reset() interface on these devices, they could all be thrown in a List<> potentially.
            m_processor.Reset();
            m_monitor.Reset();
            m_keyboard.Reset();
            m_psg.Reset();
            m_crtc.Reset();
            m_gate_array.Reset();
            m_ppi.Reset();
            m_memory.Reset();
        }

        public uint BorderColour
        {
            get { return m_monitor.BorderColour; }
        }

        public CRTC CRTC
        {
            get { return m_crtc; }
        }

        public Memory Memory
        {
            get { return m_memory; }
        }

        public GateArray GateArray
        {
            get { return m_gate_array; }
        }

        public PPI PPI
        {
            get { return m_ppi; }
        }

        public Keyboard Keyboard
        {
            get { return m_keyboard; }
        }

        public CPU.Z80 Processor
        {
            get { return m_processor; }
        }

        public PSG PSG
        {
            get { return m_psg; }
        }

        private enum IOHardware
        {
            GateArray,
            RAMConfig,
            CRTC,
            ROMSelect,
            PrinterPort,
            PPI8255,
            Expansion,
            Unknown
        }


        private IOHardware DecodeIOPort( int port, ref int function )
        {
            // Reference:
            // http://www.cepece.info/amstrad/docs/iopord.html
            //
            // This logic reflects the first table on that page
            
            if ( ( port & 0x8000 ) == 0 )
            {
                if ( ( port & 0x4000 ) != 0 )
                {
                    return IOHardware.GateArray;
                }
                else
                {
                    return IOHardware.RAMConfig;
                }
            }
            else if ( ( port & 0x4000 ) == 0 )
            {
                // CRTC
                function = ( ( port & 0x0300 ) >> 8 );
                return IOHardware.CRTC;
            }
            else if ( ( port & 0x2000 ) == 0 )
            {
                // ROM select
                return IOHardware.ROMSelect;
            }
            else if ( ( port & 0x1000 ) == 0 )
            {
                return IOHardware.PrinterPort;
            }
            else if ( ( port & 0x0800 ) == 0 )
            {
                // 8255 PPI
                function = ( ( port & 0x0300 ) >> 8 );
                return IOHardware.PPI8255;
            }
            else if ( ( port & 0x0400 ) == 0 )
            {
                return IOHardware.Expansion;
            }

            return IOHardware.Unknown;
        }

        public void OnIOWrite( int location, int value )
        {
            int function = 0;
            IOHardware hardware = DecodeIOPort( location, ref function );

            switch ( hardware )
            {
                case IOHardware.GateArray:
                    {
                        m_gate_array.OnIOWrite( value );
                    }
                    break;

                case IOHardware.CRTC:
                    {
                        m_crtc.OnIOWrite( function, value );
                    }
                    break;

                case IOHardware.ROMSelect:
                    {
                        m_memory.SelectUpperROM( value );
                    }
                    break;

                case IOHardware.PPI8255:
                    {
                        m_ppi.IOWrite( function, value );
                    }
                    break;

                default:
                    {
                    }
                    break;
            }
        }

        public int OnIORead( int location )
        {
            int function = 0;
            IOHardware hardware = DecodeIOPort( location, ref function );
            
            switch ( hardware )
            {
                case IOHardware.PPI8255:
                {
                    return m_ppi.IORead( function );
                }

                case IOHardware.CRTC:
                {
                    return m_crtc.OnIORead( function );
                }
            }

            return 0xff;
        }

        public void Update()
        {
            // Simple update, run the CPU, and process the audio samples. That's it.
            // The CPU will call OnInterruptAcknowledge() and OnClock() as it executes. Along with any Memory/IO functions, of course.
            m_processor.execute( TSTATES_PER_FRAME );
            m_psg.OnFrame();
        }

        public void OnInterruptAcknowledge()
        {
            // Just these two need to know about interrupts
            m_gate_array.OnInterruptAcknowledge();
            m_monitor.OnInterruptAcknowledge();
        }
                        
        public void OnClock()
        {
            // Just these two have per-clock updates
            m_crtc.OnClock();
            m_psg.OnClock();
        }

        public void SkipDrawingNextFrame()
        {
            // This will still do correct emulation of the next frame. It just won't be written out to the destination texture.
            m_monitor.SkipNextFrame();
        }

        public void PreSnapshotHack()
        {
            // HACK: Some games don't like being started right at boot. I think this is something to do with Snapshot format v1 -vs- v3. I'm using v1
            // HACK: which would setup a lot of other hardware registers/values. This hack instead just executes a few of the initial boot cycles
            // HACK: before we load the snapshot.
            while ( CRTC.IsInVSync == false )
            {
                const int HACK_EXECUTE_TSTATES_BEFORE_SNAPSHOTS = (int)( 15600 );
                SkipDrawingNextFrame();
                m_processor.execute( HACK_EXECUTE_TSTATES_BEFORE_SNAPSHOTS );
            }
        }
    

    }
}
