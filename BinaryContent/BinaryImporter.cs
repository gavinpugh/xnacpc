// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;


namespace BinaryContent
{
	public class BinaryFile
	{
		public Byte[] m_data;
		public int m_data_size;
	}

	[ContentImporter( ".bin", ".sna", ".rom", ".dsk", ".cdt", DisplayName = "Binary Importer", DefaultProcessor = "None" )]
	public class BinaryImporter : ContentImporter<BinaryFile>
	{
		public override BinaryFile Import( string filename, ContentImporterContext context )
		{
			BinaryFile file = new BinaryFile();
			file.m_data = System.IO.File.ReadAllBytes( filename );
			file.m_data_size = file.m_data.Length;
			return file;
		}
	}
}
