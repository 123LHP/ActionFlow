﻿using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Collections;

namespace ActionFlow
{


    public class ActionStateMapToAsset:IDisposable
    {
        private Dictionary<int, ActionStateContainer[]> _map;// ActionStateContainer是数组只是为了能传引用

        private static ActionStateMapToAsset _inst;

        public static ActionStateMapToAsset Instance
        {
            get
            {
                if (_inst == null)
                {
                    _inst = new ActionStateMapToAsset {_map = new Dictionary<int, ActionStateContainer[]>()};
                }
                return _inst;
            }
        }

        public ref ActionStateContainer GetContainer(GraphAsset graph)
        {
            if (_map.TryGetValue(graph.GetInstanceID(), out var actionStateContainer))
            {
                return ref actionStateContainer[0];
            }
            else
            {
                ActionStateContainer[] asc = new ActionStateContainer[1];
                asc[0] = ActionStateContainer.Create(graph);
                _map.Add(graph.GetInstanceID(), asc);
                return ref asc[0];
            }
        }


        public int CreateContainer(GraphAsset graph)
        {
            var id = graph.GetInstanceID();
            if (_map.ContainsKey(id)) return id;
           
            ActionStateContainer[] asc = new ActionStateContainer[1];
            asc[0] = ActionStateContainer.Create(graph);
            _map.Add(id, asc);
            return id;
        }


        public ref ActionStateContainer GetContainer(int instanceID)
        {
            if (_map.TryGetValue(instanceID, out var actionStateContainer))
            {
                return ref actionStateContainer[0];
            }
            else
            {
                throw new System.Exception("对应的GraphAsset没有被创建");
            }
        }

        //public NativeHashMap<int,ActionStateContainer> GetAllContainer(Allocator allocator)
        //{
        //    var count = _map.Count;
        //    var nhMap = new NativeHashMap<int, ActionStateContainer>(count, allocator);
        //    foreach (var kv in _map)
        //    {
        //        nhMap.TryAdd(kv.Key, kv.Value[0]);
        //    }
        //    return nhMap;

        //}


        public void Dispose()
        {
            foreach (var kv in _map)
            {
                kv.Value[0].Dispose();
            }
            _map = null;
        }
    }

}
