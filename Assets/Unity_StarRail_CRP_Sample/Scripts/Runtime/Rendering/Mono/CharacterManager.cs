using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity_StarRail_CRP_Sample
{
    public enum CharacterType : int 
    {
        Player = 0,
        Character = 1,
        NPC = 2
    }
    
    public class CharacterInfo
    {
        public CharacterType Type;
        public Bounds AABB;
        
        public Vector3 Position;
        public Vector3 MainLightDirection;
        public Vector3 ShadowLightDirection;
        
        public Color MainLightColor;

        public CharacterInfo()
        {
            
        }
        
        public static bool operator <(CharacterInfo lhs, CharacterInfo rhs)
        {
            return lhs.Type < rhs.Type;
        }
        
        public static bool operator >(CharacterInfo lhs, CharacterInfo rhs)
        {
            return lhs.Type > rhs.Type;
        }
    }
    
    public class CharacterManager
    {
        private static readonly Lazy<CharacterManager> Ins =
            new Lazy<CharacterManager>(() => new CharacterManager());

        public static CharacterManager instance => Ins.Value;
        
        public Dictionary<GameObject, CharacterInfo> CharacterInfos => _characterInfos;

        private readonly Dictionary<GameObject, CharacterInfo> _characterInfos = new Dictionary<GameObject, CharacterInfo>();
        
        public static readonly int MaxCharacterCount = 16;

        public bool AddCharacterInfo(GameObject obj, CharacterInfo info)
        {
            if (_characterInfos.Count >= MaxCharacterCount)
            {
                return false;
            }

            if (_characterInfos.ContainsKey(obj))
            {
                _characterInfos[obj] = info;
            }
            else
            {
                _characterInfos.Add(obj, info);
            }

            return true;
        }

        public bool GetCharacterInfo(GameObject obj, out CharacterInfo info)
        {
            return _characterInfos.TryGetValue(obj, out info);
        }

        public void RemoveCharacterInfo(GameObject obj)
        {
            _characterInfos.Remove(obj);
        }
    }
}