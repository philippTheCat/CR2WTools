using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using CR2W.DDS.Utils;
using CR2W.IO;

namespace CR2W.DDS
{
	public class DDSImage : IDisposable
	{
		private Bitmap _bitmap;
		private bool _isValid;
		private bool _alpha;

		public Bitmap BitmapImage
		{
			get { return _bitmap; }
		}

		public bool IsValid
		{
			get { return _isValid; }
		}

		public bool PreserveAlpha
		{
			get { return _alpha; }
			set { _alpha = value; }
		}

		public DDSImage(byte[] ddsImage, bool preserveAlpha = true)
		{
			if (ddsImage == null)
				return;

			if (ddsImage.Length == 0)
				return;

			_alpha = preserveAlpha;

			using (MemoryStream stream = new MemoryStream(ddsImage.Length))
			{
				stream.Write(ddsImage, 0, ddsImage.Length);
				stream.Seek(0, SeekOrigin.Begin);

				using (BinaryReader reader = new BinaryReader(stream))
				{
					Parse(reader);
				}
			}
		}

		public DDSImage(Stream ddsImage, bool preserveAlpha = true)
		{
			if (ddsImage == null)
				return;

			if (!ddsImage.CanRead)
				return;

			_alpha = preserveAlpha;

			using (BinaryReader reader = new BinaryReader(ddsImage))
			{
				Parse(reader);
			}
		}

		public void Dispose()
		{
			if (_bitmap != null)
			{
				_bitmap.Dispose();
				_bitmap = null;
			}
		}

		private void Parse(BinaryReader reader)
		{
			DDSStruct header = new DDSStruct();
			Utils.PixelFormat pixelFormat = Utils.PixelFormat.UNKNOWN;
			byte[] data = null;

			if (ReadHeader(reader, ref header))
			{
				_isValid = true;
				// patches for stuff
				if (header.depth == 0) header.depth = 1;

				uint blocksize = 0;
				pixelFormat = GetFormat(header, ref blocksize);
				if (pixelFormat == Utils.PixelFormat.UNKNOWN)
				{
					throw new InvalidFileHeaderException();
				}

				data = ReadData(reader, header);
				if (data != null)
				{
					byte[] rawData = DDSDecompressor.Expand(header, data, pixelFormat);
					_bitmap = CreateBitmap((int)header.width, (int)header.height, rawData);
				}
			}
		}

        public DDSImage(CR2WBinaryReader reader, DDSStruct header, bool alpha)
        {
            Utils.PixelFormat pixelFormat = Utils.PixelFormat.UNKNOWN;
            byte[] data = null;

            if (header.depth == 0) header.depth = 1;

            _isValid = true;
            _alpha = alpha;

            uint blocksize = 0;
            pixelFormat = GetFormat(header, ref blocksize);
            if (pixelFormat == Utils.PixelFormat.UNKNOWN)
            {
                throw new InvalidFileHeaderException();
            }

            data = ReadData(reader, header);
            if (data != null)
            {
                byte[] rawData = DDSDecompressor.Expand(header, data, pixelFormat);
                _bitmap = CreateBitmap((int)header.width, (int)header.height, rawData);
            }
        }

		private byte[] ReadData(BinaryReader reader, DDSStruct header)
		{
			byte[] compdata = null;
			uint compsize = 0;

			if ((header.flags & DDSHelper.DDSD_LINEARSIZE) > 1)
			{
				compdata = reader.ReadBytes((int)header.sizeorpitch);
				compsize = (uint)compdata.Length;
			}
			else
			{
				uint bps = header.width * header.pixelformat.rgbbitcount / 8;
				compsize = bps * header.height * header.depth;
				compdata = new byte[compsize];

				MemoryStream mem = new MemoryStream((int)compsize);

				byte[] temp;
				for (int z = 0; z < header.depth; z++)
				{
					for (int y = 0; y < header.height; y++)
					{
						temp = reader.ReadBytes((int)bps);
						mem.Write(temp, 0, temp.Length);
					}
				}
				mem.Seek(0, SeekOrigin.Begin);

				mem.Read(compdata, 0, compdata.Length);
				mem.Close();
			}

			return compdata;
		}

