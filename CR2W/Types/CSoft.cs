﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CR2W.IO;
using CR2W.Types.W3;

namespace CR2W.Types
{
    class CSoft<T> : IReferencable where T : CResource
    {
        public string DepotPath { get; set; }
        public ushort Flags     { get; set; }

        public Type GetSoftType()
        {
            return typeof(T);
        }

        public void ParseBytes(CR2WBinaryReader br, uint size)
        {
            var id = br.ReadUInt16();
            if(br.resources[id].type != typeof(T).Name)
            {
                throw new InvalidOperationException($"[soft] Mismatch type! Expected type is {typeof(T).Name} but value was read as {br.resources[id].type}");
            }
            DepotPath = br.resources[id].path;
            Flags     = br.resources[id].flags;
        }
    }
}