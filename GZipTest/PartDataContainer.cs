using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{
    /// <summary>
    /// Порция данных
    /// Содержит номер порции и ее данные
    /// </summary>
    class PartDataContainer
    {
        private long index;
        private byte[] data;

        public PartDataContainer(long _index, byte[] _data)
        {
            index = _index;
            data = _data;
        }

        public long Index
        {
            get
            {
                return index;
            }

            set
            {
                index = value;
            }
        }

        public byte[] Data
        {
            get
            {
                return data;
            }

            set
            {
                data = value;
            }
        }
    }
}
