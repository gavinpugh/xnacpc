// XNACPC - An Amstrad CPC Emulator written in C#, using XNA.
// (c) Gavin Pugh 2011 - http://www.gavpugh.com/

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;

namespace BinaryContent
{

	[ContentTypeWriter]
	public class BinaryWriter : ContentTypeWriter<BinaryFile>
	{
		protected override void Write( ContentWriter output, BinaryFile value )
		{
			output.Write( value.m_data_size );
			output.Write( value.m_data );
		}

		public override string GetRuntimeReader( TargetPlatform targetPlatform )
		{
			// TODO: change this to the name of your ContentTypeReader
			// class which will be used to load this data.
			return "XNACPC.BinaryReader, XNACPC";
		}
	}
}
