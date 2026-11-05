using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ZanLibrary.Formats.Container
{
    public struct ScrEntry 
    {
        public byte[] ModelData;
        public string Name;
        public float tx, ty, tz; // Translation
        public float rx, ry, rz; // Rotation
        public float sx, sy, sz; // Scale

        public byte[] ExtraData;

        public ScrEntry(string Name, float tx, float ty, float tz, float rx, float ry, float rz, float sx, float sy, float sz, byte[] ExtraData, byte[] ModelData)
        {
            this.ModelData = ModelData;
            this.Name = Name;
            this.tx = tx;
            this.ty = ty;
            this.tz = tz;

            this.rx = rx;
            this.ry = ry;
            this.rz = rz;

            this.sx= sx;
            this.sy= sy;
            this.sz= sz;

            this.ExtraData= ExtraData;
           

        }

        public ScrEntry WithData(byte[] modelData) =>
                new ScrEntry(Name, tx, ty, tz, rx, ry, rz, sx, sy, sz, ExtraData, modelData);
    }

    public struct ScrLoadResult
    {
        public Endianness Endian;
        public ScrEntry[] Models;

        public short unknownHeaderVar;
    }


    public class ScrFile
    {
        public static ScrLoadResult Load(byte[] data, bool IsBigEndian = false)
        {


            ScrLoadResult Return = new ScrLoadResult();
            List<ScrEntry> Models = new List<ScrEntry>();

            BinReader reader = new BinReader(data, Endianness.Little);
            string magic = reader.ReadString(4);
            Return.unknownHeaderVar = reader.ReadInt16();

            uint modelCount = reader.ReadUInt16();
            uint modelStartOffset = reader.ReadUInt32();

            reader.Seek(modelStartOffset);

            uint[] modelRecordOffsets;

            modelRecordOffsets = reader.ReadUInt32Array(modelCount);

            List<uint> modelDataOffsets = new();


            for (uint i = 0; i < modelCount; i++)
            {
                reader.Seek(modelRecordOffsets[i]);
                modelDataOffsets.Add(reader.ReadUInt32());

                string name = reader.ReadString(64).Replace("\0", "");


                ScrEntry entry = new ScrEntry(name, 
                    reader.ReadFloat32(), reader.ReadFloat32(), reader.ReadFloat32(),
                    reader.ReadFloat32(), reader.ReadFloat32(), reader.ReadFloat32(),
                    reader.ReadFloat32(), reader.ReadFloat32(), reader.ReadFloat32(),
                    reader.ReadByteArray(36),
                    null);

                Models.Add(entry);
            }
            

            for (uint i = 0; i < modelCount; i++)
            {
                uint ofs = modelDataOffsets[(int)i];
                reader.Seek(ofs);

                uint dataSize = 0;
                if (i+1 < modelCount)
                {
                    dataSize = (modelRecordOffsets[(int)i + 1] - (uint)reader.Tell());
                }
                else
                {
                    dataSize = ((uint)reader.GetBufferLength() - (uint)reader.Tell());
                }


                Models[(int)i] = Models[(int)i].WithData(reader.ReadByteArray(dataSize)); // I hate ambigous pass-by-reference, porting zanlibrary to C++ after this

            }




            Return.Models = [.. Models];
            return Return;
        }

    }
}
