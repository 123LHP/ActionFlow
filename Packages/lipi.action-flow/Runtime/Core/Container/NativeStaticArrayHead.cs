﻿using UnityEngine;
using System.Collections;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace ActionFlow
{

    /// <summary>
    /// 静态的不同类型的Struct组成的数组
    /// </summary>
    public struct NativeStaticArrayHead:IDisposable
    {
        public struct TypePosition
        {
            //public int TypeIndex;
            public int Offset;
            public int Size;
        }

        internal NativeArray<TypePosition> TypePositions; //每位的类别数据

        internal int Size; //总的内存大小


        public TypePosition this[int i]
        {
            get
            {
                return TypePositions[i];
            }
        }


        


        public void Dispose()
        {
            TypePositions.Dispose();
        }

        ///==========  Builder
        ///==========  Builder
        ///==========  Builder

        public static Builder CreateBuilder()
        {
            var b = new Builder();
            return b;
        }

        

        public class Builder
        {
            internal NativeList<TypePosition> NativeLists;
            private int offset;

            public Builder()
            {
                NativeLists = new NativeList<TypePosition>(Allocator.Temp);
                offset = 0;
            }

            public Builder Add<T>() where T : struct
            {
                var size = UnsafeUtility.SizeOf<T>();
                NativeLists.Add(new TypePosition()
                {
                    Offset = offset,
                    Size = size
                });
                offset += size;
                return this;
            }

            

            public NativeStaticArrayHead ToHead()
            {
                var head = new NativeStaticArrayHead()
                {
                    TypePositions = NativeLists.ToArray(Allocator.Persistent),
                    Size = offset
                };
                NativeLists.Dispose();
                return head;
            }


        }

    }


    

}
