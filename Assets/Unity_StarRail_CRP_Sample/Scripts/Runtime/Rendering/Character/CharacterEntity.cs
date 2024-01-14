using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public struct CharacterEntity
    {
        public int index; // Index of entity in CharacterEntityItem
        public int version;
    }

    public struct CharacterCullingInfo
    {
        public Vector3 cameraPosition;
        public ulong sceneCullingMask;
        public int cullingMask;
        
        public CullingGroup cullingGroup;
        public int[] visibleCharacterIndexArray;
        public NativeArray<int> visibleCharacterIndices;
        public int visibleCharacterCount;
    }
    
    public struct CharacterLightInfo
    {
        public Vector3 mainLightPosition;
        public Vector3 mainLightDirection;
        public Color mainLightColor;
        
        public Vector3 shadowLightPosition;
        public Vector3 shadowLightDirection;
        public Color shadowLightColor;

        public Vector3 headCenter;
        public Vector3 headForward;
        public Vector3 headRight;
        public Vector3 headUp;

        public float dayTime;
    }

    public class CharacterEntityChunk
    {
        public GameObject character;
        public CharacterAdditionalRenderer characterRenderer;
        public CharacterEntity entity;
        public Transform transform;

        public CharacterLightInfo lightInfo;
        public CharacterCullingInfo cullingInfo;

        public float BoundingDistance => _boundingDistance[0];
        
        private float[] _boundingDistance = new float[1];

        public void UpdateEntityData(CharacterEntity characterEntity, CharacterAdditionalRenderer renderer)
        {
            character = renderer.gameObject;
            characterRenderer = renderer;
            entity = characterEntity;
            transform = character.transform;

            // Lighting
            switch (renderer.mainLightDirectionMode)
            {
                case DirectionMode.Fixed:
                    Quaternion rotation = Quaternion.Euler(renderer.mainLightRotation);
                    lightInfo.mainLightDirection = rotation * Vector3.up;
                    lightInfo.mainLightColor = renderer.mainLightColor;
                    break;
                case DirectionMode.FromVolume:
                    var setting = VolumeManager.instance.stack.GetComponent<LightSettingVolume>();
                    if (setting != null)
                    {
                        lightInfo.mainLightDirection = setting.GetMainLightDirection(transform.position);
                        lightInfo.mainLightColor = setting.GetMainLightColor();
                    }
                    break;
                case DirectionMode.FromDirectionLight:
                    if (renderer.mainDirectionLight != null)
                    {
                        lightInfo.mainLightDirection = renderer.mainDirectionLight.transform.forward;
                        lightInfo.mainLightColor = renderer.mainDirectionLight.color;
                    }
                    break;
                case DirectionMode.FromPointLight:
                    if (renderer.mainPointLight != null)
                    {
                        lightInfo.mainLightDirection = (renderer.mainPointLight.transform.position - transform.position).normalized;
                        lightInfo.mainLightColor = renderer.mainPointLight.color;
                    }
                    break;
            }
            
            switch (renderer.shadowLightDirectionMode)
            {
                case DirectionMode.Fixed:
                    lightInfo.shadowLightDirection = renderer.shadowLightDirection;
                    break;
                case DirectionMode.FromVolume:
                    var setting = VolumeManager.instance.stack.GetComponent<LightSettingVolume>();
                    if (setting != null)
                    {
                        lightInfo.shadowLightDirection = setting.GetShadowLightDirection(transform.position);
                    }
                    break;
                case DirectionMode.FromDirectionLight:
                    if (renderer.shadowDirectionLight != null)
                    {
                        lightInfo.shadowLightDirection = renderer.shadowDirectionLight.transform.forward;
                    }
                    break;
                case DirectionMode.FromPointLight:
                    if (renderer.shadowPointLight != null)
                    {
                        lightInfo.shadowLightDirection = (renderer.shadowPointLight.transform.position - transform.position).normalized;
                    }
                    break;
            }

            if (renderer.headBinding != null)
            {
                lightInfo.headCenter = renderer.headBinding.transform.position;
                lightInfo.headForward = renderer.headBinding.transform.up;
                lightInfo.headRight = -renderer.headBinding.transform.forward;
                lightInfo.headUp = -renderer.headBinding.transform.right;
            }
            else
            {
                lightInfo.headCenter = transform.position;
                lightInfo.headForward = transform.forward;
                lightInfo.headRight = transform.right;
                lightInfo.headUp = transform.up;
            }

            lightInfo.dayTime = 12.0f;
        }
    }
    
    public class CharacterDrawCallChunk : IDisposable
    {
        public List<int> drawCallIndices = new List<int>();
        public int drawCallCount;
        
        // All
        public Material[] materials;
        public int[] subMeshIndices;
        public int[] passIndices;
        
        // DrawRenderer
        public Renderer[] renderers;

        // DrawMesh
        public Mesh[] meshes;
        public MaterialPropertyBlock[] propertyBlocks;

        public Matrix4x4[] objectToWorlds;
        
        public Material GetMaterial(int index)
        {
            return materials[drawCallIndices[index]];
        }
        
        public int GetSubMeshIndex(int index)
        {
            return subMeshIndices[drawCallIndices[index]];
        }
        
        public int GetPassIndex(int index)
        {
            return passIndices[drawCallIndices[index]];
        }
        
        public Renderer GetRenderer(int index)
        {
            return renderers[drawCallIndices[index]];
        }

        public Mesh GetMesh(int index)
        {
            return meshes[drawCallIndices[index]];
        }

        public MaterialPropertyBlock GetPropertyBlock(int index)
        {
            return propertyBlocks[drawCallIndices[index]];
        }

        public Matrix4x4 GetObjectToWorld(int index)
        {
            return objectToWorlds[drawCallIndices[index]];
        }

        public virtual void Dispose()
        {
            drawCallIndices.Clear();

            drawCallCount = 0;
        }
    }
}