		private Bitmap CreateBitmap(int width, int height, byte[] rawData)
		{
			var pxFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
			if (_alpha)
				pxFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;

			Bitmap bitmap = new Bitmap(width, height, pxFormat);

			BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height)
				, ImageLockMode.WriteOnly, pxFormat);
			IntPtr scan = data.Scan0;
			int size = bitmap.Width * bitmap.Height * 4;

			unsafe
			{
				byte* p = (byte*)scan;
				for (int i = 0; i < size; i += 4)
				{
					// iterate through bytes.
					// Bitmap stores it's data in RGBA order.
					// DDS stores it's data in BGRA order.
					p[i] = rawData[i + 2]; // blue
					p[i + 1] = rawData[i + 1]; // green
					p[i + 2] = rawData[i];   // red
					p[i + 3] = rawData[i + 3]; // alpha
				}
			}

			bitmap.UnlockBits(data);
			return bitmap;
		}

		private bool ReadHeader(BinaryReader reader, ref DDSStruct header)
		{
			byte[] signature = reader.ReadBytes(4);
			if (!(signature[0] == 'D' && signature[1] == 'D' && signature[2] == 'S' && signature[3] == ' '))
				return false;

			header.size = reader.ReadUInt32();
			if (header.size != 124)
				return false;

			//convert the data
			header.flags = reader.ReadUInt32();
			header.height = reader.ReadUInt32();
			header.width = reader.ReadUInt32();
			header.sizeorpitch = reader.ReadUInt32();
			header.depth = reader.ReadUInt32();
			header.mipmapcount = reader.ReadUInt32();
			header.alphabitdepth = reader.ReadUInt32();

			header.reserved = new uint[10];
			for (int i = 0; i < 10; i++)
			{
				header.reserved[i] = reader.ReadUInt32();
			}

			//pixelfromat
			header.pixelformat.size = reader.ReadUInt32();
			header.pixelformat.flags = reader.ReadUInt32();
			header.pixelformat.fourcc = reader.ReadUInt32();
			header.pixelformat.rgbbitcount = reader.ReadUInt32();
			header.pixelformat.rbitmask = reader.ReadUInt32();
			header.pixelformat.gbitmask = reader.ReadUInt32();
			header.pixelformat.bbitmask = reader.ReadUInt32();
			header.pixelformat.alphabitmask = reader.ReadUInt32();

			//caps
			header.ddscaps.caps1 = reader.ReadUInt32();
			header.ddscaps.caps2 = reader.ReadUInt32();
			header.ddscaps.caps3 = reader.ReadUInt32();
			header.ddscaps.caps4 = reader.ReadUInt32();
			header.texturestage = reader.ReadUInt32();

            PrintHeader(header);

            return true;
		}

        public static void PrintHeader(DDSStruct header)
        {
            Console.WriteLine("header.");
            Console.WriteLine("\tsize             {0}", header.size);
            Console.WriteLine("\tflags            {0}", header.flags);
            Console.WriteLine("\twidth            {0}", header.width);
            Console.WriteLine("\theight           {0}", header.height);
            Console.WriteLine("\tsizeorpitch      {0}", header.sizeorpitch);
            Console.WriteLine("\tdepth            {0}", header.depth);
            Console.WriteLine("\tmipmapcount      {0}", header.mipmapcount);
            Console.WriteLine("\talphabitdepth    {0}", header.alphabitdepth);
            for (int i = 0; i < header.reserved.Length; i++)
            {
                Console.WriteLine("\treserved[{0}]      {1}", i, header.reserved[i]);
            }
            Console.WriteLine("header.pixelformat");
            Console.WriteLine("\tsize             {0}", header.pixelformat.size);
            Console.WriteLine("\tflags            {0}", header.pixelformat.flags);
            Console.WriteLine("\tfourcc           {0}", header.pixelformat.fourcc);
            Console.WriteLine("\trgbbitcount      {0}", header.pixelformat.rgbbitcount);
            Console.WriteLine("\trbitmask         {0}", header.pixelformat.rbitmask);
            Console.WriteLine("\tgbitmask         {0}", header.pixelformat.gbitmask);
            Console.WriteLine("\tbbitmask         {0}", header.pixelformat.bbitmask);
            Console.WriteLine("\talphabitmask     {0}", header.pixelformat.alphabitmask);
            Console.WriteLine("header.ddscaps");
            Console.WriteLine("\tcaps1            {0}", header.ddscaps.caps1);
            Console.WriteLine("\tcaps2            {0}", header.ddscaps.caps2);
            Console.WriteLine("\tcaps3            {0}", header.ddscaps.caps3);
            Console.WriteLine("\tcaps4            {0}", header.ddscaps.caps4);
            Console.WriteLine();
        }

        private Utils.PixelFormat GetFormat(DDSStruct header, ref uint blocksize)
		{
			Utils.PixelFormat format = Utils.PixelFormat.UNKNOWN;
			if ((header.pixelformat.flags & DDSHelper.DDPF_FOURCC) == DDSHelper.DDPF_FOURCC)
			{
				blocksize = ((header.width + 3) / 4) * ((header.height + 3) / 4) * header.depth;

				switch (header.pixelformat.fourcc)
				{
					case DDSHelper.FOURCC_DXT1:
						format = Utils.PixelFormat.DXT1;
						blocksize *= 8;
						break;

					case DDSHelper.FOURCC_DXT2:
						format = Utils.PixelFormat.DXT2;
						blocksize *= 16;
						break;

					case DDSHelper.FOURCC_DXT3:
						format = Utils.PixelFormat.DXT3;
						blocksize *= 16;
						break;

					case DDSHelper.FOURCC_DXT4:
						format = Utils.PixelFormat.DXT4;
						blocksize *= 16;
						break;

					case DDSHelper.FOURCC_DXT5:
						format = Utils.PixelFormat.DXT5;
						blocksize *= 16;
						break;

					case DDSHelper.FOURCC_ATI1:
						format = Utils.PixelFormat.ATI1N;
						blocksize *= 8;
						break;

					case DDSHelper.FOURCC_ATI2:
						format = Utils.PixelFormat.THREEDC;
						blocksize *= 16;
						break;

					case DDSHelper.FOURCC_RXGB:
						format = Utils.PixelFormat.RXGB;
						blocksize *= 16;
						break;

					case DDSHelper.FOURCC_DOLLARNULL:
						format = Utils.PixelFormat.A16B16G16R16;
						blocksize = header.width * header.height * header.depth * 8;
						break;

					case DDSHelper.FOURCC_oNULL:
						format = Utils.PixelFormat.R16F;
						blocksize = header.width * header.height * header.depth * 2;
						break;

					case DDSHelper.FOURCC_pNULL:
						format = Utils.PixelFormat.G16R16F;
						blocksize = header.width * header.height * header.depth * 4;
						break;

					case DDSHelper.FOURCC_qNULL:
						format = Utils.PixelFormat.A16B16G16R16F;
						blocksize = header.width * header.height * header.depth * 8;
						break;

					case DDSHelper.FOURCC_rNULL:
						format = Utils.PixelFormat.R32F;
						blocksize = header.width * header.height * header.depth * 4;
						break;

					case DDSHelper.FOURCC_sNULL:
						format = Utils.PixelFormat.G32R32F;
						blocksize = header.width * header.height * header.depth * 8;
						break;

					case DDSHelper.FOURCC_tNULL:
						format = Utils.PixelFormat.A32B32G32R32F;
						blocksize = header.width * header.height * header.depth * 16;
						break;

					default:
						format = Utils.PixelFormat.UNKNOWN;
						blocksize *= 16;
						break;
				}
			}
			else
			{
				// uncompressed image
				if ((header.pixelformat.flags & DDSHelper.DDPF_LUMINANCE) == DDSHelper.DDPF_LUMINANCE)
				{
					if ((header.pixelformat.flags & DDSHelper.DDPF_ALPHAPIXELS) == DDSHelper.DDPF_ALPHAPIXELS)
					{
						format = Utils.PixelFormat.LUMINANCE_ALPHA;
					}
					else
					{
						format = Utils.PixelFormat.LUMINANCE;
					}
				}
				else
				{
					if ((header.pixelformat.flags & DDSHelper.DDPF_ALPHAPIXELS) == DDSHelper.DDPF_ALPHAPIXELS)
					{
						format = Utils.PixelFormat.RGBA;
					}
					else
					{
						format = Utils.PixelFormat.RGB;
					}
				}

				blocksize = (header.width * header.height * header.depth * (header.pixelformat.rgbbitcount >> 3));
			}
            return format;
		}
	}
}