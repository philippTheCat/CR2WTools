﻿using System;
using System.IO;
using CR2W.IO;
using CR2W.Types;
using CR2W.Exceptions;

namespace CR2W.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class Uint8Attribute : CR2WPropertyAttribute
    {
        public Uint8Attribute(string name) : base(name, "Uint8") { }

        public override object DeSerialize(BinaryReader br, CR2WParser p)
        {
            return br.ReadByte();
        }

        public override void Serialize(object value, BinaryWriter bw, CR2WExporter e)
        {
            if (value is byte i)
            {
                bw.Write(e.AddNewReference(Name));
                bw.Write(e.AddNewReference(TypeStr));
                bw.Write((uint)5);
                bw.Write(i);
                return;
            }
            throw new UnknownObjectTypeException("[Uint8] Value was not a byte");
        }
    }
}
