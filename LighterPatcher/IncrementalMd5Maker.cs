using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace LighterPatcher
{
    class IncrementalMd5Maker
    {
        MD5 md5;
        public List<string> ToHashCollection;

        public IncrementalMd5Maker()
        {
            md5 = MD5.Create();
            ToHashCollection = new List<string>();
        }

        public void Step(string input)
        {
            ToHashCollection.Add(input);
        }

        public ulong Finalize()
        {
            ToHashCollection.Sort();
            int a = string.Join("", ToHashCollection).GetHashCode();
            ulong final = 0;
            if (a < 0)
            {
                final += int.MaxValue;
                a = Math.Abs(a);
            }
            final += Convert.ToUInt64(a);
            return final;
        }

        public void Clear()
        {
            md5 = null;
            ToHashCollection = null;
        }

    }
}
