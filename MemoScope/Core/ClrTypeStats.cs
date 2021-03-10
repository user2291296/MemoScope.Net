using BrightIdeasSoftware;
using Microsoft.Diagnostics.Runtime;
using MemoScope.Core.Data;
using WinFwk.UITools;

namespace MemoScope.Core
{
    public class ClrTypeStats : ITypeNameData
    {
        public int Id { get; }
        public ClrType Type { get; }
        public ulong MethodTable { get; }
        public ulong TypeAddr { get; }

        [OLVColumn(Title = "Type Name", Width = 500)]
        public string TypeName { get; }

        [IntColumn(Title = "Nb")]
        public long NbInstances { get; private set; }

        [IntColumn(Title = "Total Size")]
        public ulong TotalSize { get; private set; }

        public ClrTypeStats(int id, ClrType type, ulong typeAddr)
        {
            Id = id;
            MethodTable = type.MethodTable;
            Type = type;
            TypeAddr = typeAddr;
        }

        public ClrTypeStats(int id, ClrType type, long nbInstances, ulong totalSize, string name, ulong typeAddr) : this(id, type, typeAddr)
        {
            NbInstances = nbInstances;
            TotalSize= totalSize;
            //TypeName = Type.Name;
            TypeName = name;
            Id = id;
        }

        public void Inc(ulong size)
        {
            TotalSize += size;
            NbInstances++;
        }
    }

}