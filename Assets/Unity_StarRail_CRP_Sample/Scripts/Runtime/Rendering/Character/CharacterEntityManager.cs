using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public class CharacterEntityManagerFactory : IDisposable
    {
        private static readonly Lazy<CharacterEntityManagerFactory> Ins =
            new Lazy<CharacterEntityManagerFactory>(() => new CharacterEntityManagerFactory());

        public static CharacterEntityManagerFactory instance => Ins.Value;
        
        private CharacterEntityManager _characterEntityManager = null;
        private int _referenceCount = 0;

        public CharacterEntityManager Get()
        {
            if (_characterEntityManager == null)
            {
                Assert.AreEqual(_referenceCount, 0);
                
                _characterEntityManager = new CharacterEntityManager();
                
                var characters = GameObject.FindObjectsOfType<CharacterAdditionalRenderer>();
                foreach (var character in characters)
                {
                    if (!character.isActiveAndEnabled || _characterEntityManager.IsValid(character.CharacterEntity))
                    {
                        continue;
                    }
                    
                    character.CharacterEntity = _characterEntityManager.CreateCharacterEntity(character);
                }
                
                CharacterAdditionalRenderer.OnCharacterAdd += OnCharacterAdd;
                CharacterAdditionalRenderer.OnCharacterRemove += OnCharacterRemove;
            }

            _referenceCount++;
            
            return _characterEntityManager;
        }
        
        public void Release(CharacterEntityManager characterEntityManager)
        {
            if (_referenceCount == 0)
            {
                return;
            }

            _referenceCount--;

            if (_referenceCount == 0)
            {
                Dispose();
            }
        }
        
        public void Dispose()
        {
            _characterEntityManager.Dispose();
            _characterEntityManager = null;
            _referenceCount = 0;
            
            CharacterAdditionalRenderer.OnCharacterAdd -= OnCharacterAdd;
            CharacterAdditionalRenderer.OnCharacterRemove -= OnCharacterRemove;
        }
        
        private void OnCharacterAdd(CharacterAdditionalRenderer character)
        {
            if (!_characterEntityManager.IsValid(character.CharacterEntity))
            {
                character.CharacterEntity = _characterEntityManager.CreateCharacterEntity(character);
            }
        }

        private void OnCharacterRemove(CharacterAdditionalRenderer character)
        {
            _characterEntityManager.DestroyCharacterEntity(character.CharacterEntity);
        }
    }

    public class CharacterEntityManager : IDisposable
    {
        // Data
        public List<CharacterEntityChunk> entityChunks = new List<CharacterEntityChunk>();
        public List<CharacterShadowCasterDrawCullChunk> shadowCasterDrawCallChunks = new List<CharacterShadowCasterDrawCullChunk>();
        public List<CharacterScreenShadowDrawCullChunk> screenShadowDrawCallChunks = new List<CharacterScreenShadowDrawCullChunk>();
        public int chunkCount;
        
        private struct CombinedChunks
        {
            public CharacterEntityChunk entityChunk;
            public CharacterShadowCasterDrawCullChunk shadowCasterDrawCallChunk;
            public CharacterScreenShadowDrawCullChunk screenShadowDrawCallChunk;
            public int prevChunkIndex;
            public bool valid;
        }
        private List<CombinedChunks> _combinedChunks = new List<CombinedChunks>();
        private List<int> _combinedChunkRemap = new List<int>();

        // Sampler
        private ProfilingSampler _addCharacterSampler;
        private ProfilingSampler _sortChunksSampler;

        // Indexer
        private CharacterEntityIndexer _characterEntityIndexer = new CharacterEntityIndexer();
        private Dictionary<GameObject, int> _gameObjectToIndexDict = new Dictionary<GameObject, int>();

        public CharacterEntityManager()
        {
            _addCharacterSampler = new ProfilingSampler($"{nameof(CharacterEntityManager)}.AddCharacter");
            _sortChunksSampler = new ProfilingSampler($"{nameof(CharacterEntityManager)}.SortChunks");
        }

        public bool IsValid(CharacterEntity characterEntity)
        {
            return _characterEntityIndexer.IsValid(characterEntity);
        }

        public CharacterEntity CreateCharacterEntity(CharacterAdditionalRenderer character)
        {
            using (new ProfilingScope(null, _addCharacterSampler))
            {
                int chunkIndex = GetChunkIndex(character.gameObject);

                CharacterEntity entity = _characterEntityIndexer.CreateCharacterEntity(chunkIndex);
                
                CharacterEntityChunk entityChunk = entityChunks[chunkIndex];
                
                entityChunk.UpdateEntityData(entity, character);

                return entity;
            }
        }

        public void UpdateCharacterEntityData(CharacterEntity characterEntity, CharacterAdditionalRenderer character)
        {
            var entityItem = _characterEntityIndexer.GetItem(characterEntity);
            
            int chunkIndex = entityItem.chunkIndex;
            
            CharacterEntityChunk entityChunk = entityChunks[chunkIndex];
            
            entityChunk.UpdateEntityData(characterEntity, character);
        }

        public void UpdateAllCharacterCachedData()
        {
            foreach (var entityChunk in entityChunks)
            {
                var character = entityChunk.characterRenderer;
                if (character == null)
                {
                    continue;
                }

                var entity = entityChunk.entity;
                if (!IsValid(entity))
                {
                    continue;
                }
                
                entityChunk.UpdateEntityData(entity, character);
            }
        }
        
        public void DestroyCharacterEntity(CharacterEntity characterEntity)
        {
            if (!_characterEntityIndexer.IsValid(characterEntity))
            {
                return;
            }
            
            _characterEntityIndexer.DestroyCharacterEntity(characterEntity);
        }

        public void UpdateEntityManager()
        {
            using (new ProfilingScope(null, _sortChunksSampler))
            {
                // Combine chunks into single array
                for (int i = 0; i < chunkCount; ++i)
                {
                    _combinedChunks[i] = new CombinedChunks()
                    {
                        entityChunk = entityChunks[i],
                        shadowCasterDrawCallChunk = shadowCasterDrawCallChunks[i],
                        screenShadowDrawCallChunk = screenShadowDrawCallChunks[i],
                        prevChunkIndex = i,
                        valid = entityChunks[i].character != null,
                    };
                }
                
                // Sort
                _combinedChunks.Sort((a, b) => 
                {
                    if (a.valid && !b.valid)
                        return -1;
                    if (!a.valid && b.valid)
                        return 1;

                    if (a.entityChunk.characterRenderer.characterType < b.entityChunk.characterRenderer.characterType)
                        return -1;
                    if (a.entityChunk.characterRenderer.characterType > b.entityChunk.characterRenderer.characterType)
                        return 1;
                    
                    return a.entityChunk.character.GetHashCode().CompareTo(b.entityChunk.character.GetHashCode());
                });
                
                // Early out if nothing changed
                bool dirty = false;
                for (int i = 0; i < chunkCount; i++)
                {
                    if (_combinedChunks[i].prevChunkIndex != i || !_combinedChunks[i].valid)
                    {
                        dirty = true;
                        break;
                    }
                }

                if (!dirty)
                {
                    return;
                }
                
                // Update chunks
                int count = 0;
                _gameObjectToIndexDict.Clear();
                for (int i = 0; i < chunkCount; i++)
                {
                    var combinedChunk = _combinedChunks[i];
                    
                    entityChunks[i] = combinedChunk.entityChunk;
                    shadowCasterDrawCallChunks[i] = combinedChunk.shadowCasterDrawCallChunk;
                    screenShadowDrawCallChunks[i] = combinedChunk.screenShadowDrawCallChunk;

                    if (!_gameObjectToIndexDict.ContainsKey(entityChunks[i].character))
                    {
                        _gameObjectToIndexDict.Add(entityChunks[i].character, i);
                    }
                    
                    _combinedChunkRemap[combinedChunk.prevChunkIndex] = i;
                    count++;
                }
                
                // In case some chunks where destroyed resize the arrays
                if (chunkCount > count)
                {
                    entityChunks.RemoveRange(count, chunkCount - count);
                    shadowCasterDrawCallChunks.RemoveRange(count, chunkCount - count);
                    screenShadowDrawCallChunks.RemoveRange(count, chunkCount - count);
                    _combinedChunks.RemoveRange(count, chunkCount - count);
                    chunkCount = count;
                }
                
                // Remap entities chunk index with new sorted ones
                _characterEntityIndexer.RemapChunkIndices(_combinedChunkRemap);
            }
        }

        public void Dispose()
        {
            _characterEntityIndexer.Clear();
            _gameObjectToIndexDict.Clear();
            entityChunks.Clear();
            shadowCasterDrawCallChunks.Clear();
            screenShadowDrawCallChunks.Clear();
            _combinedChunks.Clear();
            chunkCount = 0;
        }

        private int GetChunkIndex(GameObject gameObject)
        {
            if (!_gameObjectToIndexDict.TryGetValue(gameObject, out int chunkIndex))
            {
                entityChunks.Add(new CharacterEntityChunk() { character = gameObject });
                shadowCasterDrawCallChunks.Add(new CharacterShadowCasterDrawCullChunk());
                screenShadowDrawCallChunks.Add(new CharacterScreenShadowDrawCullChunk());
                _combinedChunks.Add(new CombinedChunks());
                _combinedChunkRemap.Add(0);

                _gameObjectToIndexDict.Add(gameObject, chunkCount);
                return chunkCount++;
            }

            return chunkIndex;
        }
    }

    internal class CharacterEntityIndexer
    {
        public struct CharacterEntityItem
        {
            public int chunkIndex; // Index of chunk in CharacterEntityManager
            public int version;
        }
        
        private List<CharacterEntityItem> _entityItems = new List<CharacterEntityItem>();
        private Queue<int> _freeIndices = new Queue<int>();
        
        public bool IsValid(CharacterEntity entity)
        {
            if (_entityItems.Count <= entity.index)
            {
                return false;
            }
            
            return _entityItems[entity.index].version == entity.version;
        }

        public CharacterEntity CreateCharacterEntity(int chunkIndex)
        {
            // Reuse
            if (_freeIndices.Count != 0)
            {
                int entityIndex = _freeIndices.Dequeue();
                int newVersion = _entityItems[entityIndex].version + 1;

                _entityItems[entityIndex] = new CharacterEntityItem()
                {
                    chunkIndex = chunkIndex,
                    version = newVersion,
                };

                return new CharacterEntity()
                {
                    index = entityIndex,
                    version = newVersion,
                };
            }

            // Create new one
            {
                int entityIndex = _entityItems.Count;
                int version = 1;

                _entityItems.Add(new CharacterEntityItem()
                {
                    chunkIndex = chunkIndex,
                    version = version,
                });


                return new CharacterEntity()
                {
                    index = entityIndex,
                    version = version,
                };
            }
        }

        public void DestroyCharacterEntity(CharacterEntity characterEntity)
        {
            Assert.IsTrue(IsValid(characterEntity));
            _freeIndices.Enqueue(characterEntity.index);
            
            // Update version that everything that points to it will have outdated version
            var item = _entityItems[characterEntity.index];
            item.version++;
            _entityItems[characterEntity.index] = item;
        }

        public CharacterEntityItem GetItem(CharacterEntity characterEntity)
        {
            Assert.IsTrue(IsValid(characterEntity));
            return _entityItems[characterEntity.index];
        }

        public void RemapChunkIndices(List<int> remaper)
        {
            for (int i = 0; i < _entityItems.Count; ++i)
            {
                int newChunkIndex = remaper[_entityItems[i].chunkIndex];
                var item = _entityItems[i];
                item.chunkIndex = newChunkIndex;
                _entityItems[i] = item;
            }
        }

        public void Clear()
        {
            _entityItems.Clear();
            _freeIndices.Clear();
        }
    }
}