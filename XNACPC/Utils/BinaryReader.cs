// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;


namespace XNACPC
{
	public class BinaryFile
	{
		public Byte[] m_data;
		public int m_data_size;
	}

	public class BinaryReader : ContentTypeReader<BinaryFile>
	{
		protected override BinaryFile Read( ContentReader input, BinaryFile existingInstance )
		{
			existingInstance = new BinaryFile();
			existingInstance.m_data_size = input.ReadInt32();
			existingInstance.m_data = input.ReadBytes( existingInstance.m_data_size );
			return existingInstance;
		}
	}
}
