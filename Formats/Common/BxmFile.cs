using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualBasic;

namespace ZanLibrary.Formats.Common
{
    public struct BxmHeader
    {
        public string Magic;
        public uint Flags;
        public ushort NodeCount;
        public ushort DataCount;
        public uint DataSize;
    }
    public struct BxmDataOffset
    {
        public ushort NameOffset;
        public ushort DataOffset;

        public bool Equals(BxmDataOffset other)
        {
            return (NameOffset == other.NameOffset) && (DataOffset == other.DataOffset);
        }
    }
    public struct BxmNodeInfo
    {
        public short ChildCount;
        public ushort FirstChildIndex;
        public short AttributeCount;
        public short DataIndex;
    }
    public struct BxmAttribute
    {
        public string Name;
        public string Value;
    }
    public class BxmDataNode
    {
        public string Name;
        public string Value;
        public Dictionary<string, string> Attributes;
        public List<BxmDataNode> Children;
        public BxmDataNode Parent;

        public int Index;
        public int ChildCount;
        public int FirstChildIndex;
    }
    public class BxmFile
    {
        private static List<BxmDataNode> GetNodeChildren(BxmDataNode[] Nodes, BxmDataNode RequestedNode)
        {
            var FirstChild = RequestedNode.FirstChildIndex;
            var Children = Nodes.Skip(FirstChild).Take(RequestedNode.ChildCount);
            foreach (var node in Children)
            {
                node.Parent = RequestedNode;
                node.Children = GetNodeChildren(Nodes, node);
            }
            return Children.ToList();
        }
        public static BxmDataNode[] Load(byte[] data, bool isJis = false)
        {

            BinReader reader = new BinReader(data, Endianness.Big);
            BxmHeader header = new BxmHeader();

            header.Magic = reader.ReadString(4);
            header.Flags = reader.ReadUInt32();
            header.NodeCount = reader.ReadUInt16();
            header.DataCount = reader.ReadUInt16();
            header.DataSize = reader.ReadUInt32();

            List<BxmNodeInfo> Infos = new List<BxmNodeInfo>();
            for (int i = 0; i < header.NodeCount; i++)
            {
                BxmNodeInfo NodeInfo = new BxmNodeInfo();
                NodeInfo.ChildCount = reader.ReadInt16();
                NodeInfo.FirstChildIndex = reader.ReadUInt16();
                NodeInfo.AttributeCount = reader.ReadInt16();
                NodeInfo.DataIndex = reader.ReadInt16();
                Infos.Add(NodeInfo);
            }

            List<BxmDataOffset> dataOffsets = new List<BxmDataOffset>();
            for (int i = 0; i < header.DataCount; i++)
            {
                BxmDataOffset DataOffset = new BxmDataOffset();
                DataOffset.NameOffset = reader.ReadUInt16();
                DataOffset.DataOffset = reader.ReadUInt16();
                dataOffsets.Add(DataOffset);
            }

            uint stringsOffset = (uint)(0x10 + 8 * header.NodeCount + header.DataCount * 4);

            List<BxmDataNode> nodes = new List<BxmDataNode>();
            for (int i = 0; i < Infos.Count; i++)
            {
                BxmNodeInfo info = Infos[i];
                BxmDataNode node = new BxmDataNode();

                node.Index = i;
                node.FirstChildIndex = info.FirstChildIndex;
                node.ChildCount = info.ChildCount;

                var nameOffset = dataOffsets[info.DataIndex].NameOffset;
                if (nameOffset != 0xFFFF)
                {
                    reader.Seek(stringsOffset + nameOffset);
                    node.Name = isJis ? reader.ReadStringJIS() : reader.ReadString();
                }
                var dataOffset = dataOffsets[info.DataIndex].DataOffset;
                if (dataOffset != 0xFFFF)
                {
                    reader.Seek(stringsOffset + dataOffset);
                    node.Value = isJis ? reader.ReadStringJIS() : reader.ReadString();
                }

                node.Attributes = new Dictionary<string, string>();
                for (int j = 0; j < info.AttributeCount; j++)
                {
                    string name = string.Empty;
                    string value = string.Empty;
                    var attrNameOffset = dataOffsets[info.DataIndex + 1 + j].NameOffset;
                    if (attrNameOffset != 0xFFFF)
                    {
                        reader.Seek(stringsOffset + attrNameOffset);
                        name = isJis ? reader.ReadStringJIS() : reader.ReadString();
                    }
                    var attrDataOffset = dataOffsets[info.DataIndex + 1 + j].DataOffset;
                    if (attrDataOffset != 0xFFFF)
                    {
                        reader.Seek(stringsOffset + attrDataOffset);
                        value = isJis ? reader.ReadStringJIS() : reader.ReadString();
                    }
                    node.Attributes[name] = value;
                }
                nodes.Add(node);
            }

            foreach (var node in nodes)
            {
                node.Children = GetNodeChildren(nodes.ToArray(), node);
                foreach (var child in node.Children)
                    child.Parent = node;
            }

            return nodes.ToArray();
        }

