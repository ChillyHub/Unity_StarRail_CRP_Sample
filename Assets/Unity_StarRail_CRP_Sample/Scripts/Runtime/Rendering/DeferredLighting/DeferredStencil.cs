using System;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public enum DeferredPass
    {
        UnLitGBuffer,
        CharacterGBuffer,
        SceneGBuffer,
        SssGBuffer,
        
        StencilVolume,
        
        UnLitDirectional,
        CharacterDirectional,
        SceneDirectional,
        SssDirectional,
        
        UnLitAdditional,
        CharacterAdditional,
        SceneAdditional,
        SssAdditional
    }
    
    [Flags]
    public enum StencilConstant : byte
    {
        MaterialUnLit     = 0b00000000,
        UserReadMask      = 0b00001111,
        LightReadMask     = 0b00010000,
        LightWriteMask    = 0b00010000,
        MaterialCharacter = 0b00100000,
        MaterialScene     = 0b01000000,
        MaterialSss       = 0b01100000,
        MaterialReadMask  = 0b01100000,
        MaterialWriteMask = 0b01101111
    }

    public static class DeferredStencil
    {
        public static StencilState GetStencilState(DeferredPass pass)
        {
            StencilState state = new StencilState();
            state.enabled = true;
            
            switch (pass)
            {
                case DeferredPass.UnLitGBuffer:
                    state.readMask = 0;
                    state.writeMask = (byte)(StencilConstant.MaterialReadMask | StencilConstant.UserReadMask);
                    state.SetCompareFunction(CompareFunction.Always);
                    state.SetPassOperation(StencilOp.Replace);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Keep);
                    return state;
                case DeferredPass.CharacterGBuffer:
                    state.readMask = 0;
                    state.writeMask = (byte)(StencilConstant.MaterialReadMask | StencilConstant.UserReadMask);
                    state.SetCompareFunction(CompareFunction.Always);
                    state.SetPassOperation(StencilOp.Replace);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Keep);
                    return state;
                case DeferredPass.SceneGBuffer:
                    state.readMask = 0;
                    state.writeMask = (byte)(StencilConstant.MaterialReadMask | StencilConstant.UserReadMask);
                    state.SetCompareFunction(CompareFunction.Always);
                    state.SetPassOperation(StencilOp.Replace);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Keep);
                    return state;
                case DeferredPass.SssGBuffer:
                    state.readMask = 0;
                    state.writeMask = (byte)(StencilConstant.MaterialReadMask | StencilConstant.UserReadMask);
                    state.SetCompareFunction(CompareFunction.Always);
                    state.SetPassOperation(StencilOp.Replace);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Keep);
                    return state;
                case DeferredPass.StencilVolume:
                    state.readMask = (byte)StencilConstant.MaterialReadMask;
                    state.writeMask = (byte)StencilConstant.LightWriteMask;
                    state.SetCompareFunction(CompareFunction.NotEqual);
                    state.SetPassOperation(StencilOp.Keep);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Invert);
                    return state;
                case DeferredPass.UnLitDirectional:
                case DeferredPass.CharacterDirectional:
                case DeferredPass.SceneDirectional:
                case DeferredPass.SssDirectional:
                    state.readMask = (byte)StencilConstant.MaterialReadMask;
                    state.writeMask = 0;
                    state.SetCompareFunction(CompareFunction.Equal);
                    state.SetPassOperation(StencilOp.Keep);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Keep);
                    return state;
                case DeferredPass.UnLitAdditional:
                case DeferredPass.CharacterAdditional:
                case DeferredPass.SceneAdditional:
                case DeferredPass.SssAdditional:
                    state.readMask = (byte)(StencilConstant.MaterialReadMask | StencilConstant.LightReadMask);
                    state.writeMask = (byte)StencilConstant.LightWriteMask;
                    state.SetCompareFunction(CompareFunction.Equal);
                    state.SetPassOperation(StencilOp.Zero);
                    state.SetFailOperation(StencilOp.Keep);
                    state.SetZFailOperation(StencilOp.Keep);
                    return state;
                default:
                    return state;
            }
        }

        public static int GetStencilReference(DeferredPass pass, int userStencil = 0)
        {
            switch (pass)
            {
                case DeferredPass.UnLitGBuffer:
                    return userStencil | (int)StencilConstant.MaterialUnLit;
                case DeferredPass.CharacterGBuffer:
                    return userStencil | (int)StencilConstant.MaterialCharacter;
                case DeferredPass.SceneGBuffer:
                    return userStencil | (int)StencilConstant.MaterialScene;
                case DeferredPass.SssGBuffer:
                    return userStencil | (int)StencilConstant.MaterialSss;
                case DeferredPass.StencilVolume:
                    return 0;
                case DeferredPass.UnLitDirectional:
                    return (int)StencilConstant.MaterialUnLit;
                case DeferredPass.CharacterDirectional:
                    return (int)StencilConstant.MaterialCharacter;
                case DeferredPass.SceneDirectional:
                    return (int)StencilConstant.MaterialScene;
                case DeferredPass.SssDirectional:
                    return (int)StencilConstant.MaterialSss;
                case DeferredPass.UnLitAdditional:
                    return (int)(StencilConstant.LightReadMask | StencilConstant.MaterialUnLit);
                case DeferredPass.CharacterAdditional:
                    return (int)(StencilConstant.LightReadMask | StencilConstant.MaterialCharacter);
                case DeferredPass.SceneAdditional:
                    return (int)(StencilConstant.LightReadMask | StencilConstant.MaterialScene);
                case DeferredPass.SssAdditional:
                    return (int)(StencilConstant.LightReadMask | StencilConstant.MaterialSss);
                default:
                    return 0;
            }
        }
    }
}