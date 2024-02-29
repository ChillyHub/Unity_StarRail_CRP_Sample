# Unity_StarRail_CRP_Sample

An example of a custom rendering pipeline that restores the rendering of HSR



> [!TIP]
>
> [中文版 README 点这里](./README_CN.md)



### Effect

> [!NOTE] 
>
> Initial effect. Will continue to adjust and optimize later.

##### [Scene]  March 7th Room:

![6](./Documents~/README.assets/6.png)

![5](./Documents~/README.assets/5.png)

https://github.com/ChillyHub/Unity_StarRail_CRP_Sample/assets/75598757/b2778f2f-7ed6-43fa-a1aa-f8636c7432f4

https://github.com/ChillyHub/Unity_StarRail_CRP_Sample/assets/75598757/847f259c-47a8-4d7f-846b-8b1872e4df36

> **Support Decal Light with Shadow --** 

https://github.com/ChillyHub/Unity_StarRail_CRP_Sample/assets/75598757/b0f7fae2-c645-4997-8faa-c3b4c6153563


##### [New Scene]  Parlor Car:

> Unfinished

![ParlorCar](./Documents~/README.assets/ParlorCar.png)

##### [New Scene]  Characters Show:

> The character has no self shadow, but can receive scene shadow at the same time.

https://github.com/ChillyHub/Unity_StarRail_CRP_Sample/assets/75598757/2d956c18-dd95-41ee-bbd3-9efd5f171f68

> [!TIP]
>
> For more Effects Pictures and Videos, look at folder [Temp](./Documents~/Temp).

### Demo

Here is a very simple demo for Windows x64: [Demo Release v0.0.3](https://github.com/ChillyHub/Unity_StarRail_CRP_Sample/releases/tag/v0.0.3)

Demo for Android is coming.

> [!Tip]
>
> The operation is similar to the game, read Release for more details.


### Project Requirements

- Unity 2022.3.8f1 (Base on URP)
- Git and Git LFS

> [!IMPORTANT]
>
> Before clone this project, make sure you have install Git LFS. Otherwise some big FBX files will not clone successfully.
>
> Download Git from [this website](https://git-scm.com/downloads), and install. 
>
> Then run   `` git lfs install``.
>
> Use ``git lfs clone https://github.com/ChillyHub/Unity_StarRail_CRP_Sample.git`` to clone.
>
> If still cannot download it completely, please download the unitypackage package from [Demo Release v0.0.3](https://github.com/ChillyHub/Unity_StarRail_CRP_Sample/releases/tag/v0.0.3).

> [!WARNING]
>
> Currently, there may be memory leak problem in multiple cameras and preview cameras, which needs to be fixed. Please note the memory usage.



### About Custom Render Pipeline

Use a single Renderer Feature to manage custom passes. Use stencil deferred rendering for more colorful lighting. Also, per object shadows are used on the characters to achieve more variable shadow effects. This pipeline also has SSR and TAA Pass to help express more delicate images.

The following is the flow chart of the rendering pipeline:

![Custom Render Pipeline](./Documents~/README.assets/CustomRenderPipeline.png)

> [!TIP]
>
> For more information, read [RenderPipeline.md](./Documents~/RenderPipeline.md) and get details.



### Source of Assets

- miHoYo: models, textures, animations of characters.
- Viero月城：models, textures of scenes. (such as Mar7th Room)



### Future

- Add HBAO
- Add Volume Light
- Add Screen Space Fog
- More Efficient Bloom (Less RT switch)