        public static byte[] Save(BxmDataNode root)
        {
            /*
             * Generating part
             */

            List<BxmDataNode> nodes = new List<BxmDataNode>();

            void FlattenChildren(BxmDataNode node)
            {
                nodes.Add(node);
                foreach (var child in node.Children) { 
                    FlattenChildren(child);
                }
            }

            FlattenChildren(root);

            foreach (var node in nodes)
            {
                Console.WriteLine($"{node.Name.PadRight(16)} : {node.Index.ToString().PadRight(4)} : {node.FirstChildIndex}");
            }

            HashSet<string> UniqueStrings = new HashSet<string>();
            void AddUniqueString(string str)
            {
                if (str != null)
                    UniqueStrings.Add(str);
            }
            foreach (var node in nodes)
            {
                AddUniqueString(node.Name);
                foreach (var attr in node.Attributes)
                {
                    AddUniqueString(attr.Key);
                    AddUniqueString(attr.Value);
                }
                AddUniqueString(node.Value);
            }

            Dictionary<string, ushort> StringOffsets = new Dictionary<string, ushort>();
            int StringOffset = 0;
            foreach (var UniqueString in UniqueStrings)
            {
                StringOffsets.Add(UniqueString, (ushort)StringOffset);
                StringOffset += UniqueString.Length + 1;
            }

            List<BxmDataOffset> dataOffsets = new List<BxmDataOffset>();
            Dictionary<BxmDataNode, int> NodeDataIndices = new Dictionary<BxmDataNode, int>();
            foreach (var node in nodes)
            {
                BxmDataOffset offset = new BxmDataOffset();
                offset.NameOffset = node.Name != null && StringOffsets.ContainsKey(node.Name) ? StringOffsets[node.Name] : (ushort)0xFFFF;
                offset.DataOffset = node.Value != null && StringOffsets.ContainsKey(node.Value) ? StringOffsets[node.Value] : (ushort)0xFFFF;
                NodeDataIndices.Add(node, dataOffsets.Count);
                dataOffsets.Add(offset);
                foreach (var attr in node.Attributes)
                {
                    BxmDataOffset attrOffset = new BxmDataOffset();
                    attrOffset.NameOffset = attr.Key != null && StringOffsets.ContainsKey(attr.Key) ? StringOffsets[attr.Key] : (ushort)0xFFFF;
                    attrOffset.DataOffset = attr.Value != null && StringOffsets.ContainsKey(attr.Value) ? StringOffsets[attr.Value] : (ushort)0xFFFF;
                    dataOffsets.Add(attrOffset);
                }
            }

            List<BxmNodeInfo> Infos = new();
            Dictionary<BxmNodeInfo, BxmDataNode> InfoToNode = new();
            List<(BxmNodeInfo, BxmDataNode)> NodeCombos = new();

            BxmNodeInfo NodeToInfo(BxmDataNode Node)
            {
                BxmNodeInfo info = new();
                info.ChildCount = (short)Node.Children.Count;
                info.AttributeCount = (short)Node.Attributes.Count;
                info.DataIndex = (short)NodeDataIndices[Node];
                info.FirstChildIndex = 0;
                InfoToNode.Add(info, Node);
                NodeCombos.Add((info, Node));
                return info;
            }
            void AddChildrenInfo(BxmDataNode Node)
            {
                foreach (var child in Node.Children)
                {
                    Infos.Add(NodeToInfo(child));
                }
                foreach (var child in Node.Children)
                {
                    AddChildrenInfo(child);
                }
            }
            Infos.Add(NodeToInfo(root));
            AddChildrenInfo(root);

            for (int i = 0; i < Infos.Count; i++)
            {
                BxmNodeInfo Info = Infos[i];
                int Next = -1;
                if (Info.ChildCount > 0)
                {
                    BxmDataNode first = InfoToNode[Info].Children.First();
                    Next = NodeCombos.FindIndex((c) => c.Item2 == first);
                }
                else
                {
                    BxmDataNode Node = InfoToNode[Info];
                    BxmDataNode Parent = InfoToNode[Info].Parent;
                    if (Parent != null)
                    {
                        BxmDataNode Last = Parent.Children.Last();
                        int LastIndex = NodeCombos.FindIndex((c) => c.Item2 == Last);
                        Next = LastIndex;
                    }
                    else
                    {
                        Next = Infos.Count;
                    }
                }
                {
                    var Copy = Infos[i];
                    Copy.FirstChildIndex = (ushort)Next;
                    Infos[i] = Copy;
                }
            }

            /*
             * Binary writing part
             */

            BinWriter writer = new BinWriter(Endianness.Big);

            writer.WriteString("BXM\0");
            writer.WriteUInt32(0);
            writer.WriteUInt16((ushort)nodes.Count);
            writer.WriteUInt16((ushort)dataOffsets.Count);
            uint collectiveSum = (uint)UniqueStrings.Sum(x => x.Length);
            writer.WriteUInt32(collectiveSum);

            foreach (var info in Infos)
            {
                writer.WriteInt16(info.ChildCount);
                writer.WriteUInt16(info.FirstChildIndex);
                writer.WriteInt16(info.AttributeCount);
                writer.WriteInt16(info.DataIndex);
            }

            foreach (var data in dataOffsets)
            {
                writer.WriteUInt16(data.NameOffset);
                writer.WriteUInt16(data.DataOffset);
            }

            foreach (var str in UniqueStrings)
            {
                writer.WriteString(str);
                writer.WriteByte(0);
            }

            return writer.GetArray();
        }
    }
}
