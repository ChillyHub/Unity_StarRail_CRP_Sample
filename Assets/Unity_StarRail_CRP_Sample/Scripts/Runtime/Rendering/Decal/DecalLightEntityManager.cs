using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Unity_StarRail_CRP_Sample
{
    public class DecalLightEntityManagerFactory : IDisposable
    {
        private static readonly Lazy<DecalLightEntityManagerFactory> Ins =
            new Lazy<DecalLightEntityManagerFactory>(() => new DecalLightEntityManagerFactory());

        public static DecalLightEntityManagerFactory instance => Ins.Value;
        
        private DecalEntityManager _decalEntityManager = null;
        private int _referenceCount = 0;

        public DecalEntityManager Get()
        {
            if (_decalEntityManager == null)
            {
                Assert.AreEqual(_referenceCount, 0);
                
                _decalEntityManager = new DecalEntityManager();
                
                var decals = GameObject.FindObjectsOfType<DecalLight>();
                foreach (var decal in decals)
                {
                    if (!decal.isActiveAndEnabled || _decalEntityManager.IsValid(decal.decalEntity))
                    {
                        continue;
                    }
                    
                    decal.decalEntity = _decalEntityManager.CreateDecalEntity(decal);
                }
                
                DecalLight.onDecalLightAdd += OnDecalAdd;
                DecalLight.onDecalLightRemove += OnDecalRemove;
                DecalLight.onDecalLightPropertyChange += OnDecalPropertyChange;
                DecalLight.onDecalLightMaterialChange += OnDecalMaterialChange;
                DecalLight.onAllDecalLightPropertyChange += OnAllDecalPropertyChange;
            }

            _referenceCount++;
            
            return _decalEntityManager;
        }
        
        public void Release(DecalEntityManager decalEntityManager)
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
            _decalEntityManager.Dispose();
            _decalEntityManager = null;
            _referenceCount = 0;
            
            DecalLight.onDecalLightAdd -= OnDecalAdd;
            DecalLight.onDecalLightRemove -= OnDecalRemove;
            DecalLight.onDecalLightPropertyChange -= OnDecalPropertyChange;
            DecalLight.onDecalLightMaterialChange -= OnDecalMaterialChange;
            DecalLight.onAllDecalLightPropertyChange -= OnAllDecalPropertyChange;
        }
        
        private void OnDecalAdd(DecalProjector decalProjector)
        {
            if (!_decalEntityManager.IsValid(decalProjector.decalEntity))
                decalProjector.decalEntity = _decalEntityManager.CreateDecalEntity(decalProjector);
        }

        private void OnDecalRemove(DecalProjector decalProjector)
        {
            _decalEntityManager.DestroyDecalEntity(decalProjector.decalEntity);
        }

        private void OnDecalPropertyChange(DecalProjector decalProjector)
        {
            if (_decalEntityManager.IsValid(decalProjector.decalEntity))
                _decalEntityManager.UpdateDecalEntityData(decalProjector.decalEntity, decalProjector);
        }


        private void OnAllDecalPropertyChange()
        {
            _decalEntityManager.UpdateAllDecalEntitiesData();
        }

        private void OnDecalMaterialChange(DecalProjector decalProjector)
        {
            // Decal will end up in new chunk after material change
            OnDecalRemove(decalProjector);
            OnDecalAdd(decalProjector);
        }
    }
}