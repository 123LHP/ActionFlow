﻿using UnityEngine;
using System.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System;

namespace ActionFlow
{
    public unsafe struct ActionStateContainer:IDisposable
    {
        public struct Info
        {
            public int statesSize;//每块状态大小
            public int nodeCount;//每块节点数量
            public int chunkCount;
            public int chunkCapacity;
        }

        private NativeArray<ActionStateNode> Nodes;
        private byte* States;
        private NativeList<ActionStateChunk> Chunks;
        private NativeArray<Info> ContainerInfo;
       // private NativeStructMap.Array BlackboardArray;
        private NativeStaticMap Blackboard;



        public static ActionStateContainer Create(GraphAsset graph, int chunkCapacity = 5)
        {
            var container = new ActionStateContainer();
           // var builder = NativeStructMap.CreateBuilder();
            var builder = NativeStaticMapHead.CreateBuilder();
            var count = graph.RuntimeNodes.Length;
            container.ContainerInfo = new NativeArray<Info>(1, Allocator.Persistent);
            var _info = new Info();
            _info.nodeCount = count;
            //container.nodeCount = count;
            container.Chunks = new NativeList<ActionStateChunk>(Allocator.Persistent);
            // container.Chunks.Add(new ActionStateChunk());
            container.Nodes = new NativeArray<ActionStateNode>(count * chunkCapacity, Allocator.Persistent);
            _info.chunkCount = 0;

            var capacity = 1000;
            var statePtr = (byte*)UnsafeUtility.Malloc(capacity, 4, Allocator.Temp);

            int offset = 0;
            for (int i = 0; i < count; i++)
            {
                var inode = graph.m_Nodes[i];// as INodeAsset;
                var node = new ActionStateNode() { Cycle = NodeCycle.Inactive, offset = offset };
                container.Nodes[i] = node;

                if (inode is IStatusNode nodeObject)
                {
                    var t = nodeObject.NodeDataType();
                    var size = UnsafeUtility.SizeOf(t);
                    if (offset + size > capacity)
                    {
                        capacity = capacity * 2;
                        var ptr = (byte*)UnsafeUtility.Malloc(capacity, 4, Allocator.Temp);
                        UnsafeUtility.MemCpy(ptr, statePtr, offset);
                        UnsafeUtility.Free(ptr, Allocator.Temp);
                        statePtr = ptr;
                    }
                    nodeObject.CreateNodeDataTo(statePtr + offset);
                    offset += size;
                }
                if (inode is IAccessBlackboard accessBlackboard)
                {
                    accessBlackboard.ToBuilder(builder);
                }

            }
            container.States = (byte*)UnsafeUtility.Malloc(offset * chunkCapacity, 4, Allocator.Persistent);
            UnsafeUtility.MemCpy(container.States, statePtr, offset);
            UnsafeUtility.Free(statePtr, Allocator.Temp);

            //var array = builder.ToNativeStructMapArray(chunkCapacity, Allocator.Persistent);
            var head = builder.ToHead();
            var blackboard = NativeStaticMap.Create(ref head, chunkCapacity);
            //container.BlackboardArray = array;
            container.Blackboard = blackboard;


            _info.statesSize = offset;
            _info.nodeCount = count;
            _info.chunkCapacity = chunkCapacity;
            container.ContainerInfo[0] = _info;


            return container;
        }



        public int AddChunk()
        {
            var _info = ContainerInfo[0];
            if (ContainerInfo[0].chunkCapacity <= ContainerInfo[0].chunkCount)
            {
                var newChunkCapacity = ContainerInfo[0].chunkCapacity * 2;
                var newNodes = new NativeArray<ActionStateNode>(newChunkCapacity * ContainerInfo[0].nodeCount, Allocator.Persistent);
                NativeArray<ActionStateNode>.Copy(Nodes, 0, newNodes, 0, _info.chunkCapacity * _info.nodeCount);
                Nodes.Dispose();
                Nodes = newNodes;


                var newStates = UnsafeUtility.Malloc(newChunkCapacity * _info.statesSize, 4, Allocator.Persistent);
                UnsafeUtility.MemCpy(newStates, States, _info.chunkCapacity * _info.statesSize);
                UnsafeUtility.Free(States, Allocator.Persistent);
                States = (byte*)newStates;

                _info.chunkCapacity = newChunkCapacity;
            }

            Chunks.Add(new ActionStateChunk()
            {
                Position = _info.chunkCount
            });
            //var v = Nodes[chunkCount * nodeCount];
            //v.Cycle = NodeCycle.Active;
            //Nodes[chunkCount * nodeCount] = v;
            _info.chunkCount++;
            //BlackboardArray.Add();
            Blackboard.NewItem();
            ContainerInfo[0] = _info;
            return _info.chunkCount - 1;
        }


        public void RemoveChunkAt(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= Chunks.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "index 溢出");
            }
#endif
            var _info = ContainerInfo[0];

            var remove_chunk = Chunks[index];
            var last_chunk = Chunks[_info.chunkCount - 1];
            NativeArray<ActionStateNode>.Copy(
                Nodes, 
                last_chunk.Position * _info.nodeCount, 
                Nodes, 
                remove_chunk.Position * _info.nodeCount,
                _info.nodeCount);

