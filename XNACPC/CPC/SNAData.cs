// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    struct SNAData
    {
        // Reference:
        // http://www.cpctech.org.uk/docs/snapshot.html
        // http://cpcwiki.eu/index.php/Format:SNA_snapshot_file_format

        private const string ID_STRING = "MV - SNA";

        enum Indices : int
        {
            IDString        = 0x00,

            // 0x08 - 0x0f are unused
            Version         = 0x10,
            RegisterF       = 0x11,
            RegisterA       = 0x12,
            RegisterC       = 0x13,
            RegisterB       = 0x14,
            RegisterE       = 0x15,
            RegisterD       = 0x16,
            RegisterL       = 0x17,
            RegisterH       = 0x18,
            RegisterR       = 0x19,
            RegisterI       = 0x1a,
            RegisterIFF0    = 0x1b,
            RegisterIFF1    = 0x1c,
            RegisterIXLow   = 0x1d,
            RegisterIXHigh  = 0x1e,
            RegisterIYLow   = 0x1f,
            RegisterIYHigh  = 0x20,
            RegisterSPLow   = 0x21,
            RegisterSPHigh  = 0x22,
            RegisterPCLow   = 0x23,
            RegisterPCHigh  = 0x24,
            InterruptMode   = 0x25,
            RegisterAltF    = 0x26,
            RegisterAltA    = 0x27,
            RegisterAltC    = 0x28,
            RegisterAltB    = 0x29,
            RegisterAltE    = 0x2A,
            RegisterAltD    = 0x2B,
            RegisterAltL    = 0x2C,
            RegisterAltH    = 0x2D,
            GASelectedPen   = 0x2E,
            GAPenColours    = 0x2F, //< 17 entries. 16 pens and one border
            GAMultiConfig   = 0x40, //< Screen mode and ROM enabledness
            GARAMSelect     = 0x41,
            CRTCSelReg      = 0x42,
            CRTCRegData     = 0x43, //< 18 entries
            ROMSelect       = 0x55,
            PPIPortA        = 0x56,
            PPIPortB        = 0x57,
            PPIPortC        = 0x58,
            PPIControl      = 0x59,
            PSGSelRegister  = 0x5a,
            PSGRegData      = 0x5b, //< 16 entries
            MemDumpSizeLow  = 0x6b,
            MemDumpSizeHigh = 0x6c,

            // v2 specific
            CPCType         = 0x6d,
            
            // v3 specific
            CRTCHorizChar   = 0xa9,
            CRTCVertChar    = 0xab,
            CRTCScanline    = 0xac,
            CRTCHSyncCount  = 0xae,
            CRTCVSyncCount  = 0xaf,
            CRTCStateFlags  = 0xb0,
            GAVSyncDelay    = 0xb2,
            GAScanlineCount = 0xb3,
            CPUIRQFlag      = 0xb4,
                            
            MemDump         = 0x100
        }

        byte[] m_data;

        public SNAData( byte[] data )
        {
            m_data = data;
        }
            
        public void LoadSnapshot( Emulator emulator )
        {
            // Check header
            for ( int i = 0; i < ID_STRING.Length; i++ )
            {
                Debug.Assert( ID_STRING[i] == GetByte( Indices.IDString + i ) );
            }

            if ( GetByte( Indices.Version ) < 3 )
            {
                // Use the hack for v1/2 snapshots that don't have CRTC/GA timing info
                emulator.PreSnapshotHack();
            }

            // Z80 processor state
            emulator.Processor.F( GetByte( Indices.RegisterF ) );
            emulator.Processor.A( GetByte( Indices.RegisterA ) );
            emulator.Processor.C( GetByte( Indices.RegisterC ) );
            emulator.Processor.B( GetByte( Indices.RegisterB ) );
            emulator.Processor.E( GetByte( Indices.RegisterE ) );
            emulator.Processor.D( GetByte( Indices.RegisterD ) );
            emulator.Processor.L( GetByte( Indices.RegisterL ) );
            emulator.Processor.H( GetByte( Indices.RegisterH ) );
            emulator.Processor.R( GetByte( Indices.RegisterR ) );
            emulator.Processor.I( GetByte( Indices.RegisterI ) );

            emulator.Processor.IFF1( GetByte( Indices.RegisterIFF0 ) != 0 );
            emulator.Processor.IFF2( GetByte( Indices.RegisterIFF1 ) != 0 );

            emulator.Processor.IX( GetWord( Indices.RegisterIXHigh, Indices.RegisterIXLow ) );
            emulator.Processor.IY( GetWord( Indices.RegisterIYHigh, Indices.RegisterIYLow ) );
            emulator.Processor.SP( GetWord( Indices.RegisterSPHigh, Indices.RegisterSPLow ) );
            emulator.Processor.PC( GetWord( Indices.RegisterPCHigh, Indices.RegisterPCLow ) );

            emulator.Processor.IM( GetByte( Indices.InterruptMode ) );

            emulator.Processor.SetAltAF( GetWord( Indices.RegisterAltA, Indices.RegisterAltF ) );
            emulator.Processor.SetAltBC( GetWord( Indices.RegisterAltB, Indices.RegisterAltC ) );
            emulator.Processor.SetAltDE( GetWord( Indices.RegisterAltD, Indices.RegisterAltE ) );
            emulator.Processor.SetAltHL( GetWord( Indices.RegisterAltH, Indices.RegisterAltL ) );

            // Gate Array
            emulator.GateArray.SetCurrentPen( GetByte( Indices.GASelectedPen ) );

            for ( int i = 0; i < GateArray.NUM_PEN_SETTINGS; i++ )
            {
                emulator.GateArray.SetPenColour( i, GetByte( Indices.GAPenColours + i ) );
            }

            emulator.GateArray.ScreenModeAndROMSelect( GetByte( Indices.GAMultiConfig ) );

            emulator.Memory.RAMBankSelect(GetByte(Indices.GARAMSelect));

            // CRTC
            emulator.CRTC.SelectRegister( (CRTC.Register)GetByte( Indices.CRTCSelReg ) );
            for ( int i = 0; i < CRTC.NUM_REGISTERS; i++ )
            {
                emulator.CRTC.SetRegister( (CRTC.Register)i, GetByte( Indices.CRTCRegData + i ) );
            }
            
            // ROM Select
            emulator.Memory.SelectUpperROM( GetByte( Indices.ROMSelect ) );

            // PPI
            emulator.PPI.WritePortC( GetByte( Indices.PPIControl ) );
            emulator.PPI.SetPortA( GetByte( Indices.PPIPortA ) );
            emulator.PPI.SetPortB( GetByte( Indices.PPIPortB ) );
            emulator.PPI.SetPortC( GetByte( Indices.PPIPortC ) );

            // PSG
            emulator.PSG.SelectRegister( (PSG.Register)GetByte( Indices.PSGSelRegister ) );
            for ( int i = 0; i < PSG.NUM_REGISTERS; i++ )
            {
                emulator.PSG.WriteRegister( (PSG.Register)i, GetByte( Indices.PSGRegData + i ) );
            }

            // Extra stuff for v3 snapshots
            if ( GetByte( Indices.Version ) >= 3 )
            {
                emulator.CRTC.SetTiming( 
                    GetByte( Indices.CRTCHorizChar ),
                    GetByte( Indices.CRTCVertChar ),
                    GetByte( Indices.CRTCScanline ),
                    GetByte( Indices.CRTCHSyncCount ),
                    GetByte( Indices.CRTCVSyncCount ),
                    ( GetByte( Indices.CRTCStateFlags ) & ( 1 << 1 )) != 0, //< HSync Active
                    ( GetByte( Indices.CRTCStateFlags ) & ( 1 << 0 )) != 0  //< VSync Active
                    );

                emulator.GateArray.SetCounters(
                    GetByte( Indices.GAScanlineCount ),
                    GetByte( Indices.GAVSyncDelay )
                    );

                emulator.Processor.SetInterruptRequest( GetByte( Indices.CPUIRQFlag ) != 0 );
            }

            // Memory Dump
            int mem_size = GetWord( Indices.MemDumpSizeHigh, Indices.MemDumpSizeLow );
            Debug.Assert( (mem_size == 64) || (mem_size == 128) ); //< NOTE: 128k not currently supported

            emulator.Memory.SetRAM( m_data, (int)Indices.MemDump, mem_size );
        }

        private int GetByte( Indices index )
        {
            return m_data[(int)index];
        }

        private int GetWord( Indices high_index, Indices low_index )
        {
            return ( ( m_data[(int)high_index] << 8 ) | m_data[(int)low_index] );
        }



    }
}
