﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CR2W.IO
{
    public struct SHeader
    {
        public uint offset;
        public uint size;
        public uint crc32;
        public byte[] data;
    }
}