            UnsafeUtility.MemCpy(
                States + last_chunk.Position * _info.statesSize, 
                States + remove_chunk.Position * _info.statesSize,
                _info.statesSize);
            Chunks[index] = last_chunk;
            _info.chunkCount--;
            ContainerInfo[0] = _info;
        }

        //public ActionStateForEntity GetStateForEntity(int index)
        //{
        //    return new ActionStateForEntity()
        //    {
        //        Nodes = new NativeSlice<ActionStateNode>(Nodes, Chunks[index].Position * nodeCount, nodeCount),
        //        Chunk = Chunks,
        //        States = States + Chunks[index].Position * statesSize,
        //        CurrChunkIndex = index
        //    };
        //}

        #region Node数据操作

        public T GetValue<T>(ActionStateIndex index) where T : struct
        {
            var node = Nodes[ToNodeIndex(index)];
            var valueOffset = Chunks[index.ChunkIndex].Position * ContainerInfo[0].statesSize + node.offset;
            UnsafeUtility.CopyPtrToStructure<T>(States + valueOffset, out var value);
            return value;
        }
        public void SetValue<T>(ActionStateIndex index, T value) where T : struct
        {
            var node = Nodes[ToNodeIndex(index)];
            var valueOffset = Chunks[index.ChunkIndex].Position * ContainerInfo[0].statesSize + node.offset;
            UnsafeUtility.CopyStructureToPtr(ref value, States + valueOffset);
        }

        public void SetNodeCycle(ActionStateIndex stateIndex, NodeCycle cycle)
        {
            var index = ToNodeIndex(stateIndex);
            var node = Nodes[index];
            var Chunk = Chunks[stateIndex.ChunkIndex];
            var currCycle = node.Cycle;

            switch (cycle)
            {
                case NodeCycle.Active:
                    if (!currCycle.Has(NodeCycle.Active))
                    {
                        currCycle = currCycle.Add(cycle);
                        Chunk.Active += 1;
                    }
                    break;
                case NodeCycle.Sleeping:
                    if (!currCycle.Has(NodeCycle.Sleeping))
                    {
                        currCycle = currCycle.Add(cycle);
                        Chunk.Sleeping += 1;
                        if (currCycle.Has(NodeCycle.Waking))
                        {
                            currCycle = currCycle.Remove(NodeCycle.Waking);
                            Chunk.Waking -= 1;
                        }
                    }
                    break;
                case NodeCycle.Waking:
                    if (!currCycle.Has(NodeCycle.Waking))
                    {
                        currCycle = currCycle.Add(cycle);
                        Chunk.Waking += 1;
                        if (currCycle.Has(NodeCycle.Sleeping))
                        {
                            currCycle = currCycle.Remove(NodeCycle.Sleeping);
                            Chunk.Sleeping -= 1;
                        }
                    }
                    break;
                case NodeCycle.Inactive:
                    if (currCycle.Has(NodeCycle.Active)) Chunk.Active -= 1;
                    if (currCycle.Has(NodeCycle.Waking)) Chunk.Waking -= 1;
                    if (currCycle.Has(NodeCycle.Sleeping)) Chunk.Sleeping -= 1;
                    currCycle = cycle;
                    break;
            }
            node.Cycle = currCycle;
            Nodes[index] = node;
            Chunks[stateIndex.ChunkIndex] = Chunk;
        }

        public void RemoveNodeCycle(ActionStateIndex stateIndex, NodeCycle cycle)
        {
            var index = ToNodeIndex(stateIndex);
            var node = Nodes[index];
            var Chunk = Chunks[stateIndex.ChunkIndex];
            var currCycle = node.Cycle;
            if (currCycle.Has(cycle))
            {
                currCycle = currCycle.Remove(cycle);
                switch (cycle)
                {
                    case NodeCycle.Active: Chunk.Active -= 1; break;
                    case NodeCycle.Sleeping: Chunk.Sleeping -= 1; break;
                    case NodeCycle.Waking: Chunk.Waking -= 1; break;
                }
            }

            node.Cycle = currCycle;
            Nodes[index] = node;
            Chunks[stateIndex.ChunkIndex] = Chunk;
        }

        public NodeCycle GetNodeCycle(ActionStateIndex stateIndex)
        {
            return Nodes[ToNodeIndex(stateIndex)].Cycle;
        }

        public (int, int) GetAllActiveOrWakingIndex(
            int chunkIndex,
            ref NativeArray<int> activeArray,
            ref NativeArray<int> wakingArray)
        {
            var count_a = 0;
            var count_b = 0;
            var s = Chunks[chunkIndex].Position * ContainerInfo[0].nodeCount;
            var e = s + ContainerInfo[0].nodeCount;
            for (int i = s; i < e; i++)
            {
                var val = Nodes[i].Cycle;
                if (val == NodeCycle.Inactive) continue;
                if (val.Has(NodeCycle.Active))
                {
                    activeArray[count_a] = i-s;
                    count_a++;
                }
                if (val.Has(NodeCycle.Waking))
                {
                    wakingArray[count_b] = i-s;
                    count_b++;
                }
            }
            return (count_a, count_b);
        }


        public bool AnySleeping(int chunkIndex)
        {
            return Chunks[chunkIndex].Sleeping > 0;
        }

        public bool AnyActive(int chunkIndex)
        {
            return  Chunks[chunkIndex].Active > 0;

        }

        public bool AnyWaking(int chunkIndex)
        { return Chunks[chunkIndex].Waking > 0; }


        private int ToNodeIndex(ActionStateIndex actionStateIndex)
        {
            return Chunks[actionStateIndex.ChunkIndex].Position * ContainerInfo[0].nodeCount + actionStateIndex.NodeIndex;
        }
        #endregion


        public ref T GetBlackboard<T>(ActionStateIndex stateIndex) where T:struct
        {
            return ref Blackboard[stateIndex.ChunkIndex].RefGet<T>();
           //return ref BlackboardArray[stateIndex.ChunkIndex].GetValue<T>();
        }

        public void Dispose()
        {
            Nodes.Dispose();
            UnsafeUtility.Free(States, Allocator.Persistent);
            Chunks.Dispose();
            ContainerInfo.Dispose();
            Blackboard.Dispose();
            //BlackboardArray.Dispose();
        }


    }

}
