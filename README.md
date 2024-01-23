# Unity_StarRail_CRP_Sample

An example of a custom rendering pipeline that restores the rendering of HSR



### Effect

> [!NOTE] 
>
> Initial results. Will continue to adjust and optimize later.

![6](./Documents~/README.assets/6.png)

![5](./Documents~/README.assets/5.png)

###### Video:



### Requirements

- Unity 2022.3.8f1 (Base on URP)
- Git and Git LFS

> [!IMPORTANT]
>
> Before clone this project, make sure you have install Git LFS. Otherwise some big FBX files will not clone successfully.
>
> Download Git LFS from [this website](https://git-scm.com/downloads), and install. 
>
> Then run   `` git install lfs``

> [!WARNING]
>
> Currently, there may be memory leak problem in multiple cameras and preview cameras, which needs to be fixed.



### About Custom Render Pipeline

Use a single Renderer Feature to manage custom passes. Use stencil deferred rendering for more colorful lighting. Also, per object shadows are used on the characters to achieve more variable shadow effects. This pipeline also has SSR and TAA Pass to help express more delicate images.

The following is the flow chart of the rendering pipeline:

![Custom Render Pipeline](./Documents~/README.assets/Custom Render Pipeline.png)

> [!TIP]
>
> For more information, read [RenderPipeline.md](./Documents~/RenderPipeline.md) and get details.



### Future

- To support decal rendering
- To support decal screen space shadow
- Add HBAO
- Add Screen Space Fog
