// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

//#if !XBOX
#define EXTRA_RAM_SUPPORT   // I'm worried about slowdown on Xbox, hence the #define
                            // EDIT: Turns out it's not too bad. The difference is negligible.
//#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace XNACPC.CPC
{
    class Memory : Device
    {
        // Reference:
        // http://www.cpcwiki.eu/index.php/Gate_Array

        byte[] m_ram;
        ROM[] m_roms;

        bool m_upper_rom_active;
        bool m_lower_rom_active;
        int m_upper_rom_select;

        public const int BANK_SIZE = (16 * 1024);                   // 16kb Banks
        public const int BASE_RAM_NUM_BANKS = 4;                    // 64kb base ram
#if EXTRA_RAM_SUPPORT
        public const int TOTAL_RAM_NUM_BANKS = 8;                   // 128kb ... 64kb base ram + the 64k expansion
#else // #if EXTRA_RAM_SUPPORT
        public const int TOTAL_RAM_NUM_BANKS = BASE_RAM_NUM_BANKS;  // 64kb base ram
#endif // #if EXTRA_RAM_SUPPORT
        public const int NUM_BANK_SETUPS = 8;                       // 8 possible settings from the Gatearray for a standard 64k expansion setup

        public const int MAX_ROMS = 256;

        int[] m_current_bank_setup;

        static private readonly int[][] m_bank_setups = new int[NUM_BANK_SETUPS][];

        static Memory()
        {
            // From http://www.cpcwiki.eu/index.php/Gate_Array#Register_3_-_RAM_Banking
            m_bank_setups[0] = new int[BASE_RAM_NUM_BANKS] { 0, 1, 2, 3 };
            m_bank_setups[1] = new int[BASE_RAM_NUM_BANKS] { 0, 1, 2, 7 };
            m_bank_setups[2] = new int[BASE_RAM_NUM_BANKS] { 4, 5, 6, 7 };
            m_bank_setups[3] = new int[BASE_RAM_NUM_BANKS] { 0, 3, 2, 7 };
            m_bank_setups[4] = new int[BASE_RAM_NUM_BANKS] { 0, 4, 2, 3 };
            m_bank_setups[5] = new int[BASE_RAM_NUM_BANKS] { 0, 5, 2, 3 };
            m_bank_setups[6] = new int[BASE_RAM_NUM_BANKS] { 0, 6, 2, 3 };
            m_bank_setups[7] = new int[BASE_RAM_NUM_BANKS] { 0, 7, 2, 3 };
        }

        public Memory()
        {
            m_ram = new byte[TOTAL_RAM_NUM_BANKS * BANK_SIZE];
            m_roms = new ROM[MAX_ROMS];

            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < m_ram.Length;i++ )
            {
                m_ram[i] = 0;
            }

            m_current_bank_setup = m_bank_setups[0];
            
            m_upper_rom_active = true;
            m_lower_rom_active = true;
            m_upper_rom_select = 0;
        }

        public void LoadROM( int index, byte[] rom_data, int rom_start_pos )
        {
            m_roms[index] = new ROM( rom_data, rom_start_pos, ROM.ROM_SIZE );
        }

        public void UnloadROM( int index )
        {
            m_roms[index] = null;
        }

        public byte[] RAM
        {
            get { return m_ram; }
        }

        public void SetUpperROMState( bool enabled )
        {
            m_upper_rom_active = enabled;
        }

        public void SetLowerROMState( bool enabled )
        {
            m_lower_rom_active = enabled;
        }

        public void SelectUpperROM( int index )
        {
            if ( m_roms[index] == null )
            {
                // Drop to BASIC rom if one doesn't exist
                m_upper_rom_select = 0;
            }
            else
            {
                m_upper_rom_select = index;
            }
        }

        public void RAMBankSelect( int ga_register )
        {
            // From http://www.cpcwiki.eu/index.php/Gate_Array#Register_3_-_RAM_Banking
            int bank_setup = (ga_register & 0x07);
            m_current_bank_setup = m_bank_setups[bank_setup];
        }

        public void CPUWrite( int location, int value )
        {
            // Writes go straight through to RAM

#if EXTRA_RAM_SUPPORT
            int bank = (location & 0xC000) >> 14;
            int mem_offset = m_current_bank_setup[bank] * BANK_SIZE;
            m_ram[mem_offset + (location & 0x3FFF)] = (byte)value;
#else // #if EXTRA_RAM_SUPPORT
            m_ram[location] = (byte)value;
#endif // #if EXTRA_RAM_SUPPORT
        }

        public int CPURead( int location )
        {
            // Reads may access ROMs. If they are switched in.
            if ( location < 0x4000 )
            {
                if ( m_lower_rom_active )
                {
                    return m_roms[ROM.LOWER_ROM_INDEX].Read( location );
                }
            }

            if ( location >= 0xC000 )
            {
                if ( m_upper_rom_active )
                {
                    return m_roms[m_upper_rom_select].Read( location - 0xC000 );
                }
            }

#if EXTRA_RAM_SUPPORT
            int bank = (location & 0xC000) >> 14;
            int mem_offset = m_current_bank_setup[bank] * BANK_SIZE;
            return m_ram[mem_offset + (location & 0x3FFF)];
#else // #if EXTRA_RAM_SUPPORT
            return m_ram[location];
#endif // #if EXTRA_RAM_SUPPORT
        }

        public void SetRAM(byte[] buffer, int buffer_offset, int size_kb )
        {
            // Called by snapshot loading code
            Debug.Assert((size_kb == 64) || (size_kb == 128));
            int num_banks = BASE_RAM_NUM_BANKS;

#if EXTRA_RAM_SUPPORT
            if ( size_kb == 128 )
            {
                num_banks = TOTAL_RAM_NUM_BANKS;
            }
#endif // EXTRA_RAM_SUPPORT

            Buffer.BlockCopy(buffer, buffer_offset, m_ram, 0, num_banks * BANK_SIZE);
        }

    }
}
